using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using InvalidPsdFileExceptionNamespace;
using PsdBinaryUtilityNamespace;
using cn.efunstudio.psdreader.PsdParser;
using LinkedDocumentResolverNamespace;

namespace PsdBinaryReaderNamespace
{
    internal class PsdBinaryReader : IDisposable
    {
        private class RawBinaryReader : BinaryReader
        {
            internal static RawBinaryReader s_ObfuscationSentinel;

            public RawBinaryReader(Stream stream)
                : base(stream)
            {
            }

            internal static bool IsObfuscationSentinelNull()
            {
                return s_ObfuscationSentinel == null;
            }

            internal static RawBinaryReader GetObfuscationSentinel()
            {
                return s_ObfuscationSentinel;
            }
        }

        private readonly BinaryReader _reader;

        private readonly LinkedDocumentResolver _linkedDocumentResolver;

        private readonly Stream _stream;

        private readonly Uri _baseUri;

        private int _version = 1;

        internal static PsdBinaryReader s_ObfuscationSentinel;

        public long Position
        {
            get
            {
                return _reader.BaseStream.Position;
            }
            set
            {
                _reader.BaseStream.Position = value;
            }
        }

        public int Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 1 && value != 2)
                {
                    throw new InvalidPsdFileException();
                }
                _version = value;
            }
        }

        public PsdBinaryReader(Stream stream, LinkedDocumentResolver linkedDocumentResolver, Uri uri)
        {
            _stream = stream;
            _reader = new RawBinaryReader(stream);
            _linkedDocumentResolver = linkedDocumentResolver;
            _baseUri = uri;
        }

        public void Dispose()
        {
            _reader.Close();
        }

        public string ReadSignature()
        {
            return ReadAsciiString(4);
        }

        public string ReadAsciiString(int value)
        {
            return Encoding.ASCII.GetString(_reader.ReadBytes(value));
        }

        public bool ReadImageResourceSignature()
        {
            return ReadImageResourceSignature(false);
        }

        public bool ReadImageResourceSignature(bool enabled)
        {
            string text = ReadSignature();
            if (!(text == "8BIM"))
            {
                if (enabled && text == "8B64")
                {
                    return true;
                }
                return false;
            }
            return true;
        }

        public void ValidateSignature(string id)
        {
            if (ReadSignature() != id)
            {
                throw new InvalidPsdFileException();
            }
        }

        public void ValidateImageResourceSignature()
        {
            ValidateImageResourceSignature(false);
        }

        public void ValidateImageResourceSignature(bool enabled)
        {
            if (!ReadImageResourceSignature(enabled))
            {
                throw new InvalidPsdFileException();
            }
        }

        public void ValidatePsdSignature()
        {
            if (ReadSignature() != "8BPS")
            {
                throw new InvalidPsdFileException();
            }
        }

        private void ValidateExpectedValue<TValue>(TValue tValue, string text, Func<TValue> callback)
        {
            TValue val = callback();
            if (!object.Equals(tValue, val))
            {
                throw new InvalidPsdFileException("{0}의 값이 {1}이 아닙니다.", text, tValue);
            }
        }

        public void ValidateInt16(short value, string id)
        {
            ValidateExpectedValue(value, id, () => ReadInt16());
        }

        public void ValidateInt32(int value, string id)
        {
            ValidateExpectedValue(value, id, () => ReadInt32());
        }

        public void ValidateSignature(string id, string id2)
        {
            ValidateExpectedValue(id, id2, () => ReadSignature());
        }

        public string ReadPascalString(int value)
        {
            byte b = _reader.ReadByte();
            string empty = string.Empty;
            if (b != 0)
            {
                byte[] bytes = _reader.ReadBytes(b);
                empty = Encoding.UTF8.GetString(bytes);
                for (int i = b + 1; i % value != 0; i++)
                {
                    _reader.BaseStream.Position++;
                }
                return empty;
            }
            _reader.BaseStream.Position += value - 1;
            return empty;
        }

        public string ReadUnicodeString()
        {
            int num = ReadInt32();
            if (num == 0)
            {
                return string.Empty;
            }
            byte[] array = ReadBytes(num * 2);
            for (int i = 0; i < num; i++)
            {
                int num2 = i * 2;
                byte b = array[num2];
                array[num2] = array[num2 + 1];
                array[num2 + 1] = b;
            }
            if (array[array.Length - 1] == 0 && array[array.Length - 2] == 0)
            {
                num--;
            }
            return Encoding.Unicode.GetString(array, 0, num * 2);
        }

        public string ReadDescriptorId()
        {
            int num = ReadInt32();
            num = ((num > 0) ? num : 4);
            return ReadAsciiString(num);
        }

        public int Read(byte[] bytes, int value, int value2)
        {
            return _reader.Read(bytes, value, value2);
        }

        public byte ReadByte()
        {
            return _reader.ReadByte();
        }

        public char ReadChar()
        {
            return (char)ReadByte();
        }

        public byte[] ReadBytes(int value)
        {
            return _reader.ReadBytes(value);
        }

        public bool ReadBoolean()
        {
            return ReverseEndianness(_reader.ReadBoolean());
        }

        public double ReadDouble()
        {
            return ReverseEndianness(_reader.ReadDouble());
        }

        public double[] ReadDoubles(int value)
        {
            double[] array = new double[value];
            for (int i = 0; i < value; i++)
            {
                array[i] = ReadDouble();
            }
            return array;
        }

        public short ReadInt16()
        {
            return ReverseEndianness(_reader.ReadInt16());
        }

        public int ReadInt32()
        {
            return ReverseEndianness(_reader.ReadInt32());
        }

        public long ReadInt64()
        {
            return ReverseEndianness(_reader.ReadInt64());
        }

        public ushort ReadUInt16()
        {
            return ReverseEndianness(_reader.ReadUInt16());
        }

        public uint ReadUInt32()
        {
            return ReverseEndianness(_reader.ReadUInt32());
        }

        public ulong ReadUInt64()
        {
            return ReverseEndianness(_reader.ReadUInt64());
        }

        public long ReadVersionedLength()
        {
            if (_version != 1)
            {
                return ReadInt64();
            }
            return ReadInt32();
        }

        public void SkipBytes(int value)
        {
            ReadBytes(value);
        }

        public void ValidateChar(char value)
        {
            if (ReadChar() != value)
            {
                throw new NotSupportedException();
            }
        }

        public void ValidateRepeatedChar(char value, int value2)
        {
            for (int i = 0; i < value2; i++)
            {
                ValidateChar(value);
            }
        }

        public ColorMode ReadColorMode()
        {
            return (ColorMode)ReadInt16();
        }

        public BlendMode ReadBlendMode()
        {
            return PsdBinaryUtility.ParseBlendMode(ReadAsciiString(4));
        }

        public LayerFlags ReadLayerFlags()
        {
            return (LayerFlags)ReadByte();
        }

        public ChannelType ReadChannelType()
        {
            return (ChannelType)ReadInt16();
        }

        public CompressionType ReadCompressionType()
        {
            return (CompressionType)ReadInt16();
        }

        public void ReadFileHeaderPreamble()
        {
            ValidatePsdSignature();
            Version = ReadInt16();
            SkipBytes(6);
        }

        [SpecialName]
        public long GetStreamLength()
        {
            return _reader.BaseStream.Length;
        }

        [SpecialName]
        public LinkedDocumentResolver GetLinkedDocumentResolver()
        {
            return _linkedDocumentResolver;
        }

        [SpecialName]
        public Stream GetBaseStream()
        {
            return _stream;
        }

        [SpecialName]
        public Uri GetBaseUri()
        {
            return _baseUri;
        }

        private bool ReverseEndianness(bool enabled)
        {
            byte[] bytes = BitConverter.GetBytes(enabled);
            Array.Reverse<byte>(bytes);
            return BitConverter.ToBoolean(bytes, 0);
        }

        private double ReverseEndianness(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse<byte>(bytes);
            return BitConverter.ToDouble(bytes, 0);
        }

        private short ReverseEndianness(short value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse<byte>(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }

        private int ReverseEndianness(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse<byte>(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        private long ReverseEndianness(long value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse<byte>(bytes);
            return BitConverter.ToInt64(bytes, 0);
        }

        private ushort ReverseEndianness(ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse<byte>(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }

        private uint ReverseEndianness(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse<byte>(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        private ulong ReverseEndianness(ulong value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Array.Reverse<byte>(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }

        [CompilerGenerated]
        private short ReadInt16ForValidation()
        {
            return ReadInt16();
        }

        [CompilerGenerated]
        private int ReadInt32ForValidation()
        {
            return ReadInt32();
        }

        [CompilerGenerated]
        private string ReadSignatureForValidation()
        {
            return ReadSignature();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static PsdBinaryReader GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
