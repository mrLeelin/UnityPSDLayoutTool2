using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace Psd2UIForm.Tests
{
    public class ConfiguredTestButton : Button { }
    public class ConfiguredTestClick : MonoBehaviour { }

    public class PsdButtonComponentPolicyTests
    {
        [Test] public void DefaultAndInvalidTypes()
        {
            Assert.That(PsdButtonComponentPolicy.Resolve(null), Is.EqualTo(typeof(Button)));
            Assert.That(PsdButtonComponentPolicy.Resolve("UnityEngine.UI.Button"), Is.EqualTo(typeof(Button)));
            Assert.Throws<System.InvalidOperationException>(() => PsdButtonComponentPolicy.Resolve("Missing.ButtonClass"));
            Assert.Throws<System.InvalidOperationException>(() => PsdButtonComponentPolicy.Resolve(typeof(ConfiguredTestButton).FullName));
        }

        [Test] public void Replacement_PreservesButtonSettingsAndNavigationReferences()
        {
            var root = new GameObject("Root");
            try
            {
                var target = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button));
                target.transform.SetParent(root.transform);
                var old = target.GetComponent<Button>();
                old.interactable = false;
                old.targetGraphic = target.GetComponent<Image>();
                old.transition = Selectable.Transition.SpriteSwap;
                var neighbour = new GameObject("Neighbour", typeof(RectTransform), typeof(Button));
                neighbour.transform.SetParent(root.transform);
                var other = neighbour.GetComponent<Button>();
                var navigation = other.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnRight = old;
                other.navigation = navigation;
                PsdButtonComponentPolicy.Replace(root, old, typeof(ConfiguredTestButton));
                var replacement = target.GetComponent<ConfiguredTestButton>();
                Assert.That(replacement, Is.Not.Null);
                Assert.That(target.GetComponents<Button>().Length, Is.EqualTo(1));
                Assert.That(replacement.interactable, Is.False);
                Assert.That(replacement.targetGraphic, Is.EqualTo(target.GetComponent<Image>()));
                Assert.That(replacement.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
                Assert.That(other.navigation.selectOnRight, Is.EqualTo(replacement));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void IndependentBehaviour_ReplacesStandardButtonAndKeepsImage()
        {
            var root = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                PsdButtonComponentPolicy.Replace(root, root.GetComponent<Button>(), typeof(ConfiguredTestClick));
                Assert.That(root.GetComponent<ConfiguredTestClick>(), Is.Not.Null);
                Assert.That(root.GetComponent<Button>(), Is.Null);
                Assert.That(root.GetComponent<Image>(), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
