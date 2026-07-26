namespace PsdLayoutTool2.Tests
{
    using System;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public sealed class PsdHierarchyAiProviderSettingsTests
    {
        [Test]
        public void NewSettingsDefaultToCodexAndDefaultConnections()
        {
            PsdLayoutProjectSettings settings = CreateSettings();
            try
            {
                PsdHierarchyAiSettingsSnapshot snapshot = settings.ResolveAiSettings();

                Assert.That(snapshot.provider, Is.EqualTo(PsdHierarchyAiProvider.Codex));
                Assert.That(snapshot.codex.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Default));
                Assert.That(snapshot.claude.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Default));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ProviderConnectionValuesRemainIndependentWhenSwitching()
        {
            PsdLayoutProjectSettings settings = CreateSettings();
            try
            {
                settings.SetAiProvider(PsdHierarchyAiProvider.Claude);
                settings.SetAiConnectionMode(PsdHierarchyAiProvider.Claude, PsdHierarchyAiConnectionMode.Custom);
                settings.SetAiBaseUrl(PsdHierarchyAiProvider.Claude, "https://claude.example.com/v1");
                settings.SetAiConnectionMode(PsdHierarchyAiProvider.Codex, PsdHierarchyAiConnectionMode.Default);
                settings.SetAiBaseUrl(PsdHierarchyAiProvider.Codex, "http://127.0.0.1:8080/v1");
                settings.SetAiProvider(PsdHierarchyAiProvider.Codex);

                PsdHierarchyAiSettingsSnapshot snapshot = settings.ResolveAiSettings();

                Assert.That(snapshot.provider, Is.EqualTo(PsdHierarchyAiProvider.Codex));
                Assert.That(snapshot.claude.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Custom));
                Assert.That(snapshot.claude.baseUrl, Is.EqualTo("https://claude.example.com/v1"));
                Assert.That(snapshot.codex.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Default));
                Assert.That(snapshot.codex.baseUrl, Is.EqualTo("http://127.0.0.1:8080/v1"));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [TestCase("https://api.example.com/v1", true)]
        [TestCase("http://127.0.0.1:8080/v1", true)]
        [TestCase("http://localhost:8080/v1", true)]
        [TestCase("http://[::1]:8080/v1", true)]
        [TestCase("http://api.example.com/v1", false)]
        [TestCase("https://user:pass@api.example.com/v1", false)]
        [TestCase("https://api.example.com/v1?key=secret", false)]
        [TestCase("https://api.example.com/v1#secret", false)]
        [TestCase("/relative/path", false)]
        [TestCase("not a url", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void CustomUrlValidationEnforcesHttpsExceptLoopback(string value, bool expected)
        {
            bool valid = PsdHierarchyAiConnectionSettings.TryValidateBaseUrl(value, out string error);

            Assert.That(valid, Is.EqualTo(expected));
            Assert.That(error, expected ? Is.Empty : Is.Not.Empty);
        }

        [Test]
        public void SameValuesAreNoOpsWhileChangedValuesMarkSettingsDirty()
        {
            PsdLayoutProjectSettings settings = CreateSettings();
            try
            {
                settings.ResolveAiSettings();
                EditorUtility.ClearDirty(settings);

                settings.SetAiProvider(PsdHierarchyAiProvider.Codex);
                settings.SetAiConnectionMode(PsdHierarchyAiProvider.Codex, PsdHierarchyAiConnectionMode.Default);
                settings.SetAiBaseUrl(PsdHierarchyAiProvider.Codex, string.Empty);

                Assert.That(EditorUtility.IsDirty(settings), Is.False);

                settings.SetAiProvider(PsdHierarchyAiProvider.Claude);

                Assert.That(EditorUtility.IsDirty(settings), Is.True);

                EditorUtility.ClearDirty(settings);
                settings.SetAiConnectionMode(PsdHierarchyAiProvider.Claude, PsdHierarchyAiConnectionMode.Custom);
                Assert.That(EditorUtility.IsDirty(settings), Is.True);

                EditorUtility.ClearDirty(settings);
                settings.SetAiBaseUrl(PsdHierarchyAiProvider.Claude, "https://claude.example.com/v1");
                Assert.That(EditorUtility.IsDirty(settings), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ResolvedSnapshotDoesNotRetainMutableConnectionSettings()
        {
            PsdLayoutProjectSettings settings = CreateSettings();
            try
            {
                settings.SetAiConnectionMode(PsdHierarchyAiProvider.Codex, PsdHierarchyAiConnectionMode.Custom);
                settings.SetAiBaseUrl(PsdHierarchyAiProvider.Codex, "https://first.example.com/v1");
                PsdHierarchyAiSettingsSnapshot snapshot = settings.ResolveAiSettings();

                settings.SetAiConnectionMode(PsdHierarchyAiProvider.Codex, PsdHierarchyAiConnectionMode.Default);
                settings.SetAiBaseUrl(PsdHierarchyAiProvider.Codex, "https://second.example.com/v1");

                Assert.That(snapshot.codex.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Custom));
                Assert.That(snapshot.codex.baseUrl, Is.EqualTo("https://first.example.com/v1"));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void UnsupportedProviderValuesFailClosed()
        {
            var invalidProvider = (PsdHierarchyAiProvider)99;
            var connection = new PsdHierarchyAiConnectionSnapshot(
                PsdHierarchyAiConnectionMode.Default,
                string.Empty);
            var snapshot = new PsdHierarchyAiSettingsSnapshot(invalidProvider, connection, connection);
            PsdLayoutProjectSettings settings = CreateSettings();
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                {
                    PsdHierarchyAiConnectionSnapshot ignored = snapshot.activeConnection;
                });

                var serializedSettings = new SerializedObject(settings);
                SerializedProperty providerProperty = serializedSettings.FindProperty("aiProvider");
                providerProperty.intValue = 99;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                {
                    PsdHierarchyAiConnectionSnapshot ignored = settings.ResolveAiSettings().activeConnection;
                });

                Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetAiProvider(invalidProvider));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    settings.SetAiConnectionMode(invalidProvider, PsdHierarchyAiConnectionMode.Default));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    settings.SetAiBaseUrl(invalidProvider, "https://api.example.com/v1"));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void UnsupportedConnectionModesFailClosed()
        {
            var invalidMode = (PsdHierarchyAiConnectionMode)99;
            var connection = new PsdHierarchyAiConnectionSettings();

            Assert.Throws<ArgumentOutOfRangeException>(() => connection.SetMode(invalidMode));

            JsonUtility.FromJsonOverwrite("{\"mode\":99}", connection);

            Assert.Throws<InvalidOperationException>(() => connection.Resolve());
        }

        [Test]
        public void LegacySerializedSettingsWithoutAiFieldsResolveToCompatibleDefaults()
        {
            PsdLayoutProjectSettings settings = CreateSettings();
            try
            {
                EditorJsonUtility.FromJsonOverwrite("{\"settingsVersion\":1}", settings);

                PsdHierarchyAiSettingsSnapshot snapshot = settings.ResolveAiSettings();

                Assert.That(snapshot.provider, Is.EqualTo(PsdHierarchyAiProvider.Codex));
                Assert.That(snapshot.codex.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Default));
                Assert.That(snapshot.claude.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Default));

                settings.SetAiConnectionMode(PsdHierarchyAiProvider.Claude, PsdHierarchyAiConnectionMode.Custom);
                PsdHierarchyAiSettingsSnapshot changed = settings.ResolveAiSettings();
                Assert.That(changed.claude.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Custom));
                Assert.That(changed.codex.mode, Is.EqualTo(PsdHierarchyAiConnectionMode.Default));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [TestCase("https://user:pass@api.example.com/v1")]
        [TestCase("https://api.example.com/v1?key=secret")]
        [TestCase("https://api.example.com/v1#secret")]
        public void SetAiBaseUrlRejectsUnsafeUrlsWithoutChangingSettings(string unsafeUrl)
        {
            PsdLayoutProjectSettings settings = CreateSettings();
            try
            {
                const string validUrl = "https://api.example.com/v1";
                settings.SetAiBaseUrl(PsdHierarchyAiProvider.Codex, validUrl);
                EditorUtility.ClearDirty(settings);

                Assert.Throws<ArgumentException>(() =>
                    settings.SetAiBaseUrl(PsdHierarchyAiProvider.Codex, unsafeUrl));

                Assert.That(settings.ResolveAiSettings().codex.baseUrl, Is.EqualTo(validUrl));
                Assert.That(EditorUtility.IsDirty(settings), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void SetAiBaseUrlAllowsWhitespaceToClearAnUnconfiguredConnection()
        {
            PsdLayoutProjectSettings settings = CreateSettings();
            try
            {
                settings.ResolveAiSettings();
                EditorUtility.ClearDirty(settings);
                settings.SetAiBaseUrl(PsdHierarchyAiProvider.Codex, "  ");

                Assert.That(settings.ResolveAiSettings().codex.baseUrl, Is.Empty);
                Assert.That(EditorUtility.IsDirty(settings), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        private static PsdLayoutProjectSettings CreateSettings()
        {
            return ScriptableObject.CreateInstance<PsdLayoutProjectSettings>();
        }
    }
}
