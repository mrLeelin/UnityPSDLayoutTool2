using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PsdProtectionGuards;
using PsdVirtualMachine;

namespace PsdProtectionRuntime
{

internal class AssemblyProtectionRuntime
{
	private delegate void NRI4Sbq6XDJ2Ce4sVsn(object o);

	internal class ProtectionMarkerAttribute : Attribute
	{
		internal class ProtectionMarkerSentinel<TMarker>
		{
			internal static object MarkerControlFlowSentinel;

			public ProtectionMarkerSentinel()
			{
				ThrowIfDebuggerAttached();
				ReactorTrialGuard.CheckTrialPeriodOnce();
			}

			internal static bool IsMarkerSentinelNull()
			{
				return MarkerControlFlowSentinel == null;
			}

			internal static object GetMarkerSentinel()
			{
				return MarkerControlFlowSentinel;
			}
		}

		public ProtectionMarkerAttribute(object P_0)
		{
		}
	}

	internal class FixedKeyUtf16Encryptor
	{
		internal static string EncryptUtf16ToBase64(object plaintext, object initializationVectorSeed)
		{
			byte[] plaintextBytes = Encoding.Unicode.GetBytes((string)plaintext);
			byte[] key = new byte[32]
			{
				82, 102, 104, 110, 32, 77, 24, 34, 118, 181,
				51, 17, 18, 51, 12, 109, 10, 32, 77, 24,
				34, 158, 161, 41, 97, 28, 118, 181, 5, 25,
				1, 88
			};
			byte[] initializationVector = ComputeMd5Hash(Encoding.Unicode.GetBytes((string)initializationVectorSeed));
			MemoryStream output = new MemoryStream();
			SymmetricAlgorithm algorithm = CreateAesCompatibleAlgorithm();
			algorithm.Key = key;
			algorithm.IV = initializationVector;
			CryptoStream cryptoStream = new CryptoStream(output, algorithm.CreateEncryptor(), CryptoStreamMode.Write);
			cryptoStream.Write(plaintextBytes, 0, plaintextBytes.Length);
			cryptoStream.Close();
			return Convert.ToBase64String(output.ToArray());
		}
	}

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	internal delegate uint JitCompileMethodDelegate(IntPtr classthis, IntPtr comp, IntPtr info, [MarshalAs(UnmanagedType.U4)] uint flags, IntPtr nativeEntry, ref uint nativeSizeOfCode);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate IntPtr bRqxs2qxnLw5T7yTKdu();

	internal struct KYq12hqIvTxBI2YuVts
	{
		internal bool jUWqGW0QOx;

		internal byte[] gWyqbhgQAq;
	}

	internal class ProtectedResourceReader
	{
		private BinaryReader _reader;

		public ProtectedResourceReader(Stream stream)
		{
			_reader = new BinaryReader(stream);
		}

		[SpecialName]
		internal Stream GetBaseStream()
		{
			return _reader.BaseStream;
		}

		internal byte[] ReadBytes(int count)
		{
			return _reader.ReadBytes(count);
		}

		internal int ReadIntoBuffer(byte[] buffer, int offset, int count)
		{
			return _reader.Read(buffer, offset, count);
		}

		internal int ReadInt32()
		{
			return _reader.ReadInt32();
		}

		internal void Close()
		{
			_reader.Close();
		}
	}

	[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
	private delegate IntPtr FindResourceDelegate(IntPtr hModule, string lpName, uint lpType);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate IntPtr VirtualAllocDelegate(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int WriteProcessMemoryDelegate(IntPtr hProcess, IntPtr lpBaseAddress, [In][Out] byte[] buffer, uint size, out IntPtr lpNumberOfBytesWritten);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int VirtualProtectDelegate(IntPtr lpAddress, int dwSize, int flNewProtect, ref int lpflOldProtect);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate IntPtr OpenProcessDelegate(uint dwDesiredAccess, int bInheritHandle, uint dwProcessId);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int CloseHandleDelegate(IntPtr ptr);

	[Flags]
	private enum WasfmIqlQ0LYBhbSIus
	{

	}

	private static bool _requiresFipsCompatibleCrypto;

	private static byte[] _decodedStringTable;

	private static byte[] hnBCzyYUX8;

	private static IntPtr v8IqZcH4Vy;

	private static object iEsqOn3Ngm;

	private static bool NEWqpbLoEv;

	private static int VZUq2AxxqB;

	private static long u4tq5VaQt2;

	internal static JitCompileMethodDelegate l8qqBkRXJj;

	internal static JitCompileMethodDelegate zBRqUuTqVZ;

	private static long Glqq9k7e1M;

	private static int jhHquji48S;

	internal static Hashtable UYvqMeI3ZZ;

	private static IntPtr kNHqTxnMRU;

	private static SortedList UfpqPgbbKk;

	private static uint[] _md5RoundConstants;

	private static Dictionary<int, int> _proxyFieldToMethodTokens;

	private static int L8kqnv7fio;

	private static IntPtr _kernel32ModuleHandle;

	private static FindResourceDelegate _findResourceDelegate;

	private static int[] tRCqhQoVv2;

	private static List<string> _decodedStringCache;

	private static object _stringTableLock;

	private static bool _isCryptoPolicyInitialized;

	private static List<int> _cachedStringOffsets;

	private static object _proxyMapLock;

	[ProtectionMarkerAttribute(typeof(ProtectionMarkerAttribute.ProtectionMarkerSentinel<object>[]))]
	private static bool VhsqATmUV0;

	private static int _validatedStringCallerCount;

	private static int IpIqm1kLF1;

	private static bool LVHqJsh2mo;

	private static WriteProcessMemoryDelegate _writeProcessMemoryDelegate;

	internal static RSACryptoServiceProvider IntegritySignatureVerifier;

	private static IntPtr pmsq13AP0x;

	internal static Assembly ProtectedAssembly;

	private static bool iIcqo5vsN4;

	private static VirtualProtectDelegate _virtualProtectDelegate;

	private static bool N9kCgDc3Ey;

	private static CloseHandleDelegate _closeHandleDelegate;

	private static OpenProcessDelegate _openProcessDelegate;

	private static VirtualAllocDelegate _virtualAllocDelegate;

	static AssemblyProtectionRuntime()
	{
		N9kCgDc3Ey = false;
		ProtectedAssembly = typeof(AssemblyProtectionRuntime).Assembly;
		_md5RoundConstants = new uint[64]
		{
			3614090360u, 3905402710u, 606105819u, 3250441966u, 4118548399u, 1200080426u, 2821735955u, 4249261313u, 1770035416u, 2336552879u,
			4294925233u, 2304563134u, 1804603682u, 4254626195u, 2792965006u, 1236535329u, 4129170786u, 3225465664u, 643717713u, 3921069994u,
			3593408605u, 38016083u, 3634488961u, 3889429448u, 568446438u, 3275163606u, 4107603335u, 1163531501u, 2850285829u, 4243563512u,
			1735328473u, 2368359562u, 4294588738u, 2272392833u, 1839030562u, 4259657740u, 2763975236u, 1272893353u, 4139469664u, 3200236656u,
			681279174u, 3936430074u, 3572445317u, 76029189u, 3654602809u, 3873151461u, 530742520u, 3299628645u, 4096336452u, 1126891415u,
			2878612391u, 4237533241u, 1700485571u, 2399980690u, 4293915773u, 2240044497u, 1873313359u, 4264355552u, 2734768916u, 1309151649u,
			4149444226u, 3174756917u, 718787259u, 3951481745u
		};
		_isCryptoPolicyInitialized = false;
		_requiresFipsCompatibleCrypto = false;
		IntegritySignatureVerifier = null;
		_proxyFieldToMethodTokens = null;
		_proxyMapLock = new object();
		_validatedStringCallerCount = 0;
		_stringTableLock = new object();
		_decodedStringCache = null;
		_cachedStringOffsets = null;
		_decodedStringTable = new byte[0];
		hnBCzyYUX8 = new byte[0];
		pmsq13AP0x = IntPtr.Zero;
		v8IqZcH4Vy = IntPtr.Zero;
		iEsqOn3Ngm = new string[0];
		tRCqhQoVv2 = new int[0];
		L8kqnv7fio = 1;
		NEWqpbLoEv = false;
		UfpqPgbbKk = new SortedList();
		VZUq2AxxqB = 0;
		u4tq5VaQt2 = 0L;
		l8qqBkRXJj = null;
		zBRqUuTqVZ = null;
		Glqq9k7e1M = 0L;
		IpIqm1kLF1 = 0;
		iIcqo5vsN4 = false;
		LVHqJsh2mo = false;
		jhHquji48S = 0;
		kNHqTxnMRU = IntPtr.Zero;
		VhsqATmUV0 = false;
		UYvqMeI3ZZ = new Hashtable();
		_findResourceDelegate = null;
		_virtualAllocDelegate = null;
		_writeProcessMemoryDelegate = null;
		_virtualProtectDelegate = null;
		_openProcessDelegate = null;
		_closeHandleDelegate = null;
		_kernel32ModuleHandle = IntPtr.Zero;
		try
		{
			RSACryptoServiceProvider.UseMachineKeyStore = true;
		}
		catch
		{
		}
	}

	private void mueZVB9HFmu()
	{
	}

	internal static byte[] ComputeManagedMd5Hash(object inputBytes)
	{
		uint[] blockWords = new uint[16];
		uint paddingBitCount = (uint)((448 - ((Array)inputBytes).Length * 8 % 512 + 512) % 512);
		if (paddingBitCount == 0)
		{
			paddingBitCount = 512u;
		}
		uint paddedByteCount = (uint)(((Array)inputBytes).Length + paddingBitCount / 8 + 8L);
		ulong messageBitCount = (ulong)(((Array)inputBytes).Length * 8L);
		byte[] paddedInput = new byte[paddedByteCount];
		for (int inputIndex = 0; inputIndex < ((Array)inputBytes).Length; inputIndex++)
		{
			paddedInput[inputIndex] = ((byte[])inputBytes)[inputIndex];
		}
		paddedInput[((Array)inputBytes).Length] |= 128;
		for (int remainingLengthBytes = 8; remainingLengthBytes > 0; remainingLengthBytes--)
		{
			paddedInput[paddedByteCount - remainingLengthBytes] = (byte)((messageBitCount >> (8 - remainingLengthBytes) * 8) & 0xFFL);
		}
		uint paddedWordCount = (uint)(paddedInput.Length * 8) / 32u;
		uint stateA = 1732584193u;
		uint stateB = 4023233417u;
		uint stateC = 2562383102u;
		uint stateD = 271733878u;
		for (uint blockIndex = 0u; blockIndex < paddedWordCount / 16; blockIndex++)
		{
			uint blockByteOffset = blockIndex << 6;
			for (uint wordByteOffset = 0u; wordByteOffset < 61; wordByteOffset += 4)
			{
				blockWords[wordByteOffset >> 2] = (uint)((paddedInput[blockByteOffset + (wordByteOffset + 3)] << 24) | (paddedInput[blockByteOffset + (wordByteOffset + 2)] << 16) | (paddedInput[blockByteOffset + (wordByteOffset + 1)] << 8) | paddedInput[blockByteOffset + wordByteOffset]);
			}
			uint previousStateA = stateA;
			uint previousStateB = stateB;
			uint previousStateC = stateC;
			uint previousStateD = stateD;
			ApplyMd5RoundF(ref stateA, stateB, stateC, stateD, 0u, 7, 1u, blockWords);
			ApplyMd5RoundF(ref stateD, stateA, stateB, stateC, 1u, 12, 2u, blockWords);
			ApplyMd5RoundF(ref stateC, stateD, stateA, stateB, 2u, 17, 3u, blockWords);
			ApplyMd5RoundF(ref stateB, stateC, stateD, stateA, 3u, 22, 4u, blockWords);
			ApplyMd5RoundF(ref stateA, stateB, stateC, stateD, 4u, 7, 5u, blockWords);
			ApplyMd5RoundF(ref stateD, stateA, stateB, stateC, 5u, 12, 6u, blockWords);
			ApplyMd5RoundF(ref stateC, stateD, stateA, stateB, 6u, 17, 7u, blockWords);
			ApplyMd5RoundF(ref stateB, stateC, stateD, stateA, 7u, 22, 8u, blockWords);
			ApplyMd5RoundF(ref stateA, stateB, stateC, stateD, 8u, 7, 9u, blockWords);
			ApplyMd5RoundF(ref stateD, stateA, stateB, stateC, 9u, 12, 10u, blockWords);
			ApplyMd5RoundF(ref stateC, stateD, stateA, stateB, 10u, 17, 11u, blockWords);
			ApplyMd5RoundF(ref stateB, stateC, stateD, stateA, 11u, 22, 12u, blockWords);
			ApplyMd5RoundF(ref stateA, stateB, stateC, stateD, 12u, 7, 13u, blockWords);
			ApplyMd5RoundF(ref stateD, stateA, stateB, stateC, 13u, 12, 14u, blockWords);
			ApplyMd5RoundF(ref stateC, stateD, stateA, stateB, 14u, 17, 15u, blockWords);
			ApplyMd5RoundF(ref stateB, stateC, stateD, stateA, 15u, 22, 16u, blockWords);
			ApplyMd5RoundG(ref stateA, stateB, stateC, stateD, 1u, 5, 17u, blockWords);
			ApplyMd5RoundG(ref stateD, stateA, stateB, stateC, 6u, 9, 18u, blockWords);
			ApplyMd5RoundG(ref stateC, stateD, stateA, stateB, 11u, 14, 19u, blockWords);
			ApplyMd5RoundG(ref stateB, stateC, stateD, stateA, 0u, 20, 20u, blockWords);
			ApplyMd5RoundG(ref stateA, stateB, stateC, stateD, 5u, 5, 21u, blockWords);
			ApplyMd5RoundG(ref stateD, stateA, stateB, stateC, 10u, 9, 22u, blockWords);
			ApplyMd5RoundG(ref stateC, stateD, stateA, stateB, 15u, 14, 23u, blockWords);
			ApplyMd5RoundG(ref stateB, stateC, stateD, stateA, 4u, 20, 24u, blockWords);
			ApplyMd5RoundG(ref stateA, stateB, stateC, stateD, 9u, 5, 25u, blockWords);
			ApplyMd5RoundG(ref stateD, stateA, stateB, stateC, 14u, 9, 26u, blockWords);
			ApplyMd5RoundG(ref stateC, stateD, stateA, stateB, 3u, 14, 27u, blockWords);
			ApplyMd5RoundG(ref stateB, stateC, stateD, stateA, 8u, 20, 28u, blockWords);
			ApplyMd5RoundG(ref stateA, stateB, stateC, stateD, 13u, 5, 29u, blockWords);
			ApplyMd5RoundG(ref stateD, stateA, stateB, stateC, 2u, 9, 30u, blockWords);
			ApplyMd5RoundG(ref stateC, stateD, stateA, stateB, 7u, 14, 31u, blockWords);
			ApplyMd5RoundG(ref stateB, stateC, stateD, stateA, 12u, 20, 32u, blockWords);
			ApplyMd5RoundH(ref stateA, stateB, stateC, stateD, 5u, 4, 33u, blockWords);
			ApplyMd5RoundH(ref stateD, stateA, stateB, stateC, 8u, 11, 34u, blockWords);
			ApplyMd5RoundH(ref stateC, stateD, stateA, stateB, 11u, 16, 35u, blockWords);
			ApplyMd5RoundH(ref stateB, stateC, stateD, stateA, 14u, 23, 36u, blockWords);
			ApplyMd5RoundH(ref stateA, stateB, stateC, stateD, 1u, 4, 37u, blockWords);
			ApplyMd5RoundH(ref stateD, stateA, stateB, stateC, 4u, 11, 38u, blockWords);
			ApplyMd5RoundH(ref stateC, stateD, stateA, stateB, 7u, 16, 39u, blockWords);
			ApplyMd5RoundH(ref stateB, stateC, stateD, stateA, 10u, 23, 40u, blockWords);
			ApplyMd5RoundH(ref stateA, stateB, stateC, stateD, 13u, 4, 41u, blockWords);
			ApplyMd5RoundH(ref stateD, stateA, stateB, stateC, 0u, 11, 42u, blockWords);
			ApplyMd5RoundH(ref stateC, stateD, stateA, stateB, 3u, 16, 43u, blockWords);
			ApplyMd5RoundH(ref stateB, stateC, stateD, stateA, 6u, 23, 44u, blockWords);
			ApplyMd5RoundH(ref stateA, stateB, stateC, stateD, 9u, 4, 45u, blockWords);
			ApplyMd5RoundH(ref stateD, stateA, stateB, stateC, 12u, 11, 46u, blockWords);
			ApplyMd5RoundH(ref stateC, stateD, stateA, stateB, 15u, 16, 47u, blockWords);
			ApplyMd5RoundH(ref stateB, stateC, stateD, stateA, 2u, 23, 48u, blockWords);
			ApplyMd5RoundI(ref stateA, stateB, stateC, stateD, 0u, 6, 49u, blockWords);
			ApplyMd5RoundI(ref stateD, stateA, stateB, stateC, 7u, 10, 50u, blockWords);
			ApplyMd5RoundI(ref stateC, stateD, stateA, stateB, 14u, 15, 51u, blockWords);
			ApplyMd5RoundI(ref stateB, stateC, stateD, stateA, 5u, 21, 52u, blockWords);
			ApplyMd5RoundI(ref stateA, stateB, stateC, stateD, 12u, 6, 53u, blockWords);
			ApplyMd5RoundI(ref stateD, stateA, stateB, stateC, 3u, 10, 54u, blockWords);
			ApplyMd5RoundI(ref stateC, stateD, stateA, stateB, 10u, 15, 55u, blockWords);
			ApplyMd5RoundI(ref stateB, stateC, stateD, stateA, 1u, 21, 56u, blockWords);
			ApplyMd5RoundI(ref stateA, stateB, stateC, stateD, 8u, 6, 57u, blockWords);
			ApplyMd5RoundI(ref stateD, stateA, stateB, stateC, 15u, 10, 58u, blockWords);
			ApplyMd5RoundI(ref stateC, stateD, stateA, stateB, 6u, 15, 59u, blockWords);
			ApplyMd5RoundI(ref stateB, stateC, stateD, stateA, 13u, 21, 60u, blockWords);
			ApplyMd5RoundI(ref stateA, stateB, stateC, stateD, 4u, 6, 61u, blockWords);
			ApplyMd5RoundI(ref stateD, stateA, stateB, stateC, 11u, 10, 62u, blockWords);
			ApplyMd5RoundI(ref stateC, stateD, stateA, stateB, 2u, 15, 63u, blockWords);
			ApplyMd5RoundI(ref stateB, stateC, stateD, stateA, 9u, 21, 64u, blockWords);
			stateA += previousStateA;
			stateB += previousStateB;
			stateC += previousStateC;
			stateD += previousStateD;
		}
		byte[] hashBytes = new byte[16];
		Array.Copy(BitConverter.GetBytes(stateA), 0, hashBytes, 0, 4);
		Array.Copy(BitConverter.GetBytes(stateB), 0, hashBytes, 4, 4);
		Array.Copy(BitConverter.GetBytes(stateC), 0, hashBytes, 8, 4);
		Array.Copy(BitConverter.GetBytes(stateD), 0, hashBytes, 12, 4);
		return hashBytes;
	}

	private static void ApplyMd5RoundF(ref uint accumulator, uint stateB, uint stateC, uint stateD, uint wordIndex, ushort rotationBits, uint roundIndex, object blockWords)
	{
		accumulator = stateB + RotateLeft32(accumulator + ((stateB & stateC) | (~stateB & stateD)) + ((uint[])blockWords)[wordIndex] + _md5RoundConstants[roundIndex - 1], rotationBits);
	}

	private static void ApplyMd5RoundG(ref uint accumulator, uint stateB, uint stateC, uint stateD, uint wordIndex, ushort rotationBits, uint roundIndex, object blockWords)
	{
		accumulator = stateB + RotateLeft32(accumulator + ((stateB & stateD) | (stateC & ~stateD)) + ((uint[])blockWords)[wordIndex] + _md5RoundConstants[roundIndex - 1], rotationBits);
	}

	private static void ApplyMd5RoundH(ref uint accumulator, uint stateB, uint stateC, uint stateD, uint wordIndex, ushort rotationBits, uint roundIndex, object blockWords)
	{
		accumulator = stateB + RotateLeft32(accumulator + (stateB ^ stateC ^ stateD) + ((uint[])blockWords)[wordIndex] + _md5RoundConstants[roundIndex - 1], rotationBits);
	}

	private static void ApplyMd5RoundI(ref uint accumulator, uint stateB, uint stateC, uint stateD, uint wordIndex, ushort rotationBits, uint roundIndex, object blockWords)
	{
		accumulator = stateB + RotateLeft32(accumulator + (stateC ^ (stateB | ~stateD)) + ((uint[])blockWords)[wordIndex] + _md5RoundConstants[roundIndex - 1], rotationBits);
	}

	private static uint RotateLeft32(uint value, ushort rotationBits)
	{
		return (value >> 32 - rotationBits) | (value << (int)rotationBits);
	}

	internal static bool RequiresFipsCompatibleCrypto()
	{
		if (!_isCryptoPolicyInitialized)
		{
			DetectCryptoPolicy();
			_isCryptoPolicyInitialized = true;
		}
		return _requiresFipsCompatibleCrypto;
	}

	internal AssemblyProtectionRuntime()
	{
	}

	private void DecodeStringTableBytes(byte[] key, byte[] initializationVector, byte[] encryptedBytes)
	{
		int tailByteCount = encryptedBytes.Length % 4;
		int wordCount = encryptedBytes.Length / 4;
		byte[] decodedBytes = new byte[encryptedBytes.Length];
		int keyWordCount = key.Length / 4;
		uint accumulator = 0u;
		uint keyWord = 0u;
		uint encryptedWord = 0u;
		if (tailByteCount > 0)
		{
			wordCount++;
		}
		uint wordByteOffset = 0u;
		for (int wordIndex = 0; wordIndex < wordCount; wordIndex++)
		{
			int keyWordIndex = wordIndex % keyWordCount;
			int outputByteOffset = wordIndex * 4;
			wordByteOffset = (uint)(keyWordIndex * 4);
			keyWord = (uint)((key[wordByteOffset + 3] << 24) | (key[wordByteOffset + 2] << 16) | (key[wordByteOffset + 1] << 8) | key[wordByteOffset]);
			uint byteMask = 255u;
			int byteShift = 0;
			if (wordIndex == wordCount - 1 && tailByteCount > 0)
			{
				encryptedWord = 0u;
				accumulator += keyWord;
				for (int tailReadIndex = 0; tailReadIndex < tailByteCount; tailReadIndex++)
				{
					if (tailReadIndex > 0)
					{
						encryptedWord <<= 8;
					}
					encryptedWord |= encryptedBytes[encryptedBytes.Length - (1 + tailReadIndex)];
				}
			}
			else
			{
				accumulator += keyWord;
				wordByteOffset = (uint)outputByteOffset;
				encryptedWord = (uint)((encryptedBytes[wordByteOffset + 3] << 24) | (encryptedBytes[wordByteOffset + 2] << 16) | (encryptedBytes[wordByteOffset + 1] << 8) | encryptedBytes[wordByteOffset]);
			}
			uint stateBeforeMix = accumulator;
			accumulator = 0u;
			uint mixedState = stateBeforeMix;
			mixedState = (uint)((ulong)(mixedState * mixedState) % 3552659686uL);
			mixedState ^= mixedState << 13;
			mixedState += 1554755323;
			mixedState ^= mixedState >> 3;
			mixedState += 3428374719u;
			mixedState ^= mixedState << 17;
			mixedState += 3103922918u;
			mixedState = 1618460672 + mixedState;
			accumulator = stateBeforeMix + (uint)(double)mixedState;
			if (wordIndex == wordCount - 1 && tailByteCount > 0)
			{
				uint decodedTailWord = accumulator ^ encryptedWord;
				for (int tailWriteIndex = 0; tailWriteIndex < tailByteCount; tailWriteIndex++)
				{
					if (tailWriteIndex > 0)
					{
						byteMask <<= 8;
						byteShift += 8;
					}
					decodedBytes[outputByteOffset + tailWriteIndex] = (byte)((decodedTailWord & byteMask) >> byteShift);
				}
			}
			else
			{
				uint decodedWord = accumulator ^ encryptedWord;
				decodedBytes[outputByteOffset] = (byte)(decodedWord & 0xFF);
				decodedBytes[outputByteOffset + 1] = (byte)((decodedWord & 0xFF00) >> 8);
				decodedBytes[outputByteOffset + 2] = (byte)((decodedWord & 0xFF0000) >> 16);
				decodedBytes[outputByteOffset + 3] = (byte)((decodedWord & 0xFF000000u) >> 24);
			}
		}
		_decodedStringTable = decodedBytes;
	}

	internal static SymmetricAlgorithm CreateAesCompatibleAlgorithm()
	{
		SymmetricAlgorithm symmetricAlgorithm = null;
		if (RequiresFipsCompatibleCrypto())
		{
			return new AesCryptoServiceProvider();
		}
		try
		{
			return new RijndaelManaged();
		}
		catch
		{
			try
			{
				return (SymmetricAlgorithm)Activator.CreateInstance(Type.GetType("System.Security.Cryptography.AesCryptoServiceProvider, System.Core, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", throwOnError: true));
			}
			catch
			{
				return (SymmetricAlgorithm)Activator.CreateInstance(Type.GetType("System.Security.Cryptography.AesCryptoServiceProvider, System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", throwOnError: true));
			}
		}
	}

	internal static void DetectCryptoPolicy()
	{
		try
		{
			new MD5CryptoServiceProvider();
		}
		catch
		{
			_requiresFipsCompatibleCrypto = true;
			return;
		}
		try
		{
			_requiresFipsCompatibleCrypto = CryptoConfig.AllowOnlyFipsAlgorithms;
		}
		catch
		{
		}
	}

	internal static byte[] ComputeMd5Hash(object inputBytes)
	{
		if (!RequiresFipsCompatibleCrypto())
		{
			return new MD5CryptoServiceProvider().ComputeHash((byte[])inputBytes);
		}
		return ComputeManagedMd5Hash(inputBytes);
	}

	internal static void HashStreamRange(object hashAlgorithm, object stream, uint remainingByteCount, object buffer)
	{
		while (remainingByteCount != 0)
		{
			int blockByteCount = ((remainingByteCount > (uint)((Array)buffer).Length) ? ((Array)buffer).Length : ((int)remainingByteCount));
			((Stream)stream).Read((byte[])buffer, 0, blockByteCount);
			AppendHashBlock(hashAlgorithm, buffer, 0, blockByteCount);
			remainingByteCount -= (uint)blockByteCount;
		}
	}

	internal static void AppendHashBlock(object hashAlgorithm, object buffer, int offset, int count)
	{
		((HashAlgorithm)hashAlgorithm).TransformBlock((byte[])buffer, offset, count, (byte[])buffer, offset);
	}

	internal static uint MapRvaToFileOffset(uint relativeVirtualAddress, int sectionCount, long sectionHeadersOffset, object reader)
	{
		int sectionIndex = 0;
		uint sectionVirtualAddress;
		uint rawDataOffset;
		while (true)
		{
			if (sectionIndex < sectionCount)
			{
				((BinaryReader)reader).BaseStream.Position = sectionHeadersOffset + (sectionIndex * 40 + 8);
				uint virtualSize = ((BinaryReader)reader).ReadUInt32();
				sectionVirtualAddress = ((BinaryReader)reader).ReadUInt32();
				((BinaryReader)reader).ReadUInt32();
				rawDataOffset = ((BinaryReader)reader).ReadUInt32();
				if (sectionVirtualAddress <= relativeVirtualAddress && relativeVirtualAddress < sectionVirtualAddress + virtualSize)
				{
					break;
				}
				sectionIndex++;
				continue;
			}
			return 0u;
		}
		return rawDataOffset + relativeVirtualAddress - sectionVirtualAddress;
	}

	internal static void VerifyAssemblyIntegrity()
	{
		if (IntegritySignatureVerifier != null)
		{
			return;
		}
		ReactorTrialGuard.CheckTrialPeriodOnce();
		RSACryptoServiceProvider.UseMachineKeyStore = true;
		IntegritySignatureVerifier = new RSACryptoServiceProvider();
		string location = typeof(AssemblyProtectionRuntime).Assembly.Location;
		if (location == null || location.Length == 0)
		{
			return;
		}
		HashAlgorithm hashAlgorithm = null;
		string text = null;
		try
		{
			hashAlgorithm = SHA1.Create();
			text = CryptoConfig.MapNameToOID("SHA1");
			if (!File.Exists(location))
			{
				return;
			}
		}
		catch
		{
			return;
		}
		bool flag = false;
		try
		{
			ProtectedResourceReader yfACkCqwBCnxfyFrN5A = new ProtectedResourceReader(ProtectedAssembly.GetManifestResourceStream("{11111-22222-20001-00000}"));
			yfACkCqwBCnxfyFrN5A.GetBaseStream().Position = 0L;
			byte[] array = yfACkCqwBCnxfyFrN5A.ReadBytes((int)yfACkCqwBCnxfyFrN5A.GetBaseStream().Length);
			byte[] rgbKey = new AssemblyProtectionRuntime().GetIntegrityResourceKey();
			byte[] rgbIV = new AssemblyProtectionRuntime().GetIntegrityResourceInitializationVector();
			SymmetricAlgorithm symmetricAlgorithm = CreateAesCompatibleAlgorithm();
			symmetricAlgorithm.Mode = CipherMode.CBC;
			ICryptoTransform transform = symmetricAlgorithm.CreateDecryptor(rgbKey, rgbIV);
			Stream stream = CreateMemoryStream();
			CryptoStream cryptoStream = new CryptoStream(stream, transform, CryptoStreamMode.Write);
			cryptoStream.Write(array, 0, array.Length);
			cryptoStream.FlushFinalBlock();
			IntegritySignatureVerifier.FromXmlString(Encoding.UTF8.GetString(GetMemoryStreamBytes(stream)));
			stream.Close();
			cryptoStream.Close();
			yfACkCqwBCnxfyFrN5A.Close();
		}
		catch
		{
			flag = true;
		}
		if (!flag)
		{
			BinaryReader binaryReader = null;
			try
			{
				FileStream fileStream = new FileStream(location, FileMode.Open, FileAccess.Read, FileShare.Read);
				binaryReader = new BinaryReader(fileStream);
				byte[] array2 = new byte[65536];
				HashStreamRange(hashAlgorithm, fileStream, 152u, array2);
				bool num = binaryReader.ReadUInt16() != 523;
				int num2 = (num ? 96 : 112);
				fileStream.Position = 152L;
				fileStream.Read(array2, 0, num2);
				array2[64] = 0;
				array2[65] = 0;
				array2[66] = 0;
				array2[67] = 0;
				AppendHashBlock(hashAlgorithm, array2, 0, num2);
				fileStream.Read(array2, 0, 128);
				array2[32] = 0;
				array2[33] = 0;
				array2[34] = 0;
				array2[35] = 0;
				array2[36] = 0;
				array2[37] = 0;
				array2[38] = 0;
				array2[39] = 0;
				AppendHashBlock(hashAlgorithm, array2, 0, 128);
				long position = fileStream.Position;
				fileStream.Position = 134L;
				int num3 = binaryReader.ReadUInt16();
				fileStream.Position = position;
				HashStreamRange(hashAlgorithm, fileStream, (uint)(num3 * 40), array2);
				long position2 = fileStream.Position;
				if (num)
				{
					fileStream.Position = 360L;
				}
				else
				{
					fileStream.Position = 376L;
				}
				uint num4 = MapRvaToFileOffset(binaryReader.ReadUInt32(), num3, position, binaryReader);
				fileStream.Position = num4 + 32;
				uint num5 = binaryReader.ReadUInt32();
				uint num6 = binaryReader.ReadUInt32();
				long num7 = MapRvaToFileOffset(num5, num3, position, binaryReader);
				long num8 = num7 + num6;
				fileStream.Position = position2;
				for (int i = 0; i < num3; i++)
				{
					fileStream.Position = position + i * 40 + 16L;
					uint num9 = binaryReader.ReadUInt32();
					uint num10 = binaryReader.ReadUInt32();
					fileStream.Position = num10;
					while (num9 != 0)
					{
						long position3 = fileStream.Position;
						if (num7 <= position3 && position3 < num8)
						{
							uint num11 = (uint)(num8 - position3);
							if (num11 >= num9)
							{
								break;
							}
							num9 -= num11;
							fileStream.Position += num11;
							continue;
						}
						if (position3 < num8)
						{
							uint num12 = (uint)Math.Min(num7 - position3, num9);
							HashStreamRange(hashAlgorithm, fileStream, num12, array2);
							num9 -= num12;
							continue;
						}
						HashStreamRange(hashAlgorithm, fileStream, num9, array2);
						break;
					}
				}
				hashAlgorithm.TransformFinalBlock(new byte[0], 0, 0);
				fileStream.Position = num7;
				byte[] array3 = binaryReader.ReadBytes((int)num6);
				Array.Reverse(array3);
				flag = !IntegritySignatureVerifier.VerifyHash(hashAlgorithm.Hash, text, array3);
			}
			catch
			{
				flag = true;
			}
			try
			{
				binaryReader?.Close();
			}
			catch
			{
			}
		}
		if (flag)
		{
			throw new Exception(typeof(AssemblyProtectionRuntime).Assembly.GetName().Name + " is tampered.");
		}
		flag = false;
	}

	public static void BindProxyDelegates(RuntimeTypeHandle proxyTypeHandle)
	{
		try
		{
			Type typeFromHandle = Type.GetTypeFromHandle(proxyTypeHandle);
			if (_proxyFieldToMethodTokens == null)
			{
				lock (_proxyMapLock)
				{
					Dictionary<int, int> dictionary = new Dictionary<int, int>();
					BinaryReader binaryReader = new BinaryReader(typeof(AssemblyProtectionRuntime).Assembly.GetManifestResourceStream("coUH4Y9yd2UU8ERX8u.n4DKpImMTsbqAPD1Mb"));
					binaryReader.BaseStream.Position = 0L;
					byte[] array = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length);
					binaryReader.Close();
					if (array.Length != 0)
					{
						int num = array.Length % 4;
						int num2 = array.Length / 4;
						byte[] array2 = new byte[array.Length];
						uint num3 = 0u;
						uint num4 = 0u;
						if (num > 0)
						{
							num2++;
						}
						uint num5 = 0u;
						for (int i = 0; i < num2; i++)
						{
							int num6 = i * 4;
							uint num7 = 255u;
							int num8 = 0;
							if (i == num2 - 1 && num > 0)
							{
								num4 = 0u;
								for (int j = 0; j < num; j++)
								{
									if (j > 0)
									{
										num4 <<= 8;
									}
									num4 |= array[array.Length - (1 + j)];
								}
							}
							else
							{
								num5 = (uint)num6;
								num4 = (uint)((array[num5 + 3] << 24) | (array[num5 + 2] << 16) | (array[num5 + 1] << 8) | array[num5]);
							}
							num3 = num3;
							uint num9 = num3;
							uint num10 = num3;
							num10 = (uint)((ulong)(num10 * num10) % 3552659686uL);
							num10 ^= num10 << 13;
							num10 += 1554755323;
							num10 ^= num10 >> 3;
							num10 += 3428374719u;
							num10 ^= num10 << 17;
							num10 += 3103922918u;
							num10 = 1618460672 + num10;
							num3 = num9 + (uint)(double)num10;
							if (i == num2 - 1 && num > 0)
							{
								uint num11 = num3 ^ num4;
								for (int k = 0; k < num; k++)
								{
									if (k > 0)
									{
										num7 <<= 8;
										num8 += 8;
									}
									array2[num6 + k] = (byte)((num11 & num7) >> num8);
								}
							}
							else
							{
								uint num12 = num3 ^ num4;
								array2[num6] = (byte)(num12 & 0xFF);
								array2[num6 + 1] = (byte)((num12 & 0xFF00) >> 8);
								array2[num6 + 2] = (byte)((num12 & 0xFF0000) >> 16);
								array2[num6 + 3] = (byte)((num12 & 0xFF000000u) >> 24);
							}
						}
						array = array2;
						array2 = null;
						int num13 = array.Length / 8;
						ProtectedResourceReader yfACkCqwBCnxfyFrN5A = new ProtectedResourceReader(new MemoryStream(array));
						for (int l = 0; l < num13; l++)
						{
							int key = yfACkCqwBCnxfyFrN5A.ReadInt32();
							int value = yfACkCqwBCnxfyFrN5A.ReadInt32();
							dictionary.Add(key, value);
						}
						yfACkCqwBCnxfyFrN5A.Close();
					}
					_proxyFieldToMethodTokens = dictionary;
				}
			}
			FieldInfo[] fields = typeFromHandle.GetFields(BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Static);
			for (int m = 0; m < fields.Length; m++)
			{
				try
				{
					FieldInfo fieldInfo = fields[m];
					int metadataToken = fieldInfo.MetadataToken;
					int num14 = _proxyFieldToMethodTokens[metadataToken];
					bool flag = (num14 & 0x40000000) > 0;
					num14 &= 0x3FFFFFFF;
					MethodInfo methodInfo = (MethodInfo)typeof(AssemblyProtectionRuntime).Module.ResolveMethod(num14, typeFromHandle.GetGenericArguments(), new Type[0]);
					if (methodInfo.IsStatic)
					{
						fieldInfo.SetValue(null, Delegate.CreateDelegate(fieldInfo.FieldType, methodInfo));
						continue;
					}
					ParameterInfo[] parameters = methodInfo.GetParameters();
					int num15 = parameters.Length + 1;
					Type[] array3 = new Type[num15];
					if (methodInfo.DeclaringType.IsValueType)
					{
						array3[0] = methodInfo.DeclaringType.MakeByRefType();
					}
					else
					{
						array3[0] = typeof(object);
					}
					for (int n = 0; n < parameters.Length; n++)
					{
						array3[n + 1] = parameters[n].ParameterType;
					}
					DynamicMethod dynamicMethod = new DynamicMethod(string.Empty, methodInfo.ReturnType, array3, typeFromHandle, skipVisibility: true);
					ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
					for (int num16 = 0; num16 < num15; num16++)
					{
						switch (num16)
						{
						default:
							iLGenerator.Emit(OpCodes.Ldarg_S, num16);
							break;
						case 0:
							iLGenerator.Emit(OpCodes.Ldarg_0);
							break;
						case 1:
							iLGenerator.Emit(OpCodes.Ldarg_1);
							break;
						case 2:
							iLGenerator.Emit(OpCodes.Ldarg_2);
							break;
						case 3:
							iLGenerator.Emit(OpCodes.Ldarg_3);
							break;
						}
					}
					iLGenerator.Emit(OpCodes.Tailcall);
					iLGenerator.Emit(flag ? OpCodes.Callvirt : OpCodes.Call, methodInfo);
					iLGenerator.Emit(OpCodes.Ret);
					fieldInfo.SetValue(null, dynamicMethod.CreateDelegate(typeFromHandle));
				}
				catch (Exception)
				{
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static uint MfMCTrEQlE(uint P_0)
	{
		return (uint)"{11111-22222-10009-11112}".Length;
	}

	internal static void ThrowIfDebuggerAttached()
	{
		if (Debugger.IsAttached)
		{
			throw new Exception("Debugger Detected");
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void LoadStringTableFromResource(object resourceStream, int resourceOffset)
	{
		var reader = new ProtectedResourceReader((Stream)resourceStream);
		reader.GetBaseStream().Position = 0L;
		byte[] encrypted = reader.ReadBytes(unchecked((int)reader.GetBaseStream().Length));
		reader.Close();
		byte[] publicKeyToken = ProtectedAssembly.GetName().GetPublicKeyToken();
		byte[] key = global::Psd2UIForm.Reconstruction.StringResourceDecoder.CreateKey(publicKeyToken, out byte[] initializationVector);
		if (resourceOffset == -1)
		{
			SymmetricAlgorithm algorithm = CreateAesCompatibleAlgorithm();
			algorithm.Mode = CipherMode.CBC;
			ICryptoTransform transform = algorithm.CreateDecryptor(key, initializationVector);
			MemoryStream output = (MemoryStream)CreateMemoryStream();
			var cryptoStream = new CryptoStream(output, transform, CryptoStreamMode.Write);
			cryptoStream.Write(encrypted, 0, encrypted.Length);
			cryptoStream.FlushFinalBlock();
			_decodedStringTable = GetMemoryStreamBytes(output);
			output.Close();
			cryptoStream.Close();
			encrypted = _decodedStringTable;
		}
		if (ProtectedAssembly.EntryPoint == null)
		{
			_validatedStringCallerCount = 80;
		}
		new AssemblyProtectionRuntime().DecodeStringTableBytes(key, initializationVector, encrypted);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	internal static string GetStringAtResourceOffset(int resourceOffset)
	{
		if (_decodedStringTable.Length == 0)
		{
			_decodedStringCache = new List<string>();
			_cachedStringOffsets = new List<int>();
			LoadStringTableFromResource(ProtectedAssembly.GetManifestResourceStream("fbpuQ8ZRa1uQ0ctowe.xWdQM7OG9n4x320DV5"), resourceOffset);
		}
		if (_validatedStringCallerCount < 75)
		{
			if (ProtectedAssembly != new StackFrame(1).GetMethod().DeclaringType.Assembly)
			{
				throw new Exception();
			}
			_validatedStringCallerCount++;
		}
		lock (_stringTableLock)
		{
			int lengthOrCacheIndex = BitConverter.ToInt32(_decodedStringTable, resourceOffset);
			if (lengthOrCacheIndex < _cachedStringOffsets.Count && _cachedStringOffsets[lengthOrCacheIndex] == resourceOffset)
			{
				return _decodedStringCache[lengthOrCacheIndex];
			}
			try
			{
				byte[] stringBytes = new byte[lengthOrCacheIndex];
				Array.Copy(_decodedStringTable, resourceOffset + 4, stringBytes, 0, lengthOrCacheIndex);
				string decodedString = Encoding.Unicode.GetString(stringBytes, 0, stringBytes.Length);
				_decodedStringCache.Add(decodedString);
				_cachedStringOffsets.Add(resourceOffset);
				Array.Copy(BitConverter.GetBytes(_decodedStringCache.Count - 1), 0, _decodedStringTable, resourceOffset, 4);
				return decodedString;
			}
			catch
			{
			}
		}
		return "";
	}

	internal static string DecodeBase64Utf16String(object base64Text)
	{
		"{11111-22222-50001-00000}".Trim();
		byte[] utf16Bytes = Convert.FromBase64String((string)base64Text);
		return Encoding.Unicode.GetString(utf16Bytes, 0, utf16Bytes.Length);
	}

	private static int VCYCsBgc8p()
	{
		return 5;
	}

	private static void TryEnableMachineKeyStore()
	{
		try
		{
			RSACryptoServiceProvider.UseMachineKeyStore = true;
		}
		catch
		{
		}
	}

	private static Delegate CreateDelegateFromFunctionPointer(IntPtr functionPointer, Type delegateType)
	{
		return (Delegate)typeof(Marshal).GetMethod("GetDelegateForFunctionPointer", new Type[2]
		{
			typeof(IntPtr),
			typeof(Type)
		}).Invoke(null, new object[2] { functionPointer, delegateType });
	}

	internal static object FindExistingAssemblyPath(object assembly)
	{
		try
		{
			if (File.Exists(((Assembly)assembly).Location))
			{
				return ((Assembly)assembly).Location;
			}
		}
		catch
		{
		}
		try
		{
			if (File.Exists(((Assembly)assembly).GetName().CodeBase.ToString().Replace("file:///", "")))
			{
				return ((Assembly)assembly).GetName().CodeBase.ToString().Replace("file:///", "");
			}
		}
		catch
		{
		}
		try
		{
			if (File.Exists(assembly.GetType().GetProperty("Location").GetValue(assembly, new object[0])
				.ToString()))
			{
				return assembly.GetType().GetProperty("Location").GetValue(assembly, new object[0])
					.ToString();
			}
		}
		catch
		{
		}
		return "";
	}

	[DllImport("kernel32", EntryPoint = "LoadLibrary")]
	public static extern IntPtr LoadNativeLibrary(string libraryName);

	[DllImport("kernel32", CharSet = CharSet.Ansi, EntryPoint = "GetProcAddress")]
	public static extern IntPtr GetNativeProcAddress(IntPtr moduleHandle, string procedureName);

	private static IntPtr FindNativeResource(IntPtr moduleHandle, object resourceName, uint resourceType)
	{
		if (_findResourceDelegate == null)
		{
			_findResourceDelegate = (FindResourceDelegate)Marshal.GetDelegateForFunctionPointer(GetNativeProcAddress(GetKernel32ModuleHandle(), "Find ".Trim() + "ResourceA"), typeof(FindResourceDelegate));
		}
		return _findResourceDelegate(moduleHandle, (string)resourceName, resourceType);
	}

	private static IntPtr AllocateVirtualMemory(IntPtr address, uint byteCount, uint allocationType, uint protection)
	{
		if (_virtualAllocDelegate == null)
		{
			_virtualAllocDelegate = (VirtualAllocDelegate)Marshal.GetDelegateForFunctionPointer(GetNativeProcAddress(GetKernel32ModuleHandle(), "Virtual ".Trim() + "Alloc"), typeof(VirtualAllocDelegate));
		}
		return _virtualAllocDelegate(address, byteCount, allocationType, protection);
	}

	private static int WriteProcessMemory(IntPtr processHandle, IntPtr baseAddress, [In][Out] byte[] buffer, uint byteCount, out IntPtr bytesWritten)
	{
		if (_writeProcessMemoryDelegate == null)
		{
			_writeProcessMemoryDelegate = (WriteProcessMemoryDelegate)Marshal.GetDelegateForFunctionPointer(GetNativeProcAddress(GetKernel32ModuleHandle(), "Write ".Trim() + "Process ".Trim() + "Memory"), typeof(WriteProcessMemoryDelegate));
		}
		return _writeProcessMemoryDelegate(processHandle, baseAddress, buffer, byteCount, out bytesWritten);
	}

	private static int ProtectVirtualMemory(IntPtr address, int byteCount, int newProtection, ref int oldProtection)
	{
		if (_virtualProtectDelegate == null)
		{
			_virtualProtectDelegate = (VirtualProtectDelegate)Marshal.GetDelegateForFunctionPointer(GetNativeProcAddress(GetKernel32ModuleHandle(), "Virtual ".Trim() + "Protect"), typeof(VirtualProtectDelegate));
		}
		return _virtualProtectDelegate(address, byteCount, newProtection, ref oldProtection);
	}

	private static IntPtr OpenNativeProcess(uint desiredAccess, int inheritHandle, uint processId)
	{
		if (_openProcessDelegate == null)
		{
			_openProcessDelegate = (OpenProcessDelegate)Marshal.GetDelegateForFunctionPointer(GetNativeProcAddress(GetKernel32ModuleHandle(), "Open ".Trim() + "Process"), typeof(OpenProcessDelegate));
		}
		return _openProcessDelegate(desiredAccess, inheritHandle, processId);
	}

	private static int CloseNativeHandle(IntPtr handle)
	{
		if (_closeHandleDelegate == null)
		{
			_closeHandleDelegate = (CloseHandleDelegate)Marshal.GetDelegateForFunctionPointer(GetNativeProcAddress(GetKernel32ModuleHandle(), "Close ".Trim() + "Handle"), typeof(CloseHandleDelegate));
		}
		return _closeHandleDelegate(handle);
	}

	[SpecialName]
	private static IntPtr GetKernel32ModuleHandle()
	{
		if (_kernel32ModuleHandle == IntPtr.Zero)
		{
			_kernel32ModuleHandle = LoadNativeLibrary("kernel ".Trim() + "32.dll");
		}
		return _kernel32ModuleHandle;
	}

	private static byte[] ReadFileBytes(object filePath)
	{
		using FileStream fileStream = new FileStream((string)filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
		int bytesRead = 0;
		int remainingByteCount = (int)fileStream.Length;
		byte[] fileBytes = new byte[remainingByteCount];
		while (remainingByteCount > 0)
		{
			int readCount = fileStream.Read(fileBytes, bytesRead, remainingByteCount);
			bytesRead += readCount;
			remainingByteCount -= readCount;
		}
		return fileBytes;
	}

	internal static Stream CreateMemoryStream()
	{
		return new MemoryStream();
	}

	internal static byte[] GetMemoryStreamBytes(object memoryStream)
	{
		return ((MemoryStream)memoryStream).ToArray();
	}

	private static byte[] DecryptBytesWithEmbeddedAesKey(object encryptedBytes)
	{
		Stream output = CreateMemoryStream();
		SymmetricAlgorithm algorithm = CreateAesCompatibleAlgorithm();
		algorithm.Key = new byte[32]
		{
			117, 246, 101, 59, 214, 177, 244, 190, 112, 0,
			202, 168, 20, 205, 159, 212, 4, 117, 178, 204,
			11, 42, 85, 61, 147, 86, 79, 89, 196, 240,
			181, 195
		};
		algorithm.IV = new byte[16]
		{
			150, 138, 215, 81, 93, 48, 210, 145, 37, 227,
			199, 109, 21, 153, 11, 178
		};
		CryptoStream cryptoStream = new CryptoStream(output, algorithm.CreateDecryptor(), CryptoStreamMode.Write);
		cryptoStream.Write((byte[])encryptedBytes, 0, ((Array)encryptedBytes).Length);
		cryptoStream.Close();
		return GetMemoryStreamBytes(output);
	}

	private byte[] bAeCevbWAN()
	{
		return null;
	}

	private byte[] jX7CWpPEh5()
	{
		return null;
	}

	private byte[] GetIntegrityResourceInitializationVector()
	{
		_ = "{11111-22222-20001-00001}".Length;
		return new byte[2] { 1, 2 };
	}

	private byte[] GetIntegrityResourceKey()
	{
		_ = "{11111-22222-20001-00002}".Length;
		return new byte[2] { 1, 2 };
	}

	private byte[] B31CjGK2ek()
	{
		_ = "{11111-22222-30001-00001}".Length;
		return new byte[2] { 1, 2 };
	}

	private byte[] ec2CYMu6sk()
	{
		_ = "{11111-22222-30001-00002}".Length;
		return new byte[2] { 1, 2 };
	}

	internal byte[] n50CtkZqLR()
	{
		_ = "{11111-22222-40001-00001}".Length;
		return new byte[2] { 1, 2 };
	}

	internal byte[] f4oC4NGK57()
	{
		_ = "{11111-22222-40001-00002}".Length;
		return new byte[2] { 1, 2 };
	}

	internal byte[] XARCfkrd9l()
	{
		_ = "{11111-22222-50001-00001}".Length;
		return new byte[2] { 1, 2 };
	}

	internal byte[] rPPCcZx1HQ()
	{
		_ = "{11111-22222-50001-00002}".Length;
		return new byte[2] { 1, 2 };
	}

	internal static bool IsRuntimeControlFlowSentinelNull()
	{
		return (object)null == null;
	}

	internal static object GetRuntimeControlFlowSentinel()
	{
		return null;
	}
}
}
