using System.IO;
using UnityEditor;
using UnityEngine;

namespace UGF.EditorTools.Psd2UGUI
{

internal static class RightClickExtension
{
	private const string CropMinimalNineSliceMenuPath = "Assets/Psd2UIForm/Crop Minimal 9-Slice";

	[MenuItem("Assets/Psd2UIForm/Auto Sprite Border", priority = 1004)]
	private static void AutoSpriteSliceBorder()
	{
		string[] assetGUIDs = Selection.assetGUIDs;
		for (int i = 0; i < assetGUIDs.Length; i++)
		{
			string text = AssetDatabase.GUIDToAssetPath(assetGUIDs[i]);
			TextureImporter textureImporter = AssetImporter.GetAtPath(text) as TextureImporter;
			if (!(textureImporter == null) && textureImporter.textureType == TextureImporterType.Sprite)
			{
				bool isReadable;
				if (!(isReadable = textureImporter.isReadable))
				{
					textureImporter.isReadable = true;
					textureImporter.SaveAndReimport();
				}
				Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(text);
				if (sprite != null && sprite.texture != null)
				{
					textureImporter.spriteBorder = UGUIParser.CalculateNineSliceBorder(sprite.texture, 0);
				}
				textureImporter.isReadable = isReadable;
				textureImporter.SaveAndReimport();
			}
		}
	}

	[MenuItem("Assets/Psd2UIForm/Crop Minimal 9-Slice", priority = 1005)]
	private static void CropMinimalNineSlice()
	{
		int num = 0;
		int num2 = 0;
		string[] assetGUIDs = Selection.assetGUIDs;
		for (int i = 0; i < assetGUIDs.Length; i++)
		{
			string text = AssetDatabase.GUIDToAssetPath(assetGUIDs[i]);
			if (!AssetDatabase.IsValidFolder(text))
			{
				if (!TryCropMinimalNineSlice(text))
				{
					num2++;
				}
				else
				{
					num++;
				}
				continue;
			}
			string[] array = AssetDatabase.FindAssets("t:Texture2D", new string[1] { text });
			for (int j = 0; j < array.Length; j++)
			{
				if (TryCropMinimalNineSlice(AssetDatabase.GUIDToAssetPath(array[j])))
				{
					num++;
				}
				else
				{
					num2++;
				}
			}
		}
		if (num > 0)
		{
			Debug.Log($"Crop Minimal 9-Slice finished. Success:{num} Skip:{num2}");
		}
	}

	[MenuItem("Assets/Psd2UIForm/Crop Minimal 9-Slice", true)]
	private static bool ValidateCropMinimalNineSlice()
	{
		string[] assetGUIDs = Selection.assetGUIDs;
		for (int i = 0; i < assetGUIDs.Length; i++)
		{
			string path = AssetDatabase.GUIDToAssetPath(assetGUIDs[i]);
			if (!AssetDatabase.IsValidFolder(path))
			{
				TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
				if (textureImporter != null && textureImporter.textureType == TextureImporterType.Sprite && textureImporter.spriteImportMode == SpriteImportMode.Single)
				{
					return true;
				}
				continue;
			}
			return true;
		}
		return false;
	}

	internal static bool TryCropMinimalNineSlice(string assetPath)
	{
		TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
		if (!(textureImporter == null) && textureImporter.textureType == TextureImporterType.Sprite)
		{
			if (textureImporter.spriteImportMode != SpriteImportMode.Single)
			{
				Debug.LogWarning("Crop Minimal 9-Slice only supports Single Sprite: " + assetPath);
				return false;
			}
			Vector4 spriteBorder = textureImporter.spriteBorder;
			int num = Mathf.RoundToInt(spriteBorder.x);
			int num2 = Mathf.RoundToInt(spriteBorder.y);
			int num3 = Mathf.RoundToInt(spriteBorder.z);
			int num4 = Mathf.RoundToInt(spriteBorder.w);
			if (!(spriteBorder == Vector4.zero) && num >= 0 && num2 >= 0 && num3 >= 0 && num4 >= 0)
			{
				string assetFullPath = GetAssetFullPath(assetPath);
				if (!File.Exists(assetFullPath))
				{
					Debug.LogWarning("Sprite file not found: " + assetPath);
					return false;
				}
				Texture2D texture2D = null;
				Texture2D texture2D2 = null;
				try
				{
					texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
					if (!texture2D.LoadImage(File.ReadAllBytes(assetFullPath), markNonReadable: false))
					{
						Debug.LogWarning("Load sprite source failed: " + assetPath);
						return false;
					}
					int width = texture2D.width;
					int height = texture2D.height;
					int num5 = width - num - num3;
					int num6 = height - num2 - num4;
					if (num5 > 0 && num6 > 0)
					{
						bool flag = num5 > 1;
						bool flag2 = num6 > 1;
						if (!flag && !flag2)
						{
							Debug.LogWarning("Sprite center area is already minimal: " + assetPath);
							return false;
						}
						int num7 = (flag ? (num + 1 + num3) : width);
						int num8 = ((!flag2) ? height : (num2 + 1 + num4));
						int centerIndex = num + (num5 - 1) / 2;
						int centerIndex2 = num2 + (num6 - 1) / 2;
						Color32[] pixels = texture2D.GetPixels32();
						Color32[] array = new Color32[num7 * num8];
						for (int i = 0; i < num8; i++)
						{
							int num9 = MapCollapsedAxisIndex(i, flag2, num2, num6, centerIndex2) * width;
							int num10 = i * num7;
							for (int j = 0; j < num7; j++)
							{
								int num11 = MapCollapsedAxisIndex(j, flag, num, num5, centerIndex);
								array[num10 + j] = pixels[num9 + num11];
							}
						}
						texture2D2 = new Texture2D(num7, num8, TextureFormat.RGBA32, mipChain: false)
						{
							alphaIsTransparency = texture2D.alphaIsTransparency,
							filterMode = texture2D.filterMode,
							wrapMode = texture2D.wrapMode,
							anisoLevel = texture2D.anisoLevel
						};
						texture2D2.SetPixels32(array);
						texture2D2.Apply();
						byte[] array2 = EncodeTextureBytes(assetPath, texture2D2);
						if (array2 != null && array2.Length != 0)
						{
							File.WriteAllBytes(assetFullPath, array2);
							AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
							textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
							if (textureImporter == null)
							{
								Debug.LogWarning("TextureImporter reload failed: " + assetPath);
								return false;
							}
							Vector4 vector = new Vector4(num, num2, num3, num4);
							if (textureImporter.spriteBorder != vector)
							{
								textureImporter.spriteBorder = vector;
								textureImporter.SaveAndReimport();
							}
							Debug.Log($"Crop Minimal 9-Slice success: {assetPath} ({width}x{height} -> {num7}x{num8})");
							return true;
						}
						Debug.LogWarning("Unsupported texture format for Crop Minimal 9-Slice: " + assetPath);
						return false;
					}
					Debug.LogWarning("Sprite Border exceeds texture size: " + assetPath);
					return false;
				}
				finally
				{
					if (texture2D != null)
					{
						Object.DestroyImmediate(texture2D);
					}
					if (texture2D2 != null)
					{
						Object.DestroyImmediate(texture2D2);
					}
				}
			}
			Debug.LogWarning("Sprite Border is invalid or empty: " + assetPath);
			return false;
		}
		return false;
	}

	private static int MapCollapsedAxisIndex(int targetIndex, bool cropAxis, int stretchStart, int stretchLength, int centerIndex)
	{
		if (!cropAxis)
		{
			return targetIndex;
		}
		if (targetIndex < stretchStart)
		{
			return targetIndex;
		}
		if (targetIndex == stretchStart)
		{
			return centerIndex;
		}
		return stretchStart + stretchLength + (targetIndex - stretchStart - 1);
	}

	private static byte[] EncodeTextureBytes(string assetPath, Texture2D texture)
	{
		switch (Path.GetExtension(assetPath)?.ToLowerInvariant())
		{
		default:
			return null;
		case ".jpg":
		case ".jpeg":
			return texture.EncodeToJPG(100);
		case ".png":
			return texture.EncodeToPNG();
		}
	}

	private static string GetAssetFullPath(string assetPath)
	{
		string text = Directory.GetParent(Application.dataPath)?.FullName;
		if (!string.IsNullOrEmpty(text))
		{
			return Path.GetFullPath(Path.Combine(text, assetPath));
		}
		return assetPath;
	}
}
}
