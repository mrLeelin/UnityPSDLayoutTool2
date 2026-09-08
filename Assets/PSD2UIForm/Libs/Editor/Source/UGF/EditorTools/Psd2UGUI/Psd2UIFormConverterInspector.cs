using System.IO;
using PsdProtectionGuards;
using UnityEditor;
using UnityEngine;
using cn.efunstudio.psdreader;
using PsdProtectionRuntime;
using PsdPathUtilities;

namespace UGF.EditorTools.Psd2UGUI
{

[CustomEditor(typeof(Psd2UIFormConverter))]
internal sealed class Psd2UIFormConverterInspector : Editor
{
	private Psd2UIFormConverter targetLogic;

	private GUIContent parsePsd2NodesBt;

	private GUIContent exportUISpritesBt;

	private GUIContent generateUIFormBt;

	private GUILayoutOption btHeight;

	private bool metadataDebugFoldout;

	private int metadataDebugSelection;

	private bool metadataDebugShowJson;

	private Vector2 metadataDebugScroll;

	private void OnEnable()
	{
		btHeight = GUILayout.Height(30f);
		targetLogic = base.target as Psd2UIFormConverter;
		parsePsd2NodesBt = new GUIContent("解析psd图层", "把psd图层解析为可编辑节点树");
		exportUISpritesBt = new GUIContent("导出Images", "导出勾选的psd图层为碎图");
		generateUIFormBt = new GUIContent("生成UIForm", "根据解析后的节点树生成UIForm Prefab");
		if (ScriptableSingleton<Psd2UIFormSettings>.Instance.CompressImage)
		{
			ScriptableSingleton<Psd2UIFormSettings>.Instance.CompressImage = false;
			ScriptableSingleton<Psd2UIFormSettings>.SFSnrIUk3L();
		}
		if (string.IsNullOrWhiteSpace(ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir))
		{
			Debug.LogWarning("UIForm输出路径为空!");
		}
	}

	private void OnDisable()
	{
		ScriptableSingleton<Psd2UIFormSettings>.SFSnrIUk3L();
	}

	public override void OnInspectorGUI()
	{
		bool flag = false;
		PsdReaderProductAccess.EnsureDailyUpdateCheck();
		DrawPendingUpdateTip();
		if (targetLogic.HasLoadedDocument())
		{
			EditorGUILayout.BeginVertical("box");
			if (GUILayout.Button("查看使用文档"))
			{
				Application.OpenURL("https://efunstudio.cn");
				GUIUtility.ExitGUI();
			}
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("UI图片导出路径:", GUILayout.Width(150f));
			ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir = EditorGUILayout.TextField(ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir);
			if (GUILayout.Button("选择路径", GUILayout.Width(80f)))
			{
				string text = EditorUtility.OpenFolderPanel("选择导出路径", ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir, null);
				if (!string.IsNullOrWhiteSpace(text))
				{
					if (!text.StartsWith("Assets/"))
					{
						text = RelativePathUtilities.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text);
					}
					ScriptableSingleton<Psd2UIFormSettings>.Instance.UIImagesOutputDir = text;
					ScriptableSingleton<Psd2UIFormSettings>.SFSnrIUk3L();
				}
				flag = true;
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			ScriptableSingleton<Psd2UIFormSettings>.Instance.UseUIFormOutputDir = EditorGUILayout.ToggleLeft("使用UIForm导出路径:", ScriptableSingleton<Psd2UIFormSettings>.Instance.UseUIFormOutputDir, GUILayout.Width(150f));
			EditorGUI.BeginDisabledGroup(!ScriptableSingleton<Psd2UIFormSettings>.Instance.UseUIFormOutputDir);
			ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir = EditorGUILayout.TextField(ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir);
			if (GUILayout.Button("选择路径", GUILayout.Width(80f)))
			{
				string text2 = EditorUtility.OpenFolderPanel("选择导出路径", ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir, null);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					if (!text2.StartsWith("Assets/"))
					{
						text2 = RelativePathUtilities.GetRelativePath(Directory.GetParent(Application.dataPath).FullName, text2);
					}
					ScriptableSingleton<Psd2UIFormSettings>.Instance.UIFormOutputDir = text2;
					ScriptableSingleton<Psd2UIFormSettings>.SFSnrIUk3L();
				}
				flag = true;
			}
			EditorGUI.EndDisabledGroup();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button(parsePsd2NodesBt, btHeight) && TryGetKeepExistingUITypeSelection(out var keepExistingUIType))
			{
				Psd2UIFormConverter.CreateUiFormEditorPrefabFromPsd(targetLogic.GetPsdAssetPath(), targetLogic, keepExistingUIType);
			}
			if (GUILayout.Button(exportUISpritesBt, btHeight))
			{
				targetLogic.ExportMarkedLayerImages();
			}
			EditorGUILayout.EndHorizontal();
			if (GUILayout.Button(generateUIFormBt, btHeight))
			{
				targetLogic.ExportUiForm();
			}
			DrawMetadataDebugPanel();
			base.OnInspectorGUI();
			if (flag)
			{
				GUIUtility.ExitGUI();
			}
		}
		else
		{
			EditorGUILayout.HelpBox("请打开Prefab,进入Psd2UIForm编辑界面后才能操作!", MessageType.Error);
			if (GUILayout.Button("打开编辑界面"))
			{
				Psd2UIFormConverter.OpenUiFormEditorPrefab(AssetDatabase.GetAssetPath(base.target));
			}
		}
	}

	private static void DrawPendingUpdateTip()
	{
		if (PsdReaderProductAccess.HasPendingUpdateTip() && Psd2UIFormEditorNoticeUtility.DrawVersionUpdateNotice(PsdReaderProductAccess.GetPendingUpdateTipMessage(), "下载") && PsdReaderProductAccess.TryOpenPendingUpdateDownloadUrl())
		{
			GUIUtility.ExitGUI();
		}
	}

	private static bool TryGetKeepExistingUITypeSelection(out bool keepExistingUIType)
	{
		int num = EditorUtility.DisplayDialogComplex("重新解析PSD图层", "重新解析会重建当前节点树。\n\n是否保留现有UI类型？\n\n选择“是”会按当前节点层级尽量回填已有 UI Type，新图层仍按默认规则初始化。\n选择“否”会按PSD图层标记重新初始化 UI Type。", "是，保留现有UI类型", "否，重新解析UI类型", "取消");
		keepExistingUIType = num == 0;
		return num != 2;
	}

	private void DrawMetadataDebugPanel()
	{
		metadataDebugFoldout = EditorGUILayout.Foldout(metadataDebugFoldout, "生成元数据调试", toggleOnLabelClick: true);
		if (!metadataDebugFoldout)
		{
			return;
		}
		EditorGUILayout.BeginVertical("box");
		EditorGUILayout.LabelField("数据宿主:", targetLogic.GetConverterAssetDescription() ?? "<None>");
		string[] array = targetLogic.GetMetadataPrefabPaths();
		if (array != null && array.Length >= 1)
		{
			metadataDebugSelection = Mathf.Clamp(metadataDebugSelection, 0, array.Length - 1);
			metadataDebugSelection = EditorGUILayout.Popup("目标Prefab", metadataDebugSelection, array);
			string text = array[metadataDebugSelection];
			EditorGUILayout.LabelField("路径:", text);
			string text2 = targetLogic.SerializePrefabMetadata(text);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button(metadataDebugShowJson ? "隐藏内容" : "查看元数据", GUILayout.Width(100f)))
			{
				metadataDebugShowJson = !metadataDebugShowJson;
			}
			if (GUILayout.Button("复制JSON", GUILayout.Width(100f)))
			{
				EditorGUIUtility.systemCopyBuffer = text2;
			}
			EditorGUILayout.EndHorizontal();
			if (metadataDebugShowJson)
			{
				metadataDebugScroll = EditorGUILayout.BeginScrollView(metadataDebugScroll, GUILayout.MinHeight(140f), GUILayout.MaxHeight(320f));
				EditorGUILayout.TextArea(text2 ?? string.Empty, GUILayout.ExpandHeight(expand: true));
				EditorGUILayout.EndScrollView();
			}
		}
		else
		{
			metadataDebugShowJson = false;
			EditorGUILayout.HelpBox("当前未找到生成元数据。先执行一次生成UIForm或增量导出后再查看。", MessageType.Info);
		}
		EditorGUILayout.EndVertical();
	}

	public override bool HasPreviewGUI()
	{
		return targetLogic.GetDisplaySprite() != null;
	}

	public override void OnPreviewGUI(Rect r, GUIStyle background)
	{
		GUI.DrawTexture(r, targetLogic.GetDisplaySprite().texture, ScaleMode.ScaleToFit);
	}

	public Psd2UIFormConverterInspector()
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
	{
	}

	private Psd2UIFormConverterInspector(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
		: base()
	{
	}
}
}
