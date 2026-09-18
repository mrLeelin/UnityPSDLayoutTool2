namespace PsdLayoutTool2.Tests
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;

    public sealed class PsdLayoutLocalUserSettingsTests
    {
        private string tempFile;

        [SetUp]
        public void SetUp()
        {
            tempFile = Path.Combine(Path.GetTempPath(), "psd-layout-tool2-user-settings-" + Guid.NewGuid().ToString("N") + ".json");
            PsdLayoutLocalUserSettings.ResetCacheForTests();
            PsdLayoutLocalUserSettings.PathOverride = tempFile;
        }

        [TearDown]
        public void TearDown()
        {
            PsdLayoutLocalUserSettings.ResetCacheForTests();
            if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }

            if (!string.IsNullOrEmpty(tempFile) && Directory.Exists(tempFile))
            {
                Directory.Delete(tempFile, true);
            }
        }

        [Test]
        public void SaveThenLoadRoundTripsPersonalFields()
        {
            var data = new PsdLayoutLocalUserSettings.Data
            {
                previewServerPort = 12345,
                showNineSliceImageMarkers = true,
            };
            data.ai.provider = (int)PsdHierarchyAiProvider.Grok;
            data.ai.customModel = "grok-4";
            data.ai.reasoningEffort = "high";
            data.ai.customEndpoint = string.Empty;

            PsdLayoutLocalUserSettings.Save(data);
            PsdLayoutLocalUserSettings.ResetCacheForTests();
            PsdLayoutLocalUserSettings.PathOverride = tempFile;

            PsdLayoutLocalUserSettings.Data loaded = PsdLayoutLocalUserSettings.Load();

            Assert.That(File.Exists(tempFile), Is.True);
            Assert.That(loaded.previewServerPort, Is.EqualTo(12345));
            Assert.That(loaded.showNineSliceImageMarkers, Is.True);
            Assert.That(loaded.ai.provider, Is.EqualTo((int)PsdHierarchyAiProvider.Grok));
            Assert.That(loaded.ai.customModel, Is.EqualTo("grok-4"));
            Assert.That(loaded.ai.reasoningEffort, Is.EqualTo("high"));
        }

        [Test]
        public void MissingFileUsesDefaults()
        {
            PsdLayoutLocalUserSettings.Data data = PsdLayoutLocalUserSettings.Load();

            Assert.That(data.ai.provider, Is.EqualTo((int)PsdHierarchyAiProvider.None));
            Assert.That(data.previewServerPort, Is.EqualTo(PsdCommonAssetPreviewSettings.DefaultPort));
            Assert.That(data.showNineSliceImageMarkers, Is.EqualTo(PsdLayoutProjectNineSliceSettings.DefaultShowImageMarkers));
        }

        [Test]
        public void CorruptJsonFallsBackToDefaultsInsteadOfThrowing()
        {
            File.WriteAllText(tempFile, "{ not valid json");
            PsdLayoutLocalUserSettings.ResetCacheForTests();
            PsdLayoutLocalUserSettings.PathOverride = tempFile;

            PsdLayoutLocalUserSettings.Data data = PsdLayoutLocalUserSettings.Load();

            Assert.That(data.ai.provider, Is.EqualTo((int)PsdHierarchyAiProvider.None));
            Assert.That(data.previewServerPort, Is.EqualTo(PsdCommonAssetPreviewSettings.DefaultPort));
        }

        [Test]
        public void OutOfRangePortAndUnknownProviderAreClamped()
        {
            File.WriteAllText(
                tempFile,
                "{\"version\":1,\"ai\":{\"provider\":99,\"customModel\":null,\"reasoningEffort\":null,\"customEndpoint\":null},\"previewServerPort\":0,\"showNineSliceImageMarkers\":true}");
            PsdLayoutLocalUserSettings.ResetCacheForTests();
            PsdLayoutLocalUserSettings.PathOverride = tempFile;

            PsdLayoutLocalUserSettings.Data data = PsdLayoutLocalUserSettings.Load();

            Assert.That(data.ai.provider, Is.EqualTo((int)PsdHierarchyAiProvider.None));
            Assert.That(data.ai.customModel, Is.EqualTo(string.Empty));
            Assert.That(data.previewServerPort, Is.EqualTo(PsdCommonAssetPreviewSettings.DefaultPort));
            Assert.That(data.showNineSliceImageMarkers, Is.True);
        }

        [Test]
        public void FailedSaveReturnsErrorAndDoesNotReplaceCachedSettings()
        {
            var original = new PsdLayoutLocalUserSettings.Data { previewServerPort = 12345 };
            Assert.That(PsdLayoutLocalUserSettings.Save(original, out string initialError), Is.True, initialError);
            File.Delete(tempFile);
            Directory.CreateDirectory(tempFile);

            var replacement = new PsdLayoutLocalUserSettings.Data { previewServerPort = 23456 };
            ExpectSaveFailureLog();
            bool saved = PsdLayoutLocalUserSettings.Save(replacement, out string error);

            Assert.That(saved, Is.False);
            Assert.That(error, Does.Contain("保存个人配置失败"));
            Assert.That(PsdLayoutLocalUserSettings.Load().previewServerPort, Is.EqualTo(12345));
        }

        [Test]
        public void ProjectSettingsSurfacePersonalSettingsPersistenceFailures()
        {
            var original = new PsdLayoutLocalUserSettings.Data
            {
                previewServerPort = 12345,
                showNineSliceImageMarkers = false,
            };
            original.ai.provider = (int)PsdHierarchyAiProvider.Codex;
            Assert.That(PsdLayoutLocalUserSettings.Save(original, out string initialError), Is.True, initialError);

            PsdLayoutProjectSettings settings = ScriptableObject.CreateInstance<PsdLayoutProjectSettings>();
            try
            {
                // 在路径仍可读时先完成一次迁移检查；随后同一路径改成目录，稳定制造写盘失败。
                Assert.That(settings.ResolvePreviewServerPort(), Is.EqualTo(12345));
                File.Delete(tempFile);
                Directory.CreateDirectory(tempFile);

                ExpectSaveFailureLog();
                Assert.That(
                    settings.TrySetHierarchyAiSettings(
                        PsdHierarchyAiProvider.Claude,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        out string aiError),
                    Is.False);
                Assert.That(aiError, Does.Contain("保存个人配置失败"));

                ExpectSaveFailureLog();
                Assert.That(settings.TryClearHierarchyAiSettings(out string clearError), Is.False);
                Assert.That(clearError, Does.Contain("保存个人配置失败"));

                ExpectSaveFailureLog();
                Assert.That(settings.TrySetPreviewServerPort(23456, out string portError), Is.False);
                Assert.That(portError, Does.Contain("保存个人配置失败"));

                ExpectSaveFailureLog();
                Assert.That(settings.TrySetNineSliceImageMarkers(true, out string markerError), Is.False);
                Assert.That(markerError, Does.Contain("保存个人配置失败"));

                Assert.That(settings.ResolveHierarchyAiSettings().provider, Is.EqualTo(PsdHierarchyAiProvider.Codex));
                Assert.That(settings.ResolvePreviewServerPort(), Is.EqualTo(12345));
                Assert.That(settings.ResolveNineSliceSettings().showImageMarkers, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void WebSettingsExposePersonalSettingsPersistenceFailureInLastError()
        {
            var original = new PsdLayoutLocalUserSettings.Data { previewServerPort = 12345 };
            Assert.That(PsdLayoutLocalUserSettings.Save(original, out string initialError), Is.True, initialError);

            PsdLayoutProjectSettings settings = ScriptableObject.CreateInstance<PsdLayoutProjectSettings>();
            try
            {
                Assert.That(settings.ResolvePreviewServerPort(), Is.EqualTo(12345));
                File.Delete(tempFile);
                Directory.CreateDirectory(tempFile);

                var server = new PsdLayoutProjectSettingsWebServer();
                typeof(PsdLayoutProjectSettingsWebServer)
                    .GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(server, settings);

                ExpectSaveFailureLog();
                LogAssert.Expect(
                    LogType.Warning,
                    new Regex(@"\[PSDLayoutTool2\] 网页设置未全部生效.*", RegexOptions.CultureInvariant));
                typeof(PsdLayoutProjectSettingsWebServer)
                    .GetMethod("ApplyJson", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(server, new object[] { "{\"previewServerPort\":23456}" });

                string lastError = (string)typeof(PsdLayoutProjectSettingsWebServer)
                    .GetField("lastError", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(server);
                Assert.That(lastError, Does.Contain("保存个人配置失败"));
                Assert.That(settings.ResolvePreviewServerPort(), Is.EqualTo(12345));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static void ExpectSaveFailureLog()
        {
            LogAssert.Expect(
                LogType.Error,
                new Regex(@"\[PSDLayoutTool2\] 保存个人配置失败.*", RegexOptions.CultureInvariant));
        }
    }
}
