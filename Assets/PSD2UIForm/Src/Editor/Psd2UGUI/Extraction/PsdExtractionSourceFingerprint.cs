using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UGF.EditorTools.Psd2UGUI
{
    internal static class PsdExtractionSourceFingerprint
    {
        [Serializable]
        sealed class NodeLayout
        {
            public string name;
            public bool active;
            public Vector3 position, scale;
            public Quaternion rotation;
            public Vector2 anchorMin, anchorMax, pivot, size, anchoredPosition;
        }

        internal static string Capture(GameObject source)
        {
            var identities = new Dictionary<Object, string>();
            var nodes = source.GetComponentsInChildren<Transform>(true);
            foreach (var node in nodes)
            {
                string address = node == source.transform ? "root" : PsdCommonPrefabExtraction.GetNodeAddress(source.transform, node);
                identities[node.gameObject] = address;
                var components = node.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null) throw new InvalidOperationException("编辑树存在丢失脚本，不能确认生成来源。");
                    identities[components[i]] = address + ":" + components[i].GetType().FullName + ":" + i;
                }
            }
            var snapshot = new StringBuilder();
            foreach (var node in nodes)
            {
                var layout = new NodeLayout { name = node.name, active = node.gameObject.activeSelf,
                    position = node.localPosition, rotation = node.localRotation, scale = node.localScale };
                if (node is RectTransform rect)
                {
                    layout.anchorMin = rect.anchorMin; layout.anchorMax = rect.anchorMax; layout.pivot = rect.pivot;
                    layout.size = rect.sizeDelta; layout.anchoredPosition = rect.anchoredPosition;
                }
                snapshot.Append(identities[node.gameObject]).Append(JsonUtility.ToJson(layout));
                foreach (var component in node.GetComponents<Component>())
                {
                    // Preview renderers and generatedMetadataEntries are outputs, not generation inputs.
                    if (!(component is PsdLayerNode) && !(component is UIHelperBase)) continue;
                    string json = JsonUtility.ToJson(component);
                    json = Regex.Replace(json, "\"instanceID\":(-?[0-9]+)", match =>
                        "\"reference\":\"" + Reference(EditorUtility.InstanceIDToObject(int.Parse(match.Groups[1].Value)), identities) + "\"");
                    snapshot.Append(component.GetType().FullName).Append(json);
                }
            }
            var converter = source.GetComponent<Psd2UIFormConverter>();
            if (converter != null)
            {
                snapshot.Append(converter.uiFormName).Append(converter.psdAssetPath);
                snapshot.Append(Reference(converter.psdAsset, identities));
                if (!string.IsNullOrEmpty(converter.psdAssetPath))
                    snapshot.Append(AssetDatabase.GetAssetDependencyHash(converter.psdAssetPath));
            }
            snapshot.Append(Reference(UGUIParser.Instance, identities));
            using (var hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(snapshot.ToString())));
        }

        static string Reference(Object asset, Dictionary<Object, string> identities)
        {
            if (asset == null) return "null";
            if (identities.TryGetValue(asset, out string identity)) return identity;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                throw new InvalidOperationException("生成输入包含未保存的外部引用，不能记录稳定来源：" + asset.name);
            return guid + ":" + localId + ":" + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
