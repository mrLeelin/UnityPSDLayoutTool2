namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using NUnit.Framework;

    public sealed class PsdAiSecretStoreTests
    {
        private const string ProjectA = @"C:\Work\Game";
        private const string ProjectB = @"C:\Work\OtherGame";

        [Test]
        public void SaveReadAndClearRoundTripWithoutPersistingPlaintext()
        {
            var values = new FakeLocalValueStore();
            var store = new PsdAiSecretStore(values, new ReversingProtectedDataAdapter());

            store.Save(ProjectA, PsdHierarchyAiProvider.Codex, "sk-secret-value");

            Assert.That(store.TryRead(ProjectA, PsdHierarchyAiProvider.Codex, out string key), Is.True);
            Assert.That(key, Is.EqualTo("sk-secret-value"));
            Assert.That(values.SingleSerializedValue, Does.Not.Contain("sk-secret-value"));

            store.Clear(ProjectA, PsdHierarchyAiProvider.Codex);

            Assert.That(store.TryRead(ProjectA, PsdHierarchyAiProvider.Codex, out key), Is.False);
            Assert.That(key, Is.Empty);
        }

        [Test]
        public void HasSavedCredentialChecksStorageWithoutUnprotecting()
        {
            var values = new FakeLocalValueStore();
            var protection = new CountingProtectedDataAdapter();
            var store = new PsdAiSecretStore(values, protection);
            store.Save(ProjectA, PsdHierarchyAiProvider.Codex, "protected-key");

            bool exists = store.HasSavedCredential(ProjectA, PsdHierarchyAiProvider.Codex);

            Assert.That(exists, Is.True);
            Assert.That(protection.UnprotectCount, Is.Zero);
        }

        [Test]
        public void HasSavedCredentialReportsUnavailableStorageWithoutUnprotecting()
        {
            var protection = new CountingProtectedDataAdapter();
            var store = new PsdAiSecretStore(new ThrowingLocalValueStore(), protection);

            Assert.Throws<PsdAiSecretStoreException>(() =>
                store.HasSavedCredential(ProjectA, PsdHierarchyAiProvider.Codex));
            Assert.That(protection.UnprotectCount, Is.Zero);
        }

        [Test]
        public void ProviderCredentialsDoNotCollide()
        {
            var store = new PsdAiSecretStore(
                new FakeLocalValueStore(),
                new ReversingProtectedDataAdapter());

            store.Save(ProjectA, PsdHierarchyAiProvider.Codex, "codex-key");
            store.Save(ProjectA, PsdHierarchyAiProvider.Claude, "claude-key");

            Assert.That(store.TryRead(ProjectA, PsdHierarchyAiProvider.Codex, out string codexKey), Is.True);
            Assert.That(store.TryRead(ProjectA, PsdHierarchyAiProvider.Claude, out string claudeKey), Is.True);
            Assert.That(codexKey, Is.EqualTo("codex-key"));
            Assert.That(claudeKey, Is.EqualTo("claude-key"));
        }

        [Test]
        public void ProjectCredentialsDoNotCollide()
        {
            var store = new PsdAiSecretStore(
                new FakeLocalValueStore(),
                new ReversingProtectedDataAdapter());

            store.Save(ProjectA, PsdHierarchyAiProvider.Codex, "project-a-key");
            store.Save(ProjectB, PsdHierarchyAiProvider.Codex, "project-b-key");

            Assert.That(store.TryRead(ProjectA, PsdHierarchyAiProvider.Codex, out string projectAKey), Is.True);
            Assert.That(store.TryRead(ProjectB, PsdHierarchyAiProvider.Codex, out string projectBKey), Is.True);
            Assert.That(projectAKey, Is.EqualTo("project-a-key"));
            Assert.That(projectBKey, Is.EqualTo("project-b-key"));
        }

        [Test]
        public void WindowsProjectPathNormalizationIsCaseAndSeparatorInsensitive()
        {
            var store = new PsdAiSecretStore(
                new FakeLocalValueStore(),
                new ReversingProtectedDataAdapter());

            store.Save(@"c:/work/game/", PsdHierarchyAiProvider.Codex, "normalized-key");

            Assert.That(
                store.TryRead(@"C:\WORK\GAME", PsdHierarchyAiProvider.Codex, out string key),
                Is.True);
            Assert.That(key, Is.EqualTo("normalized-key"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void EmptyKeysAreRejectedWithoutChangingExistingCredential(string invalidKey)
        {
            var store = new PsdAiSecretStore(
                new FakeLocalValueStore(),
                new ReversingProtectedDataAdapter());
            store.Save(ProjectA, PsdHierarchyAiProvider.Codex, "existing-key");

            Assert.Throws<ArgumentException>(() =>
                store.Save(ProjectA, PsdHierarchyAiProvider.Codex, invalidKey));
            Assert.That(store.TryRead(ProjectA, PsdHierarchyAiProvider.Codex, out string key), Is.True);
            Assert.That(key, Is.EqualTo("existing-key"));
        }

        [Test]
        public void CorruptSerializedCredentialRaisesActionableSecretStoreError()
        {
            var values = new FakeLocalValueStore();
            var store = new PsdAiSecretStore(values, new ReversingProtectedDataAdapter());
            values.ForcedReadValue = "not-base64";

            PsdAiSecretStoreException exception = Assert.Throws<PsdAiSecretStoreException>(() =>
                store.TryRead(ProjectA, PsdHierarchyAiProvider.Codex, out _));

            Assert.That(exception.Message, Does.Contain("local AI credential"));
            Assert.That(exception.Message, Does.Not.Contain("not-base64"));
        }

        [Test]
        public void ProtectionFailureNeverFallsBackToPlaintext()
        {
            var values = new FakeLocalValueStore();
            var store = new PsdAiSecretStore(values, new ThrowingProtectedDataAdapter());

            PsdAiSecretStoreException exception = Assert.Throws<PsdAiSecretStoreException>(() =>
                store.Save(ProjectA, PsdHierarchyAiProvider.Codex, "must-not-leak"));

            Assert.That(exception.Message, Does.Contain("local AI credential"));
            Assert.That(values.Count, Is.Zero);
        }

        [Test]
        public void UnprotectFailureRaisesActionableSecretStoreError()
        {
            var values = new FakeLocalValueStore();
            var writer = new PsdAiSecretStore(values, new ReversingProtectedDataAdapter());
            writer.Save(ProjectA, PsdHierarchyAiProvider.Codex, "protected-key");
            var reader = new PsdAiSecretStore(values, new ThrowingProtectedDataAdapter());

            PsdAiSecretStoreException exception = Assert.Throws<PsdAiSecretStoreException>(() =>
                reader.TryRead(ProjectA, PsdHierarchyAiProvider.Codex, out _));

            Assert.That(exception.Message, Does.Contain("local AI credential"));
        }

        [Test]
        public void UnavailableLocalValueStoreRaisesActionableSecretStoreError()
        {
            var store = new PsdAiSecretStore(
                new ThrowingLocalValueStore(),
                new ReversingProtectedDataAdapter());

            PsdAiSecretStoreException exception = Assert.Throws<PsdAiSecretStoreException>(() =>
                store.TryRead(ProjectA, PsdHierarchyAiProvider.Codex, out _));

            Assert.That(exception.Message, Does.Contain("local AI credential"));
        }

        [Test]
        public void UnsupportedProviderFailsClosedBeforeStorageAccess()
        {
            var values = new FakeLocalValueStore();
            var store = new PsdAiSecretStore(values, new ReversingProtectedDataAdapter());

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                store.Save(ProjectA, (PsdHierarchyAiProvider)99, "key"));
            Assert.That(values.Count, Is.Zero);
        }

        [Test]
        public void WindowsDpapiAdapterRoundTripsForCurrentUser()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Assert.Ignore("Windows DPAPI is only available on Windows.");
            }

            var adapter = new WindowsDpapiProtectedDataAdapter();
            byte[] plaintext = Encoding.UTF8.GetBytes("temporary-test-credential");
            byte[] protectedData = null;
            byte[] restored = null;
            try
            {
                protectedData = adapter.Protect(plaintext);
                restored = adapter.Unprotect(protectedData);

                Assert.That(protectedData, Is.Not.EqualTo(plaintext));
                Assert.That(restored, Is.EqualTo(plaintext));
            }
            finally
            {
                Array.Clear(plaintext, 0, plaintext.Length);
                if (protectedData != null)
                {
                    Array.Clear(protectedData, 0, protectedData.Length);
                }

                if (restored != null)
                {
                    Array.Clear(restored, 0, restored.Length);
                }
            }
        }

        private sealed class ReversingProtectedDataAdapter : IPsdProtectedDataAdapter
        {
            public byte[] Protect(byte[] plaintext)
            {
                byte[] protectedData = (byte[])plaintext.Clone();
                Array.Reverse(protectedData);
                return protectedData;
            }

            public byte[] Unprotect(byte[] protectedData)
            {
                byte[] plaintext = (byte[])protectedData.Clone();
                Array.Reverse(plaintext);
                return plaintext;
            }
        }

        private sealed class ThrowingProtectedDataAdapter : IPsdProtectedDataAdapter
        {
            public byte[] Protect(byte[] plaintext)
            {
                throw new PlatformNotSupportedException("Protection unavailable.");
            }

            public byte[] Unprotect(byte[] protectedData)
            {
                throw new InvalidOperationException("Protected value is corrupt.");
            }
        }

        private sealed class CountingProtectedDataAdapter : IPsdProtectedDataAdapter
        {
            public int UnprotectCount { get; private set; }

            public byte[] Protect(byte[] plaintext)
            {
                return (byte[])plaintext.Clone();
            }

            public byte[] Unprotect(byte[] protectedData)
            {
                UnprotectCount++;
                return (byte[])protectedData.Clone();
            }
        }

        private sealed class FakeLocalValueStore : IPsdLocalValueStore
        {
            private readonly Dictionary<string, string> values = new Dictionary<string, string>();

            public string ForcedReadValue { get; set; }

            public int Count => values.Count;

            public string SingleSerializedValue
            {
                get
                {
                    Assert.That(values.Count, Is.EqualTo(1));
                    foreach (string value in values.Values)
                    {
                        return value;
                    }

                    throw new InvalidOperationException();
                }
            }

            public bool HasValue(string name)
            {
                return ForcedReadValue != null || values.ContainsKey(name);
            }

            public bool TryRead(string name, out string value)
            {
                if (ForcedReadValue != null)
                {
                    value = ForcedReadValue;
                    return true;
                }

                return values.TryGetValue(name, out value);
            }

            public void Save(string name, string value)
            {
                values[name] = value;
            }

            public void Clear(string name)
            {
                values.Remove(name);
            }
        }

        private sealed class ThrowingLocalValueStore : IPsdLocalValueStore
        {
            public bool HasValue(string name)
            {
                throw new InvalidOperationException("Store unavailable.");
            }

            public bool TryRead(string name, out string value)
            {
                value = string.Empty;
                throw new InvalidOperationException("Store unavailable.");
            }

            public void Save(string name, string value)
            {
                throw new InvalidOperationException("Store unavailable.");
            }

            public void Clear(string name)
            {
                throw new InvalidOperationException("Store unavailable.");
            }
        }
    }
}
