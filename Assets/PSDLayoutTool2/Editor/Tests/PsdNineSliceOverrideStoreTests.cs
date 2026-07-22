namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;

    public sealed class PsdNineSliceOverrideStoreTests
    {
        [Test]
        public void WriteThenReadPreservesOtherUserDataAndBorder()
        {
            string userData = PsdNineSliceOverrideStore.Write(
                "other-tool=value",
                41U,
                true,
                new PsdNineSliceBorder(10, 20, 30, 40));

            PsdNineSliceOverride value;
            Assert.That(PsdNineSliceOverrideStore.TryGet(userData, 41U, out value), Is.True);
            Assert.That(value.Enabled, Is.True);
            Assert.That(value.Border.Left, Is.EqualTo(10));
            Assert.That(value.Border.Top, Is.EqualTo(20));
            Assert.That(value.Border.Right, Is.EqualTo(30));
            Assert.That(value.Border.Bottom, Is.EqualTo(40));
            Assert.That(userData, Does.Contain("other-tool=value"));
        }

        [Test]
        public void DisabledOverrideIsPersistedWithoutBorder()
        {
            string userData = PsdNineSliceOverrideStore.Write("", 42U, false, null);

            PsdNineSliceOverride value;
            Assert.That(PsdNineSliceOverrideStore.TryGet(userData, 42U, out value), Is.True);
            Assert.That(value.Enabled, Is.False);
            Assert.That(value.Border, Is.Null);
        }

        [Test]
        public void RemoveOnlyDeletesRequestedLayerOverride()
        {
            string userData = PsdNineSliceOverrideStore.Write("", 41U, true, new PsdNineSliceBorder(1, 2, 3, 4));
            userData = PsdNineSliceOverrideStore.Write(userData, 42U, false, null);
            userData = PsdNineSliceOverrideStore.Remove(userData, 41U);

            PsdNineSliceOverride value;
            Assert.That(PsdNineSliceOverrideStore.TryGet(userData, 41U, out value), Is.False);
            Assert.That(PsdNineSliceOverrideStore.TryGet(userData, 42U, out value), Is.True);
            Assert.That(value.Enabled, Is.False);
        }
    }
}
