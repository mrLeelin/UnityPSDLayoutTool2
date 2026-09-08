using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using PsdDocumentResolution;
using PsdProtectionGuards;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdBinaryUtilities
{

internal class PsdBigEndianReader : IDisposable
{
	private class PsdStreamBinaryReader : BinaryReader
	{
		public PsdStreamBinaryReader(Stream P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private PsdStreamBinaryReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, Stream P_0)
			: base(P_0)
		{
		}
	}

	private readonly BinaryReader _binaryReader;

	private readonly PsdDocumentResolver _documentResolver;

	private readonly Stream _baseStream;

	private readonly Uri _documentUri;

	private int _version;

	public long Position
	{
		get
		{
			return _binaryReader.BaseStream.Position;
		}
		set
		{
			_binaryReader.BaseStream.Position = value;
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
				throw new PsdInvalidDataException();
			}
			_version = value;
		}
	}

	public PsdBigEndianReader(Stream P_0, PsdDocumentResolver P_1, Uri P_2)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_version = 1;
		_baseStream = P_0;
		_binaryReader = new PsdStreamBinaryReader(P_0);
		_documentResolver = P_1;
		_documentUri = P_2;
	}

	public void Dispose()
	{
		_binaryReader.Close();
	}

	public string ReadFourCharacterCode()
	{
		return ReadAsciiString(4);
	}

	public string ReadAsciiString(int byteCount)
	{
		return Encoding.ASCII.GetString(_binaryReader.ReadBytes(byteCount));
	}

	public bool ReadPsdBlockSignature()
	{
		return ReadPsdOrPsbBlockSignature(false);
	}

	public bool ReadPsdOrPsbBlockSignature(bool allowPsbSignature)
	{
		string text = ReadFourCharacterCode();
		if (text == "8BIM")
		{
			return true;
		}
		if (allowPsbSignature && text == "8B64")
		{
			return true;
		}
		return false;
	}

	public void ExpectFourCharacterCode(string expectedSignature)
	{
		if (ReadFourCharacterCode() != expectedSignature)
		{
			throw new PsdInvalidDataException();
		}
	}

	public void ExpectPsdBlockSignature()
	{
		ExpectPsdOrPsbBlockSignature(false);
	}

	public void ExpectPsdOrPsbBlockSignature(bool allowPsbSignature)
	{
		if (!ReadPsdOrPsbBlockSignature(allowPsbSignature))
		{
			throw new PsdInvalidDataException();
		}
	}

	public void ExpectPsdFileSignature()
	{
		if (ReadFourCharacterCode() != "8BPS")
		{
			throw new PsdInvalidDataException();
		}
	}

	private void ExpectValue<OfbQqxabDVZq47GKwyF>(OfbQqxabDVZq47GKwyF TbRZggaw3Ei6sv9qw7D, string P_1, Func<OfbQqxabDVZq47GKwyF> P_2)
	{
		OfbQqxabDVZq47GKwyF val = P_2();
		if (!object.Equals(TbRZggaw3Ei6sv9qw7D, val))
		{
			throw new PsdInvalidDataException("{0}의 값이 {1}이 아닙니다.", P_1, TbRZggaw3Ei6sv9qw7D);
		}
	}

	public void ExpectInt16(short expectedValue, string fieldName)
	{
		ExpectValue(expectedValue, fieldName, () => ReadInt16());
	}

	public void ExpectInt32(int expectedValue, string fieldName)
	{
		ExpectValue(expectedValue, fieldName, () => ReadInt32());
	}

	public void ExpectNamedFourCharacterCode(string expectedSignature, string fieldName)
	{
		ExpectValue(expectedSignature, fieldName, () => ReadFourCharacterCode());
	}

	public string ReadPaddedPascalString(int paddingMultiple)
	{
		byte b = _binaryReader.ReadByte();
		string empty = string.Empty;
		if (b == 0)
		{
			_binaryReader.BaseStream.Position += paddingMultiple - 1;
			return empty;
		}
		byte[] bytes = _binaryReader.ReadBytes(b);
		empty = Encoding.UTF8.GetString(bytes);
		for (int i = b + 1; i % paddingMultiple != 0; i++)
		{
			_binaryReader.BaseStream.Position++;
		}
		return empty;
	}

	public string ReadUnicodeString()
	{
		int characterCount = ReadInt32();
		if (characterCount == 0)
		{
			return string.Empty;
		}
		byte[] utf16Bytes = ReadBytes(characterCount * 2);
		for (int i = 0; i < characterCount; i++)
		{
			int characterByteOffset = i * 2;
			byte firstByte = utf16Bytes[characterByteOffset];
			utf16Bytes[characterByteOffset] = utf16Bytes[characterByteOffset + 1];
			utf16Bytes[characterByteOffset + 1] = firstByte;
		}
		if (utf16Bytes[utf16Bytes.Length - 1] == 0 && utf16Bytes[utf16Bytes.Length - 2] == 0)
		{
			characterCount--;
		}
		return Encoding.Unicode.GetString(utf16Bytes, 0, characterCount * 2);
	}

	public string ReadDescriptorKey()
	{
		int num = ReadInt32();
		num = ((num <= 0) ? 4 : num);
		return ReadAsciiString(num);
	}

	public int Read(byte[] destinationBuffer, int destinationOffset, int byteCount)
	{
		return _binaryReader.Read(destinationBuffer, destinationOffset, byteCount);
	}

	public byte ReadByte()
	{
		return _binaryReader.ReadByte();
	}

	public char ReadByteAsChar()
	{
		return (char)ReadByte();
	}

	public byte[] ReadBytes(int byteCount)
	{
		return _binaryReader.ReadBytes(byteCount);
	}

	public bool ReadBoolean()
	{
		return ReverseBooleanBytes(_binaryReader.ReadBoolean());
	}

	public double ReadDouble()
	{
		return ReverseDoubleBytes(_binaryReader.ReadDouble());
	}

	public double[] ReadDoubleArray(int elementCount)
	{
		double[] array = new double[elementCount];
		for (int i = 0; i < elementCount; i++)
		{
			array[i] = ReadDouble();
		}
		return array;
	}

	public short ReadInt16()
	{
		return ReverseInt16Bytes(_binaryReader.ReadInt16());
	}

	public int ReadInt32()
	{
		return ReverseInt32Bytes(_binaryReader.ReadInt32());
	}

	public long ReadInt64()
	{
		return ReverseInt64Bytes(_binaryReader.ReadInt64());
	}

	public ushort ReadUInt16()
	{
		return ReverseUInt16Bytes(_binaryReader.ReadUInt16());
	}

	public uint ReadUInt32()
	{
		return ReverseUInt32Bytes(_binaryReader.ReadUInt32());
	}

	public ulong ReadUInt64()
	{
		return ReverseUInt64Bytes(_binaryReader.ReadUInt64());
	}

	public long ReadVersionedLength()
	{
		if (_version != 1)
		{
			return ReadInt64();
		}
		return ReadInt32();
	}

	public void SkipBytes(int byteCount)
	{
		ReadBytes(byteCount);
	}

	public void ExpectCharacter(char expectedCharacter)
	{
		if (ReadByteAsChar() != expectedCharacter)
		{
			throw new NotSupportedException();
		}
	}

	public void ExpectRepeatedCharacter(char expectedCharacter, int repeatCount)
	{
		for (int i = 0; i < repeatCount; i++)
		{
			ExpectCharacter(expectedCharacter);
		}
	}

	public ColorMode ReadColorMode()
	{
		return (ColorMode)ReadInt16();
	}

	public BlendMode ReadBlendMode()
	{
		return PsdBinaryDataUtilities.ParseBlendModeCode(ReadAsciiString(4));
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

	public void ReadSignatureAndVersion()
	{
		ExpectPsdFileSignature();
		Version = ReadInt16();
		SkipBytes(6);
	}

	[SpecialName]
	public long GetLength()
	{
		return _binaryReader.BaseStream.Length;
	}

	[SpecialName]
	public PsdDocumentResolver GetDocumentResolver()
	{
		return _documentResolver;
	}

	[SpecialName]
	public Stream GetBaseStream()
	{
		return _baseStream;
	}

	[SpecialName]
	public Uri GetDocumentUri()
	{
		return _documentUri;
	}

	private bool ReverseBooleanBytes(bool encodedValue)
	{
		byte[] bytes = BitConverter.GetBytes(encodedValue);
		Array.Reverse<byte>(bytes);
		return BitConverter.ToBoolean(bytes, 0);
	}

	private double ReverseDoubleBytes(double encodedValue)
	{
		byte[] bytes = BitConverter.GetBytes(encodedValue);
		Array.Reverse<byte>(bytes);
		return BitConverter.ToDouble(bytes, 0);
	}

	private short ReverseInt16Bytes(short encodedValue)
	{
		byte[] bytes = BitConverter.GetBytes(encodedValue);
		Array.Reverse<byte>(bytes);
		return BitConverter.ToInt16(bytes, 0);
	}

	private int ReverseInt32Bytes(int encodedValue)
	{
		byte[] bytes = BitConverter.GetBytes(encodedValue);
		Array.Reverse<byte>(bytes);
		return BitConverter.ToInt32(bytes, 0);
	}

	private long ReverseInt64Bytes(long encodedValue)
	{
		byte[] bytes = BitConverter.GetBytes(encodedValue);
		Array.Reverse<byte>(bytes);
		return BitConverter.ToInt64(bytes, 0);
	}

	private ushort ReverseUInt16Bytes(ushort encodedValue)
	{
		byte[] bytes = BitConverter.GetBytes(encodedValue);
		Array.Reverse<byte>(bytes);
		return BitConverter.ToUInt16(bytes, 0);
	}

	private uint ReverseUInt32Bytes(uint encodedValue)
	{
		byte[] bytes = BitConverter.GetBytes(encodedValue);
		Array.Reverse<byte>(bytes);
		return BitConverter.ToUInt32(bytes, 0);
	}

	private ulong ReverseUInt64Bytes(ulong encodedValue)
	{
		byte[] bytes = BitConverter.GetBytes(encodedValue);
		Array.Reverse<byte>(bytes);
		return BitConverter.ToUInt64(bytes, 0);
	}

	[CompilerGenerated]
	private short ReadExpectedInt16Value()
	{
		return ReadInt16();
	}

	[CompilerGenerated]
	private int ReadExpectedInt32Value()
	{
		return ReadInt32();
	}

	[CompilerGenerated]
	private string ReadExpectedSignatureValue()
	{
		return ReadFourCharacterCode();
	}
}
}
