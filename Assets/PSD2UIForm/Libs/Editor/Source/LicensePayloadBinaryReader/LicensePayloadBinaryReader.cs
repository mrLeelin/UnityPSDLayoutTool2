using System;
using System.IO;
using System.Text;
using PsdLicensing;
using cn.efunstudio.psdreader;

namespace PsdLicensing
{

internal static class LicensePayloadBinaryReader
{
	internal static bool TryReadPayloadDocument<GotwRXmanTMqKaOr12C>(object P_0, out GotwRXmanTMqKaOr12C P_1) where GotwRXmanTMqKaOr12C : class
	{
		P_1 = null;
		if (P_0 != null && ((Array)P_0).Length != 0)
		{
			try
			{
				object obj = ((typeof(GotwRXmanTMqKaOr12C) == typeof(PsdReaderProductMetaPayloadDocument)) ? ReadProductMetadata(P_0) : ((typeof(GotwRXmanTMqKaOr12C) == typeof(PsdReaderProductUpdatePayloadDocument)) ? ReadProductUpdate(P_0) : ((typeof(GotwRXmanTMqKaOr12C) == typeof(PsdReaderLicensePayloadDocument)) ? ReadLicensePayload(P_0) : ((typeof(GotwRXmanTMqKaOr12C) == typeof(PsdReaderDeviceShardPayloadDocument)) ? ((object)ReadDeviceShard(P_0)) : ((object)((typeof(GotwRXmanTMqKaOr12C) == typeof(PsdReaderOfflineLeasePayloadDocument)) ? ReadOfflineLease(P_0) : null))))));
				P_1 = obj as GotwRXmanTMqKaOr12C;
				return P_1 != null;
			}
			catch (Exception)
			{
				P_1 = null;
				return false;
			}
		}
		return false;
	}

	internal static string GetLicenseStatusText(byte P_0)
	{
		return P_0 switch
		{
			0 => "Active", 
			1 => "Suspended", 
			2 => "Revoked", 
			_ => "Unknown", 
		};
	}

	private static PsdReaderProductMetaPayloadDocument ReadProductMetadata(object P_0)
	{
		using MemoryStream memoryStream = new MemoryStream((byte[])P_0, writable: false);
		using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
		int num = binaryReader.ReadInt32();
		if (num != 1)
		{
			throw LicenseExceptionFactory.CreateInvalidData();
		}
		PsdReaderProductMetaPayloadDocument obj = new PsdReaderProductMetaPayloadDocument
		{
			Schema = num,
			VendorCode = ReadUtf8String(binaryReader),
			ProductCode = ReadUtf8String(binaryReader),
			DisplayName = ReadUtf8String(binaryReader),
			CurrentMajorVersion = binaryReader.ReadInt32(),
			FeatureCatalogMask = binaryReader.ReadUInt32(),
			SoftMaxMachines = binaryReader.ReadInt32(),
			Revision = binaryReader.ReadInt32(),
			ActiveKid = ReadUtf8String(binaryReader),
			ActiveLeafPublicKeyPem = ReadUtf8String(binaryReader),
			PublishedUtcTicks = binaryReader.ReadInt64(),
			RepositoryBaseUrls = ReadStringArray(binaryReader),
			TelemetryUrl = ReadUtf8String(binaryReader),
			TelemetrySiteId = ReadUtf8String(binaryReader),
			StateSalt = ReadByteArray(binaryReader)
		};
		obj.Features = LicenseFeatureMask.DecodeFeatures(obj.FeatureCatalogMask);
		obj.PublishedAtUtc = FormatUtcTicks(obj.PublishedUtcTicks);
		EnsureEndOfStream(memoryStream);
		return obj;
	}

	private static PsdReaderLicensePayloadDocument ReadLicensePayload(object P_0)
	{
		using MemoryStream memoryStream = new MemoryStream((byte[])P_0, writable: false);
		using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
		PsdReaderLicensePayloadDocument obj = new PsdReaderLicensePayloadDocument
		{
			Schema = binaryReader.ReadInt32(),
			Revision = binaryReader.ReadInt32(),
			LookupId = ReadUtf8String(binaryReader),
			LicenseId = ReadUtf8String(binaryReader),
			VendorCode = ReadUtf8String(binaryReader),
			ProductCode = ReadUtf8String(binaryReader),
			StatusCode = binaryReader.ReadByte(),
			FeatureMask = binaryReader.ReadUInt32(),
			IssuedUtcTicks = binaryReader.ReadInt64(),
			SupportUntilUtcTicks = binaryReader.ReadInt64(),
			MaxMajorVersion = binaryReader.ReadInt32(),
			GrantSeed = ReadByteArray(binaryReader)
		};
		obj.Features = LicenseFeatureMask.DecodeFeatures(obj.FeatureMask);
		obj.Status = GetLicenseStatusText(obj.StatusCode);
		obj.IssuedAtUtc = FormatUtcTicks(obj.IssuedUtcTicks);
		obj.SupportUntilUtc = FormatUtcTicks(obj.SupportUntilUtcTicks);
		EnsureEndOfStream(memoryStream);
		return obj;
	}

	private static PsdReaderProductUpdatePayloadDocument ReadProductUpdate(object P_0)
	{
		using MemoryStream memoryStream = new MemoryStream((byte[])P_0, writable: false);
		using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
		int num = binaryReader.ReadInt32();
		if (num != 1)
		{
			throw LicenseExceptionFactory.CreateInvalidData();
		}
		PsdReaderProductUpdatePayloadDocument obj = new PsdReaderProductUpdatePayloadDocument
		{
			Schema = num,
			VendorCode = ReadUtf8String(binaryReader),
			ProductCode = ReadUtf8String(binaryReader),
			Revision = binaryReader.ReadInt32(),
			Version = ReadUtf8String(binaryReader),
			DownloadUrl = ReadUtf8String(binaryReader),
			ReleaseNotes = ReadUtf8String(binaryReader),
			PublishedUtcTicks = binaryReader.ReadInt64()
		};
		obj.PublishedAtUtc = FormatUtcTicks(obj.PublishedUtcTicks);
		EnsureEndOfStream(memoryStream);
		return obj;
	}

	private static PsdReaderDeviceShardPayloadDocument ReadDeviceShard(object P_0)
	{
		using MemoryStream memoryStream = new MemoryStream((byte[])P_0, writable: false);
		using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
		PsdReaderDeviceShardPayloadDocument psdReaderDeviceShardPayloadDocument = new PsdReaderDeviceShardPayloadDocument
		{
			Schema = binaryReader.ReadInt32(),
			Kind = ReadUtf8String(binaryReader),
			ProductCode = ReadUtf8String(binaryReader),
			Prefix = ReadUtf8String(binaryReader),
			Revision = binaryReader.ReadInt32(),
			IssuedUtcTicks = binaryReader.ReadInt64()
		};
		int num = binaryReader.ReadInt32();
		if (num >= 0 && num <= 1000000)
		{
			psdReaderDeviceShardPayloadDocument.BlockedTargets = new string[num];
			for (int i = 0; i < num; i++)
			{
				psdReaderDeviceShardPayloadDocument.BlockedTargets[i] = ReadUtf8String(binaryReader);
			}
			EnsureEndOfStream(memoryStream);
			return psdReaderDeviceShardPayloadDocument;
		}
		throw LicenseExceptionFactory.CreateInvalidData();
	}

	private static PsdReaderOfflineLeasePayloadDocument ReadOfflineLease(object P_0)
	{
		using MemoryStream memoryStream = new MemoryStream((byte[])P_0, writable: false);
		using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
		PsdReaderOfflineLeasePayloadDocument obj = new PsdReaderOfflineLeasePayloadDocument
		{
			Schema = binaryReader.ReadInt32(),
			Kind = ReadUtf8String(binaryReader),
			Revision = binaryReader.ReadInt32(),
			LookupId = ReadUtf8String(binaryReader),
			LicenseId = ReadUtf8String(binaryReader),
			VendorCode = ReadUtf8String(binaryReader),
			ProductCode = ReadUtf8String(binaryReader),
			StatusCode = binaryReader.ReadByte(),
			FeatureMask = binaryReader.ReadUInt32(),
			SupportUntilUtcTicks = binaryReader.ReadInt64(),
			OfflineCacheTimeoutDays = binaryReader.ReadInt32(),
			IssuedUtcTicks = binaryReader.ReadInt64()
		};
		obj.Features = LicenseFeatureMask.DecodeFeatures(obj.FeatureMask);
		obj.Status = GetLicenseStatusText(obj.StatusCode);
		obj.SupportUntilUtc = FormatUtcTicks(obj.SupportUntilUtcTicks);
		obj.IssuedAtUtc = FormatUtcTicks(obj.IssuedUtcTicks);
		EnsureEndOfStream(memoryStream);
		return obj;
	}

	private static string FormatUtcTicks(long P_0)
	{
		if (P_0 <= 0L)
		{
			return string.Empty;
		}
		return new DateTime(P_0, DateTimeKind.Utc).ToString("O");
	}

	private static void EnsureEndOfStream(object P_0)
	{
		if (((Stream)P_0).Position != ((Stream)P_0).Length)
		{
			throw LicenseExceptionFactory.CreateInvalidData();
		}
	}

	private static string ReadUtf8String(object P_0)
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
		throw LicenseExceptionFactory.CreateInvalidData();
	}

	private static string[] ReadStringArray(object P_0)
	{
		int num = ((BinaryReader)P_0).ReadInt32();
		if (num >= 0 && num <= 1024)
		{
			string[] array = new string[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = ReadUtf8String(P_0);
			}
			return array;
		}
		throw LicenseExceptionFactory.CreateInvalidData();
	}

	private static byte[] ReadByteArray(object P_0)
	{
		int num = ((BinaryReader)P_0).ReadInt32();
		if (num >= 0 && num <= 67108864)
		{
			byte[] array = ((BinaryReader)P_0).ReadBytes(num);
			if (array.Length != num)
			{
				throw LicenseExceptionFactory.CreateEndOfStream();
			}
			return array;
		}
		throw LicenseExceptionFactory.CreateInvalidData();
	}
}
}
