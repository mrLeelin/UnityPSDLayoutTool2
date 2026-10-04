namespace PsdLayoutTool2.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using NUnit.Framework;

    public sealed class PsdLayoutProjectSettingsWebServerTests
    {
        [Test]
        public void PortCandidatesStartAtBasePortAndCoverTheSearchRange()
        {
            IReadOnlyList<int> candidates = PsdLayoutProjectSettingsWebServer.BuildPortCandidates(0);

            Assert.That(candidates[0], Is.EqualTo(PsdLayoutProjectSettingsWebServer.BasePort));
            Assert.That(candidates.Count, Is.EqualTo(PsdLayoutProjectSettingsWebServer.PortSearchCount));
            Assert.That(candidates, Is.Unique);
            Assert.That(candidates.Last(),
                Is.EqualTo(PsdLayoutProjectSettingsWebServer.BasePort + PsdLayoutProjectSettingsWebServer.PortSearchCount - 1));
        }

        [Test]
        public void PortUsedEarlierInThisSessionIsTriedFirstSoOpenPagesKeepWorking()
        {
            int previous = PsdLayoutProjectSettingsWebServer.BasePort + 3;

            IReadOnlyList<int> candidates = PsdLayoutProjectSettingsWebServer.BuildPortCandidates(previous);

            Assert.That(candidates[0], Is.EqualTo(previous));
            Assert.That(candidates, Is.Unique);
            Assert.That(candidates.Count, Is.EqualTo(PsdLayoutProjectSettingsWebServer.PortSearchCount));
        }

        [Test]
        public void RememberedPortOutsideTheSearchRangeIsIgnored()
        {
            IReadOnlyList<int> candidates = PsdLayoutProjectSettingsWebServer.BuildPortCandidates(80);

            Assert.That(candidates[0], Is.EqualTo(PsdLayoutProjectSettingsWebServer.BasePort));
            Assert.That(candidates, Has.No.Member(80));
        }

        [Test]
        public void OccupiedPortIsReportedAsUnavailableAndAFreePortCanBeBound()
        {
            int port = FindFreePort();
            var occupant = new HttpListener();
            occupant.Prefixes.Add(PsdLayoutProjectSettingsWebServer.BuildUrl(port));
            occupant.Start();
            var server = new PsdLayoutProjectSettingsWebServer();
            try
            {
                Assert.That(InvokeTryListen(server, port, out string failure), Is.False);
                Assert.That(failure, Is.Not.Empty);

                int freePort = FindFreePort();
                Assert.That(InvokeTryListen(server, freePort, out string freeFailure), Is.True, freeFailure);
            }
            finally
            {
                occupant.Close();
                var bound = (HttpListener)typeof(PsdLayoutProjectSettingsWebServer)
                    .GetField("listener", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(server);
                bound?.Close();
            }
        }

        [Test]
        public void SettingsPageShowsTheCurrentProjectName()
        {
            string page = (string)typeof(PsdLayoutProjectSettingsWebServer)
                .GetMethod("Page", BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, null);

            Assert.That(page, Does.Contain("<title>PSD Layout Tool 设置 · {{PSD_LAYOUT_PROJECT_NAME}}</title>"));
            Assert.That(page, Does.Contain("当前工程：<strong>{{PSD_LAYOUT_PROJECT_NAME}}</strong>"));
            Assert.That(page, Does.Contain("id='aiOrganizeAnchors'"));
            Assert.That(page, Does.Contain("id='aiModelToggle'"));
            Assert.That(page, Does.Contain("id='aiModelPicker'"));
        }

        [Test]
        public void BuildModelsRequestUriStripsChatSuffixesAndAppendsModels()
        {
            Assert.That(
                PsdLayoutProjectSettingsWebServer.BuildModelsRequestUri("https://api.openai.com/v1").AbsoluteUri,
                Is.EqualTo("https://api.openai.com/v1/models"));
            Assert.That(
                PsdLayoutProjectSettingsWebServer.BuildModelsRequestUri("https://api.openai.com/v1/responses").AbsoluteUri,
                Is.EqualTo("https://api.openai.com/v1/models"));
            Assert.That(
                PsdLayoutProjectSettingsWebServer.BuildModelsRequestUri("https://api.openai.com/v1/chat/completions").AbsoluteUri,
                Is.EqualTo("https://api.openai.com/v1/models"));
            Assert.That(
                PsdLayoutProjectSettingsWebServer.BuildModelsRequestUri("https://api.anthropic.com/v1/messages").AbsoluteUri,
                Is.EqualTo("https://api.anthropic.com/v1/models"));
        }

        [Test]
        public void BuildModelsRequestUriKeepsModelsPathAndRejectsInvalid()
        {
            Assert.That(
                PsdLayoutProjectSettingsWebServer.BuildModelsRequestUri("https://proxy.example.com/openai/v1/models").AbsoluteUri,
                Is.EqualTo("https://proxy.example.com/openai/v1/models"));

            Assert.That(
                () => PsdLayoutProjectSettingsWebServer.BuildModelsRequestUri("not-a-url"),
                Throws.ArgumentException);
        }

        [Test]
        public void ParseModelIdsAcceptsDataArrayAndPlainStringList()
        {
            Newtonsoft.Json.Linq.JArray fromData =
                PsdLayoutProjectSettingsWebServer.ParseModelIds(
                    "{\"data\":[{\"id\":\"gpt-5\"},{\"id\":\"gpt-5-mini\"}]}");
            Assert.That(fromData.Count, Is.EqualTo(2));
            Assert.That(fromData[0]!.ToString(), Is.EqualTo("gpt-5"));

            Newtonsoft.Json.Linq.JArray fromList =
                PsdLayoutProjectSettingsWebServer.ParseModelIds(
                    "{\"models\":[\"alpha\",\"beta \"]}");
            Assert.That(fromList.Count, Is.EqualTo(2));
            Assert.That(fromList[1]!.ToString(), Is.EqualTo("beta"));
        }

        [Test]
        public void BuildCliModelsJsonListsCodexSuggestionsWithoutNetwork()
        {
            string json = PsdLayoutProjectSettingsWebServer.BuildCliModelsJson((int)PsdHierarchyAiProvider.Codex);
            var payload = Newtonsoft.Json.Linq.JObject.Parse(json);

            Assert.That(payload.Value<bool>("ok"), Is.True);
            Assert.That(payload.Value<string>("source"), Is.EqualTo("cli"));
            Assert.That(payload.Value<Newtonsoft.Json.Linq.JArray>("models")!.ToString(),
                Does.Contain("gpt-5"));
        }

        [Test]
        public void BuildCliModelsJsonReturnsEmptyListForNone()
        {
            string json = PsdLayoutProjectSettingsWebServer.BuildCliModelsJson((int)PsdHierarchyAiProvider.None);
            var payload = Newtonsoft.Json.Linq.JObject.Parse(json);

            Assert.That(payload.Value<Newtonsoft.Json.Linq.JArray>("models")!.Count, Is.EqualTo(0));
        }

        [Test]
        public void BuildCliCatalogJsonCoversSupportedProviders()
        {
            string json = PsdLayoutProjectSettingsWebServer.BuildCliCatalogJson();
            var catalog = Newtonsoft.Json.Linq.JObject.Parse(json);

            Assert.That(catalog["1"], Is.Not.Null); // Codex
            Assert.That(catalog["1"]!["models"]!.ToString(), Does.Contain("gpt-5"));
        }

        private static bool InvokeTryListen(PsdLayoutProjectSettingsWebServer server, int port, out string failure)
        {
            var arguments = new object[] { port, null };
            bool result = (bool)typeof(PsdLayoutProjectSettingsWebServer)
                .GetMethod("TryListen", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(server, arguments);
            failure = (string)arguments[1];
            return result;
        }

        private static int FindFreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
