namespace PsdLayoutTool2.Tests
{
    using System.Text;
    using NUnit.Framework;
    using PsdLayoutTool2.Editor;

    public sealed class PsdHierarchyWebStaticAssetsTests
    {
        [TestCase("/", "text/html; charset=utf-8", "data-role=\"psd-canvas\"")]
        [TestCase("/organizer.css", "text/css; charset=utf-8", ".group-overlay")]
        [TestCase("/organizer.js", "text/javascript; charset=utf-8", "requestAnimationFrame")]
        public void Resolve_ReturnsBundledWorkbenchAsset(
            string route,
            string expectedContentType,
            string expectedText)
        {
            PsdHierarchyWebStaticAsset asset = PsdHierarchyWebStaticAssets.Resolve(route);

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.contentType, Is.EqualTo(expectedContentType));
            StringAssert.Contains(expectedText, Encoding.UTF8.GetString(asset.bytes));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("/index.html")]
        [TestCase("/unknown")]
        [TestCase("/../organizer.js")]
        [TestCase("/%2e%2e/organizer.js")]
        [TestCase("/organizer.js?cache=1")]
        [TestCase("\\organizer.js")]
        public void Resolve_RejectsEveryRouteOutsideTheStaticAllowlist(string route)
        {
            Assert.That(PsdHierarchyWebStaticAssets.Resolve(route), Is.Null);
        }

        [Test]
        public void Html_ContainsOperationalWorkbenchLandmarks()
        {
            string html = Encoding.UTF8.GetString(PsdHierarchyWebStaticAssets.Resolve("/").bytes);

            StringAssert.Contains("data-role=\"connection-state\"", html);
            StringAssert.Contains("data-role=\"tool-select\"", html);
            StringAssert.Contains("data-role=\"group-overlays\"", html);
            StringAssert.Contains("data-role=\"minimap\"", html);
            StringAssert.Contains("data-role=\"instruction\"", html);
            StringAssert.Contains("data-role=\"apply-plan\"", html);
            StringAssert.Contains("data-role=\"prefab-candidates\"", html);
        }

        [Test]
        public void Script_AllowsPanGesturesToStartOverGroupOverlays()
        {
            string script = Encoding.UTF8.GetString(PsdHierarchyWebStaticAssets.Resolve("/organizer.js").bytes);

            StringAssert.Contains("event.button === 1 || state.spaceDown || state.tool === \"hand\"", script);
            StringAssert.Contains("state.suppressGroupClick = true", script);
        }

        [Test]
        public void LoadedWorkbenchHidesEmptyStateAndUsesSelectDragForPanning()
        {
            string css = Encoding.UTF8.GetString(PsdHierarchyWebStaticAssets.Resolve("/organizer.css").bytes);
            string script = Encoding.UTF8.GetString(PsdHierarchyWebStaticAssets.Resolve("/organizer.js").bytes);

            StringAssert.Contains("[hidden] { display: none !important; }", css);
            StringAssert.Contains("event.button === 0 && state.tool === \"select\" && !event.shiftKey", script);
            StringAssert.Contains("event.shiftKey", script);
            StringAssert.Contains("额度不足", script);
        }
    }
}
