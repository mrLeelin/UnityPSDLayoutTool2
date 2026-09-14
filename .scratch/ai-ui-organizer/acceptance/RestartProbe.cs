// Copy into an isolated project's Assets/Editor. Run Setup and Verify in separate Unity batch processes.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UGF.EditorTools.Psd2UGUI;
using Object = UnityEngine.Object;

public static class PsdExtractionRestartProbe
{
    const string Folder = "Assets/RestartProbe";
    const string Source = Folder + "/Source.prefab";
    const string Target = Folder + "/Screen.prefab";
    const string Common = Folder + "/Common/Item.prefab";
    const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    [Serializable] public sealed class Proof
    {
        public int setupPid;
        public string targetHash, commonHash;
    }

    public static void Setup()
    {
        if (AssetDatabase.IsValidFolder(Folder)) throw new Exception("Probe already exists");
        AssetDatabase.CreateFolder("Assets", "RestartProbe");
        var root = new GameObject("Source", typeof(RectTransform), typeof(Psd2UIFormConverter));
        try
        {
            var serialized = new SerializedObject(root.GetComponent<Psd2UIFormConverter>());
            serialized.FindProperty("uiFormName").stringValue = "Screen";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(root, Source);
        }
        finally { Object.DestroyImmediate(root); }
        root = new GameObject("Screen", typeof(RectTransform));
        try
        {
            for (int i = 0; i < 2; i++)
            {
                var child = new GameObject("Item" + i, typeof(RectTransform), typeof(Image));
                child.transform.SetParent(root.transform, false);
                child.GetComponent<Image>().color = i == 0 ? Color.red : Color.blue;
            }
            PrefabUtility.SaveAsPrefabAsset(root, Target);
        }
        finally { Object.DestroyImmediate(root); }
        var assembly = typeof(PsdCommonPrefabExtraction).Assembly;
        assembly.GetType("UGF.EditorTools.Psd2UGUI.PsdCommonPrefabPersistence")
            .GetMethod("RecordGeneration", Static).Invoke(null, new object[] { AssetDatabase.LoadAssetAtPath<GameObject>(Source), Target });
        PsdCommonPrefabExtraction.Apply(PsdCommonPrefabExtraction.Preview(Target, new[] { "0", "1" }, "Item"));
        File.WriteAllText("restart-proof.json", JsonUtility.ToJson(new Proof {
            setupPid = Process.GetCurrentProcess().Id, targetHash = Hash(Target), commonHash = Hash(Common) }));
        File.WriteAllText("setup-result.txt", "PASS");
    }

    public static void Verify()
    {
        var proof = JsonUtility.FromJson<Proof>(File.ReadAllText("restart-proof.json"));
        if (proof.setupPid == Process.GetCurrentProcess().Id) throw new Exception("Must restart the Unity process");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        var type = typeof(PsdCommonPrefabExtraction).Assembly.GetType("UGF.EditorTools.Psd2UGUI.Psd2UIFormConverterEditor");
        var editor = type.GetMethod("GetOrCreate", Static).Invoke(null, new object[] { source.GetComponent<Psd2UIFormConverter>() });
        bool reused = (bool)type.GetMethod("GenerateAndSaveUIFormPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(editor, new object[] { source.transform, Folder });
        if (!reused || Hash(Target) != proof.targetHash || Hash(Common) != proof.commonHash)
            throw new Exception("Persisted graph was not preserved after restart");
        File.WriteAllText("verify-result.txt", "PASS: independent Unity process; converter reused persisted graph; target and common prefab bytes unchanged");
    }

    static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }
}
