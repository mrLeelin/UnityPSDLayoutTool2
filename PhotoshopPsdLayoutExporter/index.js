const photoshop = require("photoshop");
const uxp = require("uxp");

const app = photoshop.app;
const core = photoshop.core;
const xmp = uxp.xmp;
const namespace = "https://codex.openai.com/psd-layout/1.0/";
const prefix = "psdUnity";

const statusElement = document.getElementById("status");
document.getElementById("write-xmp").addEventListener("click", writeManifestToPsd);

function setStatus(message, isError) {
  statusElement.textContent = message;
  statusElement.style.color = isError ? "#f88" : "#bbb";
}

async function writeManifestToPsd() {
  try {
    const document = app.activeDocument;
    if (!document) {
      throw new Error("没有打开的 Photoshop 文档。");
    }

    if (!document.path || document.path.startsWith("cloud:")) {
      throw new Error("请先把文档保存为本地 PSD/PSB 文件。");
    }

    // XMPFile writes directly to disk. Save the Photoshop document first so a
    // later Ctrl+S cannot overwrite the metadata written by this panel.
    if (!document.saved) {
      setStatus("Saving PSD before embedding layout metadata...", false);
      await core.executeAsModal(
        async () => {
          await document.save();
        },
        { commandName: "Save PSD before embedding layout metadata" }
      );
    }

    const manifest = buildManifest(document);
    const json = JSON.stringify(manifest);
    const encoded = toBase64Utf8(json);
    const xmpFile = new xmp.XMPFile(
      document.path,
      xmp.XMPConst.FILE_PHOTOSHOP,
      xmp.XMPConst.OPEN_FOR_UPDATE
    );

    if (!xmpFile) {
      throw new Error("无法打开 PSD 的 XMP 元数据。");
    }

    let metadata = xmpFile.getXMP();
    if (!metadata) {
      metadata = new xmp.XMPMeta();
    }

    xmp.XMPMeta.registerNamespace(namespace, prefix);
    metadata.setProperty(namespace, "schemaVersion", "1");
    metadata.setProperty(namespace, "encoding", "base64+utf8");
    metadata.setProperty(namespace, "manifest", encoded);
    metadata.setProperty(namespace, "manifestFingerprint", manifest.documentFingerprint);
    xmpFile.putXMP(metadata);
    xmpFile.closeFile(xmp.XMPConst.CLOSE_UPDATE_SAFELY);

    setStatus(
      `已写入 PSD 内嵌 XMP\n图层: ${manifest.layers.length}\n指纹: ${manifest.documentFingerprint}`,
      false
    );
  } catch (error) {
    setStatus(`写入失败: ${error.message || error}`, true);
  }
}

function buildManifest(document) {
  const layers = [];
  const walk = (sourceLayers, parentId, parentPath) => {
    sourceLayers.forEach((layer, siblingIndex) => {
      const layerId = String(layer.id);
      const path = parentPath.concat([siblingIndex]);
      const record = {
        layerId,
        parentId: parentId || null,
        siblingIndex,
        path,
        name: layer.name || "",
        kind: String(layer.kind || "unknown"),
        visible: Boolean(layer.visible),
        opacity: numberOr(layer.opacity, 100),
        bounds: readBounds(layer.bounds),
        text: readText(layer),
        fingerprint: ""
      };
      record.fingerprint = hashString(JSON.stringify(record, stableKeys(record)));
      layers.push(record);

      if (layer.layers && layer.layers.length) {
        walk(layer.layers, layerId, path);
      }
    });
  };

  walk(document.layers, null, []);
  const documentCore = {
    width: numberOr(document.width, 0),
    height: numberOr(document.height, 0),
    resolution: numberOr(document.resolution, 72),
    layers: layers.map(layer => ({ layerId: layer.layerId, fingerprint: layer.fingerprint }))
  };

  return {
    schema: "psd-unity-layout",
    schemaVersion: 1,
    source: "photoshop-uxp",
    document: {
      name: document.name || "",
      width: documentCore.width,
      height: documentCore.height,
      resolution: documentCore.resolution
    },
    documentFingerprint: hashString(JSON.stringify(documentCore, stableKeys(documentCore))),
    layers
  };
}

function readBounds(bounds) {
  if (!bounds) {
    return { x: 0, y: 0, width: 0, height: 0 };
  }
  return {
    x: numberOr(bounds.left, 0),
    y: numberOr(bounds.top, 0),
    width: numberOr(bounds.width, 0),
    height: numberOr(bounds.height, 0)
  };
}

function readText(layer) {
  try {
    if (!layer.textItem) {
      return null;
    }
    const textItem = layer.textItem;
    const characterStyle = textItem.characterStyle || {};
    const paragraphStyle = textItem.paragraphStyle || {};
    return {
      contents: textItem.contents || "",
      isPointText: Boolean(textItem.isPointText),
      isParagraphText: Boolean(textItem.isParagraphText),
      fontSize: numberOr(characterStyle.size, 0),
      fontName: stringOr(characterStyle.font, ""),
      justification: stringOr(paragraphStyle.justification, "")
    };
  } catch (_) {
    return null;
  }
}

function stableKeys(value) {
  return Object.keys(value).sort();
}

function numberOr(value, fallback) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function stringOr(value, fallback) {
  return typeof value === "string" ? value : fallback;
}

function hashString(value) {
  let hash = 2166136261;
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0).toString(16).padStart(8, "0");
}

function toBase64Utf8(value) {
  const bytes = unescape(encodeURIComponent(value));
  return btoa(bytes);
}
