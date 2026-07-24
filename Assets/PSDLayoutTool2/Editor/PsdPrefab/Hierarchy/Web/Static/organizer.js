(function () {
  "use strict";

  var svgNamespace = "http://www.w3.org/2000/svg";
  var roles = {};
  document.querySelectorAll("[data-role]").forEach(function (element) {
    roles[element.getAttribute("data-role")] = element;
  });

  var context = readSessionContext();
  var state = {
    session: null,
    snapshot: null,
    connected: false,
    busy: false,
    tool: "select",
    spaceDown: false,
    scale: 1,
    offsetX: 0,
    offsetY: 0,
    selection: new Set(),
    acceptedLocks: new Set(),
    compositeUrl: "",
    pointer: null,
    marquee: null,
    pollTimer: null,
    afterOperation: null,
    renderQueued: false
  };

  bindControls();
  setDisconnected("等待 Unity 会话");
  if (context.sessionId) loadWorkbench(true);

  function readSessionContext() {
    var url = new URL(window.location.href);
    var match = /^\/(?:session|open)\/([A-Za-z0-9_-]{1,128})(?:\/[A-Za-z0-9_-]{1,256})?\/?$/.exec(url.pathname);
    var sessionId = match ? match[1] : safeIdentifier(url.searchParams.get("session"));
    var hash = new URLSearchParams(url.hash.replace(/^#/, ""));
    var token = safeToken(hash.get("token")) || safeToken(url.searchParams.get("token"));
    if (url.searchParams.has("token") || hash.has("token")) {
      url.searchParams.delete("token");
      hash.delete("token");
      url.hash = hash.toString();
      window.history.replaceState(null, "", url.pathname + url.search + url.hash);
    }
    return { sessionId: sessionId, token: token };
  }

  function safeIdentifier(value) {
    return typeof value === "string" && /^[A-Za-z0-9_-]{1,128}$/.test(value) ? value : "";
  }

  function safeToken(value) {
    return typeof value === "string" && /^[A-Za-z0-9_-]{16,512}$/.test(value) ? value : "";
  }

  function apiPath(suffix) {
    return "/session/" + encodeURIComponent(context.sessionId) + (suffix || "");
  }

  async function request(suffix, options) {
    var init = Object.assign({ credentials: "same-origin", cache: "no-store" }, options || {});
    init.headers = Object.assign({ "Accept": "application/json" }, init.headers || {});
    if (context.token) init.headers["X-PSD-Session-Token"] = context.token;
    var response = await fetch(apiPath(suffix), init);
    if (!response.ok) throw new Error("HTTP " + response.status);
    return response;
  }

  async function requestJson(suffix, options) {
    var response = await request(suffix, options);
    return response.json();
  }

  async function loadWorkbench(fitAfterLoad) {
    if (!context.sessionId) return setDisconnected("缺少有效的 Unity 会话标识");
    try {
      var results = await Promise.all([requestJson(""), requestJson("/snapshot"), loadComposite()]);
      state.session = results[0] || {};
      replaceSnapshot(results[1] || {});
      state.connected = true;
      roles["empty-state"].hidden = true;
      roles["canvas-transform"].hidden = false;
      roles.minimap.hidden = false;
      renderSession();
      if (fitAfterLoad) fitView(false);
      setOperation(state.session.operation || null);
      updateControls();
    } catch (error) {
      setDisconnected("Unity 连接不可用。请返回 Unity 并重新打开页面。");
    }
  }

  async function loadComposite() {
    var response = await request("/composite.png", { headers: { "Accept": "image/png" } });
    var blob = await response.blob();
    var nextUrl = URL.createObjectURL(blob);
    if (state.compositeUrl) URL.revokeObjectURL(state.compositeUrl);
    state.compositeUrl = nextUrl;
    roles["composite-image"].src = nextUrl;
    roles["minimap-image"].src = nextUrl;
  }

  function replaceSnapshot(snapshot) {
    state.snapshot = snapshot;
    groups().forEach(function (group) {
      if (group.isAccepted || group.isLocked) state.acceptedLocks.add(group.key);
    });
    var available = new Set(groups().map(function (group) { return group.key; }));
    state.selection.forEach(function (key) {
      if (!available.has(key)) state.selection.delete(key);
    });
    renderCanvas();
    renderInspector();
  }

  function groups() {
    return state.snapshot && Array.isArray(state.snapshot.groups) ? state.snapshot.groups : [];
  }

  function nodes() {
    return state.snapshot && Array.isArray(state.snapshot.nodes) ? state.snapshot.nodes : [];
  }

  function renderSession() {
    roles["psd-filename"].textContent = state.session.sourcePsdName || "未命名 PSD";
    roles["target-prefab"].textContent = state.session.targetPrefabName ? "目标 Prefab · " + state.session.targetPrefabName : "尚未生成目标 Prefab";
    roles["connection-state"].classList.remove("is-offline");
    roles["connection-state"].replaceChildren(createDot(), document.createTextNode("已连接 Unity"));
  }

  function createDot() {
    return document.createElement("i");
  }

  function renderCanvas() {
    if (!state.snapshot || !state.snapshot.canvas) return;
    var canvas = state.snapshot.canvas;
    var width = positive(canvas.width, 1);
    var height = positive(canvas.height, 1);
    roles["canvas-transform"].style.width = width + "px";
    roles["canvas-transform"].style.height = height + "px";
    roles["overlay-svg"].setAttribute("viewBox", "0 0 " + width + " " + height);
    roles["overlay-svg"].setAttribute("width", String(width));
    roles["overlay-svg"].setAttribute("height", String(height));
    clear(roles["node-overlays"]);
    clear(roles["group-overlays"]);

    nodes().forEach(function (node) {
      var rect = svgElement("rect");
      setBounds(rect, node.bounds);
      rect.setAttribute("class", "node-overlay");
      var title = svgElement("title");
      title.textContent = (node.name || "未命名节点") + " · " + (node.stableId || "无标识");
      rect.appendChild(title);
      roles["node-overlays"].appendChild(rect);
    });

    groups().forEach(function (group) {
      var groupElement = svgElement("g");
      var rect = svgElement("rect");
      var classes = ["group-overlay"];
      if (state.selection.has(group.key)) classes.push("is-selected");
      if (state.acceptedLocks.has(group.key)) classes.push("is-accepted");
      if (groupHasWarning(group)) classes.push("is-warning");
      rect.setAttribute("class", classes.join(" "));
      rect.setAttribute("tabindex", "0");
      rect.setAttribute("role", "button");
      rect.setAttribute("aria-label", "选择分组 " + (group.displayName || group.key || "未命名"));
      rect.setAttribute("aria-pressed", state.selection.has(group.key) ? "true" : "false");
      setBounds(rect, group.bounds);
      var title = svgElement("title");
      title.textContent = group.displayName || group.key || "未命名分组";
      rect.appendChild(title);
      rect.addEventListener("pointerdown", function (event) { event.stopPropagation(); });
      rect.addEventListener("click", function (event) { selectGroup(group.key, event.shiftKey); });
      rect.addEventListener("keydown", function (event) {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          selectGroup(group.key, event.shiftKey);
        }
      });
      groupElement.appendChild(rect);
      if (positive(group.bounds && group.bounds.width, 0) > 36 && positive(group.bounds && group.bounds.height, 0) > 18) {
        var label = svgElement("text");
        label.setAttribute("class", "group-label");
        label.setAttribute("x", String(number(group.bounds.x) + 5));
        label.setAttribute("y", String(number(group.bounds.y) + 15));
        label.textContent = group.displayName || group.key || "未命名分组";
        groupElement.appendChild(label);
      }
      roles["group-overlays"].appendChild(groupElement);
    });
    scheduleTransform();
  }

  function svgElement(name) {
    return document.createElementNS(svgNamespace, name);
  }

  function setBounds(element, bounds) {
    bounds = bounds || {};
    element.setAttribute("x", String(number(bounds.x)));
    element.setAttribute("y", String(number(bounds.y)));
    element.setAttribute("width", String(positive(bounds.width, 0)));
    element.setAttribute("height", String(positive(bounds.height, 0)));
  }

  function groupHasWarning(group) {
    var members = new Set(Array.isArray(group.memberStableIds) ? group.memberStableIds : []);
    return warnings().some(function (warning) {
      return (warning.stableIds || []).some(function (stableId) { return members.has(stableId); });
    });
  }

  function warnings() {
    return state.snapshot && Array.isArray(state.snapshot.warnings) ? state.snapshot.warnings : [];
  }

  function selectGroup(key, additive) {
    if (!additive) state.selection.clear();
    if (additive && state.selection.has(key)) state.selection.delete(key);
    else state.selection.add(key);
    renderCanvas();
    renderInspector();
    updateControls();
  }

  function renderInspector() {
    var selectedGroups = groups().filter(function (group) { return state.selection.has(group.key); });
    var memberIds = new Set();
    selectedGroups.forEach(function (group) {
      (group.memberStableIds || []).forEach(function (stableId) { memberIds.add(stableId); });
    });
    var selectedNodes = nodes().filter(function (node) { return memberIds.has(node.stableId); });
    roles["selection-count"].textContent = String(selectedGroups.length);
    roles["selection-summary"].textContent = selectedGroups.length ? selectedGroups.length + " 个分组 · " + selectedNodes.length + " 个节点" : "未选择分组";

    clear(roles["selected-scope"]);
    if (!selectedGroups.length) appendMuted(roles["selected-scope"], "在画布上选择一个或多个分组。");
    selectedGroups.forEach(function (group) {
      var item = element("div", "scope-item" + (state.acceptedLocks.has(group.key) ? " is-accepted" : ""));
      appendTextElement(item, "strong", group.displayName || group.key || "未命名分组");
      appendTextElement(item, "span", (group.memberStableIds || []).length + " 个节点" + (state.acceptedLocks.has(group.key) ? " · 已锁定" : ""));
      roles["selected-scope"].appendChild(item);
    });

    renderHierarchy(roles["current-hierarchy"], unique(selectedNodes.map(function (node) { return node.sourceGroupKey || "根节点"; })));
    renderHierarchy(roles["proposed-hierarchy"], unique(selectedNodes.map(function (node) { return node.proposedGroupKey || "根节点"; })));

    clear(roles["naming-changes"]);
    var changed = selectedNodes.filter(function (node) { return node.proposedName && node.proposedName !== node.name; });
    if (!changed.length) appendMuted(roles["naming-changes"], "没有待检查的命名变更。");
    changed.slice(0, 80).forEach(function (node) {
      var item = element("div", "change-item");
      appendTextElement(item, "strong", node.name || "未命名");
      appendTextElement(item, "span", "→ " + node.proposedName);
      roles["naming-changes"].appendChild(item);
    });

    var relevantWarnings = warnings().filter(function (warning) {
      return !selectedGroups.length || !(warning.stableIds || []).length || (warning.stableIds || []).some(function (id) { return memberIds.has(id); });
    });
    roles["warning-count"].textContent = String(relevantWarnings.length);
    clear(roles.warnings);
    if (!relevantWarnings.length) appendMuted(roles.warnings, "当前没有警告。");
    relevantWarnings.forEach(function (warning) {
      var item = element("div", "warning-item");
      appendTextElement(item, "strong", warning.code || "检查项");
      appendTextElement(item, "span", warning.message || "需要在 Unity 中检查此项目。");
      roles.warnings.appendChild(item);
    });
  }

  function renderHierarchy(container, values) {
    clear(container);
    container.classList.toggle("muted", !values.length);
    if (!values.length) return appendText(container, "未选择");
    values.forEach(function (value) { appendTextElement(container, "span", value); });
  }

  function unique(values) {
    return Array.from(new Set(values.filter(Boolean)));
  }

  function clear(container) {
    while (container.firstChild) container.removeChild(container.firstChild);
  }

  function element(tag, className) {
    var result = document.createElement(tag);
    if (className) result.className = className;
    return result;
  }

  function appendText(container, value) {
    container.appendChild(document.createTextNode(value));
  }

  function appendTextElement(container, tag, value) {
    var child = document.createElement(tag);
    child.textContent = value;
    container.appendChild(child);
    return child;
  }

  function appendMuted(container, value) {
    var paragraph = element("p", "muted");
    paragraph.textContent = value;
    container.appendChild(paragraph);
  }

  function bindControls() {
    roles["tool-select"].addEventListener("click", function () { setTool("select"); });
    roles["tool-hand"].addEventListener("click", function () { setTool("hand"); });
    roles["fit-view"].addEventListener("click", function () { fitView(true); });
    roles["actual-size"].addEventListener("click", actualSize);
    roles["reanalyze"].addEventListener("click", function () { startMutation("/analyze", {}); });
    roles["refine-selection"].addEventListener("click", refineSelection);
    roles["accept-selection"].addEventListener("click", acceptSelection);
    roles["apply-plan"].addEventListener("click", applyPlan);
    roles["create-prefabs"].addEventListener("click", createPrefabs);
    roles["psd-canvas"].addEventListener("wheel", onWheel, { passive: false });
    roles["psd-canvas"].addEventListener("pointerdown", onCanvasPointerDown);
    roles["minimap-viewport"].addEventListener("pointerdown", onMinimapPointerDown);
    window.addEventListener("pointermove", onPointerMove);
    window.addEventListener("pointerup", onPointerUp);
    window.addEventListener("pointercancel", onPointerUp);
    window.addEventListener("resize", scheduleTransform);
    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("keyup", onKeyUp);
    window.addEventListener("blur", function () { state.spaceDown = false; });
    window.addEventListener("beforeunload", function () {
      if (state.compositeUrl) URL.revokeObjectURL(state.compositeUrl);
      if (state.pollTimer) window.clearTimeout(state.pollTimer);
    });
  }

  function setTool(tool) {
    state.tool = tool;
    roles["tool-select"].classList.toggle("is-active", tool === "select");
    roles["tool-hand"].classList.toggle("is-active", tool === "hand");
    roles["tool-select"].setAttribute("aria-pressed", tool === "select" ? "true" : "false");
    roles["tool-hand"].setAttribute("aria-pressed", tool === "hand" ? "true" : "false");
  }

  function onWheel(event) {
    if (!state.snapshot) return;
    event.preventDefault();
    var point = viewportPoint(event.clientX, event.clientY);
    var canvasX = (point.x - state.offsetX) / state.scale;
    var canvasY = (point.y - state.offsetY) / state.scale;
    var factor = Math.exp(-event.deltaY * .0015);
    var nextScale = clamp(state.scale * factor, .03, 8);
    state.offsetX = point.x - canvasX * nextScale;
    state.offsetY = point.y - canvasY * nextScale;
    state.scale = nextScale;
    scheduleTransform();
  }

  function onCanvasPointerDown(event) {
    if (!state.snapshot) return;
    var pan = event.button === 1 || state.spaceDown || state.tool === "hand";
    if (pan) {
      event.preventDefault();
      state.pointer = { kind: "pan", x: event.clientX, y: event.clientY, offsetX: state.offsetX, offsetY: state.offsetY };
      roles["psd-canvas"].classList.add("is-panning");
      return;
    }
    if (event.button !== 0 || state.tool !== "select") return;
    var start = canvasPoint(event.clientX, event.clientY);
    state.pointer = { kind: "marquee", additive: event.shiftKey };
    state.marquee = { x1: start.x, y1: start.y, x2: start.x, y2: start.y };
    updateMarquee();
  }

  function onPointerMove(event) {
    if (!state.pointer) return;
    if (state.pointer.kind === "pan") {
      state.offsetX = state.pointer.offsetX + event.clientX - state.pointer.x;
      state.offsetY = state.pointer.offsetY + event.clientY - state.pointer.y;
      scheduleTransform();
    } else if (state.pointer.kind === "marquee") {
      var point = canvasPoint(event.clientX, event.clientY);
      state.marquee.x2 = point.x;
      state.marquee.y2 = point.y;
      updateMarquee();
    } else if (state.pointer.kind === "minimap") {
      moveFromMinimap(event.clientX, event.clientY, state.pointer.dx, state.pointer.dy);
    }
  }

  function onPointerUp() {
    if (!state.pointer) return;
    if (state.pointer.kind === "marquee") completeMarquee(state.pointer.additive);
    state.pointer = null;
    roles["psd-canvas"].classList.remove("is-panning");
  }

  function updateMarquee() {
    if (!state.marquee) return;
    var rect = normalizedRect(state.marquee);
    roles.marquee.hidden = false;
    roles.marquee.setAttribute("x", String(rect.x));
    roles.marquee.setAttribute("y", String(rect.y));
    roles.marquee.setAttribute("width", String(rect.width));
    roles.marquee.setAttribute("height", String(rect.height));
  }

  function completeMarquee(additive) {
    var rect = normalizedRect(state.marquee);
    roles.marquee.hidden = true;
    state.marquee = null;
    if (!additive) state.selection.clear();
    if (rect.width > 2 / state.scale || rect.height > 2 / state.scale) {
      groups().forEach(function (group) {
        if (intersects(rect, group.bounds || {})) state.selection.add(group.key);
      });
    }
    renderCanvas();
    renderInspector();
    updateControls();
  }

  function normalizedRect(rect) {
    return { x: Math.min(rect.x1, rect.x2), y: Math.min(rect.y1, rect.y2), width: Math.abs(rect.x2 - rect.x1), height: Math.abs(rect.y2 - rect.y1) };
  }

  function intersects(left, right) {
    return left.x < number(right.x) + positive(right.width, 0) && left.x + left.width > number(right.x) &&
      left.y < number(right.y) + positive(right.height, 0) && left.y + left.height > number(right.y);
  }

  function onKeyDown(event) {
    if (isTyping(event.target)) return;
    if (event.code === "Space") {
      state.spaceDown = true;
      event.preventDefault();
    } else if (event.key === "Escape") {
      state.selection.clear();
      renderCanvas();
      renderInspector();
      updateControls();
    } else if (event.key.toLowerCase() === "f") {
      event.preventDefault();
      fitView(true);
    }
  }

  function onKeyUp(event) {
    if (event.code === "Space") state.spaceDown = false;
  }

  function isTyping(target) {
    return target && (target.tagName === "TEXTAREA" || target.tagName === "INPUT" || target.isContentEditable);
  }

  function fitView(useSelection) {
    if (!state.snapshot || !state.snapshot.canvas) return;
    var bounds = useSelection ? selectedBounds() : null;
    if (!bounds) bounds = { x: 0, y: 0, width: positive(state.snapshot.canvas.width, 1), height: positive(state.snapshot.canvas.height, 1) };
    var viewport = roles["psd-canvas"].getBoundingClientRect();
    var padding = 52;
    state.scale = clamp(Math.min((viewport.width - padding * 2) / positive(bounds.width, 1), (viewport.height - padding * 2) / positive(bounds.height, 1)), .03, 8);
    state.offsetX = viewport.width / 2 - (number(bounds.x) + positive(bounds.width, 1) / 2) * state.scale;
    state.offsetY = viewport.height / 2 - (number(bounds.y) + positive(bounds.height, 1) / 2) * state.scale;
    scheduleTransform();
  }

  function actualSize() {
    if (!state.snapshot || !state.snapshot.canvas) return;
    var viewport = roles["psd-canvas"].getBoundingClientRect();
    state.scale = 1;
    state.offsetX = (viewport.width - positive(state.snapshot.canvas.width, 1)) / 2;
    state.offsetY = (viewport.height - positive(state.snapshot.canvas.height, 1)) / 2;
    scheduleTransform();
  }

  function selectedBounds() {
    var selected = groups().filter(function (group) { return state.selection.has(group.key); });
    if (!selected.length) return null;
    var x1 = Math.min.apply(null, selected.map(function (group) { return number(group.bounds && group.bounds.x); }));
    var y1 = Math.min.apply(null, selected.map(function (group) { return number(group.bounds && group.bounds.y); }));
    var x2 = Math.max.apply(null, selected.map(function (group) { return number(group.bounds && group.bounds.x) + positive(group.bounds && group.bounds.width, 0); }));
    var y2 = Math.max.apply(null, selected.map(function (group) { return number(group.bounds && group.bounds.y) + positive(group.bounds && group.bounds.height, 0); }));
    return { x: x1, y: y1, width: x2 - x1, height: y2 - y1 };
  }

  function scheduleTransform() {
    if (state.renderQueued) return;
    state.renderQueued = true;
    window.requestAnimationFrame(function () {
      state.renderQueued = false;
      roles["canvas-transform"].style.transform = "translate(" + state.offsetX + "px," + state.offsetY + "px) scale(" + state.scale + ")";
      roles["zoom-level"].textContent = Math.round(state.scale * 100) + "%";
      updateMinimap();
    });
  }

  function onMinimapPointerDown(event) {
    if (!state.snapshot) return;
    event.preventDefault();
    var rect = roles["minimap-viewport"].getBoundingClientRect();
    state.pointer = { kind: "minimap", dx: event.clientX - rect.left, dy: event.clientY - rect.top };
  }

  function minimapMetrics() {
    var rect = roles.minimap.getBoundingClientRect();
    var canvas = state.snapshot.canvas;
    var scale = Math.min(rect.width / positive(canvas.width, 1), rect.height / positive(canvas.height, 1));
    return { rect: rect, scale: scale, x: (rect.width - canvas.width * scale) / 2, y: (rect.height - canvas.height * scale) / 2 };
  }

  function moveFromMinimap(clientX, clientY, dx, dy) {
    var metrics = minimapMetrics();
    var viewport = roles["psd-canvas"].getBoundingClientRect();
    var visibleWidth = viewport.width / state.scale;
    var visibleHeight = viewport.height / state.scale;
    var canvasX = (clientX - metrics.rect.left - dx - metrics.x) / metrics.scale;
    var canvasY = (clientY - metrics.rect.top - dy - metrics.y) / metrics.scale;
    state.offsetX = -canvasX * state.scale;
    state.offsetY = -canvasY * state.scale;
    if (visibleWidth >= state.snapshot.canvas.width) state.offsetX = (viewport.width - state.snapshot.canvas.width * state.scale) / 2;
    if (visibleHeight >= state.snapshot.canvas.height) state.offsetY = (viewport.height - state.snapshot.canvas.height * state.scale) / 2;
    scheduleTransform();
  }

  function updateMinimap() {
    if (!state.snapshot || roles.minimap.hidden) return;
    var metrics = minimapMetrics();
    var viewport = roles["psd-canvas"].getBoundingClientRect();
    var left = clamp(-state.offsetX / state.scale, 0, state.snapshot.canvas.width);
    var top = clamp(-state.offsetY / state.scale, 0, state.snapshot.canvas.height);
    var width = clamp(viewport.width / state.scale, 0, state.snapshot.canvas.width - left);
    var height = clamp(viewport.height / state.scale, 0, state.snapshot.canvas.height - top);
    roles["minimap-viewport"].style.left = metrics.x + left * metrics.scale + "px";
    roles["minimap-viewport"].style.top = metrics.y + top * metrics.scale + "px";
    roles["minimap-viewport"].style.width = Math.max(8, width * metrics.scale) + "px";
    roles["minimap-viewport"].style.height = Math.max(8, height * metrics.scale) + "px";
  }

  function viewportPoint(clientX, clientY) {
    var rect = roles["psd-canvas"].getBoundingClientRect();
    return { x: clientX - rect.left, y: clientY - rect.top };
  }

  function canvasPoint(clientX, clientY) {
    var point = viewportPoint(clientX, clientY);
    return { x: (point.x - state.offsetX) / state.scale, y: (point.y - state.offsetY) / state.scale };
  }

  function refineSelection() {
    var stableIds = [];
    groups().forEach(function (group) {
      if (state.selection.has(group.key)) stableIds.push.apply(stableIds, group.memberStableIds || []);
    });
    startMutation("/refine", { stableIds: unique(stableIds), instruction: roles.instruction.value.trim() });
  }

  function acceptSelection() {
    var keys = Array.from(state.selection);
    startMutation("/accept", { groupKeys: keys, isAccepted: true }, function () {
      keys.forEach(function (key) { state.acceptedLocks.add(key); });
      renderCanvas();
      renderInspector();
    });
  }

  function applyPlan() {
    startMutation("/apply", { confirmed: true }, function () { loadPrefabCandidates(); });
  }

  async function startMutation(suffix, payload, onSuccess) {
    if (!state.connected || state.busy) return;
    state.busy = true;
    state.afterOperation = onSuccess || null;
    setOperation({ status: "running", message: "Unity 正在处理请求…" });
    try {
      var operation = await requestJson(suffix, {
        method: "POST",
        headers: { "Content-Type": "application/json", "Accept": "application/json" },
        body: JSON.stringify(payload)
      });
      var operationState = operation && operation.operation ? operation.operation : operation;
      setOperation(operationState);
      if (!operationState || String(operationState.status || "").toLowerCase() !== "running") {
        state.busy = false;
        await refreshSnapshot();
        finishOperationCallback(String(operationState && operationState.status || "succeeded").toLowerCase());
      }
    } catch (error) {
      state.busy = false;
      state.afterOperation = null;
      setOperation({ status: "failed", message: "Unity 未能完成请求。" });
    }
    updateControls();
  }

  function setOperation(operation) {
    var status = operation && String(operation.status || "idle").toLowerCase();
    var running = status === "running";
    state.busy = running;
    roles["ai-state"].textContent = running ? "AI 处理中" : status === "failed" ? "操作失败" : status === "succeeded" ? "操作完成" : "AI 空闲";
    roles["ai-state"].classList.toggle("is-busy", running);
    roles["operation-message"].textContent = operation && operation.message ? operation.message : state.connected ? "准备就绪" : "等待 Unity 会话";
    if (state.pollTimer) {
      window.clearTimeout(state.pollTimer);
      state.pollTimer = null;
    }
    if (running) state.pollTimer = window.setTimeout(pollStatus, 500);
    updateControls();
  }

  async function pollStatus() {
    state.pollTimer = null;
    if (!state.connected || !state.busy) return;
    try {
      var operation = await requestJson("/status");
      setOperation(operation);
      var status = String(operation.status || "").toLowerCase();
      if (status === "succeeded") {
        await refreshSnapshot();
        finishOperationCallback(status);
      } else if (status === "failed") {
        state.afterOperation = null;
      }
    } catch (error) {
      setDisconnected("Unity 连接已断开。请返回 Unity 并重新打开页面。");
    }
  }

  function finishOperationCallback(status) {
    var callback = state.afterOperation;
    state.afterOperation = null;
    if (status === "succeeded" && callback) callback();
  }

  async function refreshSnapshot() {
    try {
      replaceSnapshot(await requestJson("/snapshot"));
    } catch (error) {
      setDisconnected("无法刷新整理结果。请返回 Unity 并重新打开页面。");
    }
  }

  async function loadPrefabCandidates() {
    try {
      var result = await requestJson("/prefab-candidates");
      var candidates = Array.isArray(result) ? result : result.prefabCandidates || [];
      renderCandidates(candidates);
      if (roles["prefab-dialog"].showModal) roles["prefab-dialog"].showModal();
      else roles["prefab-dialog"].setAttribute("open", "");
    } catch (error) {
      roles["operation-message"].textContent = "层级已应用，但 Prefab 候选暂不可用。";
    }
  }

  function renderCandidates(candidates) {
    clear(roles["prefab-candidates"]);
    if (!candidates.length) appendMuted(roles["prefab-candidates"], "没有检测到可创建的公共 Prefab 候选。");
    candidates.forEach(function (candidate) {
      var label = element("label", "candidate-item");
      var checkbox = document.createElement("input");
      checkbox.type = "checkbox";
      checkbox.value = candidate.candidateId || "";
      checkbox.addEventListener("change", updateCreateButton);
      label.appendChild(checkbox);
      var preview = element("div", "candidate-preview");
      preview.setAttribute("aria-label", "代表节点预览");
      applyCandidatePreview(preview, candidate.representativeStableId);
      label.appendChild(preview);
      var copy = element("div", "candidate-copy");
      appendTextElement(copy, "strong", candidate.proposedName || "未命名 Prefab");
      appendTextElement(copy, "span", (candidate.instanceStableIds || []).length + " 个实例 · 代表节点 " + (candidate.representativeStableId || "未知"));
      if ((candidate.instanceControlledDifferences || []).length) appendTextElement(copy, "span", "保留实例差异：" + candidate.instanceControlledDifferences.join("、"));
      label.appendChild(copy);
      roles["prefab-candidates"].appendChild(label);
    });
    updateCreateButton();
  }

  function applyCandidatePreview(preview, stableId) {
    var node = nodes().find(function (item) { return item.stableId === stableId; });
    if (!node || !state.compositeUrl || !node.bounds || !state.snapshot.canvas) return;
    var width = positive(node.bounds.width, 1);
    var height = positive(node.bounds.height, 1);
    var previewScale = Math.min(96 / width, 64 / height);
    preview.style.backgroundImage = "url(\"" + state.compositeUrl.replace(/\"/g, "%22") + "\")";
    preview.style.backgroundSize = state.snapshot.canvas.width * previewScale + "px " + state.snapshot.canvas.height * previewScale + "px";
    preview.style.backgroundPosition = -number(node.bounds.x) * previewScale + "px " + -number(node.bounds.y) * previewScale + "px";
  }

  function updateCreateButton() {
    roles["create-prefabs"].disabled = !state.connected || state.busy || !roles["prefab-candidates"].querySelector("input:checked");
  }

  function createPrefabs() {
    var candidateIds = Array.from(roles["prefab-candidates"].querySelectorAll("input:checked")).map(function (input) { return input.value; });
    startMutation("/create-prefabs", { candidateIds: candidateIds }, function () { roles["prefab-dialog"].close(); });
  }

  function updateControls() {
    var locked = !state.connected || state.busy;
    var hasSelection = state.selection.size > 0;
    roles.reanalyze.disabled = locked || (state.session && state.session.canAnalyze === false);
    roles["refine-selection"].disabled = locked || !hasSelection;
    roles["accept-selection"].disabled = locked || !hasSelection;
    roles["apply-plan"].disabled = locked || (state.session && state.session.canApply === false);
    roles.instruction.disabled = locked;
    updateCreateButton();
  }

  function setDisconnected(message) {
    state.connected = false;
    state.busy = false;
    if (state.pollTimer) window.clearTimeout(state.pollTimer);
    state.pollTimer = null;
    roles["connection-state"].classList.add("is-offline");
    roles["connection-state"].replaceChildren(createDot(), document.createTextNode("Unity 已断开"));
    roles["ai-state"].textContent = "AI 不可用";
    roles["ai-state"].classList.remove("is-busy");
    roles["operation-message"].textContent = message;
    roles["empty-state"].hidden = !!state.snapshot;
    if (!state.snapshot) {
      roles["canvas-transform"].hidden = true;
      roles.minimap.hidden = true;
    }
    updateControls();
  }

  function number(value) {
    value = Number(value);
    return Number.isFinite(value) ? value : 0;
  }

  function positive(value, fallback) {
    value = Number(value);
    return Number.isFinite(value) && value > 0 ? value : fallback;
  }

  function clamp(value, minimum, maximum) {
    return Math.max(minimum, Math.min(maximum, value));
  }
}());
