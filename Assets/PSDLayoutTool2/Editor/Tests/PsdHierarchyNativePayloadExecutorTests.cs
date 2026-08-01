namespace PsdLayoutTool2.Tests
{
    using NUnit.Framework;

    public sealed class PsdHierarchyNativePayloadExecutorTests
    {
        [Test]
        public void WrapPayloadSourceCreatesStableNativeEntryPoint()
        {
            string wrapped = PsdHierarchyNativePayloadExecutor.WrapPayloadSource(
                "using System;\n\nvar value = 1;\nreturn value.ToString();\n");

            StringAssert.Contains("namespace PsdLayoutTool2", wrapped);
            StringAssert.Contains("using Object = UnityEngine.Object;", wrapped);
            StringAssert.Contains("class NativeCleanupPayload", wrapped);
            StringAssert.Contains("static string Execute()", wrapped);
            StringAssert.Contains("return value.ToString();", wrapped);
        }

        [Test]
        public void BuildNativePayloadPathStaysUnderProjectLibraryDirectory()
        {
            string path = PsdHierarchyNativePayloadExecutor.BuildNativePayloadPath(
                "E:/Project/Demo/monsterhunter",
                "abc123",
                ".payload.cs");

            StringAssert.StartsWith("E:/Project/Demo/monsterhunter/Library/PSDLayoutTool2/NativeCleanupPayloads", path.Replace('\\', '/'));
            StringAssert.EndsWith("abc123.payload.cs", path.Replace('\\', '/'));
        }
    }
}
