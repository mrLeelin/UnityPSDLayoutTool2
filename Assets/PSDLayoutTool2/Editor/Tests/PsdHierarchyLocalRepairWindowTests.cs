namespace PsdLayoutTool2
{
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public sealed class PsdHierarchyLocalRepairWindowTests
    {
        [Test]
        public void WindowBuildsDedicatedLocalRepairControls()
        {
            PsdHierarchyLocalRepairWindow window = ScriptableObject.CreateInstance<PsdHierarchyLocalRepairWindow>();
            try
            {
                window.InitializeForTests(new PsdHierarchyChatContext(
                    "E:/Project/Demo/monsterhunter",
                    "Assets/UI/Source.psd",
                    "Assets/UI/Prefab/ExampleView.prefab",
                    "Skill",
                    "Skill content",
                    "Prefab content"));

                Assert.That(
                    window.rootVisualElement.Q<VisualElement>(PsdHierarchyLocalRepairWindow.RootElementName),
                    Is.Not.Null);
                Assert.That(
                    window.rootVisualElement.Q<Label>(PsdHierarchyLocalRepairWindow.TargetLabelName).text,
                    Is.EqualTo("Assets/UI/Prefab/ExampleView.prefab"));
                Assert.That(
                    window.rootVisualElement.Q<Button>(PsdHierarchyLocalRepairWindow.LockSelectionButtonName),
                    Is.Not.Null);
                Assert.That(
                    window.rootVisualElement.Q<TextField>(PsdHierarchyLocalRepairWindow.NestedPrefabNameFieldName).value,
                    Is.EqualTo("DaySignRewardItem"));
                Assert.That(
                    window.rootVisualElement.Q<Button>(PsdHierarchyLocalRepairWindow.AnalyzeButtonName),
                    Is.Not.Null);
                Assert.That(
                    window.rootVisualElement.Q<ScrollView>(PsdHierarchyLocalRepairWindow.ReviewName),
                    Is.Not.Null);
                Assert.That(
                    window.rootVisualElement.Q<Button>(PsdHierarchyLocalRepairWindow.ConfirmButtonName).enabledSelf,
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }
    }
}
