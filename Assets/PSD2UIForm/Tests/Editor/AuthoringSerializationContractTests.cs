using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;

namespace Psd2UIForm.Tests
{
    /// <summary>
    /// 运行期壳承载 PSD 作者数据，它的字段名与类型就是生成物的序列化契约。
    /// 拆分时把字段从 Editor 程序集搬到 Runtime 程序集，刻意保持逐字一致；
    /// 这里把它冻结，防止以后改名/改类型让既有 prefab 丢数据。
    /// </summary>
    public class AuthoringSerializationContractTests
    {
        private static readonly Dictionary<string, Type> ExpectedFields = new Dictionary<string, Type>
        {
            { "psdAssetChangeTime", typeof(string) },
            { "uiFormName", typeof(string) },
            { "psdAsset", typeof(Sprite) },
            { "previewSprite", typeof(Sprite) },
            { "psdAssetPath", typeof(string) },
            { "drawLayerRectGizmos", typeof(bool) },
            { "drawLayerRectGizmosColor", typeof(Color) },
            { "preferSmallestLayerOnScenePick", typeof(bool) },
            { "generatedMetadataEntries", typeof(List<GeneratedMetadataSerializedEntry>) },
        };

        [Test]
        public void ConverterShell_KeepsSerializedFieldContract()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Dictionary<string, Type> actual = typeof(Psd2UIFormConverter).GetFields(flags)
                .Where(f => f.IsPublic || f.IsDefined(typeof(SerializeField), true))
                .ToDictionary(f => f.Name, f => f.FieldType);

            var problems = new List<string>();
            foreach (KeyValuePair<string, Type> pair in ExpectedFields)
            {
                Type actualType;
                if (!actual.TryGetValue(pair.Key, out actualType))
                {
                    problems.Add("缺少序列化字段 " + pair.Key);
                    continue;
                }
                if (actualType != pair.Value)
                {
                    problems.Add(pair.Key + " 的类型变成 " + actualType.Name + "（应为 " + pair.Value.Name + "）");
                }
            }

            Assert.That(problems, Is.Empty,
                "序列化契约被破坏，既有生成物会丢数据：\n" + string.Join("\n", problems));
        }

        [Test]
        public void ConverterShell_IsTheOnlyAuthoringHostType()
        {
            // 作者数据只能有一个宿主类型，否则 AddComponent 的目标会不明确
            Type[] hosts = typeof(Psd2UIFormConverter).Assembly.GetTypes()
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract && t.Name.Contains("Converter"))
                .ToArray();

            Assert.That(hosts.Select(t => t.Name), Is.EquivalentTo(new[] { "Psd2UIFormConverter" }));
        }

        [Test]
        public void GeneratedMetadataTypes_AreSerializableInRuntimeAssembly()
        {
            Assert.That(typeof(GeneratedMetadataEntry).Assembly.GetName().Name, Is.EqualTo("cn.efunstudio.psd2ugui"));
            Assert.That(typeof(GeneratedMetadataSerializedEntry).Assembly.GetName().Name, Is.EqualTo("cn.efunstudio.psd2ugui"));
            Assert.That(typeof(GeneratedMetadataEntry).IsSerializable, Is.True);
            Assert.That(typeof(GeneratedMetadataSerializedEntry).IsSerializable, Is.True);

            var value = new GeneratedMetadataSerializedEntry { PrefabAssetPath = "Assets/x.prefab", Json = "{}" };
            value.Entries.Add(new GeneratedMetadataEntry
            {
                GlobalObjectId = "gid",
                Key = "key",
                TypeKey = "Button",
                IsContainer = true,
            });

            string json = JsonUtility.ToJson(value);
            GeneratedMetadataSerializedEntry roundTrip = JsonUtility.FromJson<GeneratedMetadataSerializedEntry>(json);

            Assert.That(roundTrip.PrefabAssetPath, Is.EqualTo("Assets/x.prefab"));
            Assert.That(roundTrip.Entries.Count, Is.EqualTo(1));
            Assert.That(roundTrip.Entries[0].GlobalObjectId, Is.EqualTo("gid"));
            Assert.That(roundTrip.Entries[0].TypeKey, Is.EqualTo("Button"));
            Assert.That(roundTrip.Entries[0].IsContainer, Is.True);
        }
    }
}
