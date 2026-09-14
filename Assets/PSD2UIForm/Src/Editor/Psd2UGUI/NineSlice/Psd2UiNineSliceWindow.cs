namespace UGF.EditorTools.Psd2UGUI.NineSlice
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// 节点级九宫格设置窗口。
    ///
    /// 交互与 PSDLayoutTool2 的 <c>PsdNineSliceWindow</c> 保持一致：
    /// 左侧是图像列表，右侧是预览 + 四条青色可拖拽参考线 + 四边距输入 +
    /// 「使用手动九宫格」开关 + 「使用自动推断候选」按钮。
    ///
    /// 差别只在于数据来源：这里编辑的是某个 <see cref="PsdLayerNode"/>，
    /// 结果写回节点自身（随 Prefab 序列化），并顺带写进导出 PNG 的 TextureImporter.spriteBorder。
    /// </summary>
    internal sealed class Psd2UiNineSliceWindow : EditorWindow
    {
        private enum DragGuide
        {
            None,
            Left,
            Top,
            Right,
            Bottom
        }

        private const float CandidateListWidth = 272f;
        private const float PreviewHeight = 300f;
        private const float GuideHitThreshold = 8f;
        private const double AutoApplyDelay = 0.35d;

        private static readonly Color GuideColor = new Color(0.1f, 0.85f, 1f, 0.95f);
        private static readonly Color ActiveGuideColor = new Color(1f, 0.82f, 0.2f, 1f);
        private static readonly Color CheckerLight = new Color(0.76f, 0.76f, 0.76f, 1f);
        private static readonly Color CheckerDark = new Color(0.58f, 0.58f, 0.58f, 1f);

        private readonly List<PsdLayerNode> _candidates = new List<PsdLayerNode>();

        private PsdLayerNode _node;
        private Vector2 _candidateScroll;
        private string _searchText = string.Empty;
        private Texture2D _preview;
        private string _previewSource = string.Empty;
        private int _previewWidth;
        private int _previewHeight;
        private bool _enabled;
        private Psd2UiNineSliceBorder _border;
        private DragGuide _activeDrag;
        private string _status = string.Empty;
        private bool _statusIsError;
        private bool _applyPending;
        private double _applyDeadline;

        /// <summary>从 Hierarchy 行上的九宫格标识打开窗口。</summary>
        internal static void Open(PsdLayerNode node)
        {
            Psd2UiNineSliceWindow window = GetWindow<Psd2UiNineSliceWindow>(true, "九宫格设置", true);
            window.minSize = new Vector2(780f, 480f);
            window.SelectNode(node);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update -= ProcessPendingApply;
            EditorApplication.update += ProcessPendingApply;
            if (_node != null && _preview == null)
            {
                ReloadState();
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= ProcessPendingApply;

            // 域重载 / 资产导入期间不要碰 AssetDatabase，把未落盘的改动攒到下次打开窗口再写。
            if (_applyPending && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                ApplyToNode("设置九宫格");
            }

            // 兜底渲染出来的预览是本窗口自己 new 的纹理，必须在这里释放，
            // 否则每次关窗口都会漏一张（hideFlags 带 DontUnloadUnusedAsset，不会被自动回收）。
            _preview = null;
            Psd2UiNineSliceNodeState.ReleaseRenderFallback();
        }

        private void OnFocus()
        {
            ReloadState();
        }

        private void SelectNode(PsdLayerNode node)
        {
            _node = Psd2UiNineSliceNodeState.ResolveSourceNode(node);
            _activeDrag = DragGuide.None;
            _statusIsError = false;
            _applyPending = false;
            Psd2UiNineSliceNodeState.CollectCandidates(node, _candidates);
            ReloadState();
            if (_node != null)
            {
                _status = _node.nineSliceEnabled
                    ? "该节点已启用手动九宫格。拖动青色参考线或直接输入像素值即可微调。"
                    : "该节点尚未启用九宫格。勾选「使用手动九宫格」，或点「使用自动推断候选」。";
            }
        }

        private void ReloadState()
        {
            _preview = null;
            _previewSource = string.Empty;
            _previewWidth = 0;
            _previewHeight = 0;

            if (_node == null)
            {
                return;
            }

            _enabled = _node.nineSliceEnabled;
            _border = Psd2UiNineSliceNodeState.GetBorder(_node);

            _preview = Psd2UiNineSliceNodeState.ResolvePreviewTexture(_node, out _previewSource);
            if (_preview != null)
            {
                _previewWidth = _preview.width;
                _previewHeight = _preview.height;
            }

            if (_border == null)
            {
                _border = Psd2UiNineSliceNodeState.CreateDefaultBorder(_previewWidth, _previewHeight);
            }

            Repaint();
        }

        private void OnGUI()
        {
            if (_node == null)
            {
                EditorGUILayout.HelpBox("没有可编辑的节点。请在 Hierarchy 里点开某个 Image / Background 节点的九宫格标识。", MessageType.Info);
                if (GUILayout.Button("关闭窗口"))
                {
                    Close();
                }

                return;
            }

            DrawHeader();
            EditorGUILayout.BeginHorizontal();
            DrawCandidateList();
            DrawEditor();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("九宫格设置（9-Slice）", EditorStyles.boldLabel, GUILayout.Width(150f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("刷新预览", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                ReloadState();
            }

            if (GUILayout.Button("定位节点", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                EditorGUIUtility.PingObject(_node);
                Selection.activeGameObject = _node.gameObject;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCandidateList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(CandidateListWidth));
            EditorGUILayout.LabelField("可设置九宫格的图层（Image / Background）", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _searchText = EditorGUILayout.TextField(_searchText ?? string.Empty, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(44f)))
            {
                _searchText = string.Empty;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();

            _candidateScroll = EditorGUILayout.BeginScrollView(_candidateScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            int visibleCount = 0;
            for (int index = 0; index < _candidates.Count; index++)
            {
                PsdLayerNode candidate = _candidates[index];
                if (candidate == null)
                {
                    continue;
                }

                if (!MatchesSearch(candidate, _searchText))
                {
                    continue;
                }

                visibleCount++;
                string label = BuildCandidateLabel(candidate);
                bool selected = candidate == _node;
                if (GUILayout.Toggle(selected, label, "Button", GUILayout.ExpandWidth(true)) && !selected)
                {
                    SelectNode(candidate);
                }
            }

            if (visibleCount == 0)
            {
                EditorGUILayout.HelpBox("没有匹配的图层。切到 Hierarchy 里重新选择，或清空搜索词。", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private string BuildCandidateLabel(PsdLayerNode candidate)
        {
            Rect rect = candidate.GetLayerRect();
            string rectText = Mathf.RoundToInt(rect.width) + "x" + Mathf.RoundToInt(rect.height);
            string mark = Psd2UiNineSliceNodeState.ResolveSourceNode(candidate).NineSliceEnabled ? "● 九宫格" : "○";
            return candidate.name + "   [" + candidate.UIType + "]  " + rectText + "   " + mark;
        }

        private static bool MatchesSearch(PsdLayerNode candidate, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            string compact = query.Replace(" ", string.Empty).Trim();
            if (compact.Length == 0)
            {
                return true;
            }

            return IsFuzzyMatch(candidate.name, compact) ||
                IsFuzzyMatch(candidate.GetSourceLayerName(), compact) ||
                IsFuzzyMatch(candidate.UIType.ToString(), compact);
        }

        private static bool IsFuzzyMatch(string source, string query)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(query))
            {
                return false;
            }

            int queryIndex = 0;
            for (int index = 0; index < source.Length && queryIndex < query.Length; index++)
            {
                if (char.ToUpperInvariant(source[index]) == char.ToUpperInvariant(query[queryIndex]))
                {
                    queryIndex++;
                }
            }

            return queryIndex == query.Length;
        }

        private void DrawEditor()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            EditorGUILayout.LabelField(_node.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "UIType = " + _node.UIType + " · 图层 " + (_node.GetSourceLayerName() ?? "-") +
                " · " + _previewWidth + " x " + _previewHeight + " px",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("预览来源：" + (_previewSource ?? "-"), EditorStyles.wordWrappedMiniLabel);

            Rect previewRect = GUILayoutUtility.GetRect(260f, PreviewHeight, GUILayout.ExpandWidth(true), GUILayout.Height(PreviewHeight));
            if (_preview == null)
            {
                EditorGUI.HelpBox(
                    previewRect,
                    "这个节点渲染不出任何像素，没法定九宫格边距。\n\n" +
                    "可能的原因：\n" +
                    "· 该节点下面没有可渲染的 PSD 图层（空容器 / 纯 FillColor）；\n" +
                    "· 还没绑定 PSD 图层；\n" +
                    "· 源 PSD 已经不在工程里了。\n\n" +
                    "处理办法：选中该节点 → 在 Inspector 里点「导出图片资源」，" +
                    "成功后再点本窗口右上角的「刷新预览」。",
                    MessageType.Warning);
            }
            else
            {
                Rect imageRect = GetAspectFitRect(previewRect, _preview.width, _preview.height);
                DrawCheckerboard(imageRect);
                GUI.DrawTexture(imageRect, _preview, ScaleMode.StretchToFill, true);
                DrawGuides(imageRect);
                HandleGuideDrag(imageRect);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            bool enabled = EditorGUILayout.ToggleLeft("使用手动九宫格（Override）", _enabled, GUILayout.Width(220f));
            if (enabled != _enabled)
            {
                _enabled = enabled;
                ScheduleApply();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                _enabled ? "手动边距生效" : (_node.HasNineSliceBorder ? "已关闭（边距已记录）" : "未设置"),
                EditorStyles.miniLabel,
                GUILayout.Width(150f));
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(!_enabled || _preview == null);
            GUILayout.Space(4f);
            if (DrawBorderFields())
            {
                ScheduleApply();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("使用自动推断候选", GUILayout.Height(22f)))
            {
                UseAutomaticCandidate();
            }

            if (GUILayout.Button("重置为默认边距", GUILayout.Height(22f)))
            {
                _border = Psd2UiNineSliceNodeState.CreateDefaultBorder(_previewWidth, _previewHeight);
                ScheduleApply();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("应用并写入图片（spriteBorder）", GUILayout.Height(24f)))
            {
                ApplyToNode("应用九宫格");
                EditorUtility.DisplayDialog("九宫格", _status, "好");
            }

            if (GUILayout.Button("清除九宫格数据", GUILayout.Height(24f), GUILayout.Width(130f)))
            {
                Psd2UiNineSliceNodeState.Clear(_node);
                ReloadState();
                _status = "已清除该节点的九宫格开关与边距。";
                _statusIsError = false;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2f);
            EditorGUILayout.HelpBox(
                "勾选「使用手动九宫格」= Hierarchy 上的九宫格标识亮起，导出该节点图片时用下面这四个边距，" +
                "而不是图层名标签 / 像素推断的结果。\n" +
                "参考线可以直接拖动；边距单位是" + (_preview == null ? "源图像素" : _previewWidth + "x" + _previewHeight + " 图像素") + "。\n" +
                "关闭开关只会停用九宫格，已输入的边距仍会保留在节点上，方便再打开时复用。",
                MessageType.None);

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.HelpBox(_status, _statusIsError ? MessageType.Error : MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private bool DrawBorderFields()
        {
            if (_border == null)
            {
                _border = Psd2UiNineSliceNodeState.CreateDefaultBorder(_previewWidth, _previewHeight);
            }

            EditorGUILayout.BeginHorizontal();
            int left = EditorGUILayout.IntField("Left", _border.Left);
            int right = EditorGUILayout.IntField("Right", _border.Right);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            int top = EditorGUILayout.IntField("Top", _border.Top);
            int bottom = EditorGUILayout.IntField("Bottom", _border.Bottom);
            EditorGUILayout.EndHorizontal();

            Psd2UiNineSliceBorder next = Psd2UiNineSliceNodeState.ClampBorder(
                new Psd2UiNineSliceBorder(left, top, right, bottom), _previewWidth, _previewHeight);

            if (!Psd2UiNineSliceNodeState.BordersEqual(next, _border))
            {
                _border = next;
                return true;
            }

            return false;
        }

        private void UseAutomaticCandidate()
        {
            if (!Psd2UiNineSliceNodeState.TryInferBorder(_node, out Psd2UiNineSliceBorder inferred, out string method, out string error))
            {
                _status = error;
                _statusIsError = true;
                return;
            }

            _border = inferred;
            _enabled = true;
            ScheduleApply();
            _status = "已套用自动推断结果（" + method + "）。拖动参考线可继续微调。";
            _statusIsError = false;
        }

        private void ScheduleApply()
        {
            _applyPending = true;
            _applyDeadline = EditorApplication.timeSinceStartup + AutoApplyDelay;
        }

        private void ProcessPendingApply()
        {
            if (!_applyPending || EditorApplication.timeSinceStartup < _applyDeadline)
            {
                return;
            }

            _applyPending = false;
            ApplyToNode("设置九宫格");
            Repaint();
        }

        /// <summary>
        /// 把当前开关与边距写进节点（随 Prefab 序列化），并同步到导出 PNG 的 spriteBorder。
        /// </summary>
        private void ApplyToNode(string undoLabel)
        {
            if (_node == null)
            {
                return;
            }

            _border = Psd2UiNineSliceNodeState.ClampBorder(_border, _previewWidth, _previewHeight);
            Psd2UiNineSliceNodeState.SetEnabledAndBorder(_node, _enabled, _border, undoLabel);

            string path;
            string error;
            if (_enabled)
            {
                if (Psd2UiNineSliceNodeState.TryApplyCurrentBorderToExportedSprite(_node, out path, out error))
                {
                    _status = "已记录到节点，并写入 spriteBorder：" + path;
                    _statusIsError = false;
                }
                else
                {
                    _status = "已记录到节点。（" + error + "）";
                    _statusIsError = false;
                }
            }
            else
            {
                ClearExportedSpriteBorder(out path, out error);
                _status = string.IsNullOrEmpty(error)
                    ? "已关闭九宫格（节点上的边距保留，导出图的 spriteBorder 已清空）。"
                    : "已关闭九宫格。（" + error + "）";
                _statusIsError = false;
            }
        }

        private void ClearExportedSpriteBorder(out string texturePath, out string error)
        {
            texturePath = null;
            error = null;
            if (!Psd2UiNineSliceNodeState.TryResolveExportedSpriteAsset(_node, out texturePath))
            {
                error = "找不到该节点导出的 PNG。";
                return;
            }

            Psd2UiNineSliceNodeState.ApplyBorderToImportedSprite(
                _node, texturePath, new Psd2UiNineSliceBorder(0, 0, 0, 0), out error);
        }

        private void DrawGuides(Rect imageRect)
        {
            if (!_enabled || _border == null || _previewWidth <= 0 || _previewHeight <= 0)
            {
                return;
            }

            Color old = Handles.color;
            float left = imageRect.xMin + (imageRect.width * _border.Left / _previewWidth);
            float right = imageRect.xMax - (imageRect.width * _border.Right / _previewWidth);
            float top = imageRect.yMin + (imageRect.height * _border.Top / _previewHeight);
            float bottom = imageRect.yMax - (imageRect.height * _border.Bottom / _previewHeight);

            Handles.color = _activeDrag == DragGuide.Left || _activeDrag == DragGuide.Right ? ActiveGuideColor : GuideColor;
            Handles.DrawLine(new Vector3(left, imageRect.yMin), new Vector3(left, imageRect.yMax));
            Handles.DrawLine(new Vector3(right, imageRect.yMin), new Vector3(right, imageRect.yMax));
            Handles.color = _activeDrag == DragGuide.Top || _activeDrag == DragGuide.Bottom ? ActiveGuideColor : GuideColor;
            Handles.DrawLine(new Vector3(imageRect.xMin, top), new Vector3(imageRect.xMax, top));
            Handles.DrawLine(new Vector3(imageRect.xMin, bottom), new Vector3(imageRect.xMax, bottom));
            Handles.color = old;

            DrawGuideHandle(new Vector2(left, imageRect.yMin));
            DrawGuideHandle(new Vector2(left, imageRect.yMax));
            DrawGuideHandle(new Vector2(right, imageRect.yMin));
            DrawGuideHandle(new Vector2(right, imageRect.yMax));
            DrawGuideHandle(new Vector2(imageRect.xMin, top));
            DrawGuideHandle(new Vector2(imageRect.xMax, top));
            DrawGuideHandle(new Vector2(imageRect.xMin, bottom));
            DrawGuideHandle(new Vector2(imageRect.xMax, bottom));
        }

        private static void DrawGuideHandle(Vector2 center)
        {
            const float size = 7f;
            EditorGUI.DrawRect(new Rect(center.x - (size * 0.5f), center.y - (size * 0.5f), size, size), GuideColor);
        }

        private void HandleGuideDrag(Rect imageRect)
        {
            if (!_enabled || _border == null || Event.current == null || _previewWidth <= 0 || _previewHeight <= 0)
            {
                return;
            }

            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && imageRect.Contains(evt.mousePosition))
            {
                _activeDrag = FindClosestGuide(imageRect, evt.mousePosition);
                if (_activeDrag != DragGuide.None)
                {
                    evt.Use();
                }
            }
            else if (evt.type == EventType.MouseDrag && _activeDrag != DragGuide.None)
            {
                _border = BorderFromDrag(_activeDrag, imageRect, evt.mousePosition);
                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && _activeDrag != DragGuide.None)
            {
                _activeDrag = DragGuide.None;
                ScheduleApply();
                evt.Use();
            }
        }

        private DragGuide FindClosestGuide(Rect imageRect, Vector2 mouse)
        {
            float bestDistance = float.MaxValue;
            DragGuide best = DragGuide.None;
            TrySelectGuide(DragGuide.Left, Mathf.Abs(mouse.x - (imageRect.xMin + (imageRect.width * _border.Left / _previewWidth))), ref bestDistance, ref best);
            TrySelectGuide(DragGuide.Right, Mathf.Abs(mouse.x - (imageRect.xMax - (imageRect.width * _border.Right / _previewWidth))), ref bestDistance, ref best);
            TrySelectGuide(DragGuide.Top, Mathf.Abs(mouse.y - (imageRect.yMin + (imageRect.height * _border.Top / _previewHeight))), ref bestDistance, ref best);
            TrySelectGuide(DragGuide.Bottom, Mathf.Abs(mouse.y - (imageRect.yMax - (imageRect.height * _border.Bottom / _previewHeight))), ref bestDistance, ref best);
            return bestDistance < GuideHitThreshold ? best : DragGuide.None;
        }

        private static void TrySelectGuide(DragGuide guide, float distance, ref float bestDistance, ref DragGuide best)
        {
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = guide;
            }
        }

        private Psd2UiNineSliceBorder BorderFromDrag(DragGuide guide, Rect imageRect, Vector2 mouse)
        {
            int left = _border.Left;
            int top = _border.Top;
            int right = _border.Right;
            int bottom = _border.Bottom;
            switch (guide)
            {
                case DragGuide.Left:
                    left = Mathf.RoundToInt((mouse.x - imageRect.xMin) / imageRect.width * _previewWidth);
                    break;
                case DragGuide.Right:
                    right = Mathf.RoundToInt((imageRect.xMax - mouse.x) / imageRect.width * _previewWidth);
                    break;
                case DragGuide.Top:
                    top = Mathf.RoundToInt((mouse.y - imageRect.yMin) / imageRect.height * _previewHeight);
                    break;
                case DragGuide.Bottom:
                    bottom = Mathf.RoundToInt((imageRect.yMax - mouse.y) / imageRect.height * _previewHeight);
                    break;
            }

            return Psd2UiNineSliceNodeState.ClampBorder(new Psd2UiNineSliceBorder(left, top, right, bottom), _previewWidth, _previewHeight);
        }

        private static Rect GetAspectFitRect(Rect available, int width, int height)
        {
            if (width <= 0 || height <= 0 || available.width <= 0f || available.height <= 0f)
            {
                return available;
            }

            float sourceAspect = width / (float)height;
            float availableAspect = available.width / available.height;
            if (sourceAspect > availableAspect)
            {
                float heightFit = available.width / sourceAspect;
                return new Rect(available.x, available.y + ((available.height - heightFit) * 0.5f), available.width, heightFit);
            }

            float widthFit = available.height * sourceAspect;
            return new Rect(available.x + ((available.width - widthFit) * 0.5f), available.y, widthFit, available.height);
        }

        /// <summary>
        /// 透明底棋盘格：Photoshop 会保留透明像素下方的 RGB，
        /// 直接画纹理会把被隐藏的底色透出来。
        /// </summary>
        private static void DrawCheckerboard(Rect rect)
        {
            const float cellSize = 12f;
            int columns = Mathf.CeilToInt(rect.width / cellSize);
            int rows = Mathf.CeilToInt(rect.height / cellSize);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    EditorGUI.DrawRect(
                        new Rect(rect.x + (column * cellSize), rect.y + (row * cellSize), cellSize, cellSize),
                        ((row + column) & 1) == 0 ? CheckerLight : CheckerDark);
                }
            }
        }
    }
}
