namespace PsdLayoutTool2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    public sealed class ClaudeCliHierarchyRunnerTests
    {
        private string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "ClaudeCliHierarchyRunnerTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }

        [Test]
        public async Task DefaultInvocationIsNonInteractiveToolFreeAndUsesStructuredOutput()
        {
            PsdHierarchyRequest request = Request();
            var adapter = new RecordingAdapter(invocation => new PsdHierarchyProcessResult
            {
                exitCode = 0,
                standardOutput = JsonConvert.SerializeObject(new
                {
                    type = "result",
                    subtype = "success",
                    structured_output = IdentityPlan(request)
                })
            });
            var runner = Runner(adapter, PsdHierarchyAiConnectionMode.Default, string.Empty, string.Empty);

            PsdHierarchyAiRunResult result = await runner.RunAsync(RunRequest(request), CancellationToken.None);

            Assert.That(result.succeeded, Is.True, result.error);
            CollectionAssert.Contains(adapter.Invocation.arguments, "--print");
            CollectionAssert.Contains(adapter.Invocation.arguments, "--output-format");
            CollectionAssert.Contains(adapter.Invocation.arguments, "json");
            CollectionAssert.Contains(adapter.Invocation.arguments, "--json-schema");
            CollectionAssert.Contains(adapter.Invocation.arguments, "--no-session-persistence");
            int toolsIndex = adapter.Invocation.arguments.IndexOf("--tools");
            Assert.That(toolsIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(adapter.Invocation.arguments[toolsIndex + 1], Is.Empty);
            Assert.That(adapter.Invocation.arguments, Has.None.EqualTo("--model"));
            Assert.That(adapter.Invocation.childEnvironment, Is.Empty);
        }

        [Test]
        public async Task CustomInvocationUsesAnthropicChildEnvironmentOnly()
        {
            const string endpoint = "https://claude.example.com/v1";
            const string key = "claude-test-secret";
            PsdHierarchyRequest request = Request();
            var adapter = new RecordingAdapter(invocation => new PsdHierarchyProcessResult
            {
                exitCode = 0,
                standardOutput = JsonConvert.SerializeObject(new { structured_output = IdentityPlan(request) })
            });
            var runner = Runner(adapter, PsdHierarchyAiConnectionMode.Custom, endpoint, key);

            PsdHierarchyAiRunResult result = await runner.RunAsync(RunRequest(request), CancellationToken.None);

            Assert.That(result.succeeded, Is.True, result.error);
            Assert.That(adapter.Invocation.childEnvironment["ANTHROPIC_BASE_URL"], Is.EqualTo(endpoint));
            Assert.That(adapter.Invocation.childEnvironment["ANTHROPIC_AUTH_TOKEN"], Is.EqualTo(key));
            Assert.That(string.Join(" ", adapter.Invocation.arguments), Does.Not.Contain(endpoint));
            Assert.That(string.Join(" ", adapter.Invocation.arguments), Does.Not.Contain(key));
        }

        [Test]
        public async Task FailureRedactsCredentialAndAuthorizationValues()
        {
            const string key = "claude-secret-value";
            var adapter = new RecordingAdapter(invocation => new PsdHierarchyProcessResult
            {
                exitCode = 7,
                standardError = "Authorization: Bearer " + key + " endpoint=https://user:pass@example.com/v1"
            });
            var runner = Runner(adapter, PsdHierarchyAiConnectionMode.Custom, "https://claude.example.com/v1", key);

            PsdHierarchyAiRunResult result = await runner.RunAsync(RunRequest(Request()), CancellationToken.None);

            Assert.That(result.succeeded, Is.False);
            Assert.That(result.error, Does.Not.Contain(key));
            Assert.That(result.standardError, Does.Not.Contain(key));
            Assert.That(result.error, Does.Not.Contain("user:pass"));
            Assert.That(result.error, Does.Not.Contain("https://claude.example.com/v1"));
            Assert.That(result.error, Does.Contain("Claude"));
            Assert.That(result.error, Does.Contain("exit 7"));
        }

        [Test]
        public async Task UsageLimitMessageNamesClaudeAndNotCodex()
        {
            var adapter = new RecordingAdapter(invocation => new PsdHierarchyProcessResult
            {
                exitCode = 1,
                standardError = "rate limit exceeded"
            });
            var runner = Runner(adapter, PsdHierarchyAiConnectionMode.Default, string.Empty, string.Empty);

            PsdHierarchyAiRunResult result = await runner.RunAsync(RunRequest(Request()), CancellationToken.None);

            Assert.That(result.error, Does.Contain("Claude"));
            Assert.That(result.error, Does.Not.Contain("Codex"));
        }

        [Test]
        public async Task StructuredOutputUsesRequestIdentityInsteadOfModelEcho()
        {
            PsdHierarchyRequest request = Request();
            PsdHierarchyPlan response = IdentityPlan(request);
            response.sourcePsdGuid = "model-guid";
            response.sourceFingerprint = "model-source";
            response.contentFingerprint = "model-content";
            response.structureFingerprint = "model-structure";
            response.geometryFingerprint = "model-geometry";
            var adapter = new RecordingAdapter(invocation => new PsdHierarchyProcessResult
            {
                exitCode = 0,
                standardOutput = JsonConvert.SerializeObject(new { structured_output = response })
            });
            var runner = Runner(adapter, PsdHierarchyAiConnectionMode.Default, string.Empty, string.Empty);

            PsdHierarchyAiRunResult result = await runner.RunAsync(RunRequest(request), CancellationToken.None);

            Assert.That(result.succeeded, Is.True, result.error);
            Assert.That(result.plan.sourcePsdGuid, Is.EqualTo(request.sourcePsdGuid));
            Assert.That(result.plan.sourceFingerprint, Is.EqualTo(request.sourceFingerprint));
            Assert.That(result.plan.contentFingerprint, Is.EqualTo(request.contentFingerprint));
            Assert.That(result.plan.structureFingerprint, Is.EqualTo(request.structureFingerprint));
            Assert.That(result.plan.geometryFingerprint, Is.EqualTo(request.geometryFingerprint));
        }

        [Test]
        public async Task StructuredOutputWithEmptyContentFingerprintUsesRequestIdentityBeforeParsing()
        {
            PsdHierarchyRequest request = Request();
            PsdHierarchyPlan response = IdentityPlan(request);
            response.contentFingerprint = string.Empty;
            JObject envelope = JObject.FromObject(new { structured_output = response });
            var adapter = new RecordingAdapter(invocation => new PsdHierarchyProcessResult
            {
                exitCode = 0,
                standardOutput = envelope.ToString(Formatting.None)
            });
            var runner = Runner(adapter, PsdHierarchyAiConnectionMode.Default, string.Empty, string.Empty);

            PsdHierarchyAiRunResult result = await runner.RunAsync(RunRequest(request), CancellationToken.None);

            Assert.That(result.succeeded, Is.True, result.error);
            Assert.That(result.plan.contentFingerprint, Is.EqualTo(request.contentFingerprint));
        }

        [Test]
        public async Task FocusedOutputWithoutDecisionGetsIdentityRename()
        {
            PsdHierarchyRequest request = Request();
            var adapter = new RecordingAdapter(invocation => new PsdHierarchyProcessResult
            {
                exitCode = 0,
                standardOutput = JsonConvert.SerializeObject(new { structured_output = IdentityPlan(request) })
            });
            var runner = Runner(adapter, PsdHierarchyAiConnectionMode.Default, string.Empty, string.Empty);
            PsdHierarchyAiRunRequest runRequest = RunRequest(request);
            runRequest.modifiableStableIds.Add("101");

            PsdHierarchyAiRunResult result = await runner.RunAsync(runRequest, CancellationToken.None);

            Assert.That(result.succeeded, Is.True, result.error);
            Assert.That(result.plan.renames, Has.Count.EqualTo(1));
            Assert.That(result.plan.renames[0].stableId, Is.EqualTo("101"));
            Assert.That(result.plan.renames[0].name, Is.EqualTo("Node 101"));
        }

        [Test]
        public void FactorySelectsProviderAndFailsClosed()
        {
            var adapter = new RecordingAdapter(invocation => new PsdHierarchyProcessResult());
            var secrets = new FakeSecretStore(PsdHierarchyAiProvider.Codex, "key");
            var defaultConnection = new PsdHierarchyAiConnectionSnapshot(PsdHierarchyAiConnectionMode.Default, string.Empty);

            IPsdHierarchyAiRunner codex = PsdHierarchyAiRunnerFactory.Create(
                new PsdHierarchyAiSettingsSnapshot(PsdHierarchyAiProvider.Codex, defaultConnection, defaultConnection),
                secrets, "project-a", adapter, tempRoot, () => "codex-test", () => "claude-test");
            IPsdHierarchyAiRunner claude = PsdHierarchyAiRunnerFactory.Create(
                new PsdHierarchyAiSettingsSnapshot(PsdHierarchyAiProvider.Claude, defaultConnection, defaultConnection),
                secrets, "project-a", adapter, tempRoot, () => "codex-test", () => "claude-test");

            Assert.That(codex, Is.TypeOf<CodexCliHierarchyRunner>());
            Assert.That(claude, Is.TypeOf<ClaudeCliHierarchyRunner>());
            Assert.Throws<ArgumentOutOfRangeException>(() => PsdHierarchyAiRunnerFactory.Create(
                new PsdHierarchyAiSettingsSnapshot((PsdHierarchyAiProvider)99, defaultConnection, defaultConnection),
                secrets, "project-a", adapter, tempRoot, () => "codex-test", () => "claude-test"));
        }

        [Test]
        public void ConfiguredFactoryUsesTheProviderSavedInProjectSettings()
        {
            PsdLayoutProjectSettings settings =
                UnityEngine.ScriptableObject.CreateInstance<PsdLayoutProjectSettings>();
            try
            {
                settings.SetAiProvider(PsdHierarchyAiProvider.Claude);

                IPsdHierarchyAiRunner runner = PsdHierarchyAiRunnerFactory.CreateConfigured(
                    settings,
                    new FakeSecretStore(PsdHierarchyAiProvider.Claude, string.Empty),
                    "project-a",
                    new RecordingAdapter(invocation => new PsdHierarchyProcessResult()),
                    tempRoot,
                    () => "codex-test",
                    () => "claude-test");

                Assert.That(runner, Is.TypeOf<ClaudeCliHierarchyRunner>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void DefaultExecutableUsesInstalledClaudeExeBeforePathFallback()
        {
            string roamingAppData = Path.Combine(tempRoot, "Roaming");
            string executablePath = Path.Combine(
                roamingAppData,
                "npm",
                "node_modules",
                "@anthropic-ai",
                "claude-code",
                "bin",
                "claude.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(executablePath));
            File.WriteAllText(executablePath, string.Empty);

            string executable = ClaudeCliHierarchyRunner.ResolveDefaultExecutable(roamingAppData);

            Assert.That(executable, Is.EqualTo(Path.GetFullPath(executablePath)));
        }

        private ClaudeCliHierarchyRunner Runner(
            IHierarchyProcessAdapter adapter,
            PsdHierarchyAiConnectionMode mode,
            string endpoint,
            string key)
        {
            return new ClaudeCliHierarchyRunner(
                adapter,
                () => "claude-test",
                tempRoot,
                new PsdHierarchyAiConnectionSnapshot(mode, endpoint),
                new FakeSecretStore(PsdHierarchyAiProvider.Claude, key),
                "project-a");
        }

        private static PsdHierarchyAiRunRequest RunRequest(PsdHierarchyRequest request)
        {
            return new PsdHierarchyAiRunRequest
            {
                operationId = "claude-op",
                request = request,
                targetPrefabPath = "Assets/UI/Target.prefab",
                timeout = TimeSpan.FromSeconds(1)
            };
        }

        private static PsdHierarchyRequest Request()
        {
            var request = new PsdHierarchyRequest
            {
                sourcePsdGuid = "guid",
                sourceFingerprint = "source",
                contentFingerprint = "content",
                structureFingerprint = "structure",
                geometryFingerprint = "geometry"
            };
            request.nodes.Add(new PsdHierarchyRequestNode
            {
                stableId = "101",
                originalName = "Node 101",
                kind = "Pixel",
                siblingIndex = 0,
                rectangle = new PsdHierarchyRectangle { width = 10, height = 10 }
            });
            return request;
        }

        private static PsdHierarchyPlan IdentityPlan(PsdHierarchyRequest request)
        {
            return new PsdHierarchyPlan
            {
                schemaVersion = 1,
                sourcePsdGuid = request.sourcePsdGuid,
                sourceFingerprint = request.sourceFingerprint,
                contentFingerprint = request.contentFingerprint,
                structureFingerprint = request.structureFingerprint,
                geometryFingerprint = request.geometryFingerprint
            };
        }

        private sealed class RecordingAdapter : IHierarchyProcessAdapter
        {
            private readonly Func<PsdHierarchyProcessInvocation, PsdHierarchyProcessResult> run;
            internal RecordingAdapter(Func<PsdHierarchyProcessInvocation, PsdHierarchyProcessResult> run) { this.run = run; }
            internal PsdHierarchyProcessInvocation Invocation { get; private set; }
            public Task<PsdHierarchyProcessResult> RunAsync(PsdHierarchyProcessInvocation invocation, TimeSpan timeout, CancellationToken token)
            {
                Invocation = invocation;
                return Task.FromResult(run(invocation));
            }
        }

        private sealed class FakeSecretStore : IPsdAiSecretStore
        {
            private readonly PsdHierarchyAiProvider provider;
            private readonly string key;
            internal FakeSecretStore(PsdHierarchyAiProvider provider, string key) { this.provider = provider; this.key = key; }
            public bool HasSavedCredential(string projectIdentity, PsdHierarchyAiProvider requested) { return requested == provider && !string.IsNullOrEmpty(key); }
            public bool TryRead(string projectIdentity, PsdHierarchyAiProvider requested, out string value)
            {
                value = requested == provider ? key : string.Empty;
                return !string.IsNullOrEmpty(value);
            }
            public void Save(string projectIdentity, PsdHierarchyAiProvider requested, string value) { }
            public void Clear(string projectIdentity, PsdHierarchyAiProvider requested) { }
        }
    }
}
