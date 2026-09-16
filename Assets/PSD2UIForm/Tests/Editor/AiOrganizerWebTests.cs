using System;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;

namespace Psd2UIForm.Tests
{
    public class AiOrganizerWebTests
    {
        [Test] public void StaleTabCannotChangeCurrentSessionOrVersion()
        {
            var state = new AiOrganizerWebState { sessionId = "current", revision = 7 };
            Assert.Throws<InvalidOperationException>(() => AiOrganizerWebSession.RequireCurrent(state,
                new AiOrganizerWebCommand { sessionId = "old", revision = 7, action = "apply" }));
            Assert.Throws<InvalidOperationException>(() => AiOrganizerWebSession.RequireCurrent(state,
                new AiOrganizerWebCommand { sessionId = "current", revision = 6, action = "revise" }));
            Assert.DoesNotThrow(() => AiOrganizerWebSession.RequireCurrent(state,
                new AiOrganizerWebCommand { sessionId = "current", revision = 7, action = "revise" }));
        }

        [Test] public void BusySessionOnlyAcceptsCancellationAndPublishedSessionIsClosed()
        {
            var state = new AiOrganizerWebState { sessionId = "s", revision = 1, running = true };
            foreach (string action in new[] { "start", "revise", "apply", "restore" })
                Assert.Throws<InvalidOperationException>(() => AiOrganizerWebSession.RequireCurrent(state,
                    new AiOrganizerWebCommand { sessionId = "s", revision = 1, action = action }));
            Assert.DoesNotThrow(() => AiOrganizerWebSession.RequireCurrent(state,
                new AiOrganizerWebCommand { sessionId = "s", revision = 1, action = "cancel" }));
            state.running = false; state.publishedPath = "Assets/Published/UI.prefab";
            Assert.Throws<InvalidOperationException>(() => AiOrganizerWebSession.RequireCurrent(state,
                new AiOrganizerWebCommand { sessionId = "s", revision = 1, action = "restore" }));
        }

        [Test] public void LocalBridgeRequiresSessionTokenAndRejectsForeignOrigins()
        {
            const string origin = "http://127.0.0.1:12345";
            Assert.IsTrue(AiOrganizerWebServer.IsAuthorized(origin, origin, "secret", "secret"));
            Assert.IsTrue(AiOrganizerWebServer.IsAuthorized(null, origin, "secret", "secret"));
            Assert.IsFalse(AiOrganizerWebServer.IsAuthorized(origin, origin, "wrong", "secret"));
            Assert.IsFalse(AiOrganizerWebServer.IsAuthorized("https://foreign.invalid", origin, "secret", "secret"));
            Assert.IsFalse(AiOrganizerWebServer.IsAuthorized(origin, origin, "", ""));
        }
    }
}
