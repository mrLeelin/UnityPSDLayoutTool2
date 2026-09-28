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
