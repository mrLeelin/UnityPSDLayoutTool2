using System;
using System.Collections.Generic;
using PsdProtectionGuards;
using UnityEngine;
using PsdProtectionRuntime;

namespace UGF.EditorTools.Psd2UGUI
{

public static class PsdLayerPreviewCache
{
	private sealed class PreviewTextureCacheEntry
	{
		public Texture2D Texture;

		public int ReferenceCount;

		public PreviewTextureCacheEntry()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	private static readonly object _cacheLock;

	private static readonly Dictionary<string, PreviewTextureCacheEntry> _entriesByCacheKey;

	public static Texture2D Acquire(string cacheKey, Func<Texture2D> textureFactory)
	{
		if (!string.IsNullOrWhiteSpace(cacheKey) && textureFactory != null)
		{
			lock (_cacheLock)
			{
				if (_entriesByCacheKey.TryGetValue(cacheKey, out var value))
				{
					if (value != null && value.Texture != null)
					{
						value.ReferenceCount++;
						return value.Texture;
					}
					_entriesByCacheKey.Remove(cacheKey);
				}
				Texture2D texture2D = textureFactory();
				if (texture2D == null)
				{
					return null;
				}
				_entriesByCacheKey[cacheKey] = new PreviewTextureCacheEntry
				{
					Texture = texture2D,
					ReferenceCount = 1
				};
				return texture2D;
			}
		}
		return textureFactory?.Invoke();
	}

	public static bool Release(string cacheKey)
	{
		if (!string.IsNullOrWhiteSpace(cacheKey))
		{
			Texture2D texture2D = null;
			lock (_cacheLock)
			{
				if (!_entriesByCacheKey.TryGetValue(cacheKey, out var value) || value == null)
				{
					return false;
				}
				value.ReferenceCount--;
				if (value.ReferenceCount > 0)
				{
					return true;
				}
				texture2D = value.Texture;
				_entriesByCacheKey.Remove(cacheKey);
			}
			if (texture2D != null)
			{
				UnityEngine.Object.DestroyImmediate(texture2D);
			}
			return true;
		}
		return false;
	}

	public static void Clear()
	{
		Texture2D[] array;
		lock (_cacheLock)
		{
			if (_entriesByCacheKey.Count == 0)
			{
				return;
			}
			List<Texture2D> list = new List<Texture2D>(_entriesByCacheKey.Count);
			foreach (PreviewTextureCacheEntry value in _entriesByCacheKey.Values)
			{
				if (value?.Texture != null)
				{
					list.Add(value.Texture);
				}
			}
			_entriesByCacheKey.Clear();
			array = list.ToArray();
		}
		for (int i = 0; i < array.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(array[i]);
		}
	}

	static PsdLayerPreviewCache()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_cacheLock = new object();
		_entriesByCacheKey = new Dictionary<string, PreviewTextureCacheEntry>(StringComparer.Ordinal);
	}
}
}
