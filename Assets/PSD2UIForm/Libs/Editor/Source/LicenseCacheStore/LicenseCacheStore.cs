using System;
using System.IO;
using System.Linq;
using System.Text;
using PsdLicensing;
using PsdProtectionGuards;
using UnityEngine;
using cn.efunstudio.psdreader;
using PsdProtectionRuntime;

namespace PsdLicensing
{

internal sealed class LicenseCacheStore
{
	internal PsdReaderLicenseCacheDocument Load()
	{
		string path = LicenseStoragePaths.GetLicenseCachePath();
		if (!File.Exists(path))
		{
			return null;
		}
		try
		{
			string text = GetDeviceBindingHash();
			if (!string.IsNullOrWhiteSpace(text))
			{
				if (!LicenseCryptography.TryDecryptCacheStore(File.ReadAllBytes(path), text, out var bytes))
				{
					Clear();
					return null;
				}
				string text2 = Encoding.UTF8.GetString(bytes);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					PsdReaderLicenseCacheDocument psdReaderLicenseCacheDocument = JsonUtility.FromJson<PsdReaderLicenseCacheDocument>(text2);
					if (psdReaderLicenseCacheDocument != null && psdReaderLicenseCacheDocument.Schema == 1)
					{
						return psdReaderLicenseCacheDocument;
					}
					Clear();
					return null;
				}
				return null;
			}
			return null;
		}
		catch (Exception)
		{
			Clear();
			return null;
		}
	}

	internal void Save(PsdReaderLicenseCacheDocument P_0)
	{
		if (P_0 == null)
		{
			return;
		}
		string text = GetDeviceBindingHash();
		if (!string.IsNullOrWhiteSpace(text))
		{
			Directory.CreateDirectory(LicenseStoragePaths.GetLicenseCacheDirectory());
			string s = JsonUtility.ToJson(P_0, prettyPrint: true);
			byte[] array = LicenseCryptography.EncryptCacheStore(Encoding.UTF8.GetBytes(s), text);
			if (array != null && array.Length != 0)
			{
				File.WriteAllBytes(LicenseStoragePaths.GetLicenseCachePath(), array);
			}
		}
	}

	internal void Clear()
	{
		string path = LicenseStoragePaths.GetLicenseCachePath();
		if (File.Exists(path))
		{
			File.Delete(path);
		}
		string path2 = LicenseStoragePaths.GetLicenseCacheDirectory();
		if (Directory.Exists(path2) && !Directory.EnumerateFileSystemEntries(path2).Any())
		{
			Directory.Delete(path2);
		}
	}

	private static string GetDeviceBindingHash()
	{
		return LicenseCryptography.HashDeviceIdentifier(SystemInfo.deviceUniqueIdentifier);
	}

	public LicenseCacheStore()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}
}
}
