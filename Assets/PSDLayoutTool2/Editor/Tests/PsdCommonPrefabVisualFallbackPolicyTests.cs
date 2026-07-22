namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;

    /// <summary>
    /// Regression coverage for Common Prefabs with missing visual references.
    /// </summary>
    public sealed class PsdCommonPrefabVisualFallbackPolicyTests
    {
        [Test]
        public void MissingVisualRequiresPsdSourceFallback()
        {
            Assert.That(PsdCommonPrefabVisualFallbackPolicy.RequiresSourceVisualFallback(false), Is.True);
        }

        [Test]
        public void RenderableVisualDoesNotRequirePsdSourceFallback()
        {
            Assert.That(PsdCommonPrefabVisualFallbackPolicy.RequiresSourceVisualFallback(true), Is.False);
        }
    }
}
