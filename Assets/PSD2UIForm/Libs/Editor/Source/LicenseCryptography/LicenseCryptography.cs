using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using PsdProtectionGuards;
using PsdLicensing;
using UnityEngine;
using cn.efunstudio.psdreader;
using PsdProtectionRuntime;

namespace PsdLicensing
{

internal static class LicenseCryptography
{
	private static readonly byte[] _rsaEncryptionObjectIdentifier;

	private static readonly uint[] _embeddedKeyWords;

	private static readonly uint[] _embeddedKeyMasks;

	private static string _assemblyFingerprint;

	[SpecialName]
	internal static string GetProtocolVersion()
	{
		return "v1";
	}

	internal static string NormalizeOrderNumber(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(((string)P_0).Length);
		for (int i = 0; i < ((string)P_0).Length; i++)
		{
			if (char.IsDigit(((string)P_0)[i]))
			{
				stringBuilder.Append(((string)P_0)[i]);
			}
		}
		return stringBuilder.ToString();
	}

	internal static string NormalizeIdentifier(object P_0)
	{
		if (!string.IsNullOrWhiteSpace((string)P_0))
		{
			StringBuilder stringBuilder = new StringBuilder(((string)P_0).Length);
			for (int i = 0; i < ((string)P_0).Length; i++)
			{
				char c = char.ToLowerInvariant(((string)P_0)[i]);
				if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')
				{
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.ToString();
		}
		return string.Empty;
	}

	internal static string NormalizeHexString(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(((string)P_0).Length);
		for (int i = 0; i < ((string)P_0).Length; i++)
		{
			char c = char.ToLowerInvariant(((string)P_0)[i]);
			if ((c >= 'a' && c <= 'f') || (c >= '0' && c <= '9'))
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	internal static string NormalizeDeviceIdentifier(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return string.Empty;
		}
		return ((string)P_0).Trim();
	}

	internal static string HashDeviceIdentifier(object P_0)
	{
		string text = NormalizeDeviceIdentifier(P_0);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return ComputeStringSha256Hex("GPUAnim|DeviceId|" + GetProtocolVersion() + "|" + text);
		}
		return string.Empty;
	}

	internal static string HashLicenseLookupId(object P_0)
	{
		return ComputeStringSha256Hex("EFunStudio|License|" + GetProtocolVersion() + "|psd2ugui|" + (string)P_0);
	}

	internal static string ComputeStringSha256Hex(object P_0)
	{
		using SHA256 sHA = SHA256.Create();
		return EncodeHex(sHA.ComputeHash(Encoding.UTF8.GetBytes((string)(P_0 ?? string.Empty))));
	}

	internal static string ComputeBytesSha256Hex(object P_0)
	{
		using SHA256 sHA = SHA256.Create();
		return EncodeHex(sHA.ComputeHash((byte[])(P_0 ?? Array.Empty<byte>())));
	}

	internal static byte[] ComputeStringSha256(object P_0)
	{
		using SHA256 sHA = SHA256.Create();
		return sHA.ComputeHash(Encoding.UTF8.GetBytes((string)(P_0 ?? string.Empty)));
	}

	internal static byte[] ComputeStringHmacSha256(object P_0, object P_1)
	{
		using HMACSHA256 hMACSHA = new HMACSHA256((byte[])(P_0 ?? Array.Empty<byte>()));
		return hMACSHA.ComputeHash(Encoding.UTF8.GetBytes((string)(P_1 ?? string.Empty)));
	}

	internal static byte[] ComputeBytesHmacSha256(object P_0, object P_1)
	{
		using HMACSHA256 hMACSHA = new HMACSHA256((byte[])(P_0 ?? Array.Empty<byte>()));
		return hMACSHA.ComputeHash((byte[])(P_1 ?? Array.Empty<byte>()));
	}

	internal static byte[] GetEmbeddedKeyMaterial()
	{
		byte[] array = new byte[_embeddedKeyWords.Length * 4];
		for (int i = 0; i < _embeddedKeyWords.Length; i++)
		{
			Buffer.BlockCopy(BitConverter.GetBytes(_embeddedKeyWords[i] ^ _embeddedKeyMasks[i]), 0, array, i * 4, 4);
		}
		return array;
	}

	internal static string GetAssemblyFingerprint()
	{
		Assembly assembly;
		object obj;
		if (string.IsNullOrWhiteSpace(_assemblyFingerprint))
		{
			assembly = typeof(LicenseCryptography).Assembly;
			if ((object)assembly == null)
			{
				obj = null;
			}
			else
			{
				AssemblyName name = assembly.GetName();
				if (name == null)
				{
					obj = null;
				}
				else
				{
					obj = name.Name;
					if (obj != null)
					{
						goto IL_0043;
					}
				}
			}
			obj = string.Empty;
			goto IL_0043;
		}
		return _assemblyFingerprint;
		IL_0043:
		string text = (string)obj;
		object obj2;
		if ((object)assembly != null)
		{
			AssemblyName name2 = assembly.GetName();
			if (name2 == null)
			{
				obj2 = null;
			}
			else
			{
				Version version = name2.Version;
				if ((object)version == null)
				{
					obj2 = null;
				}
				else
				{
					obj2 = version.ToString();
					if (obj2 != null)
					{
						goto IL_0074;
					}
				}
			}
		}
		else
		{
			obj2 = null;
		}
		obj2 = string.Empty;
		goto IL_0074;
		IL_0099:
		object obj3;
		string text2 = (string)obj3;
		object obj4;
		if ((object)assembly != null)
		{
			Module manifestModule = assembly.ManifestModule;
			if ((object)manifestModule == null)
			{
				obj4 = null;
			}
			else
			{
				obj4 = manifestModule.ModuleVersionId.ToString("N");
				if (obj4 != null)
				{
					goto IL_00cc;
				}
			}
		}
		else
		{
			obj4 = null;
		}
		obj4 = string.Empty;
		goto IL_00cc;
		IL_00cc:
		string text3 = (string)obj4;
		string text4 = string.Empty;
		try
		{
			object obj5;
			if ((object)assembly == null)
			{
				obj5 = null;
			}
			else
			{
				obj5 = assembly.Location;
				if (obj5 != null)
				{
					goto IL_00ec;
				}
			}
			obj5 = string.Empty;
			goto IL_00ec;
			IL_00ec:
			string text5 = (string)obj5;
			if (!string.IsNullOrWhiteSpace(text5) && File.Exists(text5))
			{
				text4 = ComputeBytesSha256Hex(File.ReadAllBytes(text5));
			}
		}
		catch
		{
			text4 = string.Empty;
		}
		string text6;
		_assemblyFingerprint = ComputeStringSha256Hex("psd2ugui|AssemblyFingerprint|" + GetProtocolVersion() + "|" + text + "|" + text6 + "|" + text2 + "|" + text3 + "|" + text4);
		return _assemblyFingerprint;
		IL_0074:
		text6 = (string)obj2;
		if ((object)assembly == null)
		{
			obj3 = null;
		}
		else
		{
			Module manifestModule2 = assembly.ManifestModule;
			if ((object)manifestModule2 != null)
			{
				obj3 = manifestModule2.Name;
				if (obj3 != null)
				{
					goto IL_0099;
				}
			}
			else
			{
				obj3 = null;
			}
		}
		obj3 = string.Empty;
		goto IL_0099;
	}

	internal static string HashProjectScope(object P_0)
	{
		return ComputeStringSha256Hex("psd2ugui|ProjectScope|" + GetProtocolVersion() + "|" + NormalizeScopeIdentifier(P_0));
	}

	internal static byte[] DeriveRepositoryDocumentKey(object P_0)
	{
		return ComputeStringHmacSha256(GetEmbeddedKeyMaterial(), "EFunStudio|RepoDoc|" + GetProtocolVersion() + "|" + NormalizeScopeIdentifier(P_0));
	}

	internal static byte[] DeriveLicenseAccessKey(object P_0, object P_1)
	{
		return ComputeStringHmacSha256(GetEmbeddedKeyMaterial(), "EFunStudio|LicenseAccess|" + GetProtocolVersion() + "|psd2ugui|" + NormalizeOrderNumber(P_0) + "|" + NormalizeHexString(P_1));
	}

	internal static byte[] DeriveLicensePayloadKey(object P_0, object P_1)
	{
		return ComputeStringHmacSha256(P_1, "EFunStudio|LicensePayload|" + GetProtocolVersion() + "|psd2ugui|" + NormalizeHexString(P_0));
	}

	internal static byte[] DeriveOfflineLeaseKey(object P_0, object P_1)
	{
		return ComputeStringHmacSha256(P_1, "EFunStudio|OfflineLease|" + GetProtocolVersion() + "|psd2ugui|" + NormalizeHexString(P_0));
	}

	internal static string EncodeBase64Url(object P_0)
	{
		return Convert.ToBase64String((byte[])(P_0 ?? Array.Empty<byte>())).TrimEnd('=').Replace('+', '-')
			.Replace('/', '_');
	}

	internal static byte[] DecodeBase64Url(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return Array.Empty<byte>();
		}
		string text = ((string)P_0).Replace('-', '+').Replace('_', '/');
		int num = (4 - text.Length % 4) & 3;
		if (num > 0)
		{
			text = text.PadRight(text.Length + num, '=');
		}
		return Convert.FromBase64String(text);
	}

	internal static string ComputeLicenseStateFingerprint(object P_0, int P_1, int P_2, int P_3, object P_4, object P_5, object P_6)
	{
		if (P_0 != null)
		{
			return ComputeStringSha256Hex(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}", "psd2ugui", ((PsdReaderLicensePayloadDocument)P_0).LicenseId, ((PsdReaderLicensePayloadDocument)P_0).LookupId, ((PsdReaderLicensePayloadDocument)P_0).Revision, ((PsdReaderLicensePayloadDocument)P_0).IssuedUtcTicks, ((PsdReaderLicensePayloadDocument)P_0).SupportUntilUtcTicks, ((PsdReaderLicensePayloadDocument)P_0).FeatureMask, Math.Max(1, P_1), Math.Max(1, P_2), Math.Max(1, P_3), P_4 ?? string.Empty, NormalizeHexString(P_5), EncodeHex(((PsdReaderLicensePayloadDocument)P_0).GrantSeed), EncodeHex(P_6)));
		}
		return ComputeStringSha256Hex(string.Format("{0}|null|{1}|{2}|{3}", "psd2ugui", Math.Max(1, P_1), Math.Max(1, P_2), Math.Max(1, P_3)));
	}

	internal static string ComputeCacheProof(object P_0, object P_1, DateTime P_2, DateTime P_3)
	{
		if (P_0 != null)
		{
			DateTime dateTime = ToUtc(P_2);
			DateTime dateTime2 = ToUtc(P_3);
			string text = ComputeStringSha256Hex((((PsdReaderLicenseCacheDocument)P_0).MetaEnvelopeBase64 ?? string.Empty) + "|" + (((PsdReaderLicenseCacheDocument)P_0).LicenseEnvelopeBase64 ?? string.Empty) + "|" + (((PsdReaderLicenseCacheDocument)P_0).OfflineLeaseEnvelopeBase64 ?? string.Empty) + "|" + (((PsdReaderLicenseCacheDocument)P_0).DeviceShardEnvelopeBase64 ?? string.Empty));
			return ComputeStringSha256Hex(string.Format("{0}|CacheProof|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}|{16}|{17}|{18}", "psd2ugui", GetProtocolVersion(), NormalizeHexString(((PsdReaderLicenseCacheDocument)P_0).LookupId), NormalizeHexString(((PsdReaderLicenseCacheDocument)P_0).LicenseId), ((PsdReaderLicenseCacheDocument)P_0).StatusCode, ((PsdReaderLicenseCacheDocument)P_0).Revision, ((PsdReaderLicenseCacheDocument)P_0).FeatureMask, ((PsdReaderLicenseCacheDocument)P_0).CatalogMask, Math.Max(1, ((PsdReaderLicenseCacheDocument)P_0).CurrentMajorVersion), Math.Max(1, ((PsdReaderLicenseCacheDocument)P_0).MaxMajorVersion), Math.Max(1, ((PsdReaderLicenseCacheDocument)P_0).LocalCacheTimeoutDays), Math.Max(1, ((PsdReaderLicenseCacheDocument)P_0).MetaRevision), NormalizeIdentifier(((PsdReaderLicenseCacheDocument)P_0).ActiveKid), NormalizeIdentifier(((PsdReaderLicenseCacheDocument)P_0).ActivationSource), NormalizeHexString(((PsdReaderLicenseCacheDocument)P_0).StateSaltHex), NormalizeHexString(P_1), text, dateTime.Ticks, dateTime2.Ticks));
		}
		return string.Empty;
	}

	internal static string ComputeTranscriptHead(object P_0, object P_1, DateTime P_2, DateTime P_3, object P_4)
	{
		if (P_0 == null)
		{
			return string.Empty;
		}
		DateTime dateTime = ToUtc(P_2);
		DateTime dateTime2 = ToUtc(P_3);
		string text = NormalizeHexString(P_4);
		string text2 = ComputeStringSha256Hex((((PsdReaderLicenseCacheDocument)P_0).MetaEnvelopeBase64 ?? string.Empty) + "|" + (((PsdReaderLicenseCacheDocument)P_0).LicenseEnvelopeBase64 ?? string.Empty) + "|" + (((PsdReaderLicenseCacheDocument)P_0).OfflineLeaseEnvelopeBase64 ?? string.Empty) + "|" + (((PsdReaderLicenseCacheDocument)P_0).DeviceShardEnvelopeBase64 ?? string.Empty));
		return ComputeStringSha256Hex(string.Format("{0}|TranscriptHead|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}|{16}|{17}|{18}|{19}", "psd2ugui", GetProtocolVersion(), text, NormalizeHexString(((PsdReaderLicenseCacheDocument)P_0).LookupId), NormalizeHexString(((PsdReaderLicenseCacheDocument)P_0).LicenseId), ((PsdReaderLicenseCacheDocument)P_0).StatusCode, ((PsdReaderLicenseCacheDocument)P_0).Revision, ((PsdReaderLicenseCacheDocument)P_0).FeatureMask, ((PsdReaderLicenseCacheDocument)P_0).CatalogMask, Math.Max(1, ((PsdReaderLicenseCacheDocument)P_0).CurrentMajorVersion), Math.Max(1, ((PsdReaderLicenseCacheDocument)P_0).MaxMajorVersion), Math.Max(1, ((PsdReaderLicenseCacheDocument)P_0).LocalCacheTimeoutDays), Math.Max(1, ((PsdReaderLicenseCacheDocument)P_0).MetaRevision), NormalizeIdentifier(((PsdReaderLicenseCacheDocument)P_0).ActiveKid), NormalizeIdentifier(((PsdReaderLicenseCacheDocument)P_0).ActivationSource), NormalizeHexString(((PsdReaderLicenseCacheDocument)P_0).StateSaltHex), NormalizeHexString(P_1), text2, dateTime.Ticks, dateTime2.Ticks));
	}

	internal static string EncodeClockWatermark(DateTime P_0, DateTime P_1, object P_2, object P_3, object P_4)
	{
		if (!string.IsNullOrWhiteSpace((string)P_4) && !string.IsNullOrWhiteSpace((string)P_2) && !string.IsNullOrWhiteSpace((string)P_3))
		{
			DateTime dateTime = ToUtc(P_0);
			DateTime dateTime2 = ToUtc(P_1);
			byte[] array;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
				binaryWriter.Write(827805008u);
				binaryWriter.Write(1);
				binaryWriter.Write(dateTime.Ticks);
				binaryWriter.Write(dateTime2.Ticks);
				WriteLengthPrefixedUtf8(binaryWriter, NormalizeHexString(P_2));
				WriteLengthPrefixedUtf8(binaryWriter, NormalizeHexString(P_3));
				binaryWriter.Flush();
				array = memoryStream.ToArray();
			}
			byte[] array3;
			byte[] array2 = EncryptAesCbc(array, DeriveClockWatermarkEncryptionKey(P_4), out array3);
			byte[] array4;
			using (MemoryStream memoryStream2 = new MemoryStream())
			{
				using BinaryWriter binaryWriter2 = new BinaryWriter(memoryStream2, Encoding.UTF8, leaveOpen: true);
				binaryWriter2.Write(827804995u);
				binaryWriter2.Write(1);
				WriteLengthPrefixedByteArray(binaryWriter2, array3);
				WriteLengthPrefixedByteArray(binaryWriter2, array2);
				binaryWriter2.Flush();
				array4 = memoryStream2.ToArray();
			}
			byte[] array5 = ComputeBytesHmacSha256(DeriveClockWatermarkAuthenticationKey(P_4), array4);
			using MemoryStream memoryStream3 = new MemoryStream();
			using (BinaryWriter binaryWriter3 = new BinaryWriter(memoryStream3, Encoding.UTF8, leaveOpen: true))
			{
				binaryWriter3.Write(array4);
				WriteLengthPrefixedByteArray(binaryWriter3, array5);
				binaryWriter3.Flush();
			}
			return EncodeBase64Url(memoryStream3.ToArray());
		}
		return string.Empty;
	}

	internal static bool TryDecodeClockWatermark(object P_0, object P_1, out DateTime P_2, out DateTime P_3, out string P_4, out string P_5)
	{
		P_2 = DateTime.MinValue;
		P_3 = DateTime.MinValue;
		P_4 = string.Empty;
		P_5 = string.Empty;
		if (!string.IsNullOrWhiteSpace((string)P_0) && !string.IsNullOrWhiteSpace((string)P_1))
		{
			try
			{
				byte[] array = DecodeBase64Url(P_0);
				if (array != null && array.Length != 0)
				{
					using (MemoryStream memoryStream = new MemoryStream(array, writable: false))
					{
						using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
						uint num = binaryReader.ReadUInt32();
						int num2 = binaryReader.ReadInt32();
						if (num == 827804995 && num2 == 1)
						{
							byte[] array2 = ReadBoundedByteArray(binaryReader, 64);
							byte[] array3 = ReadBoundedByteArray(binaryReader, 1048576);
							byte[] array4 = ReadBoundedByteArray(binaryReader, 128);
							if (array4.Length != 0 && memoryStream.Position == memoryStream.Length)
							{
								int num3 = array.Length - array4.Length - 4;
								if (num3 <= 0)
								{
									return false;
								}
								byte[] array5 = new byte[num3];
								Buffer.BlockCopy(array, 0, array5, 0, array5.Length);
								if (ByteArraysEqual(ComputeBytesHmacSha256(DeriveClockWatermarkAuthenticationKey(P_1), array5), array4))
								{
									using (MemoryStream memoryStream2 = new MemoryStream(DecryptAesCbc(array3, DeriveClockWatermarkEncryptionKey(P_1), array2), writable: false))
									{
										using BinaryReader binaryReader2 = new BinaryReader(memoryStream2, Encoding.UTF8, leaveOpen: false);
										uint num4 = binaryReader2.ReadUInt32();
										int num5 = binaryReader2.ReadInt32();
										if (num4 == 827805008 && num5 == 1)
										{
											long ticks = binaryReader2.ReadInt64();
											long ticks2 = binaryReader2.ReadInt64();
											P_4 = NormalizeHexString(ReadLengthPrefixedUtf8(binaryReader2));
											P_5 = NormalizeHexString(ReadLengthPrefixedUtf8(binaryReader2));
											if (memoryStream2.Position != memoryStream2.Length)
											{
												return false;
											}
											P_2 = new DateTime(ticks, DateTimeKind.Utc);
											P_3 = new DateTime(ticks2, DateTimeKind.Utc);
											return !string.IsNullOrWhiteSpace(P_4) && !string.IsNullOrWhiteSpace(P_5);
										}
										return false;
									}
								}
								return false;
							}
							return false;
						}
						return false;
					}
				}
				return false;
			}
			catch
			{
				P_2 = DateTime.MinValue;
				P_3 = DateTime.MinValue;
				P_4 = string.Empty;
				P_5 = string.Empty;
				return false;
			}
		}
		return false;
	}

	internal static int CombineStateIntegers(object P_0)
	{
		if (P_0 != null && ((Array)P_0).Length != 0)
		{
			int num = 1595565015;
			for (int i = 0; i < ((Array)P_0).Length; i++)
			{
				int num2 = ((int[])P_0)[i] ^ -1640531527 ^ (i * 73244475);
				num = (num << 5) | (int)((uint)num >> 27);
				num = num ^ num2 ^ (num >> 3);
			}
			return num;
		}
		return 0;
	}

	internal static string DecodeUtf8(object P_0)
	{
		return Encoding.UTF8.GetString((byte[])(P_0 ?? Array.Empty<byte>()));
	}

	internal static DateTime ParseUtcDateTime(object P_0, DateTime P_1)
	{
		if (!DateTime.TryParse((string)P_0, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
		{
			return P_1;
		}
		return result.ToUniversalTime();
	}

	internal static string EncodeHex(object P_0)
	{
		if (P_0 != null && ((Array)P_0).Length != 0)
		{
			char[] array = new char[((Array)P_0).Length * 2];
			for (int i = 0; i < ((Array)P_0).Length; i++)
			{
				byte b = ((byte[])P_0)[i];
				array[i * 2] = "0123456789abcdef"[b >> 4];
				array[i * 2 + 1] = "0123456789abcdef"[b & 0xF];
			}
			return new string(array);
		}
		return string.Empty;
	}

	internal static byte[] DecodeHex(object P_0)
	{
		string text = NormalizeHexString(P_0);
		if (!string.IsNullOrWhiteSpace(text) && (text.Length & 1) == 0)
		{
			byte[] array = new byte[text.Length / 2];
			for (int i = 0; i < array.Length; i++)
			{
				int num = HexDigitToValue(text[i * 2]);
				int num2 = HexDigitToValue(text[i * 2 + 1]);
				if (num >= 0 && num2 >= 0)
				{
					array[i] = (byte)((num << 4) | num2);
					continue;
				}
				return Array.Empty<byte>();
			}
			return array;
		}
		return Array.Empty<byte>();
	}

	internal static string EncryptLocalCacheText(object P_0, object P_1)
	{
		byte[] array2;
		byte[] array = EncryptAesCbc(P_0, DeriveLocalCacheKey(P_1), out array2);
		return EncodeBase64Url(array2) + "." + EncodeBase64Url(array);
	}

	internal static bool TryDecryptLocalCacheText(object P_0, object P_1, out byte[] P_2)
	{
		P_2 = Array.Empty<byte>();
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return false;
		}
		string[] array = ((string)P_0).Split('.', StringSplitOptions.None);
		if (array.Length != 2)
		{
			return false;
		}
		try
		{
			byte[] array2 = DecodeBase64Url(array[0]);
			byte[] array3 = DecodeBase64Url(array[1]);
			P_2 = DecryptAesCbc(array3, DeriveLocalCacheKey(P_1), array2);
			return P_2.Length != 0;
		}
		catch
		{
			P_2 = Array.Empty<byte>();
			return false;
		}
	}

	internal static byte[] EncryptCacheStore(object P_0, object P_1)
	{
		if (P_0 != null && ((Array)P_0).Length != 0 && !string.IsNullOrWhiteSpace((string)P_1))
		{
			byte[] array2;
			byte[] array = EncryptAesCbc(P_0, DeriveCacheEncryptionKey(P_1), out array2);
			byte[] array3;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
				binaryWriter.Write(826360645u);
				binaryWriter.Write(1);
				WriteLengthPrefixedByteArray(binaryWriter, array2);
				WriteLengthPrefixedByteArray(binaryWriter, array);
				binaryWriter.Flush();
				array3 = memoryStream.ToArray();
			}
			byte[] array4 = ComputeBytesHmacSha256(DeriveCacheAuthenticationKey(P_1), array3);
			using MemoryStream memoryStream2 = new MemoryStream();
			using (BinaryWriter binaryWriter2 = new BinaryWriter(memoryStream2, Encoding.UTF8, leaveOpen: true))
			{
				binaryWriter2.Write(array3);
				WriteLengthPrefixedByteArray(binaryWriter2, array4);
				binaryWriter2.Flush();
			}
			return memoryStream2.ToArray();
		}
		return Array.Empty<byte>();
	}

	internal static bool TryDecryptCacheStore(object P_0, object P_1, out byte[] P_2)
	{
		P_2 = Array.Empty<byte>();
		if (P_0 != null && ((Array)P_0).Length != 0 && !string.IsNullOrWhiteSpace((string)P_1))
		{
			try
			{
				using MemoryStream memoryStream = new MemoryStream((byte[])P_0, writable: false);
				using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
				uint num = binaryReader.ReadUInt32();
				int num2 = binaryReader.ReadInt32();
				if (num == 826360645 && num2 == 1)
				{
					byte[] array = ReadBoundedByteArray(binaryReader, 64);
					byte[] array2 = ReadBoundedByteArray(binaryReader, 16777216);
					byte[] array3 = ReadBoundedByteArray(binaryReader, 128);
					if (array3.Length != 0 && memoryStream.Position == memoryStream.Length)
					{
						int num3 = ((Array)P_0).Length - array3.Length - 4;
						if (num3 <= 0)
						{
							return false;
						}
						byte[] array4 = new byte[num3];
						Buffer.BlockCopy((Array)P_0, 0, array4, 0, array4.Length);
						if (!ByteArraysEqual(ComputeBytesHmacSha256(DeriveCacheAuthenticationKey(P_1), array4), array3))
						{
							return false;
						}
						P_2 = DecryptAesCbc(array2, DeriveCacheEncryptionKey(P_1), array);
						return P_2.Length != 0;
					}
					return false;
				}
				return false;
			}
			catch
			{
				P_2 = Array.Empty<byte>();
				return false;
			}
		}
		return false;
	}

	internal static byte[] EncodeProjectLicenseBundle(object P_0)
	{
		if (!TryNormalizeProjectLicenseBundle(P_0, out var psdReaderProjectLicenseBundleDocument))
		{
			return Array.Empty<byte>();
		}
		psdReaderProjectLicenseBundleDocument.BundleSeal = ComputeProjectLicenseSeal(psdReaderProjectLicenseBundleDocument);
		byte[] array;
		using (MemoryStream memoryStream = new MemoryStream())
		{
			using BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
			binaryWriter.Write(827081296u);
			binaryWriter.Write(2);
			WriteLengthPrefixedUtf8(binaryWriter, psdReaderProjectLicenseBundleDocument.VendorCode);
			WriteLengthPrefixedUtf8(binaryWriter, psdReaderProjectLicenseBundleDocument.ProductCode);
			WriteLengthPrefixedUtf8(binaryWriter, psdReaderProjectLicenseBundleDocument.LookupId);
			WriteLengthPrefixedUtf8(binaryWriter, psdReaderProjectLicenseBundleDocument.LicenseId);
			WriteLengthPrefixedUtf8(binaryWriter, psdReaderProjectLicenseBundleDocument.MetaEnvelopeBase64);
			WriteLengthPrefixedUtf8(binaryWriter, psdReaderProjectLicenseBundleDocument.LicenseEnvelopeBase64);
			WriteLengthPrefixedUtf8(binaryWriter, psdReaderProjectLicenseBundleDocument.OfflineLeaseEnvelopeBase64);
			WriteLengthPrefixedUtf8(binaryWriter, psdReaderProjectLicenseBundleDocument.WrappedLicenseAccessKey);
			WriteLengthPrefixedUtf8(binaryWriter, psdReaderProjectLicenseBundleDocument.WrappedOrderIdCipher);
			binaryWriter.Write(psdReaderProjectLicenseBundleDocument.ExportIssuedUtcTicks);
			binaryWriter.Write(psdReaderProjectLicenseBundleDocument.ExportExpiresUtcTicks);
			WriteLengthPrefixedUtf8(binaryWriter, psdReaderProjectLicenseBundleDocument.BundleSeal);
			binaryWriter.Flush();
			array = memoryStream.ToArray();
		}
		byte[] array3;
		byte[] array2 = EncryptAesCbc(array, DeriveProjectLicenseEncryptionKey(), out array3);
		byte[] array4;
		using (MemoryStream memoryStream2 = new MemoryStream())
		{
			using BinaryWriter binaryWriter2 = new BinaryWriter(memoryStream2, Encoding.UTF8, leaveOpen: true);
			binaryWriter2.Write(827084871u);
			binaryWriter2.Write(2);
			WriteLengthPrefixedByteArray(binaryWriter2, array3);
			WriteLengthPrefixedByteArray(binaryWriter2, array2);
			binaryWriter2.Flush();
			array4 = memoryStream2.ToArray();
		}
		byte[] array5 = ComputeBytesHmacSha256(DeriveProjectLicenseAuthenticationKey(), array4);
		using MemoryStream memoryStream3 = new MemoryStream();
		using (BinaryWriter binaryWriter3 = new BinaryWriter(memoryStream3, Encoding.UTF8, leaveOpen: true))
		{
			binaryWriter3.Write(array4);
			WriteLengthPrefixedByteArray(binaryWriter3, array5);
			binaryWriter3.Flush();
		}
		return memoryStream3.ToArray();
	}

	internal static bool TryDecodeProjectLicenseBundle(object P_0, out PsdReaderProjectLicenseBundleDocument P_1)
	{
		P_1 = null;
		if (P_0 != null && ((Array)P_0).Length != 0)
		{
			try
			{
				using MemoryStream memoryStream = new MemoryStream((byte[])P_0, writable: false);
				using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
				uint num = binaryReader.ReadUInt32();
				int num2 = binaryReader.ReadInt32();
				if (num == 827084871 && num2 == 2)
				{
					byte[] array = ReadBoundedByteArray(binaryReader, 64);
					byte[] array2 = ReadBoundedByteArray(binaryReader, 4194304);
					byte[] array3 = ReadBoundedByteArray(binaryReader, 128);
					if (array3.Length != 0 && memoryStream.Position == memoryStream.Length)
					{
						int num3 = ((Array)P_0).Length - array3.Length - 4;
						if (num3 <= 0)
						{
							return false;
						}
						byte[] array4 = new byte[num3];
						Buffer.BlockCopy((Array)P_0, 0, array4, 0, array4.Length);
						if (ByteArraysEqual(ComputeBytesHmacSha256(DeriveProjectLicenseAuthenticationKey(), array4), array3))
						{
							using (MemoryStream memoryStream2 = new MemoryStream(DecryptAesCbc(array2, DeriveProjectLicenseEncryptionKey(), array), writable: false))
							{
								using BinaryReader binaryReader2 = new BinaryReader(memoryStream2, Encoding.UTF8, leaveOpen: false);
								uint num4 = binaryReader2.ReadUInt32();
								int num5 = binaryReader2.ReadInt32();
								if (num4 == 827081296 && num5 == 2)
								{
									PsdReaderProjectLicenseBundleDocument psdReaderProjectLicenseBundleDocument = new PsdReaderProjectLicenseBundleDocument
									{
										Schema = num5,
										VendorCode = ReadLengthPrefixedUtf8(binaryReader2),
										ProductCode = ReadLengthPrefixedUtf8(binaryReader2),
										LookupId = ReadLengthPrefixedUtf8(binaryReader2),
										LicenseId = ReadLengthPrefixedUtf8(binaryReader2),
										MetaEnvelopeBase64 = ReadLengthPrefixedUtf8(binaryReader2),
										LicenseEnvelopeBase64 = ReadLengthPrefixedUtf8(binaryReader2),
										OfflineLeaseEnvelopeBase64 = ReadLengthPrefixedUtf8(binaryReader2),
										WrappedLicenseAccessKey = ReadLengthPrefixedUtf8(binaryReader2),
										WrappedOrderIdCipher = ReadLengthPrefixedUtf8(binaryReader2),
										ExportIssuedUtcTicks = binaryReader2.ReadInt64(),
										ExportExpiresUtcTicks = binaryReader2.ReadInt64(),
										BundleSeal = ReadLengthPrefixedUtf8(binaryReader2)
									};
									psdReaderProjectLicenseBundleDocument.ExportIssuedUtc = ((psdReaderProjectLicenseBundleDocument.ExportIssuedUtcTicks > 0L) ? new DateTime(psdReaderProjectLicenseBundleDocument.ExportIssuedUtcTicks, DateTimeKind.Utc).ToString("O") : string.Empty);
									psdReaderProjectLicenseBundleDocument.ExportExpiresUtc = ((psdReaderProjectLicenseBundleDocument.ExportExpiresUtcTicks <= 0L) ? string.Empty : new DateTime(psdReaderProjectLicenseBundleDocument.ExportExpiresUtcTicks, DateTimeKind.Utc).ToString("O"));
									if (memoryStream2.Position == memoryStream2.Length && TryNormalizeProjectLicenseBundle(psdReaderProjectLicenseBundleDocument, out var psdReaderProjectLicenseBundleDocument2))
									{
										if (string.Equals(ComputeProjectLicenseSeal(psdReaderProjectLicenseBundleDocument2), psdReaderProjectLicenseBundleDocument2.BundleSeal, StringComparison.Ordinal))
										{
											P_1 = psdReaderProjectLicenseBundleDocument2;
											return true;
										}
										return false;
									}
									return false;
								}
								return false;
							}
						}
						return false;
					}
					return false;
				}
				return false;
			}
			catch
			{
				P_1 = null;
				return false;
			}
		}
		return false;
	}

	internal static string WrapProjectLicenseBytes(object P_0, object P_1, DateTime P_2)
	{
		byte[] array = (byte[])(P_0 ?? Array.Empty<byte>());
		string text = NormalizeHexString(P_1);
		DateTime dateTime = ToUtc(P_2);
		if (array.Length != 0 && !string.IsNullOrWhiteSpace(text) && !(dateTime <= DateTime.MinValue))
		{
			byte[] array3;
			byte[] array2 = EncryptAesCbc(array, DeriveProjectLicenseWrapEncryptionKey(text, dateTime.Ticks), out array3);
			byte[] array4;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8, leaveOpen: true);
				binaryWriter.Write(827015511u);
				binaryWriter.Write(1);
				WriteLengthPrefixedByteArray(binaryWriter, array3);
				WriteLengthPrefixedByteArray(binaryWriter, array2);
				binaryWriter.Flush();
				array4 = memoryStream.ToArray();
			}
			byte[] array5 = ComputeBytesHmacSha256(DeriveProjectLicenseWrapAuthenticationKey(text, dateTime.Ticks), array4);
			using MemoryStream memoryStream2 = new MemoryStream();
			using (BinaryWriter binaryWriter2 = new BinaryWriter(memoryStream2, Encoding.UTF8, leaveOpen: true))
			{
				binaryWriter2.Write(array4);
				WriteLengthPrefixedByteArray(binaryWriter2, array5);
				binaryWriter2.Flush();
			}
			return EncodeBase64Url(memoryStream2.ToArray());
		}
		return string.Empty;
	}

	internal static bool TryUnwrapProjectLicenseBytes(object P_0, object P_1, DateTime P_2, out byte[] P_3)
	{
		P_3 = Array.Empty<byte>();
		string text = NormalizeHexString(P_1);
		DateTime dateTime = ToUtc(P_2);
		if (!string.IsNullOrWhiteSpace((string)P_0) && !string.IsNullOrWhiteSpace(text) && !(dateTime <= DateTime.MinValue))
		{
			try
			{
				byte[] array = DecodeBase64Url(P_0);
				using MemoryStream memoryStream = new MemoryStream(array, writable: false);
				using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
				uint num = binaryReader.ReadUInt32();
				int num2 = binaryReader.ReadInt32();
				if (num == 827015511 && num2 == 1)
				{
					byte[] array2 = ReadBoundedByteArray(binaryReader, 64);
					byte[] array3 = ReadBoundedByteArray(binaryReader, 1024);
					byte[] array4 = ReadBoundedByteArray(binaryReader, 128);
					if (array4.Length != 0 && memoryStream.Position == memoryStream.Length)
					{
						int num3 = array.Length - array4.Length - 4;
						if (num3 > 0)
						{
							byte[] array5 = new byte[num3];
							Buffer.BlockCopy(array, 0, array5, 0, array5.Length);
							if (ByteArraysEqual(ComputeBytesHmacSha256(DeriveProjectLicenseWrapAuthenticationKey(text, dateTime.Ticks), array5), array4))
							{
								P_3 = DecryptAesCbc(array3, DeriveProjectLicenseWrapEncryptionKey(text, dateTime.Ticks), array2);
								return P_3.Length != 0;
							}
							return false;
						}
						return false;
					}
					return false;
				}
				return false;
			}
			catch
			{
				P_3 = Array.Empty<byte>();
				return false;
			}
		}
		return false;
	}

	internal static string ComputeProjectLicenseSeal(object P_0)
	{
		if (TryNormalizeProjectLicenseBundle(P_0, out var psdReaderProjectLicenseBundleDocument))
		{
			string text = ComputeStringSha256Hex(psdReaderProjectLicenseBundleDocument.MetaEnvelopeBase64 + "|" + psdReaderProjectLicenseBundleDocument.LicenseEnvelopeBase64 + "|" + psdReaderProjectLicenseBundleDocument.OfflineLeaseEnvelopeBase64);
			string text2 = $"{psdReaderProjectLicenseBundleDocument.VendorCode}|{psdReaderProjectLicenseBundleDocument.ProductCode}|{NormalizeHexString(psdReaderProjectLicenseBundleDocument.LookupId)}|{NormalizeHexString(psdReaderProjectLicenseBundleDocument.LicenseId)}|{NormalizeHexString(psdReaderProjectLicenseBundleDocument.WrappedLicenseAccessKey)}|{NormalizeHexString(psdReaderProjectLicenseBundleDocument.WrappedOrderIdCipher)}|{text}|{psdReaderProjectLicenseBundleDocument.ExportIssuedUtcTicks}|{psdReaderProjectLicenseBundleDocument.ExportExpiresUtcTicks}";
			return EncodeHex(ComputeStringHmacSha256(DeriveProjectLicenseSealKey(), text2));
		}
		return string.Empty;
	}

	private static bool TryNormalizeProjectLicenseBundle(object P_0, out PsdReaderProjectLicenseBundleDocument P_1)
	{
		P_1 = null;
		if (P_0 == null)
		{
			return false;
		}
		DateTime dateTime = ((((PsdReaderProjectLicenseBundleDocument)P_0).ExportIssuedUtcTicks <= 0L) ? ParseUtcDateTime(((PsdReaderProjectLicenseBundleDocument)P_0).ExportIssuedUtc, DateTime.MinValue) : new DateTime(((PsdReaderProjectLicenseBundleDocument)P_0).ExportIssuedUtcTicks, DateTimeKind.Utc));
		DateTime dateTime2 = ((((PsdReaderProjectLicenseBundleDocument)P_0).ExportExpiresUtcTicks > 0L) ? new DateTime(((PsdReaderProjectLicenseBundleDocument)P_0).ExportExpiresUtcTicks, DateTimeKind.Utc) : ParseUtcDateTime(((PsdReaderProjectLicenseBundleDocument)P_0).ExportExpiresUtc, DateTime.MinValue));
		if (!(dateTime <= DateTime.MinValue) && !(dateTime2 <= dateTime))
		{
			if (((PsdReaderProjectLicenseBundleDocument)P_0).Schema != 2)
			{
				return false;
			}
			string text = NormalizeHexString(((PsdReaderProjectLicenseBundleDocument)P_0).LookupId);
			string text2 = (((PsdReaderProjectLicenseBundleDocument)P_0).LicenseId ?? string.Empty).Trim();
			string text3 = (((PsdReaderProjectLicenseBundleDocument)P_0).MetaEnvelopeBase64 ?? string.Empty).Trim();
			string text4 = (((PsdReaderProjectLicenseBundleDocument)P_0).LicenseEnvelopeBase64 ?? string.Empty).Trim();
			string text5 = (((PsdReaderProjectLicenseBundleDocument)P_0).OfflineLeaseEnvelopeBase64 ?? string.Empty).Trim();
			string text6 = (((PsdReaderProjectLicenseBundleDocument)P_0).WrappedLicenseAccessKey ?? string.Empty).Trim();
			string text7 = (((PsdReaderProjectLicenseBundleDocument)P_0).WrappedOrderIdCipher ?? string.Empty).Trim();
			if (string.Equals(((PsdReaderProjectLicenseBundleDocument)P_0).VendorCode, "efunstudio", StringComparison.Ordinal) && string.Equals(((PsdReaderProjectLicenseBundleDocument)P_0).ProductCode, "psd2ugui", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(text2) && !string.IsNullOrWhiteSpace(text3) && !string.IsNullOrWhiteSpace(text4) && !string.IsNullOrWhiteSpace(text5) && !string.IsNullOrWhiteSpace(text6) && !string.IsNullOrWhiteSpace(text7))
			{
				P_1 = new PsdReaderProjectLicenseBundleDocument
				{
					Schema = 2,
					VendorCode = "efunstudio",
					ProductCode = "psd2ugui",
					LookupId = text,
					LicenseId = text2,
					MetaEnvelopeBase64 = text3,
					LicenseEnvelopeBase64 = text4,
					OfflineLeaseEnvelopeBase64 = text5,
					WrappedLicenseAccessKey = text6,
					WrappedOrderIdCipher = text7,
					ExportIssuedUtcTicks = dateTime.Ticks,
					ExportIssuedUtc = dateTime.ToString("O"),
					ExportExpiresUtcTicks = dateTime2.Ticks,
					ExportExpiresUtc = dateTime2.ToString("O"),
					BundleSeal = NormalizeHexString(((PsdReaderProjectLicenseBundleDocument)P_0).BundleSeal)
				};
				return true;
			}
			return false;
		}
		return false;
	}

	internal static bool TryDecodeBinaryEnvelope(object P_0, object P_1, object P_2, out PsdReaderSignedEnvelopeDocument P_3)
	{
		P_3 = null;
		if (P_0 != null && ((Array)P_0).Length >= 17)
		{
			try
			{
				using MemoryStream memoryStream = new MemoryStream((byte[])P_0, writable: false);
				using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: true);
				if (binaryReader.ReadByte() == 69 && binaryReader.ReadByte() == 70 && binaryReader.ReadByte() == 83 && binaryReader.ReadByte() == 66)
				{
					if (binaryReader.ReadByte() != 1)
					{
						return false;
					}
					int num = binaryReader.ReadInt32();
					int num2 = binaryReader.ReadInt32();
					if (num > 0 && num <= 64 && num2 > 0 && num2 <= ((Array)P_0).Length)
					{
						byte[] array = binaryReader.ReadBytes(num);
						byte[] array2 = binaryReader.ReadBytes(num2);
						if (array.Length == num && array2.Length == num2 && memoryStream.Position == memoryStream.Length)
						{
							using MemoryStream memoryStream2 = new MemoryStream(DecryptAesCbc(array2, DeriveBinaryEnvelopeKey(P_1, P_2), array), writable: false);
							using BinaryReader binaryReader2 = new BinaryReader(memoryStream2, Encoding.UTF8, leaveOpen: true);
							P_3 = new PsdReaderSignedEnvelopeDocument
							{
								Schema = binaryReader2.ReadInt32(),
								Kind = ReadLengthPrefixedUtf8(binaryReader2),
								Kid = ReadLengthPrefixedUtf8(binaryReader2),
								Alg = ReadLengthPrefixedUtf8(binaryReader2),
								Enc = ReadLengthPrefixedUtf8(binaryReader2),
								Kdf = ReadLengthPrefixedUtf8(binaryReader2),
								Scope = ReadLengthPrefixedUtf8(binaryReader2),
								Iv = ReadLengthPrefixedUtf8(binaryReader2),
								Payload = ReadLengthPrefixedUtf8(binaryReader2),
								Sig = ReadLengthPrefixedUtf8(binaryReader2)
							};
							return memoryStream2.Position == memoryStream2.Length;
						}
						return false;
					}
					return false;
				}
				return false;
			}
			catch (Exception)
			{
				P_3 = null;
				return false;
			}
		}
		return false;
	}

	internal static bool TryVerifyAndDecryptSignedEnvelope(object P_0, object P_1, object P_2, object P_3, out byte[] P_4)
	{
		P_4 = Array.Empty<byte>();
		if (!string.IsNullOrWhiteSpace((string)P_0) && P_1 != null && !string.IsNullOrWhiteSpace(((PsdReaderSignedEnvelopeDocument)P_1).Payload) && !string.IsNullOrWhiteSpace(((PsdReaderSignedEnvelopeDocument)P_1).Sig) && !string.IsNullOrWhiteSpace(((PsdReaderSignedEnvelopeDocument)P_1).Iv))
		{
			if (((PsdReaderSignedEnvelopeDocument)P_1).Schema == 1 && string.Equals(((PsdReaderSignedEnvelopeDocument)P_1).Kind, "SecureEnvelope", StringComparison.Ordinal) && string.Equals(((PsdReaderSignedEnvelopeDocument)P_1).Alg, "RS256", StringComparison.Ordinal) && string.Equals(((PsdReaderSignedEnvelopeDocument)P_1).Enc, "AES256-CBC", StringComparison.Ordinal) && string.Equals(((PsdReaderSignedEnvelopeDocument)P_1).Kdf, "EFUN-HMACSHA256-V1", StringComparison.Ordinal))
			{
				if (!string.IsNullOrWhiteSpace((string)P_2) && !string.Equals((string)P_2, ((PsdReaderSignedEnvelopeDocument)P_1).Scope ?? string.Empty, StringComparison.Ordinal))
				{
					return false;
				}
				try
				{
					byte[] signature = DecodeBase64Url(((PsdReaderSignedEnvelopeDocument)P_1).Sig);
					using RSA rSA = RSA.Create();
					ImportRsaPublicKeyPem(rSA, P_0);
					if (!rSA.VerifyData(GetEnvelopeSigningBytes(P_1), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
					{
						return false;
					}
					byte[] array = DecodeBase64Url(((PsdReaderSignedEnvelopeDocument)P_1).Iv);
					byte[] array2 = DecodeBase64Url(((PsdReaderSignedEnvelopeDocument)P_1).Payload);
					P_4 = DecryptAesCbc(array2, P_3, array);
					return P_4.Length != 0;
				}
				catch (Exception)
				{
					P_4 = Array.Empty<byte>();
					return false;
				}
			}
			return false;
		}
		return false;
	}

	private static void ImportRsaPublicKeyPem(object P_0, object P_1)
	{
		if (P_0 == null)
		{
			throw LicenseExceptionFactory.CreateArgumentNull();
		}
		byte[] array = DecodePemBlock(P_1, "PUBLIC KEY");
		((RSA)P_0).ImportParameters(ParseRsaSubjectPublicKeyInfo(array));
	}

	private static RSAParameters ParseRsaSubjectPublicKeyInfo(object P_0)
	{
		int num = 0;
		int num2 = ReadAsn1ElementEnd(P_0, ref num, 48);
		int num3 = ReadAsn1ElementEnd(P_0, ref num, 48);
		if (ByteArraysEqual(ReadAsn1ElementBytes(P_0, ref num, 6), _rsaEncryptionObjectIdentifier))
		{
			if (num < num3 && ((byte[])P_0)[num] == 5 && ReadAsn1ElementEnd(P_0, ref num, 5) != num)
			{
				throw LicenseExceptionFactory.CreateInvalidOperation();
			}
			num = num3;
			byte[] array = ReadAsn1BitString(P_0, ref num);
			if (num != num2)
			{
				throw LicenseExceptionFactory.CreateInvalidOperation();
			}
			int num4 = 0;
			int num5 = ReadAsn1ElementEnd(array, ref num4, 48);
			byte[] modulus = TrimLeadingZeroBytes(ReadAsn1ElementBytes(array, ref num4, 2));
			byte[] exponent = TrimLeadingZeroBytes(ReadAsn1ElementBytes(array, ref num4, 2));
			if (num4 != num5)
			{
				throw LicenseExceptionFactory.CreateInvalidOperation();
			}
			return new RSAParameters
			{
				Modulus = modulus,
				Exponent = exponent
			};
		}
		throw LicenseExceptionFactory.CreateInvalidOperation();
	}

	private static int ReadAsn1ElementEnd(object P_0, ref int P_1, byte P_2)
	{
		if (P_0 != null && P_1 < ((Array)P_0).Length)
		{
			if (((byte[])P_0)[P_1++] != P_2)
			{
				throw LicenseExceptionFactory.CreateInvalidOperation();
			}
			int num = ReadAsn1Length(P_0, ref P_1);
			int num2 = P_1 + num;
			if (num < 0 || num2 < P_1 || num2 > ((Array)P_0).Length)
			{
				throw LicenseExceptionFactory.CreateInvalidOperation();
			}
			return num2;
		}
		throw LicenseExceptionFactory.CreateInvalidOperation();
	}

	private static int ReadAsn1Length(object P_0, ref int P_1)
	{
		if (P_0 != null && P_1 < ((Array)P_0).Length)
		{
			byte b = ((byte[])P_0)[P_1++];
			if ((b & 0x80) == 0)
			{
				return b;
			}
			int num = b & 0x7F;
			if (num > 0 && num <= 4 && P_1 + num <= ((Array)P_0).Length)
			{
				int num2 = 0;
				for (int i = 0; i < num; i++)
				{
					num2 = (num2 << 8) | ((byte[])P_0)[P_1++];
				}
				return num2;
			}
			throw LicenseExceptionFactory.CreateInvalidOperation();
		}
		throw LicenseExceptionFactory.CreateInvalidOperation();
	}

	private static byte[] ReadAsn1ElementBytes(object P_0, ref int P_1, byte P_2)
	{
		int num = ReadAsn1ElementEnd(P_0, ref P_1, P_2);
		int num2 = P_1;
		int num3 = num - num2;
		byte[] array = new byte[num3];
		Buffer.BlockCopy((Array)P_0, num2, array, 0, num3);
		P_1 = num;
		return array;
	}

	private static byte[] ReadAsn1BitString(object P_0, ref int P_1)
	{
		int num = ReadAsn1ElementEnd(P_0, ref P_1, 3);
		if (P_1 >= num)
		{
			throw LicenseExceptionFactory.CreateInvalidOperation();
		}
		if (((byte[])P_0)[P_1++] != 0)
		{
			throw LicenseExceptionFactory.CreateInvalidOperation();
		}
		int num2 = num - P_1;
		byte[] array = new byte[num2];
		Buffer.BlockCopy((Array)P_0, P_1, array, 0, num2);
		P_1 = num;
		return array;
	}

	private static byte[] TrimLeadingZeroBytes(object P_0)
	{
		if (P_0 != null && ((Array)P_0).Length != 0)
		{
			int i;
			for (i = 0; i < ((Array)P_0).Length - 1 && ((byte[])P_0)[i] == 0; i++)
			{
			}
			if (i != 0)
			{
				byte[] array = new byte[((Array)P_0).Length - i];
				Buffer.BlockCopy((Array)P_0, i, array, 0, array.Length);
				return array;
			}
			return (byte[])P_0;
		}
		throw LicenseExceptionFactory.CreateInvalidOperation();
	}

	private static bool ByteArraysEqual(object P_0, object P_1)
	{
		if (P_0 == P_1)
		{
			return true;
		}
		if (P_0 != null && P_1 != null && ((Array)P_0).Length == ((Array)P_1).Length)
		{
			for (int i = 0; i < ((Array)P_0).Length; i++)
			{
				if (((byte[])P_0)[i] != ((byte[])P_1)[i])
				{
					return false;
				}
			}
			return true;
		}
		return false;
	}

	private static byte[] DecodePemBlock(object P_0, object P_1)
	{
		string text = "-----BEGIN " + (string)P_1 + "-----";
		string value = "-----END " + (string)P_1 + "-----";
		int num = ((string)P_0).IndexOf(text, StringComparison.Ordinal);
		int num2 = ((string)P_0).IndexOf(value, StringComparison.Ordinal);
		if (num < 0 || num2 <= num)
		{
			throw LicenseExceptionFactory.CreateInvalidOperation();
		}
		return Convert.FromBase64String(((string)P_0).Substring(num + text.Length, num2 - num - text.Length).Replace("\r", string.Empty).Replace("\n", string.Empty)
			.Trim());
	}

	private static byte[] GetEnvelopeSigningBytes(object P_0)
	{
		string[] obj = new string[17]
		{
			$"schema:{((PsdReaderSignedEnvelopeDocument)P_0)?.Schema ?? 0}\n",
			"kind:",
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null
		};
		object obj2;
		if (P_0 != null)
		{
			obj2 = ((PsdReaderSignedEnvelopeDocument)P_0).Kind;
			if (obj2 != null)
			{
				goto IL_0044;
			}
		}
		else
		{
			obj2 = null;
		}
		obj2 = string.Empty;
		goto IL_0044;
		IL_0064:
		object obj3;
		obj[4] = (string)obj3;
		obj[5] = "\nalg:";
		object obj4;
		if (P_0 == null)
		{
			obj4 = null;
		}
		else
		{
			obj4 = ((PsdReaderSignedEnvelopeDocument)P_0).Alg;
			if (obj4 != null)
			{
				goto IL_0084;
			}
		}
		obj4 = string.Empty;
		goto IL_0084;
		IL_0084:
		obj[6] = (string)obj4;
		obj[7] = "\nenc:";
		object obj5;
		if (P_0 == null)
		{
			obj5 = null;
		}
		else
		{
			obj5 = ((PsdReaderSignedEnvelopeDocument)P_0).Enc;
			if (obj5 != null)
			{
				goto IL_00a4;
			}
		}
		obj5 = string.Empty;
		goto IL_00a4;
		IL_00c6:
		object obj6;
		obj[10] = (string)obj6;
		obj[11] = "\nscope:";
		object obj7;
		if (P_0 != null)
		{
			obj7 = ((PsdReaderSignedEnvelopeDocument)P_0).Scope;
			if (obj7 != null)
			{
				goto IL_00e8;
			}
		}
		else
		{
			obj7 = null;
		}
		obj7 = string.Empty;
		goto IL_00e8;
		IL_012c:
		object obj8;
		obj[16] = (string)obj8;
		string s = string.Concat(obj);
		return Encoding.UTF8.GetBytes(s);
		IL_00a4:
		obj[8] = (string)obj5;
		obj[9] = "\nkdf:";
		if (P_0 != null)
		{
			obj6 = ((PsdReaderSignedEnvelopeDocument)P_0).Kdf;
			if (obj6 != null)
			{
				goto IL_00c6;
			}
		}
		else
		{
			obj6 = null;
		}
		obj6 = string.Empty;
		goto IL_00c6;
		IL_00e8:
		obj[12] = (string)obj7;
		obj[13] = "\niv:";
		object obj9;
		if (P_0 == null)
		{
			obj9 = null;
		}
		else
		{
			obj9 = ((PsdReaderSignedEnvelopeDocument)P_0).Iv;
			if (obj9 != null)
			{
				goto IL_010a;
			}
		}
		obj9 = string.Empty;
		goto IL_010a;
		IL_0044:
		obj[2] = (string)obj2;
		obj[3] = "\nkid:";
		if (P_0 == null)
		{
			obj3 = null;
		}
		else
		{
			obj3 = ((PsdReaderSignedEnvelopeDocument)P_0).Kid;
			if (obj3 != null)
			{
				goto IL_0064;
			}
		}
		obj3 = string.Empty;
		goto IL_0064;
		IL_010a:
		obj[14] = (string)obj9;
		obj[15] = "\npayload:";
		if (P_0 == null)
		{
			obj8 = null;
		}
		else
		{
			obj8 = ((PsdReaderSignedEnvelopeDocument)P_0).Payload;
			if (obj8 != null)
			{
				goto IL_012c;
			}
		}
		obj8 = string.Empty;
		goto IL_012c;
	}

	private static byte[] EncryptAesCbc(object P_0, object P_1, out byte[] P_2)
	{
		using Aes aes = Aes.Create();
		aes.KeySize = 256;
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.PKCS7;
		aes.Key = NormalizeAes256Key(P_1);
		aes.GenerateIV();
		P_2 = aes.IV;
		using ICryptoTransform cryptoTransform = aes.CreateEncryptor();
		return cryptoTransform.TransformFinalBlock((byte[])(P_0 ?? Array.Empty<byte>()), 0, (P_0 != null) ? ((Array)P_0).Length : 0);
	}

	private static byte[] DecryptAesCbc(object P_0, object P_1, object P_2)
	{
		using Aes aes = Aes.Create();
		aes.KeySize = 256;
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.PKCS7;
		aes.Key = NormalizeAes256Key(P_1);
		aes.IV = (byte[])(P_2 ?? Array.Empty<byte>());
		using ICryptoTransform cryptoTransform = aes.CreateDecryptor();
		return cryptoTransform.TransformFinalBlock((byte[])(P_0 ?? Array.Empty<byte>()), 0, (P_0 != null) ? ((Array)P_0).Length : 0);
	}

	private static byte[] DeriveBinaryEnvelopeKey(object P_0, object P_1)
	{
		return ComputeStringHmacSha256(P_0 ?? Array.Empty<byte>(), "EFunStudio|BinaryEnvelope|v1|" + NormalizeScopeIdentifier(P_1));
	}

	private static string ReadLengthPrefixedUtf8(object P_0)
	{
		int num = ((BinaryReader)P_0).ReadInt32();
		if (num >= 0 && num <= 16777216)
		{
			byte[] array = ((BinaryReader)P_0).ReadBytes(num);
			if (array.Length != num)
			{
				throw LicenseExceptionFactory.CreateEndOfStream();
			}
			return Encoding.UTF8.GetString(array);
		}
		throw LicenseExceptionFactory.CreateInvalidOperation();
	}

	private static byte[] ReadBoundedByteArray(object P_0, int P_1)
	{
		int num = ((BinaryReader)P_0).ReadInt32();
		if (num >= 0 && num <= Mathf.Max(1, P_1))
		{
			byte[] array = ((BinaryReader)P_0).ReadBytes(num);
			if (array.Length != num)
			{
				throw LicenseExceptionFactory.CreateEndOfStream();
			}
			return array;
		}
		throw LicenseExceptionFactory.CreateInvalidOperation();
	}

	private static void WriteLengthPrefixedUtf8(object P_0, object P_1)
	{
		byte[] bytes = Encoding.UTF8.GetBytes((string)(P_1 ?? string.Empty));
		((BinaryWriter)P_0).Write(bytes.Length);
		((BinaryWriter)P_0).Write(bytes);
	}

	private static void WriteLengthPrefixedByteArray(object P_0, object P_1)
	{
		byte[] array = (byte[])(P_1 ?? Array.Empty<byte>());
		((BinaryWriter)P_0).Write(array.Length);
		((BinaryWriter)P_0).Write(array);
	}

	private static byte[] DeriveLocalCacheKey(object P_0)
	{
		return ComputeStringHmacSha256(GetEmbeddedKeyMaterial(), "EFunStudio|CacheLocal|v3|psd2ugui|" + NormalizeHexString(P_0));
	}

	private static byte[] DeriveCacheEncryptionKey(object P_0)
	{
		return ComputeStringHmacSha256(GetEmbeddedKeyMaterial(), "EFunStudio|CacheStore|v1|psd2ugui|enc|" + NormalizeHexString(P_0));
	}

	private static byte[] DeriveCacheAuthenticationKey(object P_0)
	{
		return ComputeStringHmacSha256(GetEmbeddedKeyMaterial(), "EFunStudio|CacheStore|v1|psd2ugui|mac|" + NormalizeHexString(P_0));
	}

	private static byte[] DeriveProjectLicenseEncryptionKey()
	{
		return ComputeStringHmacSha256(GetEmbeddedKeyMaterial(), "EFunStudio|ProjectLicense|" + GetProtocolVersion() + "|psd2ugui|enc");
	}

	private static byte[] DeriveProjectLicenseAuthenticationKey()
	{
		return ComputeStringHmacSha256(GetEmbeddedKeyMaterial(), "EFunStudio|ProjectLicense|" + GetProtocolVersion() + "|psd2ugui|mac");
	}

	private static byte[] DeriveProjectLicenseSealKey()
	{
		return ComputeStringHmacSha256(GetEmbeddedKeyMaterial(), "EFunStudio|ProjectLicense|" + GetProtocolVersion() + "|psd2ugui|seal");
	}

	private static byte[] DeriveProjectLicenseWrapEncryptionKey(object P_0, long P_1)
	{
		return ComputeStringHmacSha256(GetEmbeddedKeyMaterial(), string.Format("EFunStudio|ProjectLicense|{0}|{1}|wrap|enc|{2}|{3}", GetProtocolVersion(), "psd2ugui", NormalizeHexString(P_0), P_1));
	}

	private static byte[] DeriveProjectLicenseWrapAuthenticationKey(object P_0, long P_1)
	{
		return ComputeStringHmacSha256(GetEmbeddedKeyMaterial(), string.Format("EFunStudio|ProjectLicense|{0}|{1}|wrap|mac|{2}|{3}", GetProtocolVersion(), "psd2ugui", NormalizeHexString(P_0), P_1));
	}

	private static byte[] DeriveClockWatermarkEncryptionKey(object P_0)
	{
		return ComputeStringHmacSha256(GetEmbeddedKeyMaterial(), "EFunStudio|ClockWatermark|" + GetProtocolVersion() + "|psd2ugui|enc|" + NormalizeHexString(P_0));
	}

	private static byte[] DeriveClockWatermarkAuthenticationKey(object P_0)
	{
		return ComputeStringHmacSha256(GetEmbeddedKeyMaterial(), "EFunStudio|ClockWatermark|" + GetProtocolVersion() + "|psd2ugui|mac|" + NormalizeHexString(P_0));
	}

	private static int HexDigitToValue(char P_0)
	{
		if (P_0 >= '0' && P_0 <= '9')
		{
			return P_0 - 48;
		}
		if (P_0 >= 'a' && P_0 <= 'f')
		{
			return P_0 - 97 + 10;
		}
		return -1;
	}

	private static byte[] NormalizeAes256Key(object P_0)
	{
		byte[] array = new byte[32];
		if (P_0 != null && ((Array)P_0).Length != 0)
		{
			if (((Array)P_0).Length < array.Length)
			{
				for (int i = 0; i < array.Length; i += ((Array)P_0).Length)
				{
					int count = Math.Min(((Array)P_0).Length, array.Length - i);
					Buffer.BlockCopy((Array)P_0, 0, array, i, count);
				}
				return array;
			}
			Buffer.BlockCopy((Array)P_0, 0, array, 0, array.Length);
			return array;
		}
		return array;
	}

	private static string NormalizeScopeIdentifier(object P_0)
	{
		if (string.IsNullOrWhiteSpace((string)P_0))
		{
			return "default";
		}
		StringBuilder stringBuilder = new StringBuilder(((string)P_0).Length);
		for (int i = 0; i < ((string)P_0).Length; i++)
		{
			char c = char.ToLowerInvariant(((string)P_0)[i]);
			if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == ':' || c == '/' || c == '.')
			{
				stringBuilder.Append(c);
			}
		}
		if (stringBuilder.Length != 0)
		{
			return stringBuilder.ToString();
		}
		return "default";
	}

	private static DateTime ToUtc(DateTime P_0)
	{
		if (P_0.Kind != DateTimeKind.Utc)
		{
			return P_0.ToUniversalTime();
		}
		return P_0;
	}

	static LicenseCryptography()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_rsaEncryptionObjectIdentifier = new byte[9] { 42, 134, 72, 134, 247, 13, 1, 1, 1 };
		_embeddedKeyWords = new uint[8] { 1775658578u, 2358843861u, 1718708087u, 2657958266u, 2848839017u, 3219125356u, 2915019234u, 4221418734u };
		_embeddedKeyMasks = new uint[8] { 1945739969u, 1544636587u, 2195160652u, 2443463463u, 786195971u, 4213299385u, 277204594u, 1740235791u };
		_assemblyFingerprint = string.Empty;
	}
}
}
