using System;
using System.IO;
using System.Text;
using LicenseExceptionFactoryNamespace;
using LicenseFeatureCatalogNamespace;
using cn.efunstudio.psdreader;

namespace LicensePayloadBinaryReaderNamespace
{
    internal sealed class LicensePayloadBinaryReader
    {
        internal static LicensePayloadBinaryReader s_ObfuscationSentinel;

        internal static bool TryDeserializePayload<TValue>(object array, out TValue result) where TValue : class
        {
            result = null;
            if (array != null && ((Array)array).Length != 0)
            {
                try
                {
                    object obj = ((typeof(TValue) == typeof(PsdReaderProductMetaPayloadDocument)) ? ReadProductMetadata(array) : ((typeof(TValue) == typeof(PsdReaderProductUpdatePayloadDocument)) ? ReadProductUpdatePayload(array) : ((typeof(TValue) == typeof(PsdReaderLicensePayloadDocument)) ? ReadLicensePayload(array) : ((typeof(TValue) == typeof(PsdReaderDeviceShardPayloadDocument)) ? ((object)ReadDeviceShardPayload(array)) : ((object)((typeof(TValue) == typeof(PsdReaderOfflineLeasePayloadDocument)) ? ReadOfflineLeasePayload(array) : null))))));
                    result = obj as TValue;
                    return result != null;
                }
                catch (Exception)
                {
                    result = null;
                    return false;
                }
            }
            return false;
        }

        internal static string GetStatusText(byte statusCode)
        {
            return statusCode switch
            {
                0 => "Active", 
                1 => "Suspended", 
                2 => "Revoked", 
                _ => "Unknown", 
            };
        }

        private static PsdReaderProductMetaPayloadDocument ReadProductMetadata(object value)
        {
            using MemoryStream memoryStream = new MemoryStream((byte[])value, writable: false);
            using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
            int num = binaryReader.ReadInt32();
            if (num != 1)
            {
                throw LicenseExceptionFactory.CreateInvalidDataException();
            }
            PsdReaderProductMetaPayloadDocument obj = new PsdReaderProductMetaPayloadDocument
            {
                Schema = num,
                VendorCode = ReadString(binaryReader),
                ProductCode = ReadString(binaryReader),
                DisplayName = ReadString(binaryReader),
                CurrentMajorVersion = binaryReader.ReadInt32(),
                FeatureCatalogMask = binaryReader.ReadUInt32(),
                SoftMaxMachines = binaryReader.ReadInt32(),
                Revision = binaryReader.ReadInt32(),
                ActiveKid = ReadString(binaryReader),
                ActiveLeafPublicKeyPem = ReadString(binaryReader),
                PublishedUtcTicks = binaryReader.ReadInt64(),
                RepositoryBaseUrls = ReadStringArray(binaryReader),
                TelemetryUrl = ReadString(binaryReader),
                TelemetrySiteId = ReadString(binaryReader),
                StateSalt = ReadByteArray(binaryReader)
            };
            obj.Features = LicenseFeatureCatalog.DecodeFeatureMask(obj.FeatureCatalogMask);
            obj.PublishedAtUtc = FormatUtcTicks(obj.PublishedUtcTicks);
            EnsureEndOfStream(memoryStream);
            return obj;
        }

        private static PsdReaderLicensePayloadDocument ReadLicensePayload(object value)
        {
            using MemoryStream memoryStream = new MemoryStream((byte[])value, writable: false);
            using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
            PsdReaderLicensePayloadDocument obj = new PsdReaderLicensePayloadDocument
            {
                Schema = binaryReader.ReadInt32(),
                Revision = binaryReader.ReadInt32(),
                LookupId = ReadString(binaryReader),
                LicenseId = ReadString(binaryReader),
                VendorCode = ReadString(binaryReader),
                ProductCode = ReadString(binaryReader),
                StatusCode = binaryReader.ReadByte(),
                FeatureMask = binaryReader.ReadUInt32(),
                IssuedUtcTicks = binaryReader.ReadInt64(),
                SupportUntilUtcTicks = binaryReader.ReadInt64(),
                MaxMajorVersion = binaryReader.ReadInt32(),
                GrantSeed = ReadByteArray(binaryReader)
            };
            obj.Features = LicenseFeatureCatalog.DecodeFeatureMask(obj.FeatureMask);
            obj.Status = GetStatusText(obj.StatusCode);
            obj.IssuedAtUtc = FormatUtcTicks(obj.IssuedUtcTicks);
            obj.SupportUntilUtc = FormatUtcTicks(obj.SupportUntilUtcTicks);
            EnsureEndOfStream(memoryStream);
            return obj;
        }

        private static PsdReaderProductUpdatePayloadDocument ReadProductUpdatePayload(object value)
        {
            using MemoryStream memoryStream = new MemoryStream((byte[])value, writable: false);
            using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
            int num = binaryReader.ReadInt32();
            if (num != 1)
            {
                throw LicenseExceptionFactory.CreateInvalidDataException();
            }
            PsdReaderProductUpdatePayloadDocument obj = new PsdReaderProductUpdatePayloadDocument
            {
                Schema = num,
                VendorCode = ReadString(binaryReader),
                ProductCode = ReadString(binaryReader),
                Revision = binaryReader.ReadInt32(),
                Version = ReadString(binaryReader),
                DownloadUrl = ReadString(binaryReader),
                ReleaseNotes = ReadString(binaryReader),
                PublishedUtcTicks = binaryReader.ReadInt64()
            };
            obj.PublishedAtUtc = FormatUtcTicks(obj.PublishedUtcTicks);
            EnsureEndOfStream(memoryStream);
            return obj;
        }

        private static PsdReaderDeviceShardPayloadDocument ReadDeviceShardPayload(object value)
        {
            using MemoryStream memoryStream = new MemoryStream((byte[])value, writable: false);
            using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
            PsdReaderDeviceShardPayloadDocument psdReaderDeviceShardPayloadDocument = new PsdReaderDeviceShardPayloadDocument
            {
                Schema = binaryReader.ReadInt32(),
                Kind = ReadString(binaryReader),
                ProductCode = ReadString(binaryReader),
                Prefix = ReadString(binaryReader),
                Revision = binaryReader.ReadInt32(),
                IssuedUtcTicks = binaryReader.ReadInt64()
            };
            int num = binaryReader.ReadInt32();
            if (num >= 0 && num <= 1000000)
            {
                psdReaderDeviceShardPayloadDocument.BlockedTargets = new string[num];
                for (int i = 0; i < num; i++)
                {
                    psdReaderDeviceShardPayloadDocument.BlockedTargets[i] = ReadString(binaryReader);
                }
                EnsureEndOfStream(memoryStream);
                return psdReaderDeviceShardPayloadDocument;
            }
            throw LicenseExceptionFactory.CreateInvalidDataException();
        }

        private static PsdReaderOfflineLeasePayloadDocument ReadOfflineLeasePayload(object value)
        {
            using MemoryStream memoryStream = new MemoryStream((byte[])value, writable: false);
            using BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8, leaveOpen: false);
            PsdReaderOfflineLeasePayloadDocument obj = new PsdReaderOfflineLeasePayloadDocument
            {
                Schema = binaryReader.ReadInt32(),
                Kind = ReadString(binaryReader),
                Revision = binaryReader.ReadInt32(),
                LookupId = ReadString(binaryReader),
                LicenseId = ReadString(binaryReader),
                VendorCode = ReadString(binaryReader),
                ProductCode = ReadString(binaryReader),
                StatusCode = binaryReader.ReadByte(),
                FeatureMask = binaryReader.ReadUInt32(),
                SupportUntilUtcTicks = binaryReader.ReadInt64(),
                OfflineCacheTimeoutDays = binaryReader.ReadInt32(),
                IssuedUtcTicks = binaryReader.ReadInt64()
            };
            obj.Features = LicenseFeatureCatalog.DecodeFeatureMask(obj.FeatureMask);
            obj.Status = GetStatusText(obj.StatusCode);
            obj.SupportUntilUtc = FormatUtcTicks(obj.SupportUntilUtcTicks);
            obj.IssuedAtUtc = FormatUtcTicks(obj.IssuedUtcTicks);
            EnsureEndOfStream(memoryStream);
            return obj;
        }

        private static string FormatUtcTicks(long value)
        {
            if (value > 0L)
            {
                return new DateTime(value, DateTimeKind.Utc).ToString("O");
            }
            return string.Empty;
        }

        private static void EnsureEndOfStream(object value)
        {
            if (((Stream)value).Position != ((Stream)value).Length)
            {
                throw LicenseExceptionFactory.CreateInvalidDataException();
            }
        }

        private static string ReadString(object value)
        {
            int num = ((BinaryReader)value).ReadInt32();
            if (num >= 0 && num <= 16777216)
            {
                byte[] array = ((BinaryReader)value).ReadBytes(num);
                if (array.Length != num)
                {
                    throw LicenseExceptionFactory.CreateEndOfStreamException();
                }
                return Encoding.UTF8.GetString(array);
            }
            throw LicenseExceptionFactory.CreateInvalidDataException();
        }

        private static string[] ReadStringArray(object value)
        {
            int num = ((BinaryReader)value).ReadInt32();
            if (num >= 0 && num <= 1024)
            {
                string[] array = new string[num];
                for (int i = 0; i < num; i++)
                {
                    array[i] = ReadString(value);
                }
                return array;
            }
            throw LicenseExceptionFactory.CreateInvalidDataException();
        }

        private static byte[] ReadByteArray(object value)
        {
            int num = ((BinaryReader)value).ReadInt32();
            if (num < 0 || num > 67108864)
            {
                throw LicenseExceptionFactory.CreateInvalidDataException();
            }
            byte[] array = ((BinaryReader)value).ReadBytes(num);
            if (array.Length != num)
            {
                throw LicenseExceptionFactory.CreateEndOfStreamException();
            }
            return array;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LicensePayloadBinaryReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
