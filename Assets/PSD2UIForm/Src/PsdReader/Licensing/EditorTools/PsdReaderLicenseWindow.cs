using System;
using LicenseStatusPresenterNamespace;
using PsdReaderLicenseServiceNamespace;
using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using Object = UnityEngine.Object;
using ProjectLicenseExportWindowNamespace;
using cn.efunstudio.psdreader;
using LicenseAvailabilityStateNamespace;
using PsdReaderLicenseStatusNamespace;

namespace PsdReaderLicenseWindowNamespace
{
    [MovedFrom(true, sourceNamespace: "jO3MlVQ8MFUyMw374tQ", sourceAssembly: "cn.efunstudio.psd2ugui", sourceClassName: "FKkkJsQChRtjAbgrMEF")]
    internal sealed class PsdReaderLicenseWindow : EditorWindow
    {
        [SerializeField]
        private Vector2 m_ScrollPosition;

        private string _orderId = string.Empty;

        private PsdReaderLicenseStatus _licenseStatus;

        internal static PsdReaderLicenseWindow s_ObfuscationSentinel;

        internal static void OpenLicenseManager()
        {
            ShowWindow();
        }

        internal static void ClearLicenseDataWithConfirmation()
        {
            if (EditorUtility.DisplayDialog("Psd2UGUI License", "确定清除本地授权缓存和插件目录授权文件吗？", "确定", "取消"))
            {
                string message = PsdReaderLicenseService.ClearLicenseData();
                EditorUtility.DisplayDialog("Psd2UGUI License", message, "确定");
            }
        }

        internal static void CheckForUpdates()
        {
            PsdReaderProductAccess.CheckForUpdatesAndPrompt();
        }

        internal static void ShowWindow()
        {
            PsdReaderLicenseWindow window = EditorWindow.GetWindow<PsdReaderLicenseWindow>(false, "Psd2UGUI License");
            ((EditorWindow)window).minSize = new Vector2(520f, 360f);
            window.RefreshLicenseStatus();
            ((EditorWindow)window).Show();
        }

        private void OnEnable()
        {
            ((EditorWindow)this).minSize = new Vector2(520f, 360f);
            RefreshLicenseStatus();
        }

        private void OnFocus()
        {
            RefreshLicenseStatus();
        }

        private void OnGUI()
        {
            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, Array.Empty<GUILayoutOption>());
            EditorGUILayout.LabelField("Psd2UGUI 授权", EditorStyles.boldLabel, Array.Empty<GUILayoutOption>());
            EditorGUILayout.Space(6f);
            DrawActivationHelp();
            EditorGUILayout.Space(8f);
            DrawActivationControls();
            EditorGUILayout.Space(8f);
            DrawCurrentStatus();
            EditorGUILayout.EndScrollView();
        }

        private void DrawActivationHelp()
        {
            EnsureLicenseStatus();
            EditorGUILayout.HelpBox(LicenseStatusPresenter.GetActivationHelpText(_licenseStatus), GetMessageType(_licenseStatus));
        }

        private void DrawActivationControls()
        {
            EditorGUILayout.LabelField("激活", EditorStyles.boldLabel, Array.Empty<GUILayoutOption>());
            EnsureLicenseStatus();
            EditorGUI.BeginChangeCheck();
            string enteredOrderId = EditorGUILayout.PasswordField("订单号", _orderId, Array.Empty<GUILayoutOption>());
            if (EditorGUI.EndChangeCheck())
            {
                _orderId = enteredOrderId ?? string.Empty;
            }
            EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            if (GUILayout.Button("激活授权", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(28f) }))
            {
                ActivateWithOrder();
            }
            if (GUILayout.Button("刷新授权", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(28f) }))
            {
                RefreshLicense();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            EditorGUI.DisabledScope licenseFileActivationDisabledScope = default(EditorGUI.DisabledScope);
            licenseFileActivationDisabledScope = new EditorGUI.DisabledScope(!PsdReaderLicenseService.HasProjectLicenseFile());
            try
            {
                if (GUILayout.Button("使用授权文件激活", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(24f) }))
                {
                    ActivateFromProjectLicenseFile();
                }
            }
            finally
            {
                licenseFileActivationDisabledScope.Dispose();
            }
            if (PsdReaderLicenseService.CanExportProjectLicense() && GUILayout.Button("保存授权文件到插件目录", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(24f) }))
            {
                OpenProjectLicenseExportWindow();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(PsdReaderLicenseService.GetProjectLicenseHint(), (MessageType)0);
        }

        private void DrawCurrentStatus()
        {
            EnsureLicenseStatus();
            EditorGUILayout.LabelField("当前状态", EditorStyles.boldLabel, Array.Empty<GUILayoutOption>());
            if (_licenseStatus != null)
            {
                EditorGUI.DisabledScope statusFieldDisabledScope = default(EditorGUI.DisabledScope);
                statusFieldDisabledScope = new EditorGUI.DisabledScope(true);
                try
                {
                    EditorGUILayout.TextField("授权状态", LicenseStatusPresenter.GetStatusLabel(_licenseStatus), Array.Empty<GUILayoutOption>());
                }
                finally
                {
                    statusFieldDisabledScope.Dispose();
                }
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(LicenseStatusPresenter.GetStatusMessage(_licenseStatus), GetMessageType(_licenseStatus));
            }
            else
            {
                EditorGUILayout.HelpBox("授权状态不可用。", (MessageType)1);
            }
        }

        private void ActivateWithOrder()
        {
            string message;
            bool activationSucceeded = PsdReaderLicenseService.ActivateWithOrder(_orderId, out message);
            RefreshLicenseStatus();
            if (!activationSucceeded)
            {
                switch (EditorUtility.DisplayDialogComplex("Psd2UGUI License", "订单号验证失败，请前往购买获取。", "前往获取订单号", "前往官网", string.Empty))
                {
                case 0:
                    Application.OpenURL("https://shop106471535.taobao.com");
                    break;
                case 1:
                    Application.OpenURL("https://efunstudio.cn");
                    break;
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Psd2UGUI License", LicenseStatusPresenter.GetStatusMessage(_licenseStatus), "确定");
            }
        }

        private void RefreshLicense()
        {
            PsdReaderLicenseService.RefreshLicense(out var _);
            RefreshLicenseStatus();
            EditorUtility.DisplayDialog("Psd2UGUI License", LicenseStatusPresenter.GetStatusMessage(_licenseStatus), "确定");
        }

        private void ActivateFromProjectLicenseFile()
        {
            PsdReaderLicenseService.ActivateFromProjectLicenseFile(out var _);
            RefreshLicenseStatus();
            EditorUtility.DisplayDialog("Psd2UGUI License", LicenseStatusPresenter.GetStatusMessage(_licenseStatus), "确定");
        }

        private void OpenProjectLicenseExportWindow()
        {
            ProjectLicenseExportWindow.ShowWindow();
        }

        private void RefreshLicenseStatus()
        {
            _licenseStatus = PsdReaderLicenseService.GetLicenseStatus();
            ((EditorWindow)this).Repaint();
        }

        private void EnsureLicenseStatus()
        {
            if (_licenseStatus == null)
            {
                _licenseStatus = PsdReaderLicenseService.GetLicenseStatus();
            }
        }

        private static MessageType GetMessageType(object licenseStatus)
        {
            return (MessageType)(LicenseStatusPresenter.GetAvailabilityState(licenseStatus) switch
            {
                (LicenseAvailabilityState)1 => 2, 
                (LicenseAvailabilityState)0 => 1, 
                _ => 3, 
            });
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return (object)s_ObfuscationSentinel == null;
        }

        internal static PsdReaderLicenseWindow GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
