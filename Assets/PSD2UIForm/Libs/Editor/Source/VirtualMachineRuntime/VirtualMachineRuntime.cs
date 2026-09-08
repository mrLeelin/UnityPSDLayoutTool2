using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace PsdVirtualMachine
{

internal class VirtualMachineRuntime
{
	[StructLayout(LayoutKind.Explicit)]
	public struct Int32BitUnion
	{
		[FieldOffset(0)]
		public byte Byte;

		[FieldOffset(0)]
		public sbyte SByte;

		[FieldOffset(0)]
		public ushort UInt16;

		[FieldOffset(0)]
		public short Int16;

		[FieldOffset(0)]
		public uint UInt32;

		[FieldOffset(0)]
		public int Int32;
	}

	private class Int32Value : NumericValue
	{
		public Int32BitUnion Bits;

		public VmPrimitiveType PrimitiveType;

		internal static Int32Value Int32ValueObfuscationSentinel;

		internal override void CopyFrom(VmValue P_0)
		{
			while (true)
			{
				Bits = ((Int32Value)P_0).Bits;
				int num = 0;
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					goto IL_0003;
				}
				goto IL_0020;
				IL_0020:
				switch (num)
				{
				case 1:
					continue;
				case 2:
					return;
				}
				goto IL_0003;
				IL_0003:
				PrimitiveType = ((Int32Value)P_0).PrimitiveType;
				num = 2;
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					break;
				}
				goto IL_0020;
			}
		}

		internal override void Assign(VmValue P_0)
		{
			while (true)
			{
				CopyFrom(P_0);
				if (IsInt32ValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		public Int32Value(bool P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private Int32Value(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, bool P_0)
			: base()
		{
			Kind = (VmValueKind)1;
			if (P_0)
			{
				Bits.Int32 = 1;
			}
			else
			{
				Bits.Int32 = 0;
			}
			PrimitiveType = (VmPrimitiveType)11;
		}

		public Int32Value(Int32Value P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private Int32Value(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, Int32Value P_0)
			: base()
		{
			Kind = P_0.Kind;
			Bits.Int32 = P_0.Bits.Int32;
			PrimitiveType = P_0.PrimitiveType;
		}

		public override NumericValue CloneNumericValue()
		{
			return new Int32Value(this);
		}

		public Int32Value(int P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private Int32Value(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, int P_0)
			: base()
		{
			Kind = (VmValueKind)1;
			Bits.Int32 = P_0;
			PrimitiveType = (VmPrimitiveType)5;
		}

		public Int32Value(uint P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private Int32Value(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, uint P_0)
			: base()
		{
			Kind = (VmValueKind)1;
			Bits.UInt32 = P_0;
			PrimitiveType = (VmPrimitiveType)6;
		}

		public Int32Value(int P_0, VmPrimitiveType P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private Int32Value(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, int P_0, VmPrimitiveType P_1)
			: base()
		{
			Kind = (VmValueKind)1;
			Bits.Int32 = P_0;
			PrimitiveType = P_1;
		}

		public Int32Value(uint P_0, VmPrimitiveType P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private Int32Value(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, uint P_0, VmPrimitiveType P_1)
			: base()
		{
			Kind = (VmValueKind)1;
			Bits.UInt32 = P_0;
			PrimitiveType = P_1;
		}

		public override bool IsZero()
		{
			while (true)
			{
				VmPrimitiveType VmPrimitiveType = PrimitiveType;
				int num = 0;
				if (!IsInt32ValueObfuscationSentinelNull())
				{
					goto IL_0003;
				}
				goto IL_0034;
				IL_0034:
				switch (num)
				{
				case 1:
					continue;
				case 2:
					goto IL_0067;
				case 3:
					goto IL_006d;
				case 4:
					goto end_IL_004f;
				}
				goto IL_0003;
				IL_0003:
				switch (VmPrimitiveType)
				{
				case (VmPrimitiveType)2:
				case (VmPrimitiveType)4:
				case (VmPrimitiveType)6:
					goto IL_0075;
				case (VmPrimitiveType)1:
				case (VmPrimitiveType)3:
				case (VmPrimitiveType)5:
				case (VmPrimitiveType)7:
					goto end_IL_004f;
				}
				num = 1;
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					goto IL_0034;
				}
				goto IL_0067;
				IL_0075:
				return Bits.UInt32 == 0;
				IL_0067:
				if (VmPrimitiveType == (VmPrimitiveType)11)
				{
					break;
				}
				goto IL_006d;
				IL_006d:
				if (VmPrimitiveType == (VmPrimitiveType)15)
				{
					break;
				}
				goto IL_0075;
				continue;
				end_IL_004f:
				break;
			}
			return Bits.Int32 == 0;
		}

		public override bool IsNonZero()
		{
			return !IsZero();
		}

		public override VmValue ConvertToPrimitiveType(VmPrimitiveType P_0)
		{
			VmDiagnosticCode VmDiagnosticCode = default(VmDiagnosticCode);
			while (true)
			{
				int num;
				switch (P_0)
				{
				case (VmPrimitiveType)7:
				case (VmPrimitiveType)8:
				case (VmPrimitiveType)9:
				case (VmPrimitiveType)10:
				case (VmPrimitiveType)12:
				case (VmPrimitiveType)13:
				case (VmPrimitiveType)14:
					VmDiagnosticCode = (VmDiagnosticCode)4;
					num = 0;
					if (GetInt32ValueObfuscationSentinel() != null)
					{
						goto IL_0015;
					}
					goto IL_00ba;
				default:
					num = 2;
					if (GetInt32ValueObfuscationSentinel() == null)
					{
						goto case (VmPrimitiveType)7;
					}
					goto IL_0015;
				case (VmPrimitiveType)1:
					return ToSByte();
				case (VmPrimitiveType)2:
					return ToByte();
				case (VmPrimitiveType)3:
					return ToInt16();
				case (VmPrimitiveType)4:
					return ToUInt16();
				case (VmPrimitiveType)6:
					return ToUInt32();
				case (VmPrimitiveType)11:
					return ToBooleanValue();
				case (VmPrimitiveType)5:
					goto IL_00b2;
				case (VmPrimitiveType)15:
					return ToCharValue();
				case (VmPrimitiveType)16:
					{
						return CloneNumericValue();
					}
					IL_0015:
					switch (num)
					{
					case 2:
						break;
					case 3:
						goto end_IL_0041;
					default:
						goto IL_00b2;
					case 1:
						goto IL_00ba;
					}
					goto case (VmPrimitiveType)7;
					IL_00b2:
					return ToInt32();
					IL_00ba:
					throw new Exception(VmDiagnosticCode.ToString());
					end_IL_0041:
					break;
				}
			}
		}

		internal override object ToObject(Type P_0)
		{
			VmPrimitiveType VmPrimitiveType = default(VmPrimitiveType);
			while (true)
			{
				if (!(P_0 != null))
				{
					goto IL_01fb;
				}
				goto IL_0218;
				IL_0218:
				int num;
				if (P_0.IsByRef)
				{
					num = 10;
					if (GetInt32ValueObfuscationSentinel() != null)
					{
						goto IL_00f2;
					}
					goto IL_01f3;
				}
				goto IL_01fb;
				IL_0340:
				return Bits.Int32;
				IL_01fb:
				if (P_0 != null)
				{
					goto IL_01cc;
				}
				goto IL_01e5;
				IL_01e5:
				if (!(P_0 == null))
				{
					goto IL_01b5;
				}
				goto IL_0351;
				IL_01b5:
				if (!(P_0 == typeof(object)))
				{
					goto IL_019e;
				}
				goto IL_0351;
				IL_019e:
				if (!(P_0 == typeof(int)))
				{
					goto IL_0184;
				}
				goto IL_0340;
				IL_0184:
				if (!(P_0 == typeof(uint)))
				{
					if (!(P_0 == typeof(short)))
					{
						num = 4;
						if (IsInt32ValueObfuscationSentinelNull())
						{
							goto IL_002b;
						}
						goto IL_00f2;
					}
					goto IL_032f;
				}
				goto IL_031e;
				IL_0298:
				return new IntPtr(Bits.Int32);
				IL_0351:
				VmPrimitiveType = PrimitiveType;
				goto IL_0359;
				IL_00f2:
				switch (num)
				{
				case 24:
					break;
				case 13:
					goto IL_008a;
				case 16:
					goto IL_00ac;
				case 22:
					goto IL_00ce;
				case 14:
					goto IL_0184;
				case 33:
					goto IL_019e;
				case 5:
					goto IL_01b5;
				case 15:
					goto IL_01cc;
				case 3:
					goto IL_01da;
				case 4:
				case 6:
					goto IL_01e5;
				case 26:
					goto IL_01f3;
				case 25:
				case 29:
					goto IL_01fb;
				case 28:
					goto IL_0218;
				case 30:
					continue;
				case 10:
					return Bits.UInt16;
				case 18:
					return Bits.Byte;
				case 11:
					goto IL_0250;
				case 9:
					goto IL_0274;
				case 2:
					goto IL_0298;
				case 20:
					goto IL_02c0;
				case 12:
					goto IL_02de;
				case 7:
					goto IL_02ec;
				default:
					goto IL_02fe;
				case 19:
					goto IL_030d;
				case 27:
					goto IL_031e;
				case 1:
					goto IL_032f;
				case 21:
					goto IL_0340;
				case 23:
				case 32:
					goto IL_0351;
				case 17:
					goto IL_0359;
				case 31:
					goto IL_03e7;
				case 8:
					goto end_IL_0222;
				}
				goto IL_002b;
				IL_031e:
				return Bits.UInt32;
				IL_01f3:
				P_0 = P_0.GetElementType();
				goto IL_01fb;
				IL_01cc:
				if (Nullable.GetUnderlyingType(P_0) != null)
				{
					goto IL_01da;
				}
				goto IL_01e5;
				IL_01da:
				P_0 = Nullable.GetUnderlyingType(P_0);
				goto IL_01e5;
				IL_002b:
				if (P_0 == typeof(ushort))
				{
					num = 10;
					if (GetInt32ValueObfuscationSentinel() == null)
					{
						goto IL_00f2;
					}
					goto IL_01e5;
				}
				if (P_0 == typeof(byte))
				{
					num = 18;
					if (GetInt32ValueObfuscationSentinel() == null)
					{
						goto IL_00f2;
					}
				}
				else if (P_0 == typeof(sbyte))
				{
					goto IL_030d;
				}
				goto IL_008a;
				IL_02c0:
				return new UIntPtr(Bits.UInt32);
				IL_0359:
				switch (VmPrimitiveType)
				{
				case (VmPrimitiveType)1:
					return Bits.SByte;
				case (VmPrimitiveType)2:
					return Bits.Byte;
				case (VmPrimitiveType)3:
					return Bits.Int16;
				case (VmPrimitiveType)4:
					return Bits.UInt16;
				case (VmPrimitiveType)5:
					break;
				case (VmPrimitiveType)6:
					return Bits.UInt32;
				case (VmPrimitiveType)7:
					return (long)Bits.Int32;
				case (VmPrimitiveType)8:
					return (ulong)Bits.UInt32;
				case (VmPrimitiveType)11:
					return IsNonZero();
				default:
					goto end_IL_0222;
				case (VmPrimitiveType)15:
					return (char)Bits.Int32;
				}
				goto IL_03e7;
				IL_02de:
				return BoxInt32AsType(P_0);
				IL_03e7:
				return Bits.Int32;
				IL_008a:
				if (!(P_0 == typeof(bool)))
				{
					num = 9;
					if (GetInt32ValueObfuscationSentinel() == null)
					{
						goto IL_00ac;
					}
					goto IL_00f2;
				}
				goto IL_02fe;
				IL_02ec:
				return (long)Bits.Int32;
				IL_00ac:
				if (!(P_0 == typeof(long)))
				{
					num = 7;
					if (IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_00ce;
					}
					goto IL_00f2;
				}
				goto IL_02ec;
				IL_02fe:
				return !IsZero();
				IL_00ce:
				if (P_0 == typeof(ulong))
				{
					num = 5;
					if (GetInt32ValueObfuscationSentinel() != null)
					{
						goto IL_00f2;
					}
					goto IL_0250;
				}
				if (P_0 == typeof(char))
				{
					goto IL_0274;
				}
				if (P_0 == typeof(IntPtr))
				{
					goto IL_0298;
				}
				if (P_0 == typeof(UIntPtr))
				{
					goto IL_02c0;
				}
				if (!P_0.IsEnum)
				{
					throw new VmOperandException();
				}
				goto IL_02de;
				IL_030d:
				return Bits.SByte;
				IL_0250:
				return (ulong)Bits.UInt32;
				IL_032f:
				return Bits.Int16;
				IL_0274:
				return (char)Bits.Int32;
				continue;
				end_IL_0222:
				break;
			}
			return Bits.Int32;
		}

		internal object BoxInt32AsType(Type P_0)
		{
			while (true)
			{
				Type underlyingType = Enum.GetUnderlyingType(P_0);
				int num = 5;
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					break;
				}
				while (true)
				{
					switch (num)
					{
					case 15:
						if (!(underlyingType == typeof(short)))
						{
							num = 14;
							if (IsInt32ValueObfuscationSentinelNull())
							{
								continue;
							}
							goto case 11;
						}
						goto case 8;
					case 11:
						if (!(underlyingType == typeof(uint)))
						{
							goto case 15;
						}
						goto case 4;
					case 14:
						if (!(underlyingType == typeof(ushort)))
						{
							num = 0;
							if (!IsInt32ValueObfuscationSentinelNull())
							{
								continue;
							}
							goto case 1;
						}
						goto case 9;
					case 1:
						if (!(underlyingType == typeof(byte)))
						{
							goto case 10;
						}
						goto case 13;
					case 10:
						if (underlyingType == typeof(sbyte))
						{
							goto case 2;
						}
						if (!(underlyingType == typeof(long)))
						{
							if (underlyingType == typeof(ulong))
							{
								num = 5;
								if (GetInt32ValueObfuscationSentinel() != null)
								{
									continue;
								}
								goto case 12;
							}
							if (!(underlyingType == typeof(char)))
							{
								num = 0;
								if (IsInt32ValueObfuscationSentinelNull())
								{
									continue;
								}
								goto end_IL_015a;
							}
							goto IL_01aa;
						}
						goto case 16;
					case 5:
						if (!(underlyingType == typeof(int)))
						{
							goto case 11;
						}
						goto case 7;
					case 6:
						break;
					case 7:
						return Enum.ToObject(P_0, Bits.Int32);
					case 12:
						return Enum.ToObject(P_0, (ulong)Bits.UInt32);
					default:
						goto end_IL_015a;
					case 3:
						goto IL_01aa;
					case 2:
						return Enum.ToObject(P_0, Bits.SByte);
					case 13:
						return Enum.ToObject(P_0, Bits.Byte);
					case 9:
						return Enum.ToObject(P_0, Bits.UInt16);
					case 4:
						return Enum.ToObject(P_0, Bits.UInt32);
					case 8:
						return Enum.ToObject(P_0, Bits.Int16);
					case 16:
						return Enum.ToObject(P_0, (long)Bits.Int32);
					}
					break;
					IL_01aa:
					return Enum.ToObject(P_0, (ushort)Bits.Int32);
				}
				continue;
				end_IL_015a:
				break;
			}
			return Enum.ToObject(P_0, Bits.Int32);
		}

		public override Int32Value ToBooleanValue()
		{
			int num;
			while (true)
			{
				if (!IsZero())
				{
					if (IsInt32ValueObfuscationSentinelNull())
					{
						switch (0)
						{
						case 1:
							continue;
						}
					}
					num = 1;
				}
				else
				{
					num = 0;
				}
				break;
			}
			return new Int32Value(num);
		}

		internal override bool IsTruthy()
		{
			return IsNonZero();
		}

		public override Int32Value ToSByte()
		{
			return new Int32Value(Bits.SByte, (VmPrimitiveType)1);
		}

		public Int32Value ToCharValue()
		{
			return new Int32Value(Bits.Int32, (VmPrimitiveType)15);
		}

		public override Int32Value ToByte()
		{
			return new Int32Value((uint)Bits.Byte, (VmPrimitiveType)2);
		}

		public override Int32Value ToInt16()
		{
			return new Int32Value(Bits.Int16, (VmPrimitiveType)3);
		}

		public override Int32Value ToUInt16()
		{
			return new Int32Value((uint)Bits.UInt16, (VmPrimitiveType)4);
		}

		public override Int32Value ToInt32()
		{
			return new Int32Value(Bits.Int32, (VmPrimitiveType)5);
		}

		public override Int32Value ToUInt32()
		{
			return new Int32Value(Bits.UInt32, (VmPrimitiveType)6);
		}

		public override Int64Value ToInt64()
		{
			return new Int64Value(Bits.Int32, (VmPrimitiveType)7);
		}

		public override Int64Value ToUInt64()
		{
			return new Int64Value((ulong)Bits.UInt32, (VmPrimitiveType)8);
		}

		public override Int32Value ConvertToSByte()
		{
			return ToSByte();
		}

		public override Int32Value ConvertToInt16()
		{
			return ToInt16();
		}

		public override Int32Value ConvertToInt32()
		{
			return ToInt32();
		}

		public override Int64Value ConvertToInt64()
		{
			return ToInt64();
		}

		public override Int32Value ConvertToByte()
		{
			return ToByte();
		}

		public override Int32Value ConvertToUInt16()
		{
			return ToUInt16();
		}

		public override Int32Value ConvertToUInt32()
		{
			return ToUInt32();
		}

		public override Int64Value ConvertToUInt64()
		{
			return ToUInt64();
		}

		public override Int32Value ToSByteChecked()
		{
			return new Int32Value(checked((sbyte)Bits.Int32), (VmPrimitiveType)1);
		}

		public override Int32Value ToSByteCheckedUnsigned()
		{
			return new Int32Value(checked((sbyte)Bits.UInt32), (VmPrimitiveType)1);
		}

		public override Int32Value ToInt16Checked()
		{
			return new Int32Value(checked((short)Bits.Int32), (VmPrimitiveType)3);
		}

		public override Int32Value ToInt16CheckedUnsigned()
		{
			return new Int32Value(checked((short)Bits.UInt32), (VmPrimitiveType)3);
		}

		public override Int32Value ToInt32Checked()
		{
			return new Int32Value(Bits.Int32, (VmPrimitiveType)5);
		}

		public override Int32Value ToInt32CheckedUnsigned()
		{
			return new Int32Value(checked((int)Bits.UInt32), (VmPrimitiveType)5);
		}

		public override Int64Value ToInt64Checked()
		{
			return new Int64Value(Bits.Int32, (VmPrimitiveType)7);
		}

		public override Int64Value ToInt64CheckedUnsigned()
		{
			return new Int64Value(Bits.UInt32, (VmPrimitiveType)7);
		}

		public override Int32Value ToByteChecked()
		{
			return new Int32Value(checked((byte)Bits.Int32), (VmPrimitiveType)2);
		}

		public override Int32Value ToByteCheckedUnsigned()
		{
			return new Int32Value(checked((byte)Bits.UInt32), (VmPrimitiveType)2);
		}

		public override Int32Value ToUInt16Checked()
		{
			return new Int32Value(checked((ushort)Bits.Int32), (VmPrimitiveType)4);
		}

		public override Int32Value ToUInt16CheckedUnsigned()
		{
			return new Int32Value(checked((ushort)Bits.UInt32), (VmPrimitiveType)4);
		}

		public override Int32Value ToUInt32Checked()
		{
			return new Int32Value(checked((uint)Bits.Int32), (VmPrimitiveType)6);
		}

		public override Int32Value ToUInt32CheckedUnsigned()
		{
			return new Int32Value(Bits.UInt32, (VmPrimitiveType)6);
		}

		public override Int64Value ToUInt64Checked()
		{
			return new Int64Value(checked((ulong)Bits.Int32), (VmPrimitiveType)8);
		}

		public override Int64Value ToUInt64CheckedUnsigned()
		{
			return new Int64Value((ulong)Bits.UInt32, (VmPrimitiveType)8);
		}

		public override FloatingPointValue ToSingle()
		{
			return new FloatingPointValue(Bits.Int32);
		}

		public override FloatingPointValue ToDouble()
		{
			return new FloatingPointValue((double)Bits.Int32);
		}

		public override FloatingPointValue ToDoubleUnsigned()
		{
			return new FloatingPointValue((double)Bits.UInt32);
		}

		public override NativeIntegerValue ToNativeInt()
		{
			while (IntPtr.Size == 8)
			{
				if (IsInt32ValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ConvertToInt64().Bits.Int64);
			}
			return new NativeIntegerValue(ConvertToInt32().Bits.Int32);
		}

		public override NativeIntegerValue ToNativeUInt()
		{
			while (IntPtr.Size == 8)
			{
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ConvertToUInt64().Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)ConvertToUInt32().Bits.UInt32);
		}

		public override NativeIntegerValue ToNativeIntChecked()
		{
			while (IntPtr.Size == 8)
			{
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToInt64Checked().Bits.Int64);
			}
			return new NativeIntegerValue(ToInt32Checked().Bits.Int32);
		}

		public override NativeIntegerValue ToNativeUIntChecked()
		{
			while (IntPtr.Size == 8)
			{
				if (IsInt32ValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToUInt64Checked().Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)ToUInt32Checked().Bits.UInt32);
		}

		public override NativeIntegerValue ToNativeIntCheckedUnsigned()
		{
			while (IntPtr.Size == 8)
			{
				if (GetInt32ValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToInt64CheckedUnsigned().Bits.Int64);
			}
			return new NativeIntegerValue(ToInt32CheckedUnsigned().Bits.Int32);
		}

		public override NativeIntegerValue ToNativeUIntCheckedUnsigned()
		{
			while (IntPtr.Size == 8)
			{
				if (GetInt32ValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToUInt64CheckedUnsigned().Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)ToUInt32CheckedUnsigned().Bits.UInt32);
		}

		public override VmValue Negate()
		{
			while (true)
			{
				VmPrimitiveType VmPrimitiveType = PrimitiveType;
				while (true)
				{
					int num;
					switch (VmPrimitiveType)
					{
					default:
						num = 0;
						if (IsInt32ValueObfuscationSentinelNull())
						{
							goto IL_0036;
						}
						goto IL_003e;
					case (VmPrimitiveType)2:
					case (VmPrimitiveType)4:
						return new Int32Value((int)(0L - (long)Bits.UInt32));
					case (VmPrimitiveType)1:
					case (VmPrimitiveType)3:
					case (VmPrimitiveType)5:
						goto IL_0096;
						IL_003e:
						switch (num)
						{
						case 2:
							break;
						default:
							goto IL_0036;
						case 3:
							continue;
						case 4:
							goto end_IL_005d;
						case 1:
							goto IL_0096;
						}
						goto IL_0012;
						IL_0012:
						if (VmPrimitiveType == (VmPrimitiveType)15)
						{
							num = 1;
							if (!IsInt32ValueObfuscationSentinelNull())
							{
								goto IL_0036;
							}
							goto IL_003e;
						}
						goto case (VmPrimitiveType)2;
						IL_0036:
						if (VmPrimitiveType != (VmPrimitiveType)11)
						{
							num = 1;
							if (IsInt32ValueObfuscationSentinelNull())
							{
								goto IL_0012;
							}
							goto IL_003e;
						}
						goto IL_0096;
						IL_0096:
						return new Int32Value(-Bits.Int32);
						end_IL_005d:
						break;
					}
					break;
				}
			}
		}

		public override VmValue Add(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0003;
				}
				int num = 0;
				if (GetInt32ValueObfuscationSentinel() == null)
				{
					goto IL_0017;
				}
				goto IL_0056;
				IL_006c:
				return new Int32Value(Bits.Int32 + ((Int32Value)P_0).Bits.Int32);
				IL_0003:
				P_0 = P_0.ReadValue();
				num = 2;
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					continue;
				}
				goto IL_0017;
				IL_0017:
				switch (num)
				{
				case 5:
					break;
				case 1:
					continue;
				default:
					goto IL_0056;
				case 3:
					goto IL_005e;
				case 4:
					goto IL_006c;
				case 6:
					goto end_IL_004b;
				}
				goto IL_0003;
				IL_0056:
				if (!P_0.IsInt32Value())
				{
					goto IL_005e;
				}
				goto IL_006c;
				IL_005e:
				if (P_0.IsNativeIntegerValue())
				{
					break;
				}
				throw new VmOperandException();
				continue;
				end_IL_004b:
				break;
			}
			return ((NativeIntegerValue)P_0).Add(this);
		}

		public override VmValue AddChecked(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_0011;
					}
					goto IL_0028;
				}
				goto IL_0051;
				IL_0075:
				return ((NativeIntegerValue)P_0).AddChecked(this);
				IL_0051:
				if (P_0.IsInt32Value())
				{
					break;
				}
				goto IL_0047;
				IL_0047:
				if (P_0.IsNativeIntegerValue())
				{
					num = 1;
					if (GetInt32ValueObfuscationSentinel() != null)
					{
						goto IL_0028;
					}
					goto IL_0075;
				}
				throw new VmOperandException();
				IL_0011:
				do
				{
					P_0 = P_0.ReadValue();
					num = 4;
				}
				while (!IsInt32ValueObfuscationSentinelNull());
				goto IL_0028;
				IL_0028:
				switch (num)
				{
				case 5:
					goto IL_0047;
				case 4:
					goto IL_0051;
				case 1:
					continue;
				case 3:
					goto IL_0075;
				case 2:
					goto end_IL_006a;
				}
				goto IL_0011;
				continue;
				end_IL_006a:
				break;
			}
			return new Int32Value(checked(Bits.Int32 + ((Int32Value)P_0).Bits.Int32));
		}

		public override VmValue AddCheckedUnsigned(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0011;
				}
				goto IL_0045;
				IL_0045:
				int num;
				if (P_0.IsInt32Value())
				{
					num = 5;
					if (GetInt32ValueObfuscationSentinel() != null)
					{
						break;
					}
					goto IL_0025;
				}
				if (P_0.IsNativeIntegerValue())
				{
					break;
				}
				goto IL_0086;
				IL_0086:
				throw new VmOperandException();
				IL_0025:
				switch (num)
				{
				case 2:
					break;
				default:
					goto IL_0045;
				case 3:
					continue;
				case 5:
					return new Int32Value(checked(Bits.UInt32 + ((Int32Value)P_0).Bits.UInt32));
				case 4:
					goto IL_0086;
				case 1:
					goto end_IL_004f;
				}
				goto IL_0011;
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 0;
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					goto IL_0025;
				}
				goto IL_0045;
				continue;
				end_IL_004f:
				break;
			}
			return ((NativeIntegerValue)P_0).AddCheckedUnsigned(this);
		}

		public override VmValue Subtract(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_002c;
				}
				goto IL_0034;
				IL_0058:
				if (!P_0.IsNativeIntegerValue())
				{
					throw new VmOperandException();
				}
				goto IL_0066;
				IL_0034:
				if (P_0.IsInt32Value())
				{
					break;
				}
				num = 2;
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					goto IL_000f;
				}
				goto IL_0058;
				IL_0066:
				return ((NativeIntegerValue)P_0).SubtractFrom(this);
				IL_000f:
				switch (num)
				{
				case 3:
					goto IL_0034;
				case 1:
					continue;
				case 2:
					goto IL_0058;
				case 4:
					goto IL_0066;
				case 5:
					goto end_IL_004d;
				}
				goto IL_002c;
				IL_002c:
				P_0 = P_0.ReadValue();
				goto IL_0034;
				continue;
				end_IL_004d:
				break;
			}
			return new Int32Value(Bits.Int32 - ((Int32Value)P_0).Bits.Int32);
		}

		public override VmValue SubtractChecked(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0019;
				}
				goto IL_0049;
				IL_0049:
				int num;
				if (!P_0.IsInt32Value())
				{
					if (P_0.IsNativeIntegerValue())
					{
						num = 1;
						if (!IsInt32ValueObfuscationSentinelNull())
						{
							goto IL_002d;
						}
						goto IL_005e;
					}
					throw new VmOperandException();
				}
				break;
				IL_005e:
				return ((NativeIntegerValue)P_0).SubtractFromChecked(this);
				IL_0019:
				P_0 = P_0.ReadValue();
				num = 0;
				if (GetInt32ValueObfuscationSentinel() == null)
				{
					goto IL_002d;
				}
				goto IL_0049;
				IL_002d:
				switch (num)
				{
				case 2:
					break;
				default:
					goto IL_0049;
				case 3:
					continue;
				case 1:
					goto IL_005e;
				case 4:
					goto end_IL_0053;
				}
				goto IL_0019;
				continue;
				end_IL_0053:
				break;
			}
			return new Int32Value(checked(Bits.Int32 - ((Int32Value)P_0).Bits.Int32));
		}

		public override VmValue SubtractCheckedUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 1;
					if (GetInt32ValueObfuscationSentinel() == null)
					{
						goto IL_0003;
					}
					goto IL_0017;
				}
				goto IL_003e;
				IL_009b:
				throw new VmOperandException();
				IL_003e:
				P_0 = P_0.ReadValue();
				num = 1;
				if (!IsInt32ValueObfuscationSentinelNull())
				{
					goto IL_0003;
				}
				goto IL_0017;
				IL_0017:
				switch (num)
				{
				case 1:
				case 3:
					break;
				case 7:
					goto IL_003e;
				case 4:
					continue;
				default:
					goto IL_006f;
				case 2:
					goto IL_0079;
				case 5:
					goto IL_009b;
				case 6:
					goto end_IL_0064;
				}
				goto IL_0003;
				IL_0003:
				if (!P_0.IsInt32Value())
				{
					num = 0;
					if (!IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_0017;
					}
					goto IL_006f;
				}
				goto IL_0079;
				IL_0079:
				return new Int32Value(checked(Bits.UInt32 - ((Int32Value)P_0).Bits.UInt32));
				IL_006f:
				if (P_0.IsNativeIntegerValue())
				{
					break;
				}
				goto IL_009b;
				continue;
				end_IL_0064:
				break;
			}
			return ((NativeIntegerValue)P_0).SubtractFromCheckedUnsigned(this);
		}

		public override VmValue Multiply(VmValue P_0)
		{
			while (true)
			{
				IL_005b:
				if (!P_0.IsManagedReference())
				{
					goto IL_0047;
				}
				goto IL_0051;
				IL_0051:
				P_0 = P_0.ReadValue();
				goto IL_0047;
				IL_0047:
				while (true)
				{
					int num;
					if (!P_0.IsInt32Value())
					{
						if (!P_0.IsNativeIntegerValue())
						{
							throw new VmOperandException();
						}
						num = 5;
						if (GetInt32ValueObfuscationSentinel() != null)
						{
							continue;
						}
					}
					else
					{
						num = 1;
						if (GetInt32ValueObfuscationSentinel() == null)
						{
							goto IL_0066;
						}
					}
					switch (num)
					{
					case 2:
					case 3:
						break;
					default:
						goto end_IL_0047;
					case 4:
						goto IL_005b;
					case 1:
						goto IL_0066;
					case 5:
						return ((NativeIntegerValue)P_0).Multiply(this);
					}
					continue;
					IL_0066:
					return new Int32Value(Bits.Int32 * ((Int32Value)P_0).Bits.Int32);
					continue;
					end_IL_0047:
					break;
				}
				goto IL_0051;
			}
		}

		public override VmValue MultiplyChecked(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 0;
					if (IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_0041;
				}
				goto IL_004b;
				IL_000f:
				switch (num)
				{
				case 7:
					break;
				default:
					goto IL_0041;
				case 6:
					goto IL_004b;
				case 1:
					continue;
				case 5:
					throw new VmOperandException();
				case 4:
					goto IL_0074;
				case 2:
					goto end_IL_0064;
				}
				goto IL_0036;
				IL_004b:
				P_0 = P_0.ReadValue();
				goto IL_0041;
				IL_0041:
				if (P_0.IsInt32Value())
				{
					break;
				}
				goto IL_0036;
				IL_0036:
				if (!P_0.IsNativeIntegerValue())
				{
					num = 5;
					if (GetInt32ValueObfuscationSentinel() == null)
					{
						goto IL_000f;
					}
					goto IL_0041;
				}
				goto IL_0074;
				IL_0074:
				return ((NativeIntegerValue)P_0).MultiplyChecked(this);
				continue;
				end_IL_0064:
				break;
			}
			return new Int32Value(checked(Bits.Int32 * ((Int32Value)P_0).Bits.Int32));
		}

		public override VmValue MultiplyCheckedUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 2;
					if (!IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_002e;
				}
				goto IL_0038;
				IL_000f:
				switch (num)
				{
				case 2:
				case 3:
					break;
				case 1:
					goto IL_0038;
				case 4:
					continue;
				default:
					goto IL_005c;
				case 5:
					goto end_IL_0051;
				}
				goto IL_002e;
				IL_0038:
				P_0 = P_0.ReadValue();
				goto IL_002e;
				IL_002e:
				if (P_0.IsInt32Value())
				{
					num = 0;
					if (GetInt32ValueObfuscationSentinel() != null)
					{
						goto IL_000f;
					}
					goto IL_005c;
				}
				if (P_0.IsNativeIntegerValue())
				{
					break;
				}
				throw new VmOperandException();
				IL_005c:
				return new Int32Value(checked(Bits.UInt32 * ((Int32Value)P_0).Bits.UInt32));
				continue;
				end_IL_0051:
				break;
			}
			return ((NativeIntegerValue)P_0).MultiplyCheckedUnsigned(this);
		}

		public override VmValue Divide(VmValue P_0)
		{
			while (true)
			{
				if (!P_0.IsManagedReference())
				{
					goto IL_004d;
				}
				goto IL_0057;
				IL_0057:
				P_0 = P_0.ReadValue();
				int num = 0;
				if (GetInt32ValueObfuscationSentinel() == null)
				{
					goto IL_000f;
				}
				goto IL_004d;
				IL_000f:
				switch (num)
				{
				case 1:
					break;
				default:
					goto IL_004d;
				case 5:
					goto IL_0057;
				case 4:
					continue;
				case 2:
					goto IL_007b;
				case 6:
					goto IL_009d;
				case 7:
					goto end_IL_006e;
				}
				goto IL_0043;
				IL_004d:
				if (!P_0.IsInt32Value())
				{
					num = 0;
					if (GetInt32ValueObfuscationSentinel() != null)
					{
						goto IL_000f;
					}
					goto IL_0043;
				}
				goto IL_007b;
				IL_009d:
				throw new VmOperandException();
				IL_007b:
				return new Int32Value(Bits.Int32 / ((Int32Value)P_0).Bits.Int32);
				IL_0043:
				if (P_0.IsNativeIntegerValue())
				{
					break;
				}
				num = 5;
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					goto IL_000f;
				}
				goto IL_009d;
				continue;
				end_IL_006e:
				break;
			}
			return ((NativeIntegerValue)P_0).DivideInto(this);
		}

		public override VmValue DivideUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 2;
					if (GetInt32ValueObfuscationSentinel() != null)
					{
						goto IL_0024;
					}
					goto IL_0047;
				}
				goto IL_0051;
				IL_0010:
				if (P_0.IsNativeIntegerValue())
				{
					num = 0;
					if (GetInt32ValueObfuscationSentinel() != null)
					{
						goto IL_0024;
					}
					goto IL_0075;
				}
				throw new VmOperandException();
				IL_0051:
				P_0 = P_0.ReadValue();
				goto IL_0047;
				IL_0047:
				if (P_0.IsInt32Value())
				{
					break;
				}
				num = 0;
				if (IsInt32ValueObfuscationSentinelNull())
				{
					goto IL_0010;
				}
				goto IL_0024;
				IL_0075:
				return ((NativeIntegerValue)P_0).DivideUnsignedInto(this);
				IL_0024:
				switch (num)
				{
				case 1:
					break;
				case 2:
				case 5:
					goto IL_0047;
				case 6:
					goto IL_0051;
				case 3:
					continue;
				default:
					goto IL_0075;
				case 4:
					goto end_IL_006a;
				}
				goto IL_0010;
				continue;
				end_IL_006a:
				break;
			}
			return new Int32Value(Bits.UInt32 / ((Int32Value)P_0).Bits.UInt32);
		}

		public override VmValue Remainder(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 1;
					if (IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_001a;
					}
					goto IL_0047;
				}
				goto IL_0051;
				IL_001a:
				switch (num)
				{
				case 1:
				case 3:
					break;
				case 5:
					goto IL_0051;
				case 2:
					continue;
				case 4:
					return new Int32Value(Bits.Int32 % ((Int32Value)P_0).Bits.Int32);
				default:
					goto end_IL_006a;
				}
				goto IL_0047;
				IL_0051:
				P_0 = P_0.ReadValue();
				goto IL_0047;
				IL_0047:
				if (!P_0.IsInt32Value())
				{
					if (!P_0.IsNativeIntegerValue())
					{
						throw new VmOperandException();
					}
					num = 0;
					if (GetInt32ValueObfuscationSentinel() == null)
					{
						break;
					}
				}
				else
				{
					num = 4;
					if (GetInt32ValueObfuscationSentinel() != null)
					{
						break;
					}
				}
				goto IL_001a;
				continue;
				end_IL_006a:
				break;
			}
			return ((NativeIntegerValue)P_0).RemainderFrom(this);
		}

		public override VmValue RemainderUnsigned(VmValue P_0)
		{
			while (true)
			{
				IL_003a:
				if (P_0.IsManagedReference())
				{
					while (true)
					{
						P_0 = P_0.ReadValue();
						if (!IsInt32ValueObfuscationSentinelNull())
						{
							break;
						}
						switch (0)
						{
						case 3:
							break;
						case 4:
							goto IL_003a;
						default:
							goto end_IL_0003;
						case 2:
							goto IL_004d;
						case 6:
							goto IL_0055;
						case 5:
							goto IL_005b;
						case 1:
							goto end_IL_003a;
						}
						continue;
						end_IL_0003:
						break;
					}
				}
				if (P_0.IsInt32Value())
				{
					break;
				}
				goto IL_004d;
				IL_005b:
				return ((NativeIntegerValue)P_0).RemainderUnsignedFrom(this);
				IL_0055:
				throw new VmOperandException();
				IL_004d:
				if (!P_0.IsNativeIntegerValue())
				{
					goto IL_0055;
				}
				goto IL_005b;
				continue;
				end_IL_003a:
				break;
			}
			return new Int32Value(Bits.UInt32 % ((Int32Value)P_0).Bits.UInt32);
		}

		public override VmValue BitwiseAnd(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0003;
				}
				int num = 0;
				if (IsInt32ValueObfuscationSentinelNull())
				{
					goto IL_0017;
				}
				goto IL_0056;
				IL_0080:
				if (P_0.IsNativeIntegerValue())
				{
					break;
				}
				throw new VmOperandException();
				IL_0003:
				P_0 = P_0.ReadValue();
				num = 1;
				if (!IsInt32ValueObfuscationSentinelNull())
				{
					goto IL_0017;
				}
				goto IL_0056;
				IL_0017:
				switch (num)
				{
				case 6:
					break;
				case 1:
					continue;
				default:
					goto IL_0056;
				case 3:
					goto IL_005e;
				case 4:
					goto IL_0080;
				case 5:
					goto end_IL_0048;
				}
				goto IL_0003;
				IL_0056:
				if (P_0.IsInt32Value())
				{
					goto IL_005e;
				}
				goto IL_0080;
				IL_005e:
				return new Int32Value(Bits.Int32 & ((Int32Value)P_0).Bits.Int32);
				continue;
				end_IL_0048:
				break;
			}
			return ((NativeIntegerValue)P_0).BitwiseAnd(this);
		}

		public override VmValue BitwiseOr(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 1;
					if (IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_0017;
					}
					goto IL_0032;
				}
				goto IL_003a;
				IL_0017:
				switch (num)
				{
				case 1:
					break;
				case 3:
					goto IL_003a;
				case 2:
					continue;
				case 4:
					goto IL_005d;
				default:
					goto end_IL_0053;
				}
				goto IL_0032;
				IL_003a:
				if (!P_0.IsInt32Value())
				{
					if (P_0.IsNativeIntegerValue())
					{
						num = 0;
						if (!IsInt32ValueObfuscationSentinelNull())
						{
							break;
						}
						goto IL_0017;
					}
					throw new VmOperandException();
				}
				goto IL_005d;
				IL_005d:
				return new Int32Value(Bits.Int32 | ((Int32Value)P_0).Bits.Int32);
				IL_0032:
				P_0 = P_0.ReadValue();
				goto IL_003a;
				continue;
				end_IL_0053:
				break;
			}
			return ((NativeIntegerValue)P_0).BitwiseOr(this);
		}

		public override VmValue BitwiseNot()
		{
			return new Int32Value(~Bits.Int32);
		}

		public override VmValue BitwiseXor(VmValue P_0)
		{
			while (true)
			{
				IL_0058:
				if (!P_0.IsManagedReference())
				{
					goto IL_0044;
				}
				goto IL_004e;
				IL_004e:
				P_0 = P_0.ReadValue();
				goto IL_0044;
				IL_0044:
				while (true)
				{
					int num;
					if (!P_0.IsInt32Value())
					{
						if (!P_0.IsNativeIntegerValue())
						{
							throw new VmOperandException();
						}
						num = 0;
						if (GetInt32ValueObfuscationSentinel() != null)
						{
							goto IL_0017;
						}
					}
					else
					{
						num = 1;
						if (IsInt32ValueObfuscationSentinelNull())
						{
							goto IL_0017;
						}
					}
					goto IL_0090;
					IL_0090:
					return ((NativeIntegerValue)P_0).BitwiseXor(this);
					IL_0017:
					switch (num)
					{
					case 3:
					case 5:
						break;
					case 2:
						goto end_IL_0044;
					case 4:
						goto IL_0058;
					case 1:
						return new Int32Value(Bits.Int32 ^ ((Int32Value)P_0).Bits.Int32);
					default:
						goto IL_0090;
					}
					continue;
					end_IL_0044:
					break;
				}
				goto IL_004e;
			}
		}

		public override VmValue ShiftLeft(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 4;
					if (IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_0003;
					}
					goto IL_002f;
				}
				goto IL_0052;
				IL_001b:
				if (P_0.IsNativeIntegerValue())
				{
					num = 0;
					if (IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_002f;
					}
					goto IL_0083;
				}
				throw new VmOperandException();
				IL_0052:
				P_0 = P_0.ReadValue();
				num = 2;
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					goto IL_001b;
				}
				goto IL_002f;
				IL_002f:
				switch (num)
				{
				case 2:
				case 5:
					break;
				case 1:
					goto IL_001b;
				case 4:
					goto IL_0052;
				case 6:
					continue;
				default:
					goto IL_0083;
				case 3:
					goto end_IL_0078;
				}
				goto IL_0003;
				IL_0003:
				if (P_0.IsInt32Value())
				{
					break;
				}
				num = 1;
				if (IsInt32ValueObfuscationSentinelNull())
				{
					goto IL_001b;
				}
				goto IL_002f;
				IL_0083:
				return ((NativeIntegerValue)P_0).ShiftArgumentLeft(this);
				continue;
				end_IL_0078:
				break;
			}
			return new Int32Value(Bits.Int32 << ((Int32Value)P_0).Bits.Int32);
		}

		public override VmValue ShiftRight(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0003;
				}
				int num = 0;
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					goto IL_0017;
				}
				goto IL_0056;
				IL_008b:
				throw new VmOperandException();
				IL_0003:
				P_0 = P_0.ReadValue();
				num = 0;
				if (!IsInt32ValueObfuscationSentinelNull())
				{
					goto IL_0017;
				}
				goto IL_0056;
				IL_0017:
				switch (num)
				{
				case 4:
					continue;
				case 1:
				case 3:
					goto IL_0056;
				case 2:
					goto IL_005e;
				case 5:
					goto IL_008b;
				case 6:
					goto end_IL_0048;
				}
				goto IL_0003;
				IL_0056:
				if (P_0.IsInt32Value())
				{
					goto IL_005e;
				}
				if (P_0.IsNativeIntegerValue())
				{
					break;
				}
				goto IL_008b;
				IL_005e:
				return new Int32Value(Bits.Int32 >> ((Int32Value)P_0).Bits.Int32);
				continue;
				end_IL_0048:
				break;
			}
			return ((NativeIntegerValue)P_0).ShiftArgumentRight(this);
		}

		public override VmValue ShiftRightUnsigned(VmValue P_0)
		{
			while (true)
			{
				IL_0054:
				if (P_0.IsManagedReference())
				{
					goto IL_0032;
				}
				goto IL_004a;
				IL_004a:
				while (true)
				{
					int num;
					if (!P_0.IsInt32Value())
					{
						if (!P_0.IsNativeIntegerValue())
						{
							throw new VmOperandException();
						}
						num = 0;
						if (GetInt32ValueObfuscationSentinel() != null)
						{
							goto IL_008d;
						}
					}
					else
					{
						num = 1;
						if (GetInt32ValueObfuscationSentinel() != null)
						{
							goto IL_005f;
						}
					}
					switch (num)
					{
					case 2:
						break;
					case 4:
						continue;
					case 3:
						goto IL_0054;
					case 1:
						goto IL_005f;
					default:
						goto IL_008d;
					}
					break;
					IL_008d:
					return ((NativeIntegerValue)P_0).ShiftUnsignedArgumentRight(this);
					IL_005f:
					return new Int32Value(Bits.UInt32 >> ((Int32Value)P_0).Bits.Int32);
				}
				goto IL_0032;
				IL_0032:
				P_0 = P_0.ReadValue();
				goto IL_004a;
			}
		}

		public override string ToString()
		{
			while (true)
			{
				VmPrimitiveType VmPrimitiveType = PrimitiveType;
				int num = 0;
				if (IsInt32ValueObfuscationSentinelNull())
				{
					goto IL_0003;
				}
				goto IL_002c;
				IL_002c:
				switch (num)
				{
				case 1:
					continue;
				case 2:
					goto IL_005b;
				case 3:
					goto end_IL_0044;
				}
				goto IL_0003;
				IL_005b:
				if (VmPrimitiveType == (VmPrimitiveType)11)
				{
					break;
				}
				goto IL_0063;
				IL_0003:
				switch (VmPrimitiveType)
				{
				case (VmPrimitiveType)2:
				case (VmPrimitiveType)4:
					goto IL_0063;
				case (VmPrimitiveType)1:
				case (VmPrimitiveType)3:
				case (VmPrimitiveType)5:
					goto end_IL_0044;
				}
				num = 2;
				if (!IsInt32ValueObfuscationSentinelNull())
				{
					continue;
				}
				goto IL_002c;
				IL_0063:
				return Bits.UInt32.ToString();
				continue;
				end_IL_0044:
				break;
			}
			return Bits.Int32.ToString();
		}

		internal override VmValue ReadValue()
		{
			return this;
		}

		internal override bool IsIntegerValue()
		{
			return true;
		}

		internal override bool IsEqualTo(VmValue P_0)
		{
			VmValue VmValue = default(VmValue);
			while (!P_0.IsObjectValue())
			{
				while (true)
				{
					int num;
					if (!P_0.IsManagedReference())
					{
						num = 0;
						if (GetInt32ValueObfuscationSentinel() != null)
						{
							goto IL_003d;
						}
						goto IL_0053;
					}
					goto IL_00cb;
					IL_00ac:
					return Bits.Int32 == ((Int32Value)VmValue).Bits.Int32;
					IL_0053:
					switch (num)
					{
					case 3:
						break;
					default:
						goto IL_003d;
					case 5:
						continue;
					case 6:
						goto end_IL_0082;
					case 1:
						goto IL_009a;
					case 4:
						goto IL_009c;
					case 7:
						return ((NativeIntegerValue)VmValue).IsEqualTo(this);
					case 8:
						goto IL_00ac;
					case 2:
						goto IL_00cb;
					case 9:
						goto end_IL_008f;
					}
					goto IL_0015;
					IL_0015:
					if (VmValue.IsIntegerValue())
					{
						if (VmValue.IsInt64Value())
						{
							goto IL_009c;
						}
						if (VmValue.IsInt32Value())
						{
							goto IL_00ac;
						}
						num = 7;
						if (!IsInt32ValueObfuscationSentinelNull())
						{
							goto IL_003d;
						}
					}
					else
					{
						num = 0;
						if (GetInt32ValueObfuscationSentinel() == null)
						{
							goto IL_009a;
						}
					}
					goto IL_0053;
					IL_009c:
					return false;
					IL_009a:
					return false;
					IL_00cb:
					return ((ReferenceValue)P_0).IsEqualTo(this);
					IL_003d:
					VmValue = P_0.ReadValue();
					goto IL_0015;
					continue;
					end_IL_0082:
					break;
				}
				continue;
				end_IL_008f:
				break;
			}
			return ((ObjectValue)P_0).IsEqualTo(this);
		}

		private static NumericValue AsNumericComparisonValue(object P_0)
		{
			NumericValue c7EoQ7IJLjSNsQhooYs;
			while (true)
			{
				c7EoQ7IJLjSNsQhooYs = P_0 as NumericValue;
				int num = 2;
				if (GetInt32ValueObfuscationSentinel() == null)
				{
					goto IL_0003;
				}
				goto IL_002d;
				IL_002d:
				switch (num)
				{
				case 3:
					break;
				default:
					goto IL_0023;
				case 4:
					continue;
				case 5:
					goto IL_0067;
				case 1:
				case 2:
				case 6:
					goto end_IL_0050;
				}
				goto IL_0003;
				IL_0003:
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					num = 2;
					if (IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_002d;
					}
				}
				goto IL_0023;
				IL_0067:
				c7EoQ7IJLjSNsQhooYs = ((VmValue)P_0).ReadValue() as NumericValue;
				break;
				IL_0023:
				while (!((VmValue)P_0).IsManagedReference())
				{
					num = 1;
					if (GetInt32ValueObfuscationSentinel() != null)
					{
						continue;
					}
					goto IL_002d;
				}
				goto IL_0067;
				continue;
				end_IL_0050:
				break;
			}
			return c7EoQ7IJLjSNsQhooYs;
		}

		internal override bool IsNotEqualTo(VmValue P_0)
		{
			VmValue VmValue = default(VmValue);
			while (!P_0.IsObjectValue())
			{
				while (true)
				{
					if (!P_0.IsManagedReference())
					{
						VmValue = P_0.ReadValue();
						goto IL_0066;
					}
					int num = 0;
					if (IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_003f;
					}
					goto IL_009c;
					IL_00ae:
					return Bits.UInt32 != ((Int32Value)VmValue).Bits.UInt32;
					IL_003f:
					switch (num)
					{
					case 5:
						break;
					case 6:
						continue;
					case 7:
						goto end_IL_007f;
					default:
						goto IL_009c;
					case 2:
						goto IL_00a9;
					case 3:
						goto IL_00ab;
					case 1:
						goto IL_00ae;
					case 4:
						goto end_IL_008c;
					}
					goto IL_0066;
					IL_0066:
					if (VmValue.IsIntegerValue())
					{
						if (VmValue.IsInt64Value())
						{
							num = 1;
							if (GetInt32ValueObfuscationSentinel() == null)
							{
								goto IL_00ab;
							}
						}
						else
						{
							if (!VmValue.IsInt32Value())
							{
								return ((NativeIntegerValue)VmValue).IsNotEqualTo(this);
							}
							num = 0;
							if (IsInt32ValueObfuscationSentinelNull())
							{
								goto IL_00ae;
							}
						}
						goto IL_003f;
					}
					goto IL_00a9;
					IL_00ab:
					return false;
					IL_00a9:
					return false;
					IL_009c:
					return ((ReferenceValue)P_0).IsNotEqualTo(this);
					continue;
					end_IL_007f:
					break;
				}
				continue;
				end_IL_008c:
				break;
			}
			return false;
		}

		public override bool GreaterThanOrEqual(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 1;
					if (GetInt32ValueObfuscationSentinel() != null)
					{
						goto IL_001d;
					}
					goto IL_004e;
				}
				goto IL_0058;
				IL_001d:
				switch (num)
				{
				case 2:
				case 5:
					break;
				case 4:
					goto IL_0058;
				case 6:
					continue;
				case 1:
					goto IL_007c;
				case 3:
					throw new VmOperandException();
				default:
					goto end_IL_0071;
				}
				goto IL_004e;
				IL_0058:
				P_0 = P_0.ReadValue();
				goto IL_004e;
				IL_004e:
				if (!P_0.IsInt32Value())
				{
					if (P_0.IsNativeIntegerValue())
					{
						break;
					}
					num = 3;
					if (!IsInt32ValueObfuscationSentinelNull())
					{
						break;
					}
				}
				else
				{
					num = 1;
					if (GetInt32ValueObfuscationSentinel() == null)
					{
						goto IL_007c;
					}
				}
				goto IL_001d;
				IL_007c:
				return Bits.Int32 >= ((Int32Value)P_0).Bits.Int32;
				continue;
				end_IL_0071:
				break;
			}
			return ((NativeIntegerValue)P_0).LessThanOrEqual(this);
		}

		public override bool GreaterThanOrEqualUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 1;
					if (!IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_0032;
				}
				goto IL_003c;
				IL_0060:
				return Bits.UInt32 >= ((Int32Value)P_0).Bits.UInt32;
				IL_003c:
				P_0 = P_0.ReadValue();
				goto IL_0032;
				IL_0032:
				if (P_0.IsInt32Value())
				{
					num = 0;
					if (IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_0060;
				}
				if (P_0.IsNativeIntegerValue())
				{
					break;
				}
				goto IL_0089;
				IL_0089:
				throw new VmOperandException();
				IL_000f:
				switch (num)
				{
				case 1:
				case 4:
					break;
				case 5:
					goto IL_003c;
				case 2:
					continue;
				default:
					goto IL_0060;
				case 6:
					goto IL_0089;
				case 3:
					goto end_IL_0055;
				}
				goto IL_0032;
				continue;
				end_IL_0055:
				break;
			}
			return ((NativeIntegerValue)P_0).LessThanOrEqualUnsigned(this);
		}

		public override bool GreaterThan(VmValue P_0)
		{
			if (P_0.IsManagedReference())
			{
				P_0 = P_0.ReadValue();
			}
			if (!P_0.IsInt32Value())
			{
				if (!P_0.IsNativeIntegerValue())
				{
					throw new VmOperandException();
				}
				return ((NativeIntegerValue)P_0).LessThan(this);
			}
			return Bits.Int32 > ((Int32Value)P_0).Bits.Int32;
		}

		public override bool GreaterThanUnsigned(VmValue P_0)
		{
			while (true)
			{
				IL_0060:
				if (!P_0.IsManagedReference())
				{
					goto IL_004c;
				}
				goto IL_0056;
				IL_0056:
				P_0 = P_0.ReadValue();
				goto IL_004c;
				IL_004c:
				while (true)
				{
					if (!P_0.IsInt32Value())
					{
						int num = 3;
						if (!IsInt32ValueObfuscationSentinelNull())
						{
							goto IL_0074;
						}
						while (true)
						{
							switch (num)
							{
							case 3:
								if (!P_0.IsNativeIntegerValue())
								{
									num = 0;
									if (GetInt32ValueObfuscationSentinel() != null)
									{
										continue;
									}
									goto case 1;
								}
								goto IL_0074;
							case 2:
							case 4:
								break;
							case 7:
								goto end_IL_004c;
							case 5:
								goto IL_0060;
							case 1:
								throw new VmOperandException();
							default:
								goto IL_0074;
							case 6:
								goto IL_0081;
							}
							break;
						}
						continue;
					}
					goto IL_0081;
					IL_0074:
					return ((NativeIntegerValue)P_0).LessThanUnsigned(this);
					IL_0081:
					return Bits.UInt32 > ((Int32Value)P_0).Bits.UInt32;
					continue;
					end_IL_004c:
					break;
				}
				goto IL_0056;
			}
		}

		public override bool LessThanOrEqual(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0012;
				}
				goto IL_005f;
				IL_005f:
				if (P_0.IsInt32Value())
				{
					break;
				}
				int num = 0;
				if (IsInt32ValueObfuscationSentinelNull())
				{
					goto IL_0035;
				}
				goto IL_003f;
				IL_0012:
				P_0 = P_0.ReadValue();
				num = 0;
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					goto IL_003f;
				}
				goto IL_005f;
				IL_003f:
				switch (num)
				{
				case 2:
					break;
				default:
					goto IL_0035;
				case 4:
					goto IL_005f;
				case 3:
					continue;
				case 1:
					return ((NativeIntegerValue)P_0).GreaterThanOrEqual(this);
				case 5:
					goto end_IL_0069;
				}
				goto IL_0012;
				IL_0035:
				do
				{
					if (P_0.IsNativeIntegerValue())
					{
						num = 1;
						continue;
					}
					throw new VmOperandException();
				}
				while (!IsInt32ValueObfuscationSentinelNull());
				goto IL_003f;
				continue;
				end_IL_0069:
				break;
			}
			return Bits.Int32 <= ((Int32Value)P_0).Bits.Int32;
		}

		public override bool LessThanOrEqualUnsigned(VmValue P_0)
		{
			while (true)
			{
				if (!P_0.IsManagedReference())
				{
					goto IL_0049;
				}
				goto IL_0053;
				IL_0053:
				P_0 = P_0.ReadValue();
				int num = 5;
				if (GetInt32ValueObfuscationSentinel() != null)
				{
					goto IL_0012;
				}
				goto IL_0049;
				IL_0012:
				switch (num)
				{
				case 2:
				case 5:
					break;
				case 4:
					goto IL_0053;
				case 3:
					continue;
				case 1:
					goto IL_0075;
				default:
					goto end_IL_006a;
				}
				goto IL_0049;
				IL_0049:
				if (P_0.IsInt32Value())
				{
					num = 0;
					if (IsInt32ValueObfuscationSentinelNull())
					{
						goto IL_0075;
					}
				}
				else
				{
					if (!P_0.IsNativeIntegerValue())
					{
						throw new VmOperandException();
					}
					num = 0;
					if (!IsInt32ValueObfuscationSentinelNull())
					{
						break;
					}
				}
				goto IL_0012;
				IL_0075:
				return Bits.UInt32 <= ((Int32Value)P_0).Bits.UInt32;
				continue;
				end_IL_006a:
				break;
			}
			return ((NativeIntegerValue)P_0).GreaterThanOrEqualUnsigned(this);
		}

		public override bool LessThan(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (GetInt32ValueObfuscationSentinel() == null)
					{
						goto IL_0011;
					}
					goto IL_0025;
				}
				goto IL_0052;
				IL_0025:
				switch (num)
				{
				case 2:
					break;
				case 5:
					goto IL_0044;
				default:
					goto IL_0052;
				case 3:
					continue;
				case 1:
					return ((NativeIntegerValue)P_0).GreaterThan(this);
				case 4:
					goto end_IL_006b;
				}
				goto IL_0011;
				IL_0052:
				if (P_0.IsInt32Value())
				{
					break;
				}
				goto IL_0044;
				IL_0044:
				if (P_0.IsNativeIntegerValue())
				{
					num = 1;
					if (GetInt32ValueObfuscationSentinel() == null)
					{
						goto IL_0025;
					}
					goto IL_0052;
				}
				throw new VmOperandException();
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 0;
				if (IsInt32ValueObfuscationSentinelNull())
				{
					goto IL_0025;
				}
				goto IL_0052;
				continue;
				end_IL_006b:
				break;
			}
			return Bits.Int32 < ((Int32Value)P_0).Bits.Int32;
		}

		public override bool LessThanUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 1;
					if (GetInt32ValueObfuscationSentinel() == null)
					{
						goto IL_0010;
					}
					goto IL_0024;
				}
				goto IL_0052;
				IL_0068:
				return ((NativeIntegerValue)P_0).GreaterThanUnsigned(this);
				IL_0024:
				switch (num)
				{
				case 5:
					break;
				case 6:
					continue;
				case 4:
					goto IL_0052;
				case 3:
					goto IL_005a;
				case 2:
					goto IL_0062;
				case 1:
					goto IL_0068;
				default:
					goto end_IL_0047;
				}
				goto IL_0010;
				IL_0010:
				P_0 = P_0.ReadValue();
				num = 4;
				if (IsInt32ValueObfuscationSentinelNull())
				{
					goto IL_0024;
				}
				goto IL_0052;
				IL_0052:
				if (P_0.IsInt32Value())
				{
					break;
				}
				goto IL_005a;
				IL_005a:
				if (!P_0.IsNativeIntegerValue())
				{
					goto IL_0062;
				}
				goto IL_0068;
				IL_0062:
				throw new VmOperandException();
				continue;
				end_IL_0047:
				break;
			}
			return Bits.UInt32 < ((Int32Value)P_0).Bits.UInt32;
		}

		internal static bool IsInt32ValueObfuscationSentinelNull()
		{
			return Int32ValueObfuscationSentinel == null;
		}

		internal static Int32Value GetInt32ValueObfuscationSentinel()
		{
			return Int32ValueObfuscationSentinel;
		}
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct Int64BitUnion
	{
		[FieldOffset(0)]
		public byte Byte;

		[FieldOffset(0)]
		public sbyte SByte;

		[FieldOffset(0)]
		public ushort UInt16;

		[FieldOffset(0)]
		public short Int16;

		[FieldOffset(0)]
		public uint UInt32;

		[FieldOffset(0)]
		public int Int32;

		[FieldOffset(0)]
		public ulong UInt64;

		[FieldOffset(0)]
		public long Int64;
	}

	private class Int64Value : NumericValue
	{
		public Int64BitUnion Bits;

		public VmPrimitiveType PrimitiveType;

		internal static Int64Value Int64ValueObfuscationSentinel;

		internal override void CopyFrom(VmValue P_0)
		{
			while (true)
			{
				Bits = ((Int64Value)P_0).Bits;
				int num = 0;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					goto IL_0003;
				}
				goto IL_0020;
				IL_0020:
				switch (num)
				{
				case 1:
					continue;
				case 2:
					return;
				}
				goto IL_0003;
				IL_0003:
				PrimitiveType = ((Int64Value)P_0).PrimitiveType;
				num = 2;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					break;
				}
				goto IL_0020;
			}
		}

		internal override void Assign(VmValue P_0)
		{
			while (true)
			{
				CopyFrom(P_0);
				if (!IsInt64ValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		public Int64Value(long P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private Int64Value(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, long P_0)
			: base()
		{
			Kind = (VmValueKind)2;
			Bits.Int64 = P_0;
			PrimitiveType = (VmPrimitiveType)7;
		}

		public Int64Value(Int64Value P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private Int64Value(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, Int64Value P_0)
			: base()
		{
			Kind = P_0.Kind;
			Bits.Int64 = P_0.Bits.Int64;
			PrimitiveType = P_0.PrimitiveType;
		}

		public override NumericValue CloneNumericValue()
		{
			return new Int64Value(this);
		}

		public Int64Value(long P_0, VmPrimitiveType P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private Int64Value(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, long P_0, VmPrimitiveType P_1)
			: base()
		{
			Kind = (VmValueKind)2;
			Bits.Int64 = P_0;
			PrimitiveType = P_1;
		}

		public Int64Value(ulong P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private Int64Value(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, ulong P_0)
			: base()
		{
			Kind = (VmValueKind)2;
			Bits.UInt64 = P_0;
			PrimitiveType = (VmPrimitiveType)8;
		}

		public Int64Value(ulong P_0, VmPrimitiveType P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private Int64Value(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, ulong P_0, VmPrimitiveType P_1)
			: base()
		{
			Kind = (VmValueKind)2;
			Bits.UInt64 = P_0;
			PrimitiveType = P_1;
		}

		public override bool IsZero()
		{
			while (PrimitiveType == (VmPrimitiveType)7)
			{
				if (GetInt64ValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return Bits.Int64 == 0L;
			}
			return Bits.UInt64 == 0L;
		}

		public override bool IsNonZero()
		{
			return !IsZero();
		}

		public override VmValue ConvertToPrimitiveType(VmPrimitiveType P_0)
		{
			VmDiagnosticCode VmDiagnosticCode = default(VmDiagnosticCode);
			while (true)
			{
				int num;
				switch (P_0)
				{
				case (VmPrimitiveType)9:
				case (VmPrimitiveType)10:
				case (VmPrimitiveType)12:
				case (VmPrimitiveType)13:
				case (VmPrimitiveType)14:
					VmDiagnosticCode = (VmDiagnosticCode)4;
					num = 0;
					if (IsInt64ValueObfuscationSentinelNull())
					{
						goto IL_0015;
					}
					goto IL_00c1;
				default:
					num = 1;
					if (!IsInt64ValueObfuscationSentinelNull())
					{
						goto case (VmPrimitiveType)9;
					}
					goto IL_0015;
				case (VmPrimitiveType)1:
					return ToSByte();
				case (VmPrimitiveType)2:
					return ToByte();
				case (VmPrimitiveType)3:
					return ToInt16();
				case (VmPrimitiveType)4:
					return ToUInt16();
				case (VmPrimitiveType)5:
					return ToInt32();
				case (VmPrimitiveType)6:
					return ToUInt32();
				case (VmPrimitiveType)8:
					return ToUInt64();
				case (VmPrimitiveType)11:
					return ToBooleanValue();
				case (VmPrimitiveType)7:
					goto IL_00d4;
				case (VmPrimitiveType)15:
					return ToCharValueFromInt64();
				case (VmPrimitiveType)16:
					{
						return CloneNumericValue();
					}
					IL_0015:
					switch (num)
					{
					case 1:
						break;
					case 2:
						goto end_IL_0041;
					default:
						goto IL_00c1;
					case 3:
						goto IL_00d4;
					}
					goto case (VmPrimitiveType)9;
					IL_00d4:
					return ToInt64();
					IL_00c1:
					throw new Exception(VmDiagnosticCode.ToString());
					end_IL_0041:
					break;
				}
			}
		}

		internal override object ToObject(Type P_0)
		{
			VmPrimitiveType j7bxykifl = default(VmPrimitiveType);
			while (true)
			{
				IL_01f5:
				if (P_0 != null)
				{
					goto IL_01c3;
				}
				goto IL_01ea;
				IL_01ea:
				while (true)
				{
					int num;
					if (P_0 == null)
					{
						num = 0;
						if (GetInt64ValueObfuscationSentinel() == null)
						{
							goto IL_00b7;
						}
						goto IL_0130;
					}
					goto IL_01ac;
					IL_03c3:
					return Bits.UInt32;
					IL_01ac:
					if (!(P_0 == typeof(object)))
					{
						if (!(P_0 == typeof(int)))
						{
							goto IL_001b;
						}
						goto IL_03d4;
					}
					num = 9;
					if (!IsInt64ValueObfuscationSentinelNull())
					{
						break;
					}
					goto IL_0130;
					IL_03d4:
					return Bits.Int32;
					IL_00b7:
					j7bxykifl = PrimitiveType;
					num = 1;
					if (IsInt64ValueObfuscationSentinelNull())
					{
						break;
					}
					goto IL_0130;
					IL_001b:
					if (!(P_0 == typeof(uint)))
					{
						goto IL_0030;
					}
					goto IL_03c3;
					IL_0030:
					if (!(P_0 == typeof(short)))
					{
						goto IL_0045;
					}
					goto IL_03b2;
					IL_0045:
					if (P_0 == typeof(ushort))
					{
						num = 1;
						if (!IsInt64ValueObfuscationSentinelNull())
						{
							goto IL_0130;
						}
						goto IL_03a1;
					}
					if (P_0 == typeof(byte))
					{
						num = 15;
						if (IsInt64ValueObfuscationSentinelNull())
						{
							goto IL_0130;
						}
					}
					else
					{
						if (P_0 == typeof(sbyte))
						{
							num = 12;
							if (!IsInt64ValueObfuscationSentinelNull())
							{
								goto IL_0130;
							}
							goto IL_0300;
						}
						if (!(P_0 == typeof(bool)))
						{
							goto IL_006b;
						}
					}
					goto IL_0311;
					IL_0130:
					switch (num)
					{
					case 8:
						break;
					case 20:
						goto IL_0030;
					case 2:
						goto IL_0045;
					case 22:
						goto IL_006b;
					default:
						goto IL_00b7;
					case 17:
						goto IL_01ac;
					case 10:
						goto IL_01c3;
					case 5:
						goto IL_01cb;
					case 13:
						continue;
					case 11:
						goto IL_01f5;
					case 6:
						goto end_IL_01ea;
					case 3:
						goto IL_02c0;
					case 12:
						goto IL_0300;
					case 14:
						goto IL_0311;
					case 15:
						return Bits.Byte;
					case 1:
						goto IL_0332;
					case 21:
						goto IL_035d;
					case 7:
						goto IL_036c;
					case 23:
						goto IL_037d;
					case 24:
						goto end_IL_01f5;
					case 4:
						goto IL_03a1;
					case 18:
						goto IL_03b2;
					case 19:
						goto IL_03c3;
					case 16:
						goto IL_03d4;
					}
					goto IL_001b;
					IL_03a1:
					return Bits.UInt16;
					IL_037d:
					return (char)Bits.Int64;
					IL_036c:
					return Bits.Int64;
					IL_035d:
					return BoxInt64AsType(P_0);
					IL_0332:
					return Bits.UInt64;
					IL_006b:
					if (!(P_0 == typeof(long)))
					{
						if (!(P_0 == typeof(ulong)))
						{
							if (!(P_0 == typeof(char)))
							{
								if (!P_0.IsEnum)
								{
									throw new VmOperandException();
								}
								goto IL_035d;
							}
							goto IL_037d;
						}
						num = 0;
						if (GetInt64ValueObfuscationSentinel() == null)
						{
							goto IL_0332;
						}
					}
					else
					{
						num = 7;
						if (!IsInt64ValueObfuscationSentinelNull())
						{
							goto IL_036c;
						}
					}
					goto IL_0130;
					IL_0300:
					return Bits.SByte;
					IL_0311:
					return !IsZero();
					IL_03b2:
					return Bits.Int16;
					continue;
					end_IL_01ea:
					break;
				}
				switch (j7bxykifl)
				{
				case (VmPrimitiveType)1:
					return Bits.SByte;
				case (VmPrimitiveType)2:
					return Bits.Byte;
				case (VmPrimitiveType)3:
					return Bits.Int16;
				case (VmPrimitiveType)4:
					return Bits.UInt16;
				case (VmPrimitiveType)5:
					return Bits.Int32;
				case (VmPrimitiveType)6:
					return Bits.UInt32;
				case (VmPrimitiveType)7:
					break;
				case (VmPrimitiveType)8:
					return Bits.UInt64;
				case (VmPrimitiveType)11:
					return IsNonZero();
				case (VmPrimitiveType)15:
					return (char)Bits.Int32;
				default:
					goto end_IL_01f5;
				}
				goto IL_02c0;
				IL_01c3:
				if (P_0.IsByRef)
				{
					goto IL_01cb;
				}
				goto IL_01ea;
				IL_01cb:
				P_0 = P_0.GetElementType();
				goto IL_01ea;
				IL_02c0:
				return Bits.Int64;
				continue;
				end_IL_01f5:
				break;
			}
			return Bits.Int64;
		}

		internal object BoxInt64AsType(Type P_0)
		{
			while (true)
			{
				Type underlyingType = Enum.GetUnderlyingType(P_0);
				while (true)
				{
					int num;
					if (!(underlyingType == typeof(int)))
					{
						if (underlyingType == typeof(uint))
						{
							num = 0;
							if (GetInt64ValueObfuscationSentinel() == null)
							{
								goto IL_014c;
							}
						}
						else
						{
							if (underlyingType == typeof(short))
							{
								goto IL_0203;
							}
							num = 6;
							if (GetInt64ValueObfuscationSentinel() == null)
							{
								goto IL_00be;
							}
						}
					}
					else
					{
						num = 1;
						if (!IsInt64ValueObfuscationSentinelNull())
						{
							goto IL_015e;
						}
					}
					goto IL_00d9;
					IL_01df:
					return Enum.ToObject(P_0, Bits.SByte);
					IL_01f1:
					return Enum.ToObject(P_0, Bits.Byte);
					IL_0183:
					if (!(underlyingType == typeof(char)))
					{
						return Enum.ToObject(P_0, Bits.Int64);
					}
					goto IL_0196;
					IL_0196:
					return Enum.ToObject(P_0, (ushort)Bits.Int32);
					IL_0203:
					return Enum.ToObject(P_0, Bits.Int16);
					IL_00d9:
					switch (num)
					{
					case 13:
						break;
					case 10:
						goto IL_00be;
					case 8:
						continue;
					case 9:
						goto end_IL_0126;
					default:
						goto IL_014c;
					case 1:
						goto IL_015e;
					case 2:
						goto IL_0170;
					case 7:
						goto IL_0183;
					case 5:
						goto IL_0196;
					case 11:
						goto IL_01bb;
					case 4:
						goto IL_01cd;
					case 12:
						goto IL_01df;
					case 6:
						goto IL_01f1;
					case 3:
						goto IL_0203;
					}
					goto IL_0055;
					IL_015e:
					return Enum.ToObject(P_0, Bits.Int32);
					IL_014c:
					return Enum.ToObject(P_0, Bits.UInt32);
					IL_00be:
					if (!(underlyingType == typeof(ushort)))
					{
						goto IL_0055;
					}
					goto IL_0170;
					IL_0170:
					return Enum.ToObject(P_0, Bits.UInt16);
					IL_0055:
					if (!(underlyingType == typeof(byte)))
					{
						if (!(underlyingType == typeof(sbyte)))
						{
							if (!(underlyingType == typeof(long)))
							{
								if (!(underlyingType == typeof(ulong)))
								{
									num = 5;
									if (GetInt64ValueObfuscationSentinel() != null)
									{
										goto IL_00d9;
									}
									goto IL_0183;
								}
								goto IL_01bb;
							}
							goto IL_01cd;
						}
						goto IL_01df;
					}
					goto IL_01f1;
					IL_01bb:
					return Enum.ToObject(P_0, Bits.UInt64);
					IL_01cd:
					return Enum.ToObject(P_0, Bits.Int64);
					continue;
					end_IL_0126:
					break;
				}
			}
		}

		public override Int32Value ToBooleanValue()
		{
			int num;
			while (true)
			{
				if (IsZero())
				{
					if (IsInt64ValueObfuscationSentinelNull())
					{
						switch (1)
						{
						case 2:
							break;
						case 1:
							goto IL_002d;
						default:
							goto IL_0030;
						}
						continue;
					}
					goto IL_002d;
				}
				goto IL_0030;
				IL_002d:
				num = 0;
				break;
				IL_0030:
				num = 1;
				break;
			}
			return new Int32Value(num);
		}

		internal override bool IsTruthy()
		{
			return IsNonZero();
		}

		public Int32Value ToCharValueFromInt64()
		{
			return new Int32Value(Bits.SByte, (VmPrimitiveType)15);
		}

		public override Int32Value ToSByte()
		{
			return new Int32Value(Bits.SByte, (VmPrimitiveType)1);
		}

		public override Int32Value ToByte()
		{
			return new Int32Value((uint)Bits.Byte, (VmPrimitiveType)2);
		}

		public override Int32Value ToInt16()
		{
			return new Int32Value(Bits.Int16, (VmPrimitiveType)3);
		}

		public override Int32Value ToUInt16()
		{
			return new Int32Value((uint)Bits.UInt16, (VmPrimitiveType)4);
		}

		public override Int32Value ToInt32()
		{
			return new Int32Value(Bits.Int32, (VmPrimitiveType)5);
		}

		public override Int32Value ToUInt32()
		{
			return new Int32Value(Bits.UInt32, (VmPrimitiveType)6);
		}

		public override Int64Value ToInt64()
		{
			return new Int64Value(Bits.Int64, (VmPrimitiveType)7);
		}

		public override Int64Value ToUInt64()
		{
			return new Int64Value(Bits.UInt64, (VmPrimitiveType)8);
		}

		public override Int32Value ConvertToSByte()
		{
			return ToSByte();
		}

		public override Int32Value ConvertToInt16()
		{
			return ToInt16();
		}

		public override Int32Value ConvertToInt32()
		{
			return ToInt32();
		}

		public override Int64Value ConvertToInt64()
		{
			return ToInt64();
		}

		public override Int32Value ConvertToByte()
		{
			return ToByte();
		}

		public override Int32Value ConvertToUInt16()
		{
			return ToUInt16();
		}

		public override Int32Value ConvertToUInt32()
		{
			return ToUInt32();
		}

		public override Int64Value ConvertToUInt64()
		{
			return ToUInt64();
		}

		public override Int32Value ToSByteChecked()
		{
			return new Int32Value(checked((sbyte)Bits.Int64), (VmPrimitiveType)1);
		}

		public override Int32Value ToSByteCheckedUnsigned()
		{
			return new Int32Value(checked((sbyte)Bits.UInt64), (VmPrimitiveType)1);
		}

		public override Int32Value ToInt16Checked()
		{
			return new Int32Value(checked((short)Bits.Int64), (VmPrimitiveType)3);
		}

		public override Int32Value ToInt16CheckedUnsigned()
		{
			return new Int32Value(checked((short)Bits.UInt64), (VmPrimitiveType)3);
		}

		public override Int32Value ToInt32Checked()
		{
			return new Int32Value(checked((int)Bits.Int64), (VmPrimitiveType)5);
		}

		public override Int32Value ToInt32CheckedUnsigned()
		{
			return new Int32Value(checked((int)Bits.UInt64), (VmPrimitiveType)5);
		}

		public override Int64Value ToInt64Checked()
		{
			return new Int64Value(Bits.Int64, (VmPrimitiveType)7);
		}

		public override Int64Value ToInt64CheckedUnsigned()
		{
			return new Int64Value(checked((long)Bits.UInt64), (VmPrimitiveType)7);
		}

		public override Int32Value ToByteChecked()
		{
			return new Int32Value(checked((byte)Bits.Int64), (VmPrimitiveType)2);
		}

		public override Int32Value ToByteCheckedUnsigned()
		{
			return new Int32Value(checked((byte)Bits.UInt64), (VmPrimitiveType)2);
		}

		public override Int32Value ToUInt16Checked()
		{
			return new Int32Value(checked((ushort)Bits.Int64), (VmPrimitiveType)4);
		}

		public override Int32Value ToUInt16CheckedUnsigned()
		{
			return new Int32Value(checked((ushort)Bits.UInt64), (VmPrimitiveType)4);
		}

		public override Int32Value ToUInt32Checked()
		{
			return new Int32Value(checked((uint)Bits.Int64), (VmPrimitiveType)6);
		}

		public override Int32Value ToUInt32CheckedUnsigned()
		{
			return new Int32Value(checked((uint)Bits.UInt64), (VmPrimitiveType)6);
		}

		public override Int64Value ToUInt64Checked()
		{
			return new Int64Value(checked((ulong)Bits.Int64), (VmPrimitiveType)8);
		}

		public override Int64Value ToUInt64CheckedUnsigned()
		{
			return new Int64Value(Bits.UInt64, (VmPrimitiveType)8);
		}

		public override FloatingPointValue ToSingle()
		{
			return new FloatingPointValue(Bits.Int64);
		}

		public override FloatingPointValue ToDouble()
		{
			return new FloatingPointValue((double)Bits.Int64);
		}

		public override FloatingPointValue ToDoubleUnsigned()
		{
			return new FloatingPointValue((double)Bits.UInt64);
		}

		public override NativeIntegerValue ToNativeInt()
		{
			while (IntPtr.Size == 8)
			{
				if (IsInt64ValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ConvertToInt64().Bits.Int64);
			}
			return new NativeIntegerValue(ConvertToInt32().Bits.Int32);
		}

		public override NativeIntegerValue ToNativeUInt()
		{
			while (IntPtr.Size == 8)
			{
				if (IsInt64ValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ConvertToUInt64().Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)ConvertToUInt32().Bits.UInt32);
		}

		public override NativeIntegerValue ToNativeIntChecked()
		{
			while (IntPtr.Size == 8)
			{
				if (IsInt64ValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToInt64Checked().Bits.Int64);
			}
			return new NativeIntegerValue(ToInt32Checked().Bits.Int32);
		}

		public override NativeIntegerValue ToNativeUIntChecked()
		{
			while (IntPtr.Size == 8)
			{
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToUInt64Checked().Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)ToUInt32Checked().Bits.UInt32);
		}

		public override NativeIntegerValue ToNativeIntCheckedUnsigned()
		{
			while (IntPtr.Size == 8)
			{
				if (IsInt64ValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToInt64CheckedUnsigned().Bits.Int64);
			}
			return new NativeIntegerValue(ToInt32CheckedUnsigned().Bits.Int32);
		}

		public override NativeIntegerValue ToNativeUIntCheckedUnsigned()
		{
			while (IntPtr.Size == 8)
			{
				if (IsInt64ValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)checked((uint)Bits.UInt64));
		}

		public override VmValue Negate()
		{
			return new Int64Value(-Bits.Int64);
		}

		public override VmValue Add(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (GetInt64ValueObfuscationSentinel() == null)
					{
						goto IL_0011;
					}
					goto IL_0025;
				}
				goto IL_0041;
				IL_0025:
				switch (num)
				{
				case 1:
					break;
				case 3:
					goto IL_0041;
				case 2:
					continue;
				case 4:
					throw new VmOperandException();
				default:
					goto end_IL_005d;
				}
				goto IL_0011;
				IL_0041:
				if (P_0.IsInt64Value())
				{
					break;
				}
				num = 4;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					continue;
				}
				goto IL_0025;
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 0;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					goto IL_0025;
				}
				goto IL_0041;
				continue;
				end_IL_005d:
				break;
			}
			return new Int64Value(Bits.Int64 + ((Int64Value)P_0).Bits.Int64);
		}

		public override VmValue AddChecked(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 1;
					if (GetInt64ValueObfuscationSentinel() == null)
					{
						goto IL_0011;
					}
					goto IL_0025;
				}
				goto IL_0040;
				IL_0067:
				throw new VmOperandException();
				IL_0040:
				if (P_0.IsInt64Value())
				{
					break;
				}
				num = 0;
				if (GetInt64ValueObfuscationSentinel() == null)
				{
					goto IL_0025;
				}
				goto IL_0067;
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 4;
				if (!IsInt64ValueObfuscationSentinelNull())
				{
					continue;
				}
				goto IL_0025;
				IL_0025:
				switch (num)
				{
				case 1:
					break;
				case 4:
					goto IL_0040;
				case 2:
					continue;
				default:
					goto IL_0067;
				case 3:
					goto end_IL_005c;
				}
				goto IL_0011;
				continue;
				end_IL_005c:
				break;
			}
			return new Int64Value(checked(Bits.Int64 + ((Int64Value)P_0).Bits.Int64));
		}

		public override VmValue AddCheckedUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (GetInt64ValueObfuscationSentinel() != null)
					{
						goto IL_000f;
					}
					goto IL_0024;
				}
				goto IL_002c;
				IL_000f:
				switch (num)
				{
				case 3:
					goto IL_002c;
				case 1:
					continue;
				case 2:
					goto end_IL_0045;
				}
				goto IL_0024;
				IL_002c:
				if (P_0.IsInt64Value())
				{
					num = 2;
					if (!IsInt64ValueObfuscationSentinelNull())
					{
						break;
					}
					goto IL_000f;
				}
				throw new VmOperandException();
				IL_0024:
				P_0 = P_0.ReadValue();
				goto IL_002c;
				continue;
				end_IL_0045:
				break;
			}
			return new Int64Value(checked(Bits.UInt64 + ((Int64Value)P_0).Bits.UInt64));
		}

		public override VmValue Subtract(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0011;
				}
				goto IL_003d;
				IL_003d:
				int num;
				if (P_0.IsInt64Value())
				{
					num = 0;
					if (GetInt64ValueObfuscationSentinel() == null)
					{
						break;
					}
					goto IL_0025;
				}
				throw new VmOperandException();
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 0;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					goto IL_0025;
				}
				goto IL_003d;
				IL_0025:
				switch (num)
				{
				case 2:
					break;
				case 1:
					goto IL_003d;
				case 3:
					continue;
				default:
					goto end_IL_0047;
				}
				goto IL_0011;
				continue;
				end_IL_0047:
				break;
			}
			return new Int64Value(Bits.Int64 - ((Int64Value)P_0).Bits.Int64);
		}

		public override VmValue SubtractChecked(VmValue P_0)
		{
			while (true)
			{
				IL_0036:
				if (P_0.IsManagedReference())
				{
					while (true)
					{
						P_0 = P_0.ReadValue();
						if (GetInt64ValueObfuscationSentinel() == null)
						{
							break;
						}
						switch (0)
						{
						case 5:
							break;
						case 4:
							goto IL_0036;
						case 1:
						case 3:
							goto end_IL_0003;
						case 2:
							goto IL_0049;
						default:
							goto end_IL_0036;
						}
						continue;
						end_IL_0003:
						break;
					}
				}
				if (P_0.IsInt64Value())
				{
					break;
				}
				goto IL_0049;
				IL_0049:
				throw new VmOperandException();
				continue;
				end_IL_0036:
				break;
			}
			return new Int64Value(checked(Bits.Int64 - ((Int64Value)P_0).Bits.Int64));
		}

		public override VmValue SubtractCheckedUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 1;
					if (IsInt64ValueObfuscationSentinelNull())
					{
						goto IL_0003;
					}
					goto IL_0017;
				}
				goto IL_0032;
				IL_0003:
				if (P_0.IsInt64Value())
				{
					num = 2;
					if (IsInt64ValueObfuscationSentinelNull())
					{
						break;
					}
					goto IL_0017;
				}
				throw new VmOperandException();
				IL_0032:
				P_0 = P_0.ReadValue();
				num = 0;
				if (!IsInt64ValueObfuscationSentinelNull())
				{
					goto IL_0003;
				}
				goto IL_0017;
				IL_0017:
				switch (num)
				{
				case 3:
					goto IL_0032;
				case 2:
					continue;
				case 4:
					goto end_IL_0058;
				}
				goto IL_0003;
				continue;
				end_IL_0058:
				break;
			}
			return new Int64Value(checked(Bits.UInt64 - ((Int64Value)P_0).Bits.UInt64));
		}

		public override VmValue Multiply(VmValue P_0)
		{
			while (true)
			{
				IL_005a:
				int num;
				if (P_0.IsManagedReference())
				{
					num = 1;
					if (IsInt64ValueObfuscationSentinelNull())
					{
						goto IL_0025;
					}
				}
				goto IL_0041;
				IL_0025:
				while (true)
				{
					switch (num)
					{
					case 1:
						P_0 = P_0.ReadValue();
						num = 0;
						if (IsInt64ValueObfuscationSentinelNull())
						{
							continue;
						}
						break;
					case 2:
						goto IL_005a;
					case 4:
						throw new VmOperandException();
					case 3:
						goto end_IL_005a;
					}
					break;
				}
				goto IL_0041;
				IL_0041:
				if (P_0.IsInt64Value())
				{
					break;
				}
				num = 4;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					break;
				}
				goto IL_0025;
				continue;
				end_IL_005a:
				break;
			}
			return new Int64Value(Bits.Int64 * ((Int64Value)P_0).Bits.Int64);
		}

		public override VmValue MultiplyChecked(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 4;
					if (IsInt64ValueObfuscationSentinelNull())
					{
						goto IL_0017;
					}
				}
				goto IL_0036;
				IL_0067:
				throw new VmOperandException();
				IL_0036:
				P_0 = P_0.ReadValue();
				num = 0;
				if (GetInt64ValueObfuscationSentinel() == null)
				{
					goto IL_0003;
				}
				goto IL_0017;
				IL_0017:
				switch (num)
				{
				case 1:
					goto IL_0036;
				case 5:
					continue;
				case 3:
					goto IL_0067;
				case 2:
					goto end_IL_005c;
				}
				goto IL_0003;
				IL_0003:
				if (P_0.IsInt64Value())
				{
					break;
				}
				num = 1;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					goto IL_0017;
				}
				goto IL_0067;
				continue;
				end_IL_005c:
				break;
			}
			return new Int64Value(checked(Bits.Int64 * ((Int64Value)P_0).Bits.Int64));
		}

		public override VmValue MultiplyCheckedUnsigned(VmValue P_0)
		{
			if (P_0.IsManagedReference())
			{
				P_0 = P_0.ReadValue();
			}
			if (!P_0.IsInt64Value())
			{
				throw new VmOperandException();
			}
			return new Int64Value(checked(Bits.UInt64 * ((Int64Value)P_0).Bits.UInt64));
		}

		public override VmValue Divide(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_003b;
				}
				if (IsInt64ValueObfuscationSentinelNull())
				{
					switch (1)
					{
					case 2:
						break;
					case 3:
						goto IL_003b;
					case 1:
					case 5:
						goto IL_0043;
					case 4:
						goto IL_004b;
					default:
						goto end_IL_002e;
					}
					continue;
				}
				goto IL_0043;
				IL_0043:
				if (P_0.IsInt64Value())
				{
					break;
				}
				goto IL_004b;
				IL_004b:
				throw new VmOperandException();
				IL_003b:
				P_0 = P_0.ReadValue();
				goto IL_0043;
				continue;
				end_IL_002e:
				break;
			}
			return new Int64Value(Bits.Int64 / ((Int64Value)P_0).Bits.Int64);
		}

		public override VmValue DivideUnsigned(VmValue P_0)
		{
			while (true)
			{
				IL_0042:
				if (!P_0.IsManagedReference())
				{
					goto IL_002e;
				}
				goto IL_0038;
				IL_0038:
				P_0 = P_0.ReadValue();
				goto IL_002e;
				IL_002e:
				while (true)
				{
					if (!P_0.IsInt64Value())
					{
						if (GetInt64ValueObfuscationSentinel() != null)
						{
							switch (4)
							{
							case 2:
							case 4:
								break;
							case 1:
								goto end_IL_002e;
							case 3:
								goto IL_0042;
							case 5:
								goto IL_004d;
							default:
								goto IL_0053;
							}
							continue;
						}
						goto IL_004d;
					}
					goto IL_0053;
					IL_004d:
					throw new VmOperandException();
					IL_0053:
					return new Int64Value(Bits.UInt64 / ((Int64Value)P_0).Bits.UInt64);
					continue;
					end_IL_002e:
					break;
				}
				goto IL_0038;
			}
		}

		public override VmValue Remainder(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0011;
				}
				goto IL_0040;
				IL_0040:
				if (P_0.IsInt64Value())
				{
					break;
				}
				int num = 0;
				if (IsInt64ValueObfuscationSentinelNull())
				{
					goto IL_0025;
				}
				goto IL_0058;
				IL_0058:
				throw new VmOperandException();
				IL_0025:
				switch (num)
				{
				case 2:
					break;
				case 1:
					goto IL_0040;
				case 3:
					continue;
				default:
					goto IL_0058;
				case 4:
					goto end_IL_004a;
				}
				goto IL_0011;
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 1;
				if (IsInt64ValueObfuscationSentinelNull())
				{
					goto IL_0025;
				}
				goto IL_0058;
				continue;
				end_IL_004a:
				break;
			}
			return new Int64Value(Bits.Int64 % ((Int64Value)P_0).Bits.Int64);
		}

		public override VmValue RemainderUnsigned(VmValue P_0)
		{
			while (true)
			{
				IL_0056:
				int num;
				if (P_0.IsManagedReference())
				{
					num = 1;
					if (GetInt64ValueObfuscationSentinel() == null)
					{
						goto IL_0025;
					}
				}
				goto IL_003d;
				IL_0025:
				while (true)
				{
					switch (num)
					{
					case 1:
						P_0 = P_0.ReadValue();
						num = 0;
						if (IsInt64ValueObfuscationSentinelNull())
						{
							continue;
						}
						break;
					case 2:
						goto IL_0056;
					case 3:
						goto end_IL_0056;
					}
					break;
				}
				goto IL_003d;
				IL_003d:
				if (P_0.IsInt64Value())
				{
					num = 3;
					if (!IsInt64ValueObfuscationSentinelNull())
					{
						break;
					}
					goto IL_0025;
				}
				throw new VmOperandException();
				continue;
				end_IL_0056:
				break;
			}
			return new Int64Value(Bits.UInt64 % ((Int64Value)P_0).Bits.UInt64);
		}

		public override VmValue BitwiseAnd(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 1;
					if (IsInt64ValueObfuscationSentinelNull())
					{
						goto IL_0010;
					}
					goto IL_001a;
				}
				goto IL_0039;
				IL_0010:
				while (!P_0.IsInt64Value())
				{
					num = 1;
					if (!IsInt64ValueObfuscationSentinelNull())
					{
						continue;
					}
					goto IL_001a;
				}
				break;
				IL_0039:
				P_0 = P_0.ReadValue();
				num = 0;
				if (GetInt64ValueObfuscationSentinel() == null)
				{
					goto IL_0010;
				}
				goto IL_001a;
				IL_001a:
				switch (num)
				{
				case 2:
					goto IL_0039;
				case 5:
					continue;
				case 1:
					throw new VmOperandException();
				case 3:
					goto end_IL_005f;
				}
				goto IL_0010;
				continue;
				end_IL_005f:
				break;
			}
			return new Int64Value(Bits.Int64 & ((Int64Value)P_0).Bits.Int64);
		}

		public override VmValue BitwiseOr(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0011;
				}
				goto IL_003d;
				IL_003d:
				int num;
				if (P_0.IsInt64Value())
				{
					num = 0;
					if (IsInt64ValueObfuscationSentinelNull())
					{
						break;
					}
					goto IL_0025;
				}
				throw new VmOperandException();
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 1;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					goto IL_0025;
				}
				goto IL_003d;
				IL_0025:
				switch (num)
				{
				case 2:
					break;
				case 1:
					goto IL_003d;
				case 3:
					continue;
				default:
					goto end_IL_0047;
				}
				goto IL_0011;
				continue;
				end_IL_0047:
				break;
			}
			return new Int64Value(Bits.Int64 | ((Int64Value)P_0).Bits.Int64);
		}

		public override VmValue BitwiseNot()
		{
			return new Int64Value(~Bits.Int64);
		}

		public override VmValue BitwiseXor(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_003b;
				}
				if (!IsInt64ValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					case 2:
						goto IL_003b;
					default:
						goto IL_0043;
					case 3:
						goto IL_004b;
					case 4:
						goto end_IL_002e;
					}
					continue;
				}
				goto IL_0043;
				IL_0043:
				if (P_0.IsInt64Value())
				{
					break;
				}
				goto IL_004b;
				IL_004b:
				throw new VmOperandException();
				IL_003b:
				P_0 = P_0.ReadValue();
				goto IL_0043;
				continue;
				end_IL_002e:
				break;
			}
			return new Int64Value(Bits.Int64 ^ ((Int64Value)P_0).Bits.Int64);
		}

		public override VmValue ShiftLeft(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_001c;
				}
				goto IL_005c;
				IL_005c:
				int num;
				if (!P_0.IsInt64Value())
				{
					if (!P_0.IsNumericValue())
					{
						throw new VmOperandException();
					}
					num = 2;
					if (GetInt64ValueObfuscationSentinel() == null)
					{
						break;
					}
				}
				else
				{
					num = 0;
					if (GetInt64ValueObfuscationSentinel() == null)
					{
						goto IL_0076;
					}
				}
				goto IL_0030;
				IL_0030:
				switch (num)
				{
				case 3:
					break;
				case 1:
					goto IL_005c;
				case 4:
					continue;
				default:
					goto IL_0076;
				case 2:
					goto end_IL_0066;
				}
				goto IL_001c;
				IL_0076:
				return new Int64Value(Bits.Int64 << ((Int64Value)P_0).Bits.Int32);
				IL_001c:
				P_0 = P_0.ReadValue();
				num = 0;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					goto IL_0030;
				}
				goto IL_005c;
				continue;
				end_IL_0066:
				break;
			}
			return new Int64Value(Bits.Int64 << ((NumericValue)P_0).ToInt32().Bits.Int32);
		}

		public override VmValue ShiftRight(VmValue P_0)
		{
			while (true)
			{
				IL_0069:
				int num;
				if (P_0.IsManagedReference())
				{
					num = 1;
					if (GetInt64ValueObfuscationSentinel() != null)
					{
						goto IL_0029;
					}
					goto IL_0048;
				}
				goto IL_0050;
				IL_0048:
				P_0 = P_0.ReadValue();
				goto IL_0050;
				IL_0050:
				while (!P_0.IsInt64Value())
				{
					num = 4;
					if (!IsInt64ValueObfuscationSentinelNull())
					{
						continue;
					}
					goto IL_0029;
				}
				break;
				IL_0029:
				while (true)
				{
					switch (num)
					{
					case 4:
						if (P_0.IsNumericValue())
						{
							num = 0;
							if (!IsInt64ValueObfuscationSentinelNull())
							{
								continue;
							}
							goto default;
						}
						throw new VmOperandException();
					case 1:
						break;
					case 3:
						goto IL_0050;
					case 2:
						goto IL_0069;
					default:
						return new Int64Value(Bits.Int64 >> ((NumericValue)P_0).ToInt32().Bits.Int32);
					case 5:
						goto end_IL_0069;
					}
					break;
				}
				goto IL_0048;
				continue;
				end_IL_0069:
				break;
			}
			return new Int64Value(Bits.Int64 >> ((Int64Value)P_0).Bits.Int32);
		}

		public override VmValue ShiftRightUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 5;
					if (IsInt64ValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_0032;
				}
				goto IL_0044;
				IL_0068:
				throw new VmOperandException();
				IL_0044:
				if (P_0.IsInt64Value())
				{
					break;
				}
				goto IL_0032;
				IL_0032:
				if (!P_0.IsNumericValue())
				{
					num = 0;
					if (!IsInt64ValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_0068;
				}
				goto IL_006e;
				IL_006e:
				return new Int64Value(Bits.UInt64 >> ((NumericValue)P_0).ToInt32().Bits.Int32);
				IL_000f:
				switch (num)
				{
				case 4:
					break;
				case 5:
					P_0 = P_0.ReadValue();
					goto IL_0044;
				case 3:
					goto IL_0044;
				case 6:
					continue;
				case 1:
					goto IL_0068;
				case 2:
					goto IL_006e;
				default:
					goto end_IL_005d;
				}
				goto IL_0032;
				continue;
				end_IL_005d:
				break;
			}
			return new Int64Value(Bits.UInt64 >> ((Int64Value)P_0).Bits.Int32);
		}

		public override string ToString()
		{
			while (PrimitiveType == (VmPrimitiveType)7)
			{
				if (GetInt64ValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return Bits.Int64.ToString();
			}
			return Bits.UInt64.ToString();
		}

		internal override VmValue ReadValue()
		{
			return this;
		}

		internal override bool IsIntegerValue()
		{
			return true;
		}

		internal override bool IsEqualTo(VmValue P_0)
		{
			while (!P_0.IsObjectValue())
			{
				while (true)
				{
					if (!P_0.IsManagedReference())
					{
						while (true)
						{
							VmValue VmValue = P_0.ReadValue();
							if (IsInt64ValueObfuscationSentinelNull())
							{
								switch (0)
								{
								case 2:
									break;
								case 3:
									goto end_IL_0003;
								case 4:
									goto end_IL_003e;
								default:
									goto IL_0053;
								case 5:
									goto IL_005c;
								case 7:
									goto IL_007b;
								case 1:
									goto IL_007d;
								case 6:
									goto end_IL_0048;
								}
								continue;
							}
							goto IL_0053;
							IL_007b:
							return false;
							IL_0053:
							if (VmValue.IsInt64Value())
							{
								goto IL_005c;
							}
							goto IL_007b;
							IL_005c:
							return Bits.Int64 == ((Int64Value)VmValue).Bits.Int64;
							continue;
							end_IL_0003:
							break;
						}
						continue;
					}
					goto IL_007d;
					IL_007d:
					return ((ReferenceValue)P_0).IsEqualTo(this);
					continue;
					end_IL_003e:
					break;
				}
				continue;
				end_IL_0048:
				break;
			}
			return ((ObjectValue)P_0).IsEqualTo(this);
		}

		private static NumericValue AsNumericComparisonValue(object P_0)
		{
			while (true)
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = P_0 as NumericValue;
				while (true)
				{
					IL_004e:
					if (c7EoQ7IJLjSNsQhooYs == null)
					{
						while (((VmValue)P_0).IsManagedReference())
						{
							int num = 1;
							if (IsInt64ValueObfuscationSentinelNull())
							{
								goto IL_0010;
							}
							goto IL_0029;
							IL_0010:
							c7EoQ7IJLjSNsQhooYs = ((VmValue)P_0).ReadValue() as NumericValue;
							num = 0;
							if (!IsInt64ValueObfuscationSentinelNull())
							{
								break;
							}
							goto IL_0029;
							IL_0029:
							switch (num)
							{
							case 1:
								break;
							case 4:
								continue;
							case 2:
								goto IL_004e;
							case 3:
								goto end_IL_004e;
							default:
								goto end_IL_0044;
							}
							goto IL_0010;
							continue;
							end_IL_0044:
							break;
						}
					}
					return c7EoQ7IJLjSNsQhooYs;
					continue;
					end_IL_004e:
					break;
				}
			}
		}

		internal override bool IsNotEqualTo(VmValue P_0)
		{
			while (true)
			{
				IL_0042:
				if (!P_0.IsObjectValue())
				{
					if (P_0.IsManagedReference())
					{
						break;
					}
					while (true)
					{
						VmValue VmValue = P_0.ReadValue();
						if (GetInt64ValueObfuscationSentinel() != null)
						{
							break;
						}
						switch (1)
						{
						case 6:
							break;
						case 3:
							goto IL_0042;
						case 1:
							if (VmValue.IsInt64Value())
							{
								goto case 4;
							}
							goto case 5;
						case 2:
							goto IL_0057;
						case 4:
							return Bits.UInt64 != ((Int64Value)VmValue).Bits.UInt64;
						case 5:
							return false;
						default:
							goto end_IL_0042;
						}
					}
					break;
				}
				goto IL_0057;
				IL_0057:
				return false;
				continue;
				end_IL_0042:
				break;
			}
			return ((ReferenceValue)P_0).IsNotEqualTo(this);
		}

		public override bool GreaterThanOrEqual(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					int num = 1;
					if (IsInt64ValueObfuscationSentinelNull())
					{
						while (true)
						{
							switch (num)
							{
							case 1:
								P_0 = P_0.ReadValue();
								num = 0;
								if (GetInt64ValueObfuscationSentinel() == null)
								{
									continue;
								}
								goto IL_004a;
							case 2:
								break;
							default:
								goto IL_004a;
							case 3:
								goto end_IL_003c;
							}
							break;
						}
						continue;
					}
				}
				goto IL_004a;
				IL_004a:
				if (P_0.IsInt64Value())
				{
					break;
				}
				throw new VmOperandException();
				continue;
				end_IL_003c:
				break;
			}
			return Bits.Int64 >= ((Int64Value)P_0).Bits.Int64;
		}

		public override bool GreaterThanOrEqualUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 0;
					if (GetInt64ValueObfuscationSentinel() != null)
					{
						goto IL_000f;
					}
					goto IL_0028;
				}
				goto IL_0032;
				IL_000f:
				switch (num)
				{
				case 3:
					goto IL_0032;
				case 1:
					continue;
				case 2:
					goto end_IL_004b;
				}
				goto IL_0028;
				IL_0032:
				P_0 = P_0.ReadValue();
				goto IL_0028;
				IL_0028:
				if (P_0.IsInt64Value())
				{
					num = 0;
					if (IsInt64ValueObfuscationSentinelNull())
					{
						break;
					}
					goto IL_000f;
				}
				throw new VmOperandException();
				continue;
				end_IL_004b:
				break;
			}
			return Bits.UInt64 >= ((Int64Value)P_0).Bits.UInt64;
		}

		public override bool GreaterThan(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (IsInt64ValueObfuscationSentinelNull())
					{
						goto IL_0010;
					}
					goto IL_0024;
				}
				goto IL_0046;
				IL_0046:
				if (P_0.IsInt64Value())
				{
					break;
				}
				throw new VmOperandException();
				IL_0024:
				switch (num)
				{
				case 1:
					break;
				case 2:
					continue;
				default:
					goto IL_0046;
				case 3:
					goto end_IL_003b;
				}
				goto IL_0010;
				IL_0010:
				P_0 = P_0.ReadValue();
				num = 0;
				if (!IsInt64ValueObfuscationSentinelNull())
				{
					goto IL_0024;
				}
				goto IL_0046;
				continue;
				end_IL_003b:
				break;
			}
			return Bits.Int64 > ((Int64Value)P_0).Bits.Int64;
		}

		public override bool GreaterThanUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (GetInt64ValueObfuscationSentinel() == null)
					{
						goto IL_000f;
					}
					goto IL_0028;
				}
				goto IL_0030;
				IL_0028:
				P_0 = P_0.ReadValue();
				goto IL_0030;
				IL_0030:
				if (P_0.IsInt64Value())
				{
					break;
				}
				num = 0;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					goto IL_000f;
				}
				goto IL_0054;
				IL_0054:
				throw new VmOperandException();
				IL_000f:
				switch (num)
				{
				case 4:
					goto IL_0030;
				case 1:
					continue;
				case 2:
					goto IL_0054;
				case 3:
					goto end_IL_0049;
				}
				goto IL_0028;
				continue;
				end_IL_0049:
				break;
			}
			return Bits.UInt64 > ((Int64Value)P_0).Bits.UInt64;
		}

		public override bool LessThanOrEqual(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0003;
				}
				goto IL_0044;
				IL_0003:
				P_0 = P_0.ReadValue();
				int num = 1;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					goto IL_0017;
				}
				goto IL_0044;
				IL_0044:
				if (P_0.IsInt64Value())
				{
					break;
				}
				num = 4;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					break;
				}
				goto IL_0017;
				IL_0017:
				switch (num)
				{
				case 2:
				case 5:
					goto IL_0044;
				case 3:
					continue;
				case 4:
					throw new VmOperandException();
				case 1:
					goto end_IL_0051;
				}
				goto IL_0003;
				continue;
				end_IL_0051:
				break;
			}
			return Bits.Int64 <= ((Int64Value)P_0).Bits.Int64;
		}

		public override bool LessThanOrEqualUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (!IsInt64ValueObfuscationSentinelNull())
					{
						goto IL_0010;
					}
					goto IL_0024;
				}
				goto IL_004c;
				IL_0054:
				throw new VmOperandException();
				IL_0024:
				switch (num)
				{
				case 1:
					continue;
				case 4:
					goto IL_004c;
				case 3:
					goto IL_0054;
				case 2:
					goto end_IL_003f;
				}
				goto IL_0010;
				IL_0010:
				P_0 = P_0.ReadValue();
				num = 4;
				if (!IsInt64ValueObfuscationSentinelNull())
				{
					break;
				}
				goto IL_0024;
				IL_004c:
				if (P_0.IsInt64Value())
				{
					break;
				}
				goto IL_0054;
				continue;
				end_IL_003f:
				break;
			}
			return Bits.UInt64 <= ((Int64Value)P_0).Bits.UInt64;
		}

		public override bool LessThan(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0011;
				}
				goto IL_0041;
				IL_0041:
				if (P_0.IsInt64Value())
				{
					break;
				}
				int num = 0;
				if (IsInt64ValueObfuscationSentinelNull())
				{
					goto IL_0025;
				}
				goto IL_0056;
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 0;
				if (GetInt64ValueObfuscationSentinel() != null)
				{
					goto IL_0025;
				}
				goto IL_0041;
				IL_0025:
				switch (num)
				{
				case 3:
					break;
				case 1:
					goto IL_0041;
				case 4:
					continue;
				default:
					goto IL_0056;
				case 2:
					goto end_IL_004b;
				}
				goto IL_0011;
				IL_0056:
				throw new VmOperandException();
				continue;
				end_IL_004b:
				break;
			}
			return Bits.Int64 < ((Int64Value)P_0).Bits.Int64;
		}

		public override bool LessThanUnsigned(VmValue P_0)
		{
			while (true)
			{
				IL_0042:
				if (!P_0.IsManagedReference())
				{
					goto IL_002e;
				}
				goto IL_0038;
				IL_0038:
				P_0 = P_0.ReadValue();
				goto IL_002e;
				IL_002e:
				while (true)
				{
					if (!P_0.IsInt64Value())
					{
						if (!IsInt64ValueObfuscationSentinelNull())
						{
							switch (0)
							{
							case 2:
							case 4:
								break;
							case 5:
								goto end_IL_002e;
							case 3:
								goto IL_0042;
							default:
								goto IL_004d;
							case 1:
								goto IL_0053;
							}
							continue;
						}
						goto IL_004d;
					}
					goto IL_0053;
					IL_004d:
					throw new VmOperandException();
					IL_0053:
					return Bits.UInt64 < ((Int64Value)P_0).Bits.UInt64;
					continue;
					end_IL_002e:
					break;
				}
				goto IL_0038;
			}
		}

		internal static bool IsInt64ValueObfuscationSentinelNull()
		{
			return Int64ValueObfuscationSentinel == null;
		}

		internal static Int64Value GetInt64ValueObfuscationSentinel()
		{
			return Int64ValueObfuscationSentinel;
		}
	}

	private class NativeIntegerValue : NumericValue
	{
		public NumericValue IntegerStorage;

		public VmPrimitiveType PrimitiveType;

		private static NativeIntegerValue NativeIntegerValueObfuscationSentinel;

		internal void CopyNativeIntegerPayload(VmValue P_0)
		{
			while (true)
			{
				if (!P_0.IsNativeIntegerValue())
				{
					goto IL_0003;
				}
				goto IL_0054;
				IL_0054:
				IntegerStorage = ((NativeIntegerValue)P_0).IntegerStorage;
				int num = 3;
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					goto IL_0018;
				}
				goto IL_0035;
				IL_0035:
				switch (num)
				{
				case 4:
					break;
				case 3:
					goto IL_0018;
				default:
					return;
				case 2:
					goto IL_0054;
				case 5:
					continue;
				case 0:
					return;
				case 1:
					return;
				}
				goto IL_0003;
				IL_0018:
				PrimitiveType = ((NativeIntegerValue)P_0).PrimitiveType;
				num = 0;
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					break;
				}
				goto IL_0035;
				IL_0003:
				CopyFrom(P_0);
				num = 0;
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					break;
				}
				goto IL_0035;
			}
		}

		internal unsafe override void CopyFrom(VmValue P_0)
		{
			IntPtr intPtr = default(IntPtr);
			object obj = default(object);
			Type type = default(Type);
			while (true)
			{
				if (!P_0.IsNativeIntegerValue())
				{
					goto IL_0213;
				}
				goto IL_02ff;
				IL_02ff:
				if (IntPtr.Size != 8)
				{
					break;
				}
				int num = 3;
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0228;
				}
				goto IL_031d;
				IL_00c5:
				*(int*)(void*)intPtr = (int)obj;
				num = 35;
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					return;
				}
				goto IL_0228;
				IL_0228:
				switch (num)
				{
				case 49:
					break;
				case 13:
					goto IL_0030;
				case 27:
					goto IL_0046;
				case 7:
					goto IL_0059;
				case 46:
					goto IL_00a2;
				case 20:
					goto IL_00c5;
				case 43:
					goto IL_00e8;
				case 42:
					goto IL_0118;
				case 11:
					goto IL_0122;
				case 1:
				case 35:
					goto IL_013e;
				case 41:
					goto IL_0157;
				case 30:
					goto IL_01bd;
				case 33:
					goto IL_01f3;
				case 37:
					goto IL_0213;
				case 34:
					goto IL_021c;
				default:
					return;
				case 21:
					goto IL_02ff;
				case 38:
					continue;
				case 8:
					goto IL_031d;
				case 16:
					return;
				case 17:
					return;
				case 23:
					return;
				case 26:
					return;
				case 15:
					return;
				case 14:
					return;
				case 28:
					goto IL_0372;
				case 4:
					return;
				case 39:
					goto IL_0382;
				case 0:
					return;
				case 48:
					return;
				case 31:
					goto IL_03b2;
				case 18:
					goto IL_03c8;
				case 5:
					goto IL_03db;
				case 36:
					goto IL_03ee;
				case 29:
					return;
				case 24:
					goto IL_0411;
				case 12:
					goto IL_0424;
				case 9:
					goto IL_042a;
				case 22:
					return;
				case 2:
					goto IL_043a;
				case 3:
					return;
				case 6:
					goto IL_044a;
				case 25:
					return;
				case 32:
					goto IL_045a;
				case 40:
					return;
				case 10:
					goto IL_046a;
				case 45:
					return;
				case 44:
					return;
				case 50:
					goto IL_047c;
				case 19:
					return;
				case 47:
					return;
				}
				goto IL_001a;
				IL_031d:
				IntPtr intPtr2 = new IntPtr(((Int64Value)IntegerStorage).Bits.Int64);
				IntPtr intPtr3 = new IntPtr(((Int64Value)((NativeIntegerValue)P_0).IntegerStorage).Bits.Int64);
				*(long*)(void*)intPtr2 = intPtr3.ToInt64();
				return;
				IL_0213:
				obj = P_0.ToObject(null);
				goto IL_021c;
				IL_021c:
				if (obj != null)
				{
					goto IL_0118;
				}
				return;
				IL_0118:
				if (IntPtr.Size == 8)
				{
					goto IL_00e8;
				}
				goto IL_0122;
				IL_0122:
				intPtr = new IntPtr(((Int32Value)IntegerStorage).Bits.Int32);
				goto IL_013e;
				IL_013e:
				type = obj.GetType();
				num = 32;
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0157;
				}
				goto IL_0228;
				IL_0157:
				if (type == typeof(string))
				{
					num = 26;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_0228;
					}
					goto IL_03db;
				}
				if (!(type == typeof(byte)))
				{
					if (!(type == typeof(sbyte)))
					{
						if (type == typeof(short))
						{
							goto IL_01bd;
						}
						if (!(type == typeof(ushort)))
						{
							goto IL_001a;
						}
						goto IL_01f3;
					}
					goto IL_0372;
				}
				goto IL_0382;
				IL_0411:
				if (!(type == typeof(char)))
				{
					goto IL_0424;
				}
				goto IL_042a;
				IL_0424:
				throw new VmOperandException();
				IL_042a:
				*(char*)(void*)intPtr = (char)obj;
				return;
				IL_047c:
				*(UIntPtr*)(void*)intPtr = (UIntPtr)obj;
				return;
				IL_01bd:
				*(short*)(void*)intPtr = (short)obj;
				num = 6;
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					return;
				}
				goto IL_0228;
				IL_043a:
				*(bool*)(void*)intPtr = (bool)obj;
				return;
				IL_01f3:
				*(ushort*)(void*)intPtr = (ushort)obj;
				num = 0;
				if (GetNativeIntegerValueObfuscationSentinel() != null)
				{
					return;
				}
				goto IL_0228;
				IL_0372:
				*(sbyte*)(void*)intPtr = (sbyte)obj;
				return;
				IL_0382:
				*(byte*)(void*)intPtr = (byte)obj;
				return;
				IL_00e8:
				intPtr = new IntPtr(((Int64Value)IntegerStorage).Bits.Int64);
				num = 1;
				if (GetNativeIntegerValueObfuscationSentinel() != null)
				{
					return;
				}
				goto IL_0228;
				IL_001a:
				if (!(type == typeof(int)))
				{
					goto IL_0030;
				}
				goto IL_00c5;
				IL_0030:
				if (!(type == typeof(uint)))
				{
					goto IL_0046;
				}
				goto IL_046a;
				IL_0046:
				if (type == typeof(long))
				{
					goto IL_0059;
				}
				if (type == typeof(ulong))
				{
					num = 16;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_00a2;
					}
					goto IL_0228;
				}
				if (!(type == typeof(float)))
				{
					goto IL_03b2;
				}
				goto IL_045a;
				IL_0059:
				*(long*)(void*)intPtr = (long)obj;
				num = 17;
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0228;
				}
				goto IL_043a;
				IL_044a:
				*(double*)(void*)intPtr = (double)obj;
				return;
				IL_045a:
				*(float*)(void*)intPtr = (float)obj;
				return;
				IL_00a2:
				*(ulong*)(void*)intPtr = (ulong)obj;
				num = 23;
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0228;
				}
				goto IL_02ff;
				IL_046a:
				*(uint*)(void*)intPtr = (uint)obj;
				return;
				IL_03b2:
				if (!(type == typeof(double)))
				{
					goto IL_03c8;
				}
				goto IL_044a;
				IL_03c8:
				if (!(type == typeof(bool)))
				{
					goto IL_03db;
				}
				goto IL_043a;
				IL_03db:
				if (type == typeof(IntPtr))
				{
					goto IL_03ee;
				}
				if (!(type == typeof(UIntPtr)))
				{
					goto IL_0411;
				}
				goto IL_047c;
				IL_03ee:
				*(IntPtr*)(void*)intPtr = (IntPtr)obj;
				return;
			}
			IntPtr intPtr4 = new IntPtr(((Int32Value)IntegerStorage).Bits.Int32);
			IntPtr intPtr5 = new IntPtr(((Int32Value)((NativeIntegerValue)P_0).IntegerStorage).Bits.Int32);
			*(int*)(void*)intPtr4 = intPtr5.ToInt32();
		}

		internal override void Assign(VmValue P_0)
		{
			while (true)
			{
				CopyFrom(P_0);
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		public NativeIntegerValue(IntPtr P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private NativeIntegerValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, IntPtr P_0)
			: base()
		{
			Kind = (VmValueKind)3;
			if (IntPtr.Size == 8)
			{
				IntegerStorage = new Int64Value(P_0.ToInt64());
				PrimitiveType = (VmPrimitiveType)12;
			}
			else
			{
				IntegerStorage = new Int32Value(P_0.ToInt32());
				PrimitiveType = (VmPrimitiveType)12;
			}
		}

		public NativeIntegerValue(UIntPtr P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private NativeIntegerValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, UIntPtr P_0)
			: base()
		{
			Kind = (VmValueKind)3;
			if (IntPtr.Size == 8)
			{
				IntegerStorage = new Int64Value(P_0.ToUInt64());
				PrimitiveType = (VmPrimitiveType)12;
			}
			else
			{
				IntegerStorage = new Int32Value(P_0.ToUInt32());
				PrimitiveType = (VmPrimitiveType)12;
			}
		}

		public NativeIntegerValue()
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
		{
		}

		private NativeIntegerValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
			: base()
		{
			Kind = (VmValueKind)3;
			if (IntPtr.Size == 8)
			{
				IntegerStorage = new Int64Value(0L);
				PrimitiveType = (VmPrimitiveType)12;
			}
			else
			{
				IntegerStorage = new Int32Value(0);
				PrimitiveType = (VmPrimitiveType)12;
			}
		}

		public override NumericValue CloneNumericValue()
		{
			return new NativeIntegerValue
			{
				IntegerStorage = IntegerStorage.CloneNumericValue(),
				PrimitiveType = PrimitiveType
			};
		}

		public NativeIntegerValue(long P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private NativeIntegerValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, long P_0)
			: base()
		{
			Kind = (VmValueKind)3;
			if (IntPtr.Size == 8)
			{
				IntegerStorage = new Int64Value(P_0);
				PrimitiveType = (VmPrimitiveType)12;
			}
			else
			{
				IntegerStorage = new Int32Value((int)P_0);
				PrimitiveType = (VmPrimitiveType)12;
			}
		}

		public NativeIntegerValue(long P_0, VmPrimitiveType P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private NativeIntegerValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, long P_0, VmPrimitiveType P_1)
			: base()
		{
			Kind = (VmValueKind)3;
			if (IntPtr.Size == 8)
			{
				IntegerStorage = new Int64Value(P_0);
				PrimitiveType = P_1;
			}
			else
			{
				IntegerStorage = new Int32Value((int)P_0);
				PrimitiveType = P_1;
			}
		}

		public NativeIntegerValue(ulong P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private NativeIntegerValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, ulong P_0)
			: base()
		{
			Kind = (VmValueKind)4;
			if (IntPtr.Size == 8)
			{
				IntegerStorage = new Int64Value(P_0);
				PrimitiveType = (VmPrimitiveType)13;
			}
			else
			{
				IntegerStorage = new Int32Value((uint)P_0);
				PrimitiveType = (VmPrimitiveType)13;
			}
		}

		public NativeIntegerValue(ulong P_0, VmPrimitiveType P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private NativeIntegerValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, ulong P_0, VmPrimitiveType P_1)
			: base()
		{
			Kind = (VmValueKind)4;
			if (IntPtr.Size == 8)
			{
				IntegerStorage = new Int64Value(P_0);
				PrimitiveType = P_1;
			}
			else
			{
				IntegerStorage = new Int32Value((uint)P_0);
				PrimitiveType = P_1;
			}
		}

		public override bool IsZero()
		{
			return IntegerStorage.IsZero();
		}

		public override bool IsNonZero()
		{
			return !IsZero();
		}

		internal override bool IsTruthy()
		{
			return IsNonZero();
		}

		internal override bool IsAddressValue()
		{
			return true;
		}

		public override VmValue ConvertToPrimitiveType(VmPrimitiveType P_0)
		{
			VmDiagnosticCode VmDiagnosticCode = default(VmDiagnosticCode);
			while (true)
			{
				int num;
				switch (P_0)
				{
				case (VmPrimitiveType)9:
				case (VmPrimitiveType)10:
				case (VmPrimitiveType)14:
				case (VmPrimitiveType)15:
					VmDiagnosticCode = (VmDiagnosticCode)4;
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_0015;
					}
					goto IL_00ce;
				default:
					num = 1;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0015;
					}
					goto IL_00ce;
				case (VmPrimitiveType)1:
					return ToSByte();
				case (VmPrimitiveType)2:
					return ToByte();
				case (VmPrimitiveType)3:
					return ToInt16();
				case (VmPrimitiveType)4:
					return ToUInt16();
				case (VmPrimitiveType)5:
					return ToInt32();
				case (VmPrimitiveType)6:
					return ToUInt32();
				case (VmPrimitiveType)7:
					return ToInt64();
				case (VmPrimitiveType)8:
					return ToUInt64();
				case (VmPrimitiveType)11:
					return ToBooleanValue();
				case (VmPrimitiveType)13:
					return this;
				case (VmPrimitiveType)12:
					goto IL_00cb;
				case (VmPrimitiveType)16:
					{
						return CloneNumericValue();
					}
					IL_0015:
					switch (num)
					{
					case 1:
						break;
					case 2:
						goto end_IL_0040;
					case 3:
						goto IL_00cb;
					default:
						goto IL_00ce;
					}
					goto case (VmPrimitiveType)9;
					IL_00cb:
					return this;
					IL_00ce:
					throw new Exception(VmDiagnosticCode.ToString());
					end_IL_0040:
					break;
				}
			}
		}

		internal IntPtr ToIntPtr()
		{
			while (IntPtr.Size == 8)
			{
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new IntPtr(((Int64Value)IntegerStorage).Bits.Int64);
			}
			return new IntPtr(((Int32Value)IntegerStorage).Bits.Int32);
		}

		internal override object ToObject(Type P_0)
		{
			while (true)
			{
				int num;
				if (P_0 != null)
				{
					num = 10;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_006a;
					}
					goto IL_00e3;
				}
				goto IL_012e;
				IL_00b1:
				if (IntPtr.Size == 8)
				{
					goto IL_004c;
				}
				if (PrimitiveType == (VmPrimitiveType)12)
				{
					goto IL_01fe;
				}
				return new UIntPtr(((Int32Value)IntegerStorage).Bits.UInt32);
				IL_012e:
				if (!(P_0 == typeof(IntPtr)))
				{
					if (P_0 == typeof(UIntPtr))
					{
						goto IL_002d;
					}
					num = 2;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_00be;
					}
					goto IL_00e3;
				}
				goto IL_00c9;
				IL_0083:
				P_0 = P_0.GetElementType();
				num = 0;
				if (GetNativeIntegerValueObfuscationSentinel() != null)
				{
					goto IL_00e3;
				}
				goto IL_012e;
				IL_004c:
				if (PrimitiveType == (VmPrimitiveType)12)
				{
					num = 4;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_00e3;
					}
					goto IL_01fe;
				}
				return new UIntPtr(((Int64Value)IntegerStorage).Bits.UInt64);
				IL_002d:
				if (IntPtr.Size != 8)
				{
					break;
				}
				num = 5;
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_00e3;
				}
				goto IL_0185;
				IL_01f7:
				throw new VmOperandException();
				IL_00e3:
				switch (num)
				{
				case 3:
					break;
				case 14:
					goto IL_004c;
				case 12:
					goto IL_006a;
				case 7:
					goto IL_0083;
				case 11:
					goto IL_009c;
				case 10:
					goto IL_00b1;
				case 9:
					goto IL_00be;
				case 8:
					goto IL_00c9;
				default:
					goto IL_012e;
				case 13:
					continue;
				case 4:
					return new IntPtr(((Int64Value)IntegerStorage).Bits.Int64);
				case 6:
					goto IL_0185;
				case 5:
					goto IL_01f7;
				case 2:
					goto IL_01fe;
				case 15:
					goto IL_023e;
				}
				goto IL_002d;
				IL_0185:
				return new UIntPtr(((Int64Value)IntegerStorage).Bits.UInt64);
				IL_00c9:
				if (IntPtr.Size == 8)
				{
					num = 15;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_00e3;
					}
					goto IL_023e;
				}
				return new IntPtr(((Int32Value)IntegerStorage).Bits.Int32);
				IL_006a:
				if (P_0.IsByRef)
				{
					goto IL_0083;
				}
				num = 1;
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					goto IL_00e3;
				}
				goto IL_012e;
				IL_023e:
				return new IntPtr(((Int64Value)IntegerStorage).Bits.Int64);
				IL_01fe:
				return new IntPtr(((Int64Value)IntegerStorage).Bits.Int32);
				IL_00be:
				if (!(P_0 == null))
				{
					goto IL_009c;
				}
				goto IL_00b1;
				IL_009c:
				if (P_0 == typeof(object))
				{
					goto IL_00b1;
				}
				goto IL_01f7;
			}
			return new UIntPtr(((Int32Value)IntegerStorage).Bits.UInt32);
		}

		public override Int32Value ToBooleanValue()
		{
			return IntegerStorage.ToBooleanValue();
		}

		public override Int32Value ToSByte()
		{
			return IntegerStorage.ToSByte();
		}

		public override Int32Value ToByte()
		{
			return IntegerStorage.ToByte();
		}

		public override Int32Value ToInt16()
		{
			return IntegerStorage.ToInt16();
		}

		public override Int32Value ToUInt16()
		{
			return IntegerStorage.ToUInt16();
		}

		public override Int32Value ToInt32()
		{
			return IntegerStorage.ToInt32();
		}

		public override Int32Value ToUInt32()
		{
			return IntegerStorage.ToUInt32();
		}

		public override Int64Value ToInt64()
		{
			return IntegerStorage.ToInt64();
		}

		public override Int64Value ToUInt64()
		{
			return IntegerStorage.ToUInt64();
		}

		public override Int32Value ConvertToSByte()
		{
			return ToSByte();
		}

		public override Int32Value ConvertToInt16()
		{
			return ToInt16();
		}

		public override Int32Value ConvertToInt32()
		{
			return ToInt32();
		}

		public override Int64Value ConvertToInt64()
		{
			return ToInt64();
		}

		public override Int32Value ConvertToByte()
		{
			return ToByte();
		}

		public override Int32Value ConvertToUInt16()
		{
			return ToUInt16();
		}

		public override Int32Value ConvertToUInt32()
		{
			return ToUInt32();
		}

		public override Int64Value ConvertToUInt64()
		{
			return ToUInt64();
		}

		public override Int32Value ToSByteChecked()
		{
			return IntegerStorage.ToSByteChecked();
		}

		public override Int32Value ToSByteCheckedUnsigned()
		{
			return IntegerStorage.ToSByteCheckedUnsigned();
		}

		public override Int32Value ToInt16Checked()
		{
			return IntegerStorage.ToInt16Checked();
		}

		public override Int32Value ToInt16CheckedUnsigned()
		{
			return IntegerStorage.ToInt16CheckedUnsigned();
		}

		public override Int32Value ToInt32Checked()
		{
			return IntegerStorage.ToInt32Checked();
		}

		public override Int32Value ToInt32CheckedUnsigned()
		{
			return IntegerStorage.ToInt32CheckedUnsigned();
		}

		public override Int64Value ToInt64Checked()
		{
			return IntegerStorage.ToInt64Checked();
		}

		public override Int64Value ToInt64CheckedUnsigned()
		{
			return IntegerStorage.ToInt64CheckedUnsigned();
		}

		public override Int32Value ToByteChecked()
		{
			return IntegerStorage.ToByteChecked();
		}

		public override Int32Value ToByteCheckedUnsigned()
		{
			return IntegerStorage.ToByteCheckedUnsigned();
		}

		public override Int32Value ToUInt16Checked()
		{
			return IntegerStorage.ToUInt16Checked();
		}

		public override Int32Value ToUInt16CheckedUnsigned()
		{
			return IntegerStorage.ToUInt16CheckedUnsigned();
		}

		public override Int32Value ToUInt32Checked()
		{
			return IntegerStorage.ToUInt32Checked();
		}

		public override Int32Value ToUInt32CheckedUnsigned()
		{
			return IntegerStorage.ToUInt32CheckedUnsigned();
		}

		public override Int64Value ToUInt64Checked()
		{
			return IntegerStorage.ToUInt64Checked();
		}

		public override Int64Value ToUInt64CheckedUnsigned()
		{
			return IntegerStorage.ToUInt64CheckedUnsigned();
		}

		public override FloatingPointValue ToSingle()
		{
			return IntegerStorage.ToSingle();
		}

		public override FloatingPointValue ToDouble()
		{
			return IntegerStorage.ToDouble();
		}

		public override FloatingPointValue ToDoubleUnsigned()
		{
			return IntegerStorage.ToDoubleUnsigned();
		}

		public override NativeIntegerValue ToNativeInt()
		{
			while (IntPtr.Size == 8)
			{
				if (GetNativeIntegerValueObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ConvertToInt64().Bits.Int64);
			}
			return new NativeIntegerValue(ConvertToInt32().Bits.Int32);
		}

		public override NativeIntegerValue ToNativeUInt()
		{
			while (IntPtr.Size == 8)
			{
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ConvertToUInt64().Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)ConvertToUInt32().Bits.UInt32);
		}

		public override NativeIntegerValue ToNativeIntChecked()
		{
			while (IntPtr.Size == 8)
			{
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToInt64Checked().Bits.Int64);
			}
			return new NativeIntegerValue(ToInt32Checked().Bits.Int32);
		}

		public override NativeIntegerValue ToNativeUIntChecked()
		{
			while (IntPtr.Size == 8)
			{
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToUInt64Checked().Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)ToUInt32Checked().Bits.UInt32);
		}

		public override NativeIntegerValue ToNativeIntCheckedUnsigned()
		{
			while (IntPtr.Size == 8)
			{
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToInt64CheckedUnsigned().Bits.Int64);
			}
			return new NativeIntegerValue(ToInt32CheckedUnsigned().Bits.Int32);
		}

		public override NativeIntegerValue ToNativeUIntCheckedUnsigned()
		{
			while (IntPtr.Size == 8)
			{
				if (GetNativeIntegerValueObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToUInt64CheckedUnsigned().Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)ToUInt32CheckedUnsigned().Bits.UInt32);
		}

		public override VmValue Negate()
		{
			while (IntPtr.Size == 8)
			{
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(-((Int64Value)IntegerStorage).Bits.Int64);
			}
			return new NativeIntegerValue(-((Int32Value)IntegerStorage).Bits.Int32);
		}

		public override VmValue Add(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 2;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0055;
					}
					goto IL_008f;
				}
				goto IL_009c;
				IL_0055:
				switch (num)
				{
				case 8:
					break;
				case 7:
					goto IL_003b;
				case 2:
				case 5:
					goto IL_008f;
				case 6:
					goto IL_009c;
				case 3:
					continue;
				default:
					goto IL_00c5;
				case 4:
					goto IL_00cb;
				case 1:
					goto IL_00f8;
				}
				goto IL_001f;
				IL_009c:
				P_0 = P_0.ReadValue();
				goto IL_008f;
				IL_008f:
				if (!P_0.IsInt32Value())
				{
					if (P_0.IsNativeIntegerValue())
					{
						goto IL_001f;
					}
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_00c5;
					}
				}
				else
				{
					num = 7;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_003b;
					}
				}
				goto IL_0055;
				IL_00cb:
				return new NativeIntegerValue(ToInt64().Bits.Int64 + ((NativeIntegerValue)P_0).ToInt64().Bits.Int64);
				IL_00c5:
				throw new VmOperandException();
				IL_003b:
				if (IntPtr.Size == 8)
				{
					num = 1;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0055;
					}
					goto IL_00f8;
				}
				return new NativeIntegerValue(ToInt32().Bits.Int32 + ((Int32Value)P_0).Bits.Int32);
				IL_001f:
				if (IntPtr.Size != 8)
				{
					break;
				}
				num = 2;
				if (GetNativeIntegerValueObfuscationSentinel() != null)
				{
					goto IL_0055;
				}
				goto IL_00cb;
				IL_00f8:
				return new NativeIntegerValue(ToInt64().Bits.Int64 + ((Int32Value)P_0).ToInt64().Bits.Int64);
			}
			return new NativeIntegerValue(ToInt32().Bits.Int32 + ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
		}

		public override VmValue AddChecked(VmValue P_0)
		{
			checked
			{
				while (true)
				{
					int num;
					if (!P_0.IsManagedReference())
					{
						num = 0;
						if (GetNativeIntegerValueObfuscationSentinel() != null)
						{
							goto IL_001c;
						}
						goto IL_0054;
					}
					goto IL_005e;
					IL_00e2:
					throw new VmOperandException();
					IL_005e:
					P_0 = P_0.ReadValue();
					goto IL_0054;
					IL_0054:
					if (!P_0.IsInt32Value())
					{
						if (P_0.IsNativeIntegerValue())
						{
							goto IL_0047;
						}
						goto IL_00e2;
					}
					goto IL_00b0;
					IL_00b0:
					if (IntPtr.Size == 8)
					{
						goto IL_00e8;
					}
					return new NativeIntegerValue(ToInt32().Bits.Int32 + ((Int32Value)P_0).Bits.Int32);
					IL_0047:
					if (IntPtr.Size != 8)
					{
						break;
					}
					num = 3;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						continue;
					}
					goto IL_001c;
					IL_00e8:
					return new NativeIntegerValue(ToInt64().Bits.Int64 + ((Int32Value)P_0).ToInt64().Bits.Int64);
					IL_001c:
					switch (num)
					{
					case 2:
						break;
					default:
						goto IL_0054;
					case 5:
						goto IL_005e;
					case 1:
						continue;
					case 3:
						return new NativeIntegerValue(ToInt64().Bits.Int64 + ((NativeIntegerValue)P_0).ToInt64().Bits.Int64);
					case 6:
						goto IL_00b0;
					case 7:
						goto IL_00e2;
					case 8:
						goto IL_00e8;
					}
					goto IL_0047;
				}
				return new NativeIntegerValue(ToInt32().Bits.Int32 + ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
			}
		}

		public override VmValue AddCheckedUnsigned(VmValue P_0)
		{
			checked
			{
				while (true)
				{
					int num;
					if (P_0.IsManagedReference())
					{
						num = 0;
						if (GetNativeIntegerValueObfuscationSentinel() == null)
						{
							goto IL_0053;
						}
						goto IL_0067;
					}
					goto IL_008b;
					IL_0049:
					unchecked
					{
						do
						{
							if (IntPtr.Size == 8)
							{
								num = 4;
								continue;
							}
							return new NativeIntegerValue((ulong)checked(ToInt32().Bits.UInt32 + ((NativeIntegerValue)P_0).ToInt32().Bits.UInt32));
						}
						while (GetNativeIntegerValueObfuscationSentinel() != null);
						goto IL_0067;
					}
					IL_008b:
					if (!P_0.IsInt32Value())
					{
						if (P_0.IsNativeIntegerValue())
						{
							num = 0;
							if (GetNativeIntegerValueObfuscationSentinel() == null)
							{
								goto IL_0049;
							}
							goto IL_0067;
						}
						throw new VmOperandException();
					}
					goto IL_0020;
					IL_0020:
					if (IntPtr.Size != 8)
					{
						break;
					}
					num = 2;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0067;
					}
					goto IL_010b;
					IL_010b:
					return new NativeIntegerValue(ToInt64().Bits.UInt64 + ((Int32Value)P_0).Bits.UInt32);
					IL_0067:
					switch (num)
					{
					case 6:
						break;
					case 2:
						goto IL_0049;
					default:
						goto IL_0053;
					case 3:
						goto IL_008b;
					case 1:
						continue;
					case 4:
						return new NativeIntegerValue(ToInt64().Bits.UInt64 + ((NativeIntegerValue)P_0).ToInt64().Bits.UInt64);
					case 5:
						goto IL_010b;
					}
					goto IL_0020;
					IL_0053:
					P_0 = P_0.ReadValue();
					num = 0;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0067;
					}
					goto IL_008b;
				}
				return new NativeIntegerValue(ToInt32().Bits.UInt32 + ((Int32Value)P_0).Bits.UInt32);
			}
		}

		public override VmValue Subtract(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_0027;
					}
					goto IL_004c;
				}
				goto IL_0054;
				IL_0083:
				return new NativeIntegerValue(ToInt64().Bits.Int64 - ((NativeIntegerValue)P_0).ToInt64().Bits.Int64);
				IL_0054:
				if (!P_0.IsInt32Value())
				{
					num = 2;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_0010;
					}
					goto IL_0027;
				}
				goto IL_00e2;
				IL_00ea:
				return new NativeIntegerValue(ToInt64().Bits.Int64 - ((Int32Value)P_0).ToInt64().Bits.Int64);
				IL_0027:
				switch (num)
				{
				case 5:
					break;
				default:
					goto IL_004c;
				case 4:
					goto IL_0054;
				case 1:
					continue;
				case 2:
					goto IL_007b;
				case 3:
					goto IL_0083;
				case 7:
					goto IL_00e2;
				case 6:
					goto IL_00ea;
				}
				goto IL_0010;
				IL_004c:
				P_0 = P_0.ReadValue();
				goto IL_0054;
				IL_0010:
				if (P_0.IsNativeIntegerValue())
				{
					num = 2;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0027;
					}
					goto IL_007b;
				}
				throw new VmOperandException();
				IL_00e2:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_00ea;
				IL_007b:
				if (IntPtr.Size != 8)
				{
					return new NativeIntegerValue(ToInt32().Bits.Int32 - ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
				}
				goto IL_0083;
			}
			return new NativeIntegerValue(ToInt32().Bits.Int32 - ((Int32Value)P_0).Bits.Int32);
		}

		public VmValue SubtractFrom(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_0003;
					}
					goto IL_0022;
				}
				goto IL_004d;
				IL_0088:
				return new NativeIntegerValue(((NativeIntegerValue)P_0).ToInt64().Bits.Int64 - ToInt64().Bits.Int64);
				IL_004d:
				P_0 = P_0.ReadValue();
				num = 0;
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					goto IL_0003;
				}
				goto IL_0022;
				IL_0022:
				switch (num)
				{
				case 1:
					goto IL_004d;
				case 4:
					continue;
				case 6:
					goto IL_0080;
				case 5:
					goto IL_0088;
				case 7:
					goto IL_00e1;
				case 8:
					goto IL_00e7;
				case 2:
					goto IL_00ef;
				}
				goto IL_0003;
				IL_0003:
				if (!P_0.IsInt32Value())
				{
					if (P_0.IsNativeIntegerValue())
					{
						goto IL_0080;
					}
					num = 7;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0022;
					}
					goto IL_00e1;
				}
				goto IL_00e7;
				IL_00ef:
				return new NativeIntegerValue(((Int32Value)P_0).ToInt64().Bits.Int64 - ToInt64().Bits.Int64);
				IL_00e7:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_00ef;
				IL_00e1:
				throw new VmOperandException();
				IL_0080:
				if (IntPtr.Size != 8)
				{
					return new NativeIntegerValue(((NativeIntegerValue)P_0).ToInt32().Bits.Int32 - ToInt32().Bits.Int32);
				}
				goto IL_0088;
			}
			return new NativeIntegerValue(((Int32Value)P_0).Bits.Int32 - ToInt32().Bits.Int32);
		}

		public override VmValue SubtractChecked(VmValue P_0)
		{
			checked
			{
				while (true)
				{
					IL_00aa:
					if (!P_0.IsManagedReference())
					{
						goto IL_0085;
					}
					goto IL_0090;
					IL_0090:
					P_0 = P_0.ReadValue();
					int num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_0015;
					}
					goto IL_0085;
					IL_0015:
					while (true)
					{
						switch (num)
						{
						case 1:
							break;
						case 4:
							if (IntPtr.Size == 8)
							{
								num = 0;
								if (GetNativeIntegerValueObfuscationSentinel() != null)
								{
									continue;
								}
								goto case 3;
							}
							return new NativeIntegerValue(ToInt32().Bits.Int32 - ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
						default:
							goto IL_0085;
						case 2:
							goto IL_0090;
						case 7:
							goto IL_00aa;
						case 3:
							return new NativeIntegerValue(ToInt64().Bits.Int64 - ((NativeIntegerValue)P_0).ToInt64().Bits.Int64);
						case 5:
							goto IL_010f;
						}
						break;
					}
					goto IL_0049;
					IL_0085:
					if (P_0.IsInt32Value())
					{
						num = 0;
						if (GetNativeIntegerValueObfuscationSentinel() == null)
						{
							goto IL_0049;
						}
					}
					else
					{
						if (!P_0.IsNativeIntegerValue())
						{
							throw new VmOperandException();
						}
						num = 4;
						if (!IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_0090;
						}
					}
					goto IL_0015;
					IL_0049:
					if (IntPtr.Size != 8)
					{
						break;
					}
					num = 5;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0015;
					}
					goto IL_010f;
					IL_010f:
					return new NativeIntegerValue(ToInt64().Bits.Int64 - ((Int32Value)P_0).ToInt64().Bits.Int64);
				}
				return new NativeIntegerValue(ToInt32().Bits.Int32 - ((Int32Value)P_0).Bits.Int32);
			}
		}

		public VmValue SubtractFromChecked(VmValue P_0)
		{
			checked
			{
				while (true)
				{
					int num;
					if (!P_0.IsManagedReference())
					{
						num = 0;
						if (IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_0003;
						}
						goto IL_0034;
					}
					goto IL_005b;
					IL_00ed:
					if (IntPtr.Size != 8)
					{
						break;
					}
					goto IL_00f5;
					IL_005b:
					P_0 = P_0.ReadValue();
					num = 0;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0003;
					}
					goto IL_0034;
					IL_0034:
					switch (num)
					{
					case 2:
						goto IL_002a;
					case 5:
						goto IL_005b;
					case 1:
						continue;
					case 4:
						return new NativeIntegerValue(((Int32Value)P_0).ToInt64().Bits.Int64 - ToInt64().Bits.Int64);
					case 7:
						goto IL_00ed;
					case 6:
						goto IL_00f5;
					}
					goto IL_0003;
					IL_0003:
					if (P_0.IsInt32Value())
					{
						num = 1;
						if (IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_002a;
						}
						goto IL_0034;
					}
					if (!P_0.IsNativeIntegerValue())
					{
						throw new VmOperandException();
					}
					goto IL_00ed;
					IL_00f5:
					return new NativeIntegerValue(((NativeIntegerValue)P_0).ToInt64().Bits.Int64 - ToInt64().Bits.Int64);
					IL_002a:
					do
					{
						if (IntPtr.Size == 8)
						{
							num = 4;
							continue;
						}
						return new NativeIntegerValue(((Int32Value)P_0).Bits.Int32 - ToInt32().Bits.Int32);
					}
					while (!IsNativeIntegerValueObfuscationSentinelNull());
					goto IL_0034;
				}
				return new NativeIntegerValue(((NativeIntegerValue)P_0).ToInt32().Bits.Int32 - ToInt32().Bits.Int32);
			}
		}

		public override VmValue SubtractCheckedUnsigned(VmValue P_0)
		{
			checked
			{
				while (true)
				{
					IL_007d:
					int num;
					if (P_0.IsManagedReference())
					{
						num = 1;
						if (GetNativeIntegerValueObfuscationSentinel() == null)
						{
							goto IL_0032;
						}
						goto IL_0057;
					}
					goto IL_0064;
					IL_00bc:
					if (IntPtr.Size != 8)
					{
						return new NativeIntegerValue(ToInt32().Bits.UInt32 - ((Int32Value)P_0).Bits.UInt32);
					}
					goto IL_00c4;
					IL_0064:
					if (!P_0.IsInt32Value())
					{
						if (P_0.IsNativeIntegerValue())
						{
							goto IL_0057;
						}
						goto IL_008a;
					}
					goto IL_00bc;
					IL_00c4:
					return new NativeIntegerValue(ToInt64().Bits.UInt64 - ((Int32Value)P_0).Bits.UInt32);
					IL_0057:
					if (IntPtr.Size != 8)
					{
						break;
					}
					num = 6;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0032;
					}
					goto IL_00c4;
					IL_008a:
					throw new VmOperandException();
					IL_0032:
					while (true)
					{
						switch (num)
						{
						case 1:
							do
							{
								P_0 = P_0.ReadValue();
								num = 5;
							}
							while (GetNativeIntegerValueObfuscationSentinel() != null);
							continue;
						case 5:
							goto IL_0064;
						case 2:
							goto IL_007d;
						case 3:
							goto IL_008a;
						case 6:
							return new NativeIntegerValue(ToInt64().Bits.UInt64 - ((NativeIntegerValue)P_0).ToInt64().Bits.UInt64);
						case 7:
							goto IL_00bc;
						case 4:
							goto IL_00c4;
						}
						break;
					}
					goto IL_0057;
				}
			}
			return new NativeIntegerValue((ulong)checked(ToInt32().Bits.UInt32 - ((NativeIntegerValue)P_0).ToInt32().Bits.UInt32));
		}

		public VmValue SubtractFromCheckedUnsigned(VmValue P_0)
		{
			checked
			{
				while (true)
				{
					int num;
					if (!P_0.IsManagedReference())
					{
						num = 7;
						if (!IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_0030;
						}
						goto IL_003a;
					}
					goto IL_0075;
					IL_0017:
					if (IntPtr.Size == 8)
					{
						num = 6;
						if (IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_003a;
						}
						goto IL_00fd;
					}
					unchecked
					{
						return new NativeIntegerValue((ulong)checked(((NativeIntegerValue)P_0).ToInt32().Bits.UInt32 - ToInt32().Bits.UInt32));
					}
					IL_0075:
					P_0 = P_0.ReadValue();
					goto IL_0068;
					IL_0068:
					if (!P_0.IsInt32Value())
					{
						num = 1;
						if (IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_003a;
						}
					}
					goto IL_00fd;
					IL_00fd:
					if (IntPtr.Size != 8)
					{
						break;
					}
					goto IL_0105;
					IL_003a:
					switch (num)
					{
					case 3:
						break;
					case 1:
						goto IL_0030;
					case 5:
					case 7:
						goto IL_0068;
					case 4:
						goto IL_0075;
					case 8:
						continue;
					case 6:
						return new NativeIntegerValue(((NativeIntegerValue)P_0).ToInt64().Bits.UInt64 - ToInt64().Bits.UInt64);
					default:
						goto IL_00fd;
					case 2:
						goto IL_0105;
					}
					goto IL_0017;
					IL_0030:
					if (P_0.IsNativeIntegerValue())
					{
						goto IL_0017;
					}
					throw new VmOperandException();
					IL_0105:
					return new NativeIntegerValue(((Int32Value)P_0).Bits.UInt32 - ToInt64().Bits.UInt64);
				}
				return new NativeIntegerValue(((Int32Value)P_0).Bits.UInt32 - ToInt32().Bits.UInt32);
			}
		}

		public override VmValue Multiply(VmValue P_0)
		{
			while (true)
			{
				IL_008d:
				if (!P_0.IsManagedReference())
				{
					goto IL_0072;
				}
				int num = 3;
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					goto IL_004f;
				}
				goto IL_00d2;
				IL_00d2:
				return new NativeIntegerValue(ToInt64().Bits.Int64 * ((NativeIntegerValue)P_0).ToInt64().Bits.Int64);
				IL_0072:
				if (!P_0.IsInt32Value())
				{
					if (P_0.IsNativeIntegerValue())
					{
						num = 0;
						if (IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_004f;
						}
						goto IL_009b;
					}
					throw new VmOperandException();
				}
				goto IL_001f;
				IL_00ff:
				return new NativeIntegerValue(ToInt64().Bits.Int64 * ((Int32Value)P_0).ToInt64().Bits.Int64);
				IL_001f:
				if (IntPtr.Size != 8)
				{
					break;
				}
				num = 1;
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_004f;
				}
				goto IL_00ff;
				IL_004f:
				while (true)
				{
					switch (num)
					{
					case 6:
						break;
					case 3:
						P_0 = P_0.ReadValue();
						num = 5;
						if (IsNativeIntegerValueObfuscationSentinelNull())
						{
							continue;
						}
						goto IL_008d;
					case 5:
						goto IL_0072;
					case 4:
						goto IL_008d;
					default:
						goto IL_009b;
					case 2:
						goto IL_00d2;
					case 1:
						goto IL_00ff;
					}
					break;
				}
				goto IL_001f;
				IL_009b:
				if (IntPtr.Size == 8)
				{
					goto IL_00d2;
				}
				return new NativeIntegerValue(ToInt32().Bits.Int32 * ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
			}
			return new NativeIntegerValue(ToInt32().Bits.Int32 * ((Int32Value)P_0).Bits.Int32);
		}

		public override VmValue MultiplyChecked(VmValue P_0)
		{
			checked
			{
				while (true)
				{
					int num;
					if (P_0.IsManagedReference())
					{
						num = 3;
						if (GetNativeIntegerValueObfuscationSentinel() == null)
						{
							goto IL_0015;
						}
						goto IL_003f;
					}
					goto IL_006a;
					IL_0015:
					P_0 = P_0.ReadValue();
					num = 4;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_003f;
					}
					goto IL_0094;
					IL_006a:
					if (!P_0.IsInt32Value())
					{
						num = 0;
						if (!IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_002b;
						}
						goto IL_003f;
					}
					goto IL_00fb;
					IL_0094:
					throw new VmOperandException();
					IL_003f:
					switch (num)
					{
					case 6:
						break;
					default:
						goto IL_002b;
					case 4:
						goto IL_006a;
					case 7:
						continue;
					case 3:
						goto IL_0094;
					case 8:
						goto IL_009a;
					case 2:
						goto IL_00a2;
					case 1:
						goto IL_00fb;
					case 5:
						goto IL_0103;
					}
					goto IL_0015;
					IL_002b:
					if (!P_0.IsNativeIntegerValue())
					{
						num = 3;
						if (GetNativeIntegerValueObfuscationSentinel() == null)
						{
							goto IL_003f;
						}
						goto IL_0094;
					}
					goto IL_009a;
					IL_00fb:
					if (IntPtr.Size != 8)
					{
						break;
					}
					goto IL_0103;
					IL_009a:
					if (IntPtr.Size != 8)
					{
						return new NativeIntegerValue(ToInt32().Bits.Int32 * ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
					}
					goto IL_00a2;
					IL_00a2:
					return new NativeIntegerValue(ToInt64().Bits.Int64 * ((NativeIntegerValue)P_0).ToInt64().Bits.Int64);
					IL_0103:
					return new NativeIntegerValue(ToInt64().Bits.Int64 * ((Int32Value)P_0).ToInt64().Bits.Int64);
				}
				return new NativeIntegerValue(ToInt32().Bits.Int32 * ((Int32Value)P_0).Bits.Int32);
			}
		}

		public override VmValue MultiplyCheckedUnsigned(VmValue P_0)
		{
			checked
			{
				while (true)
				{
					if (!P_0.IsManagedReference())
					{
						goto IL_0073;
					}
					goto IL_007d;
					IL_007d:
					P_0 = P_0.ReadValue();
					int num = 0;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0015;
					}
					goto IL_0073;
					IL_0015:
					switch (num)
					{
					case 8:
						break;
					case 4:
						goto IL_004d;
					case 2:
					case 6:
						goto IL_0073;
					default:
						goto IL_007d;
					case 7:
						continue;
					case 3:
						return new NativeIntegerValue(ToInt64().Bits.UInt64 * ((Int32Value)P_0).Bits.UInt32);
					case 1:
						goto IL_00ef;
					case 5:
						goto IL_00f7;
					}
					goto IL_0040;
					IL_004d:
					if (IntPtr.Size == 8)
					{
						num = 3;
						if (IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_0015;
						}
						goto IL_0073;
					}
					return new NativeIntegerValue(ToInt32().Bits.UInt32 * ((Int32Value)P_0).Bits.UInt32);
					IL_0040:
					if (P_0.IsNativeIntegerValue())
					{
						num = 0;
						if (GetNativeIntegerValueObfuscationSentinel() != null)
						{
							goto IL_0015;
						}
						goto IL_00ef;
					}
					throw new VmOperandException();
					IL_0073:
					if (!P_0.IsInt32Value())
					{
						num = 6;
						if (!IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_0015;
						}
						goto IL_0040;
					}
					goto IL_004d;
					IL_00ef:
					if (IntPtr.Size != 8)
					{
						break;
					}
					goto IL_00f7;
					IL_00f7:
					return new NativeIntegerValue(ToInt64().Bits.UInt64 * ((NativeIntegerValue)P_0).ToInt64().Bits.UInt64);
				}
			}
			return new NativeIntegerValue((ulong)checked(ToInt32().Bits.UInt32 * ((NativeIntegerValue)P_0).ToInt32().Bits.UInt32));
		}

		public override VmValue Divide(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_000f;
					}
					goto IL_0048;
				}
				goto IL_0055;
				IL_00e8:
				return new NativeIntegerValue(ToInt64().Bits.Int64 / ((Int32Value)P_0).ToInt64().Bits.Int64);
				IL_0055:
				P_0 = P_0.ReadValue();
				goto IL_0048;
				IL_0048:
				if (!P_0.IsInt32Value())
				{
					goto IL_003e;
				}
				goto IL_00e0;
				IL_003e:
				if (!P_0.IsNativeIntegerValue())
				{
					num = 0;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_0079;
				}
				goto IL_007f;
				IL_00e0:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_00e8;
				IL_000f:
				switch (num)
				{
				case 5:
					break;
				case 3:
				case 9:
					goto IL_0048;
				case 1:
					goto IL_0055;
				case 4:
					continue;
				default:
					goto IL_0079;
				case 8:
					goto IL_007f;
				case 7:
					goto IL_0087;
				case 6:
					goto IL_00e0;
				case 2:
					goto IL_00e8;
				}
				goto IL_003e;
				IL_0079:
				throw new VmOperandException();
				IL_007f:
				if (IntPtr.Size != 8)
				{
					return new NativeIntegerValue(ToInt32().Bits.Int32 / ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
				}
				goto IL_0087;
				IL_0087:
				return new NativeIntegerValue(ToInt64().Bits.Int64 / ((NativeIntegerValue)P_0).ToInt64().Bits.Int64);
			}
			return new NativeIntegerValue(ToInt32().Bits.Int32 / ((Int32Value)P_0).Bits.Int32);
		}

		public VmValue DivideInto(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 1;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_0011;
					}
					goto IL_0025;
				}
				goto IL_004d;
				IL_00ed:
				return new NativeIntegerValue(((Int32Value)P_0).ToInt64().Bits.Int64 / ToInt64().Bits.Int64);
				IL_004d:
				if (!P_0.IsInt32Value())
				{
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_0025;
					}
					goto IL_0074;
				}
				goto IL_00e5;
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 0;
				if (GetNativeIntegerValueObfuscationSentinel() != null)
				{
					goto IL_0025;
				}
				goto IL_004d;
				IL_0025:
				switch (num)
				{
				case 2:
					break;
				case 1:
					goto IL_004d;
				case 3:
					continue;
				default:
					goto IL_0074;
				case 6:
					goto IL_0082;
				case 7:
					goto IL_00b9;
				case 5:
					goto IL_00e5;
				case 4:
					goto IL_00ed;
				}
				goto IL_0011;
				IL_0074:
				if (!P_0.IsNativeIntegerValue())
				{
					throw new VmOperandException();
				}
				goto IL_0082;
				IL_00e5:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_00ed;
				IL_0082:
				if (IntPtr.Size == 8)
				{
					goto IL_00b9;
				}
				return new NativeIntegerValue(((NativeIntegerValue)P_0).ToInt32().Bits.Int32 / ToInt32().Bits.Int32);
				IL_00b9:
				return new NativeIntegerValue(((NativeIntegerValue)P_0).ToInt64().Bits.Int64 / ToInt64().Bits.Int64);
			}
			return new NativeIntegerValue(((Int32Value)P_0).Bits.Int32 / ToInt32().Bits.Int32);
		}

		public override VmValue DivideUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_0028;
					}
					goto IL_0049;
				}
				goto IL_0061;
				IL_0085:
				if (IntPtr.Size == 8)
				{
					goto IL_00b7;
				}
				return new NativeIntegerValue(ToInt32().Bits.UInt32 / ((Int32Value)P_0).Bits.UInt32);
				IL_0061:
				if (!P_0.IsInt32Value())
				{
					if (!P_0.IsNativeIntegerValue())
					{
						throw new VmOperandException();
					}
					goto IL_000e;
				}
				num = 4;
				if (GetNativeIntegerValueObfuscationSentinel() != null)
				{
					goto IL_0028;
				}
				goto IL_0085;
				IL_00b7:
				return new NativeIntegerValue(ToInt64().Bits.UInt64 / ((Int32Value)P_0).ToInt64().Bits.UInt64);
				IL_0049:
				P_0 = P_0.ReadValue();
				goto IL_0061;
				IL_000e:
				if (IntPtr.Size != 8)
				{
					break;
				}
				num = 2;
				if (GetNativeIntegerValueObfuscationSentinel() != null)
				{
					goto IL_0028;
				}
				goto IL_00e4;
				IL_00e4:
				return new NativeIntegerValue(ToInt64().Bits.UInt64 / ((NativeIntegerValue)P_0).ToInt64().Bits.UInt64);
				IL_0028:
				switch (num)
				{
				case 6:
					break;
				default:
					goto IL_0049;
				case 3:
					goto IL_0061;
				case 1:
					continue;
				case 4:
					goto IL_0085;
				case 5:
					goto IL_00b7;
				case 2:
					goto IL_00e4;
				}
				goto IL_000e;
			}
			return new NativeIntegerValue((ulong)(ToInt32().Bits.UInt32 / ((NativeIntegerValue)P_0).ToInt32().Bits.UInt32));
		}

		public VmValue DivideUnsignedInto(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0012;
				}
				goto IL_006f;
				IL_006f:
				int num;
				if (!P_0.IsInt32Value())
				{
					num = 3;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0033;
					}
					goto IL_0047;
				}
				goto IL_00e3;
				IL_0012:
				P_0 = P_0.ReadValue();
				num = 7;
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0047;
				}
				goto IL_006f;
				IL_0047:
				switch (num)
				{
				case 5:
					break;
				case 3:
					if (!P_0.IsNativeIntegerValue())
					{
						throw new VmOperandException();
					}
					goto IL_0033;
				case 2:
					goto IL_0033;
				case 7:
					goto IL_006f;
				case 6:
					continue;
				case 1:
					goto IL_0084;
				default:
					goto IL_00e3;
				case 4:
					goto IL_00eb;
				}
				goto IL_0012;
				IL_0033:
				if (IntPtr.Size == 8)
				{
					num = 1;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0047;
					}
					goto IL_0084;
				}
				return new NativeIntegerValue((ulong)(((NativeIntegerValue)P_0).ToInt32().Bits.UInt32 / ToInt32().Bits.UInt32));
				IL_00e3:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_00eb;
				IL_0084:
				return new NativeIntegerValue(((NativeIntegerValue)P_0).ToInt64().Bits.UInt64 / ToInt64().Bits.UInt64);
				IL_00eb:
				return new NativeIntegerValue(((Int32Value)P_0).ToInt64().Bits.UInt64 / ToInt64().Bits.UInt64);
			}
			return new NativeIntegerValue(((Int32Value)P_0).Bits.UInt32 / ToInt32().Bits.UInt32);
		}

		public override VmValue Remainder(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 4;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_001d;
					}
					goto IL_0047;
				}
				goto IL_004f;
				IL_00df:
				return new NativeIntegerValue(ToInt64().Bits.Int64 % ((NativeIntegerValue)P_0).ToInt64().Bits.Int64);
				IL_004f:
				if (!P_0.IsInt32Value())
				{
					if (!P_0.IsNativeIntegerValue())
					{
						num = 1;
						if (IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_001d;
						}
					}
					goto IL_00d7;
				}
				goto IL_0078;
				IL_0047:
				P_0 = P_0.ReadValue();
				goto IL_004f;
				IL_00d7:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_00df;
				IL_001d:
				switch (num)
				{
				case 4:
					break;
				case 6:
					goto IL_004f;
				case 5:
					continue;
				case 1:
					throw new VmOperandException();
				case 2:
					goto IL_0078;
				case 7:
					goto IL_00aa;
				default:
					goto IL_00d7;
				case 3:
					goto IL_00df;
				}
				goto IL_0047;
				IL_0078:
				if (IntPtr.Size == 8)
				{
					goto IL_00aa;
				}
				return new NativeIntegerValue(ToInt32().Bits.Int32 % ((Int32Value)P_0).Bits.Int32);
				IL_00aa:
				return new NativeIntegerValue(ToInt64().Bits.Int64 % ((Int32Value)P_0).ToInt64().Bits.Int64);
			}
			return new NativeIntegerValue(ToInt32().Bits.Int32 % ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
		}

		public VmValue RemainderFrom(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0033;
					}
					goto IL_0054;
				}
				goto IL_005c;
				IL_001c:
				if (IntPtr.Size != 8)
				{
					break;
				}
				num = 4;
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0033;
				}
				goto IL_00af;
				IL_005c:
				if (!P_0.IsInt32Value())
				{
					if (P_0.IsNativeIntegerValue())
					{
						num = 0;
						if (GetNativeIntegerValueObfuscationSentinel() != null)
						{
							goto IL_0033;
						}
						goto IL_00af;
					}
					throw new VmOperandException();
				}
				goto IL_001c;
				IL_00b7:
				return new NativeIntegerValue(((NativeIntegerValue)P_0).ToInt64().Bits.Int64 % ToInt64().Bits.Int64);
				IL_00af:
				if (IntPtr.Size != 8)
				{
					return new NativeIntegerValue(((NativeIntegerValue)P_0).ToInt32().Bits.Int32 % ToInt32().Bits.Int32);
				}
				goto IL_00b7;
				IL_0033:
				switch (num)
				{
				case 6:
					break;
				default:
					goto IL_0054;
				case 3:
					goto IL_005c;
				case 1:
					continue;
				case 4:
					return new NativeIntegerValue(((Int32Value)P_0).ToInt64().Bits.Int64 % ToInt64().Bits.Int64);
				case 2:
					goto IL_00af;
				case 5:
					goto IL_00b7;
				}
				goto IL_001c;
				IL_0054:
				P_0 = P_0.ReadValue();
				goto IL_005c;
			}
			return new NativeIntegerValue(((Int32Value)P_0).Bits.Int32 % ToInt32().Bits.Int32);
		}

		public override VmValue RemainderUnsigned(VmValue P_0)
		{
			while (true)
			{
				IL_0075:
				if (P_0.IsManagedReference())
				{
					goto IL_0063;
				}
				goto IL_006b;
				IL_006b:
				while (true)
				{
					if (!P_0.IsInt32Value())
					{
						goto IL_0011;
					}
					goto IL_0056;
					IL_0056:
					int num;
					if (IntPtr.Size == 8)
					{
						num = 0;
						if (IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_002b;
						}
						goto IL_0080;
					}
					return new NativeIntegerValue(ToInt32().Bits.UInt32 % ((Int32Value)P_0).Bits.UInt32);
					IL_00b3:
					if (IntPtr.Size != 8)
					{
						return new NativeIntegerValue((ulong)(ToInt32().Bits.UInt32 % ((NativeIntegerValue)P_0).ToInt32().Bits.UInt32));
					}
					goto IL_00bb;
					IL_002b:
					switch (num)
					{
					case 2:
						break;
					case 3:
						goto IL_0056;
					case 6:
						goto end_IL_006b;
					case 8:
						continue;
					case 7:
						goto IL_0075;
					default:
						goto IL_0080;
					case 1:
						goto IL_00ad;
					case 5:
						goto IL_00b3;
					case 4:
						goto IL_00bb;
					}
					goto IL_0011;
					IL_0080:
					return new NativeIntegerValue(ToInt64().Bits.UInt64 % ((Int32Value)P_0).ToInt64().Bits.UInt64);
					IL_0011:
					if (!P_0.IsNativeIntegerValue())
					{
						num = 0;
						if (GetNativeIntegerValueObfuscationSentinel() != null)
						{
							goto IL_002b;
						}
						goto IL_00ad;
					}
					goto IL_00b3;
					IL_00bb:
					return new NativeIntegerValue(ToInt64().Bits.UInt64 % ((NativeIntegerValue)P_0).ToInt64().Bits.UInt64);
					IL_00ad:
					throw new VmOperandException();
					continue;
					end_IL_006b:
					break;
				}
				goto IL_0063;
				IL_0063:
				P_0 = P_0.ReadValue();
				goto IL_006b;
			}
		}

		public VmValue RemainderUnsignedFrom(VmValue P_0)
		{
			while (true)
			{
				if (!P_0.IsManagedReference())
				{
					goto IL_0080;
				}
				int num = 1;
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					goto IL_0040;
				}
				goto IL_00a9;
				IL_0040:
				switch (num)
				{
				case 4:
					break;
				case 5:
					goto IL_0029;
				case 1:
					P_0 = P_0.ReadValue();
					goto IL_0080;
				case 3:
					goto IL_0080;
				case 2:
					continue;
				default:
					goto IL_00a9;
				case 7:
					return new NativeIntegerValue(((NativeIntegerValue)P_0).ToInt64().Bits.UInt64 % ToInt64().Bits.UInt64);
				case 6:
					goto IL_0129;
				}
				goto IL_0011;
				IL_0080:
				if (!P_0.IsInt32Value())
				{
					if (P_0.IsNativeIntegerValue())
					{
						goto IL_0011;
					}
					goto IL_0129;
				}
				num = 5;
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					goto IL_0029;
				}
				goto IL_0040;
				IL_0029:
				if (IntPtr.Size == 8)
				{
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_0040;
					}
					goto IL_00a9;
				}
				return new NativeIntegerValue(((Int32Value)P_0).Bits.UInt32 % ToInt32().Bits.UInt32);
				IL_0129:
				throw new VmOperandException();
				IL_0011:
				if (IntPtr.Size != 8)
				{
					break;
				}
				num = 7;
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0029;
				}
				goto IL_0040;
				IL_00a9:
				return new NativeIntegerValue(((Int32Value)P_0).ToInt64().Bits.UInt64 % ToInt64().Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)(((NativeIntegerValue)P_0).ToInt32().Bits.UInt32 % ToInt32().Bits.UInt32));
		}

		public override VmValue BitwiseAnd(VmValue P_0)
		{
			while (true)
			{
				if (!P_0.IsManagedReference())
				{
					goto IL_005c;
				}
				goto IL_0069;
				IL_0069:
				P_0 = P_0.ReadValue();
				int num = 4;
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_000f;
				}
				goto IL_005c;
				IL_000f:
				switch (num)
				{
				case 1:
					break;
				case 3:
					goto IL_0052;
				case 5:
				case 8:
					goto IL_005c;
				case 4:
					goto IL_0069;
				case 6:
					continue;
				default:
					goto IL_008b;
				case 2:
					goto IL_00ea;
				case 7:
					goto IL_00f2;
				}
				goto IL_0047;
				IL_005c:
				if (!P_0.IsInt32Value())
				{
					num = 1;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_0047;
				}
				goto IL_00ea;
				IL_008b:
				return new NativeIntegerValue(ToInt64().Bits.Int64 & ((NativeIntegerValue)P_0).ToInt64().Bits.Int64);
				IL_00ea:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_00f2;
				IL_00f2:
				return new NativeIntegerValue(ToInt64().Bits.Int64 & ((Int32Value)P_0).ToInt64().Bits.Int64);
				IL_0047:
				if (!P_0.IsNativeIntegerValue())
				{
					throw new VmOperandException();
				}
				goto IL_0052;
				IL_0052:
				if (IntPtr.Size == 8)
				{
					num = 0;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_008b;
				}
				return new NativeIntegerValue(ToInt32().Bits.Int32 & ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
			}
			return new NativeIntegerValue(ToInt32().Bits.Int32 & ((Int32Value)P_0).Bits.Int32);
		}

		public override VmValue BitwiseOr(VmValue P_0)
		{
			while (true)
			{
				IL_008c:
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_0006;
					}
					goto IL_0039;
				}
				goto IL_0064;
				IL_00f9:
				throw new VmOperandException();
				IL_0064:
				P_0 = P_0.ReadValue();
				num = 8;
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					goto IL_0039;
				}
				goto IL_0107;
				IL_0039:
				while (true)
				{
					switch (num)
					{
					case 6:
						if (IntPtr.Size == 8)
						{
							num = 2;
							if (IsNativeIntegerValueObfuscationSentinelNull())
							{
								continue;
							}
							goto case 2;
						}
						return new NativeIntegerValue(ToInt32().Bits.Int32 | ((Int32Value)P_0).Bits.Int32);
					case 4:
						goto IL_0064;
					case 1:
						goto IL_008c;
					case 2:
						return new NativeIntegerValue(ToInt64().Bits.Int64 | ((Int32Value)P_0).ToInt64().Bits.Int64);
					case 7:
						goto IL_00f9;
					case 5:
						goto IL_00ff;
					case 3:
						goto IL_0107;
					}
					break;
				}
				goto IL_0006;
				IL_0006:
				if (P_0.IsInt32Value())
				{
					num = 6;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0039;
					}
				}
				else if (!P_0.IsNativeIntegerValue())
				{
					goto IL_00f9;
				}
				goto IL_00ff;
				IL_00ff:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_0107;
				IL_0107:
				return new NativeIntegerValue(ToInt64().Bits.Int64 | ((NativeIntegerValue)P_0).ToInt64().Bits.Int64);
			}
			return new NativeIntegerValue(ToInt32().Bits.Int32 | ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
		}

		public override VmValue BitwiseNot()
		{
			while (IntPtr.Size == 8)
			{
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(~ToInt64().Bits.Int64);
			}
			return new NativeIntegerValue(~ToInt32().Bits.Int32);
		}

		public override VmValue BitwiseXor(VmValue P_0)
		{
			while (true)
			{
				IL_007e:
				if (P_0.IsManagedReference())
				{
					goto IL_005a;
				}
				int num = 3;
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					goto IL_0033;
				}
				goto IL_008f;
				IL_008f:
				if (IntPtr.Size == 8)
				{
					break;
				}
				return new NativeIntegerValue(ToInt32().Bits.Int32 ^ ((Int32Value)P_0).Bits.Int32);
				IL_005a:
				P_0 = P_0.ReadValue();
				num = 1;
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0033;
				}
				goto IL_008f;
				IL_0033:
				while (true)
				{
					switch (num)
					{
					case 1:
					case 3:
						break;
					case 2:
						goto end_IL_0033;
					case 4:
						goto IL_007e;
					default:
						goto IL_008f;
					case 5:
						goto IL_00ca;
					case 6:
						goto end_IL_007e;
					case 7:
						goto IL_012d;
					}
					if (P_0.IsInt32Value())
					{
						num = 0;
						if (IsNativeIntegerValueObfuscationSentinelNull())
						{
							continue;
						}
						goto IL_008f;
					}
					if (P_0.IsNativeIntegerValue())
					{
						num = 3;
						if (GetNativeIntegerValueObfuscationSentinel() != null)
						{
							continue;
						}
						goto IL_00ca;
					}
					throw new VmOperandException();
					IL_00ca:
					if (IntPtr.Size == 8)
					{
						goto IL_012d;
					}
					return new NativeIntegerValue(ToInt32().Bits.Int32 ^ ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
					IL_012d:
					return new NativeIntegerValue(ToInt64().Bits.Int64 ^ ((NativeIntegerValue)P_0).ToInt64().Bits.Int64);
					continue;
					end_IL_0033:
					break;
				}
				goto IL_005a;
				continue;
				end_IL_007e:
				break;
			}
			return new NativeIntegerValue(ToInt64().Bits.Int64 ^ ((Int32Value)P_0).ToInt64().Bits.Int64);
		}

		public override VmValue ShiftLeft(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 5;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_003b;
					}
					goto IL_0045;
				}
				goto IL_0077;
				IL_00fe:
				throw new VmOperandException();
				IL_0077:
				P_0 = P_0.ReadValue();
				num = 1;
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					goto IL_003b;
				}
				goto IL_0045;
				IL_0045:
				switch (num)
				{
				case 8:
					break;
				case 7:
					goto IL_0022;
				case 3:
				case 5:
					goto IL_003b;
				case 9:
					goto IL_0077;
				case 6:
					continue;
				case 1:
					goto IL_00a8;
				default:
					goto IL_00fe;
				case 4:
					goto IL_0104;
				case 2:
					goto IL_010c;
				}
				goto IL_0006;
				IL_003b:
				if (!P_0.IsInt32Value())
				{
					goto IL_0006;
				}
				goto IL_0022;
				IL_0022:
				if (IntPtr.Size == 8)
				{
					num = 1;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_0045;
					}
					goto IL_00a8;
				}
				return new NativeIntegerValue(ToInt32().Bits.Int32 << ((Int32Value)P_0).Bits.Int32);
				IL_0104:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_010c;
				IL_00a8:
				return new NativeIntegerValue(ToInt64().Bits.Int64 << ((Int32Value)P_0).Bits.Int32);
				IL_010c:
				return new NativeIntegerValue(ToInt64().Bits.Int64 << ((NativeIntegerValue)P_0).ToInt64().Bits.Int32);
				IL_0006:
				if (!P_0.IsNativeIntegerValue())
				{
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_0045;
					}
					goto IL_00fe;
				}
				goto IL_0104;
			}
			return new NativeIntegerValue(ToInt32().Bits.Int32 << ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
		}

		public override VmValue ShiftRight(VmValue P_0)
		{
			while (true)
			{
				IL_0065:
				if (!P_0.IsManagedReference())
				{
					goto IL_004d;
				}
				int num = 6;
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0025;
				}
				goto IL_00a8;
				IL_00e0:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_00e8;
				IL_004d:
				if (P_0.IsInt32Value())
				{
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_0025;
					}
					goto IL_0073;
				}
				if (!P_0.IsNativeIntegerValue())
				{
					goto IL_00da;
				}
				goto IL_00e0;
				IL_00da:
				throw new VmOperandException();
				IL_0025:
				while (true)
				{
					switch (num)
					{
					case 6:
						P_0 = P_0.ReadValue();
						num = 1;
						if (!IsNativeIntegerValueObfuscationSentinelNull())
						{
							continue;
						}
						break;
					case 5:
						break;
					case 7:
						goto IL_0065;
					default:
						goto IL_0073;
					case 2:
						goto IL_00a8;
					case 3:
						goto IL_00da;
					case 1:
						goto IL_00e0;
					case 4:
						goto IL_00e8;
					}
					break;
				}
				goto IL_004d;
				IL_0073:
				if (IntPtr.Size == 8)
				{
					goto IL_00a8;
				}
				return new NativeIntegerValue(ToInt32().Bits.Int32 >> ((Int32Value)P_0).Bits.Int32);
				IL_00a8:
				return new NativeIntegerValue(ToInt64().Bits.Int64 >> ((Int32Value)P_0).Bits.Int32);
				IL_00e8:
				return new NativeIntegerValue(ToInt64().Bits.Int64 >> ((NativeIntegerValue)P_0).ToInt64().Bits.Int32);
			}
			return new NativeIntegerValue(ToInt32().Bits.Int32 >> ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
		}

		public override VmValue ShiftRightUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 1;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_0011;
					}
					goto IL_0025;
				}
				goto IL_004c;
				IL_00bc:
				return new NativeIntegerValue(ToInt64().Bits.UInt64 >> ((NativeIntegerValue)P_0).ToInt64().Bits.Int32);
				IL_004c:
				if (!P_0.IsInt32Value())
				{
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_0025;
					}
					goto IL_0073;
				}
				goto IL_00eb;
				IL_00f3:
				return new NativeIntegerValue(ToInt64().Bits.UInt64 >> ((Int32Value)P_0).Bits.Int32);
				IL_0025:
				switch (num)
				{
				case 1:
					break;
				case 7:
					goto IL_004c;
				case 2:
					continue;
				default:
					goto IL_0073;
				case 3:
					goto IL_0082;
				case 5:
					goto IL_00bc;
				case 6:
					goto IL_00eb;
				case 4:
					goto IL_00f3;
				}
				goto IL_0011;
				IL_0073:
				if (!P_0.IsNativeIntegerValue())
				{
					throw new VmOperandException();
				}
				goto IL_0082;
				IL_00eb:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_00f3;
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 7;
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					goto IL_0025;
				}
				goto IL_0082;
				IL_0082:
				if (IntPtr.Size == 8)
				{
					goto IL_00bc;
				}
				return new NativeIntegerValue(ToInt32().Bits.UInt32 >> ((NativeIntegerValue)P_0).ToInt32().Bits.Int32);
			}
			return new NativeIntegerValue(ToInt32().Bits.UInt32 >> ((Int32Value)P_0).Bits.Int32);
		}

		public VmValue ShiftUnsignedArgumentRight(Int32Value P_0)
		{
			return new NativeIntegerValue(P_0.Bits.UInt32 >> ToInt32().Bits.Int32);
		}

		public VmValue ShiftArgumentRight(Int32Value P_0)
		{
			return new NativeIntegerValue(P_0.Bits.Int32 >> ToInt64().Bits.Int32);
		}

		public VmValue ShiftArgumentLeft(Int32Value P_0)
		{
			return new NativeIntegerValue(P_0.Bits.Int32 << ToInt64().Bits.Int32);
		}

		public override string ToString()
		{
			return IntegerStorage.ToString();
		}

		internal override VmValue ReadValue()
		{
			return this;
		}

		internal override bool IsIntegerValue()
		{
			return true;
		}

		internal override bool IsEqualTo(VmValue P_0)
		{
			while (true)
			{
				if (!P_0.IsObjectValue())
				{
					if (P_0.IsManagedReference())
					{
						break;
					}
					while (true)
					{
						VmValue VmValue = P_0.ReadValue();
						while (true)
						{
							if (VmValue.IsIntegerValue())
							{
								goto IL_006a;
							}
							int num = 0;
							if (GetNativeIntegerValueObfuscationSentinel() != null)
							{
								goto IL_0022;
							}
							goto IL_00a6;
							IL_00a6:
							return false;
							IL_006a:
							if (VmValue.IsInt32Value())
							{
								goto IL_005d;
							}
							goto IL_00a8;
							IL_005d:
							if (IntPtr.Size == 8)
							{
								num = 0;
								if (!IsNativeIntegerValueObfuscationSentinelNull())
								{
									goto IL_0022;
								}
								goto IL_00e6;
							}
							return ToInt32().Bits.Int32 == ((Int32Value)P_0).Bits.Int32;
							IL_00e6:
							return ToInt64().Bits.Int64 == ((Int32Value)P_0).ToInt64().Bits.Int64;
							IL_0022:
							switch (num)
							{
							case 12:
								break;
							default:
								goto IL_006a;
							case 6:
								continue;
							case 3:
								goto end_IL_0083;
							case 8:
								goto end_IL_008e;
							case 1:
								goto IL_00a6;
							case 4:
								goto IL_00a8;
							case 5:
								goto IL_00b1;
							case 7:
								goto IL_00b9;
							case 9:
								goto IL_00bb;
							case 11:
								goto IL_00e3;
							case 2:
								goto IL_00e6;
							case 10:
								goto end_IL_0098;
							}
							goto IL_005d;
							IL_00a8:
							if (VmValue.IsNativeIntegerValue())
							{
								goto IL_00b1;
							}
							goto IL_00e3;
							IL_00b1:
							_ = IntPtr.Size;
							goto IL_00bb;
							IL_00bb:
							return ToInt64().Bits.Int64 == ((NativeIntegerValue)P_0).ToInt64().Bits.Int64;
							IL_00e3:
							return false;
							continue;
							end_IL_0083:
							break;
						}
						continue;
						end_IL_008e:
						break;
					}
					continue;
				}
				goto IL_00b9;
				IL_00b9:
				return false;
				continue;
				end_IL_0098:
				break;
			}
			return ((ReferenceValue)P_0).IsEqualTo(this);
		}

		internal override bool IsNotEqualTo(VmValue P_0)
		{
			while (!P_0.IsObjectValue())
			{
				while (true)
				{
					IL_00b3:
					VmValue VmValue;
					int num;
					if (!P_0.IsManagedReference())
					{
						VmValue = P_0.ReadValue();
						num = 4;
						if (!IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_006a;
						}
						goto IL_0075;
					}
					goto IL_012d;
					IL_00fa:
					return false;
					IL_0075:
					while (true)
					{
						switch (num)
						{
						case 6:
							break;
						case 5:
							goto IL_003a;
						case 4:
							if (VmValue.IsIntegerValue())
							{
								goto IL_006a;
							}
							num = 1;
							if (GetNativeIntegerValueObfuscationSentinel() != null)
							{
								continue;
							}
							goto case 1;
						case 3:
							goto IL_006a;
						case 8:
							goto IL_00b3;
						case 9:
							goto end_IL_00b3;
						case 1:
							return false;
						case 2:
							goto IL_00fa;
						case 7:
							goto IL_00fc;
						case 10:
							goto IL_0102;
						case 11:
							goto IL_012d;
						case 12:
							return ToInt64().Bits.UInt64 != ((Int32Value)P_0).ToInt64().Bits.UInt64;
						default:
							goto end_IL_00c0;
						}
						break;
					}
					goto IL_001d;
					IL_006a:
					if (!VmValue.IsInt32Value())
					{
						goto IL_001d;
					}
					goto IL_003a;
					IL_003a:
					if (IntPtr.Size == 8)
					{
						num = 12;
						if (IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_0075;
						}
						goto IL_012d;
					}
					return ToInt32().Bits.UInt32 != ((Int32Value)P_0).Bits.UInt32;
					IL_00fc:
					_ = IntPtr.Size;
					goto IL_0102;
					IL_012d:
					return ((ReferenceValue)P_0).IsNotEqualTo(this);
					IL_0102:
					return ToInt64().Bits.UInt64 != ((NativeIntegerValue)P_0).ToInt64().Bits.UInt64;
					IL_001d:
					if (!VmValue.IsNativeIntegerValue())
					{
						num = 2;
						if (IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_0075;
						}
						goto IL_00fa;
					}
					goto IL_00fc;
					continue;
					end_IL_00b3:
					break;
				}
				continue;
				end_IL_00c0:
				break;
			}
			return false;
		}

		public override bool GreaterThanOrEqual(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0017;
				}
				goto IL_008b;
				IL_008b:
				if (!P_0.IsInt32Value())
				{
					goto IL_0049;
				}
				goto IL_007a;
				IL_007a:
				if (IntPtr.Size != 8)
				{
					break;
				}
				int num = 0;
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0053;
				}
				goto IL_00d7;
				IL_0017:
				P_0 = P_0.ReadValue();
				num = 1;
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0053;
				}
				goto IL_008b;
				IL_0053:
				switch (num)
				{
				case 6:
					break;
				case 4:
					goto IL_002d;
				case 3:
					goto IL_0049;
				case 5:
					goto IL_007a;
				case 1:
					goto IL_008b;
				case 7:
					continue;
				case 2:
					return ToInt64().Bits.Int64 >= ((NativeIntegerValue)P_0).ToInt64().Bits.Int64;
				default:
					goto IL_00d7;
				}
				goto IL_0017;
				IL_0049:
				if (P_0.IsNativeIntegerValue())
				{
					goto IL_002d;
				}
				throw new VmOperandException();
				IL_00d7:
				return ToInt64().Bits.Int64 >= ((Int32Value)P_0).ToInt64().Bits.Int64;
				IL_002d:
				if (IntPtr.Size == 8)
				{
					num = 2;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_0053;
					}
					goto IL_00d7;
				}
				return ToInt32().Bits.Int32 >= ((NativeIntegerValue)P_0).ToInt32().Bits.Int32;
			}
			return ToInt32().Bits.Int32 >= ((Int32Value)P_0).Bits.Int32;
		}

		public override bool GreaterThanOrEqualUnsigned(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_001f;
				}
				goto IL_007e;
				IL_007e:
				int num;
				if (!P_0.IsInt32Value())
				{
					if (!P_0.IsNativeIntegerValue())
					{
						throw new VmOperandException();
					}
					num = 1;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_00c1;
					}
				}
				else
				{
					num = 0;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0035;
					}
				}
				goto IL_0049;
				IL_00f6:
				return ToInt64().Bits.UInt64 >= ((NativeIntegerValue)P_0).ToInt64().Bits.UInt64;
				IL_001f:
				P_0 = P_0.ReadValue();
				num = 3;
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0049;
				}
				goto IL_007e;
				IL_0035:
				if (IntPtr.Size == 8)
				{
					num = 3;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						break;
					}
					goto IL_0049;
				}
				return ToInt32().Bits.UInt32 >= ((Int32Value)P_0).Bits.UInt32;
				IL_0049:
				switch (num)
				{
				case 4:
					break;
				default:
					goto IL_0035;
				case 6:
					goto IL_007e;
				case 5:
					continue;
				case 1:
					goto IL_00c1;
				case 2:
					goto IL_00f6;
				case 3:
					goto end_IL_0088;
				}
				goto IL_001f;
				IL_00c1:
				if (IntPtr.Size == 8)
				{
					goto IL_00f6;
				}
				return ToInt32().Bits.UInt32 >= ((NativeIntegerValue)P_0).ToInt32().Bits.UInt32;
				continue;
				end_IL_0088:
				break;
			}
			return ToInt64().Bits.UInt64 >= ((Int32Value)P_0).ToInt64().Bits.UInt64;
		}

		public override bool GreaterThan(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0049;
				}
				goto IL_0085;
				IL_0085:
				if (!P_0.IsInt32Value())
				{
					if (P_0.IsNativeIntegerValue())
					{
						goto IL_0077;
					}
					goto IL_0110;
				}
				goto IL_006d;
				IL_0049:
				P_0 = P_0.ReadValue();
				int num = 7;
				if (!IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0022;
				}
				goto IL_0085;
				IL_0077:
				if (IntPtr.Size != 8)
				{
					break;
				}
				num = 3;
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0022;
				}
				goto IL_006d;
				IL_009d:
				return ToInt64().Bits.Int64 > ((Int32Value)P_0).ToInt64().Bits.Int64;
				IL_0022:
				switch (num)
				{
				case 4:
					break;
				case 2:
					goto IL_006d;
				default:
					goto IL_0077;
				case 7:
					goto IL_0085;
				case 5:
					continue;
				case 1:
					goto IL_009d;
				case 3:
					return ToInt64().Bits.Int64 > ((NativeIntegerValue)P_0).ToInt64().Bits.Int64;
				case 6:
					goto IL_0110;
				}
				goto IL_0049;
				IL_0110:
				throw new VmOperandException();
				IL_006d:
				if (IntPtr.Size == 8)
				{
					num = 1;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0022;
					}
					goto IL_009d;
				}
				return ToInt32().Bits.Int32 > ((Int32Value)P_0).Bits.Int32;
			}
			return ToInt32().Bits.Int32 > ((NativeIntegerValue)P_0).ToInt32().Bits.Int32;
		}

		public override bool GreaterThanUnsigned(VmValue P_0)
		{
			while (true)
			{
				if (!P_0.IsManagedReference())
				{
					goto IL_0084;
				}
				goto IL_008e;
				IL_008e:
				P_0 = P_0.ReadValue();
				int num = 6;
				if (GetNativeIntegerValueObfuscationSentinel() != null)
				{
					goto IL_0015;
				}
				goto IL_0084;
				IL_0015:
				switch (num)
				{
				case 5:
					break;
				case 3:
					goto IL_004f;
				case 6:
					goto IL_0068;
				case 7:
				case 8:
					goto IL_0084;
				case 2:
					goto IL_008e;
				case 9:
					continue;
				case 4:
					return ToInt64().Bits.UInt64 > ((NativeIntegerValue)P_0).ToInt64().Bits.UInt64;
				default:
					goto IL_00de;
				case 1:
					goto IL_010d;
				}
				goto IL_0047;
				IL_0084:
				if (!P_0.IsInt32Value())
				{
					goto IL_0047;
				}
				goto IL_0068;
				IL_0068:
				if (IntPtr.Size != 8)
				{
					break;
				}
				num = 1;
				if (GetNativeIntegerValueObfuscationSentinel() == null)
				{
					goto IL_0015;
				}
				goto IL_010d;
				IL_00de:
				throw new VmOperandException();
				IL_010d:
				return ToInt64().Bits.UInt64 > ((Int32Value)P_0).ToInt64().Bits.UInt64;
				IL_0047:
				if (P_0.IsNativeIntegerValue())
				{
					goto IL_004f;
				}
				num = 0;
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0015;
				}
				goto IL_00de;
				IL_004f:
				if (IntPtr.Size == 8)
				{
					num = 4;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0015;
					}
					goto IL_00de;
				}
				return ToInt32().Bits.UInt32 > ((NativeIntegerValue)P_0).ToInt32().Bits.UInt32;
			}
			return ToInt32().Bits.UInt32 > ((Int32Value)P_0).Bits.UInt32;
		}

		public override bool LessThanOrEqual(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 2;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0036;
					}
					goto IL_006f;
				}
				goto IL_0079;
				IL_001f:
				if (IntPtr.Size == 8)
				{
					num = 4;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0036;
					}
					goto IL_006f;
				}
				return ToInt32().Bits.Int32 <= ((Int32Value)P_0).Bits.Int32;
				IL_0079:
				P_0 = P_0.ReadValue();
				goto IL_006f;
				IL_006f:
				if (!P_0.IsInt32Value())
				{
					if (!P_0.IsNativeIntegerValue())
					{
						throw new VmOperandException();
					}
					num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() != null)
					{
						goto IL_009d;
					}
				}
				else
				{
					num = 2;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_001f;
					}
				}
				goto IL_0036;
				IL_0036:
				switch (num)
				{
				case 6:
					break;
				case 2:
				case 5:
					goto IL_006f;
				case 1:
					goto IL_0079;
				case 3:
					continue;
				default:
					goto IL_009d;
				case 4:
					return ToInt64().Bits.Int64 <= ((Int32Value)P_0).ToInt64().Bits.Int64;
				case 7:
					goto end_IL_0092;
				}
				goto IL_001f;
				IL_009d:
				if (IntPtr.Size == 8)
				{
					break;
				}
				return ToInt32().Bits.Int32 <= ((NativeIntegerValue)P_0).ToInt32().Bits.Int32;
				continue;
				end_IL_0092:
				break;
			}
			return ToInt64().Bits.Int64 <= ((NativeIntegerValue)P_0).ToInt64().Bits.Int64;
		}

		public override bool LessThanOrEqualUnsigned(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0011;
				}
				goto IL_0056;
				IL_0056:
				if (P_0.IsInt32Value())
				{
					goto IL_004c;
				}
				if (!P_0.IsNativeIntegerValue())
				{
					goto IL_00c7;
				}
				goto IL_00cd;
				IL_004c:
				int num;
				if (IntPtr.Size == 8)
				{
					num = 0;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_0025;
					}
					goto IL_006b;
				}
				return ToInt32().Bits.UInt32 <= ((Int32Value)P_0).Bits.UInt32;
				IL_00cd:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_00d5;
				IL_0025:
				switch (num)
				{
				case 4:
					break;
				case 7:
					goto IL_004c;
				case 3:
					goto IL_0056;
				case 5:
					continue;
				default:
					goto IL_006b;
				case 6:
					goto IL_00c7;
				case 1:
					goto IL_00cd;
				case 2:
					goto IL_00d5;
				}
				goto IL_0011;
				IL_006b:
				return ToInt64().Bits.UInt64 <= ((Int32Value)P_0).ToInt64().Bits.UInt64;
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 3;
				if (IsNativeIntegerValueObfuscationSentinelNull())
				{
					goto IL_0025;
				}
				goto IL_00cd;
				IL_00d5:
				return ToInt64().Bits.UInt64 <= ((NativeIntegerValue)P_0).ToInt64().Bits.UInt64;
				IL_00c7:
				throw new VmOperandException();
			}
			return ToInt32().Bits.UInt32 <= ((NativeIntegerValue)P_0).ToInt32().Bits.UInt32;
		}

		public override bool LessThan(VmValue P_0)
		{
			while (true)
			{
				IL_009c:
				if (!P_0.IsManagedReference())
				{
					goto IL_0085;
				}
				goto IL_0092;
				IL_0092:
				P_0 = P_0.ReadValue();
				goto IL_0085;
				IL_0085:
				while (true)
				{
					if (!P_0.IsInt32Value())
					{
						if (P_0.IsNativeIntegerValue())
						{
							goto IL_0069;
						}
						goto IL_011d;
					}
					int num = 0;
					if (GetNativeIntegerValueObfuscationSentinel() == null)
					{
						goto IL_0024;
					}
					goto IL_003e;
					IL_00a7:
					return ToInt64().Bits.Int64 < ((NativeIntegerValue)P_0).ToInt64().Bits.Int64;
					IL_0024:
					if (IntPtr.Size == 8)
					{
						num = 2;
						if (!IsNativeIntegerValueObfuscationSentinelNull())
						{
							goto IL_003e;
						}
						goto IL_00f5;
					}
					return ToInt32().Bits.Int32 < ((Int32Value)P_0).Bits.Int32;
					IL_0069:
					if (IntPtr.Size == 8)
					{
						num = 0;
						if (GetNativeIntegerValueObfuscationSentinel() != null)
						{
							goto IL_003e;
						}
						goto IL_00a7;
					}
					return ToInt32().Bits.Int32 < ((NativeIntegerValue)P_0).ToInt32().Bits.Int32;
					IL_00f5:
					return ToInt64().Bits.Int64 < ((Int32Value)P_0).ToInt64().Bits.Int64;
					IL_003e:
					switch (num)
					{
					case 1:
						break;
					case 4:
						goto IL_0069;
					case 5:
					case 8:
						continue;
					case 7:
						goto end_IL_0085;
					case 6:
						goto IL_009c;
					default:
						goto IL_00a7;
					case 2:
						goto IL_00f5;
					case 3:
						goto IL_011d;
					}
					goto IL_0024;
					IL_011d:
					throw new VmOperandException();
					continue;
					end_IL_0085:
					break;
				}
				goto IL_0092;
			}
		}

		public override bool LessThanUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 1;
					if (IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_003e;
				}
				goto IL_004b;
				IL_00cf:
				throw new VmOperandException();
				IL_004b:
				P_0 = P_0.ReadValue();
				goto IL_003e;
				IL_003e:
				if (!P_0.IsInt32Value())
				{
					num = 4;
					if (!IsNativeIntegerValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_006f;
				}
				goto IL_00d5;
				IL_00d5:
				if (IntPtr.Size != 8)
				{
					break;
				}
				goto IL_00dd;
				IL_000f:
				switch (num)
				{
				case 1:
				case 7:
					break;
				case 8:
					goto IL_004b;
				case 2:
					continue;
				case 6:
					goto IL_006f;
				default:
					goto IL_0077;
				case 5:
					goto IL_007f;
				case 9:
					goto IL_00cf;
				case 4:
					goto IL_00d5;
				case 3:
					goto IL_00dd;
				}
				goto IL_003e;
				IL_006f:
				if (P_0.IsNativeIntegerValue())
				{
					goto IL_0077;
				}
				goto IL_00cf;
				IL_0077:
				if (IntPtr.Size != 8)
				{
					return ToInt32().Bits.UInt32 < ((NativeIntegerValue)P_0).ToInt32().Bits.UInt32;
				}
				goto IL_007f;
				IL_007f:
				return ToInt64().Bits.UInt64 < ((NativeIntegerValue)P_0).ToInt64().Bits.UInt64;
				IL_00dd:
				return ToInt64().Bits.UInt64 < ((Int32Value)P_0).ToInt64().Bits.UInt64;
			}
			return ToInt32().Bits.UInt32 < ((Int32Value)P_0).Bits.UInt32;
		}

		internal static bool IsNativeIntegerValueObfuscationSentinelNull()
		{
			return NativeIntegerValueObfuscationSentinel == null;
		}

		internal static NativeIntegerValue GetNativeIntegerValueObfuscationSentinel()
		{
			return NativeIntegerValueObfuscationSentinel;
		}
	}

	private abstract class NumericValue : VmValue
	{
		public abstract bool IsZero();

		public abstract bool IsNonZero();

		public abstract VmValue ConvertToPrimitiveType(VmPrimitiveType P_0);

		public abstract Int32Value ToBooleanValue();

		public abstract Int32Value ToSByte();

		public abstract Int32Value ToByte();

		public abstract Int32Value ToInt16();

		public abstract Int32Value ToUInt16();

		public abstract Int32Value ToInt32();

		public abstract Int32Value ToUInt32();

		public abstract Int64Value ToInt64();

		public abstract Int64Value ToUInt64();

		public abstract Int32Value ConvertToSByte();

		public abstract Int32Value ConvertToInt16();

		public abstract Int32Value ConvertToInt32();

		public abstract Int64Value ConvertToInt64();

		public abstract Int32Value ConvertToByte();

		public abstract Int32Value ConvertToUInt16();

		public abstract Int32Value ConvertToUInt32();

		public abstract Int64Value ConvertToUInt64();

		public abstract Int32Value ToSByteChecked();

		public abstract Int32Value ToSByteCheckedUnsigned();

		public abstract Int32Value ToInt16Checked();

		public abstract Int32Value ToInt16CheckedUnsigned();

		public abstract Int32Value ToInt32Checked();

		public abstract Int32Value ToInt32CheckedUnsigned();

		public abstract Int64Value ToInt64Checked();

		public abstract Int64Value ToInt64CheckedUnsigned();

		public abstract Int32Value ToByteChecked();

		public abstract Int32Value ToByteCheckedUnsigned();

		public abstract Int32Value ToUInt16Checked();

		public abstract Int32Value ToUInt16CheckedUnsigned();

		public abstract Int32Value ToUInt32Checked();

		public abstract Int32Value ToUInt32CheckedUnsigned();

		public abstract Int64Value ToUInt64Checked();

		public abstract Int64Value ToUInt64CheckedUnsigned();

		public abstract FloatingPointValue ToSingle();

		public abstract FloatingPointValue ToDouble();

		public abstract FloatingPointValue ToDoubleUnsigned();

		public abstract NativeIntegerValue ToNativeInt();

		public abstract NativeIntegerValue ToNativeUInt();

		public abstract NativeIntegerValue ToNativeIntChecked();

		public abstract NativeIntegerValue ToNativeUIntChecked();

		public abstract NativeIntegerValue ToNativeIntCheckedUnsigned();

		public abstract NativeIntegerValue ToNativeUIntCheckedUnsigned();

		public abstract VmValue Negate();

		public abstract VmValue Add(VmValue P_0);

		public abstract VmValue AddChecked(VmValue P_0);

		public abstract VmValue AddCheckedUnsigned(VmValue P_0);

		public abstract VmValue Subtract(VmValue P_0);

		public abstract VmValue SubtractChecked(VmValue P_0);

		public abstract VmValue SubtractCheckedUnsigned(VmValue P_0);

		public abstract VmValue Multiply(VmValue P_0);

		public abstract VmValue MultiplyChecked(VmValue P_0);

		public abstract VmValue MultiplyCheckedUnsigned(VmValue P_0);

		public abstract VmValue Divide(VmValue P_0);

		public abstract VmValue DivideUnsigned(VmValue P_0);

		public abstract VmValue Remainder(VmValue P_0);

		public abstract VmValue RemainderUnsigned(VmValue P_0);

		public abstract VmValue BitwiseAnd(VmValue P_0);

		public abstract VmValue BitwiseOr(VmValue P_0);

		public abstract VmValue BitwiseNot();

		public abstract VmValue BitwiseXor(VmValue P_0);

		public abstract NumericValue CloneNumericValue();

		public abstract VmValue ShiftLeft(VmValue P_0);

		public abstract VmValue ShiftRight(VmValue P_0);

		public abstract VmValue ShiftRightUnsigned(VmValue P_0);

		public abstract bool GreaterThanOrEqual(VmValue P_0);

		public abstract bool GreaterThanOrEqualUnsigned(VmValue P_0);

		public abstract bool GreaterThan(VmValue P_0);

		public abstract bool GreaterThanUnsigned(VmValue P_0);

		public abstract bool LessThanOrEqual(VmValue P_0);

		public abstract bool LessThanOrEqualUnsigned(VmValue P_0);

		public abstract bool LessThan(VmValue P_0);

		public abstract bool LessThanUnsigned(VmValue P_0);

		internal override bool IsNumericValue()
		{
			return true;
		}

		protected NumericValue()
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
		{
		}

		private NumericValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
			: base()
		{
		}
	}

	private class FloatingPointValue : NumericValue
	{
		public double Value;

		public VmPrimitiveType PrimitiveType;

		private static FloatingPointValue FloatingPointValueObfuscationSentinel;

		internal override void CopyFrom(VmValue P_0)
		{
			while (true)
			{
				Value = ((FloatingPointValue)P_0).Value;
				int num = 1;
				if (GetFloatingPointValueObfuscationSentinel() == null)
				{
					goto IL_0003;
				}
				goto IL_0020;
				IL_0020:
				switch (num)
				{
				case 1:
					break;
				default:
					return;
				case 2:
					continue;
				case 0:
					return;
				}
				goto IL_0003;
				IL_0003:
				PrimitiveType = ((FloatingPointValue)P_0).PrimitiveType;
				num = 0;
				if (GetFloatingPointValueObfuscationSentinel() == null)
				{
					break;
				}
				goto IL_0020;
			}
		}

		internal override void Assign(VmValue P_0)
		{
			while (true)
			{
				CopyFrom(P_0);
				if (IsFloatingPointValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		public FloatingPointValue(double P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private FloatingPointValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, double P_0)
			: base()
		{
			Kind = (VmValueKind)5;
			PrimitiveType = (VmPrimitiveType)10;
			Value = P_0;
		}

		public FloatingPointValue(FloatingPointValue P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private FloatingPointValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, FloatingPointValue P_0)
			: base()
		{
			Kind = P_0.Kind;
			PrimitiveType = P_0.PrimitiveType;
			Value = P_0.Value;
		}

		public override NumericValue CloneNumericValue()
		{
			return new FloatingPointValue(this);
		}

		public FloatingPointValue(double P_0, VmPrimitiveType P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private FloatingPointValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, double P_0, VmPrimitiveType P_1)
			: base()
		{
			Kind = (VmValueKind)5;
			Value = P_0;
			PrimitiveType = P_1;
		}

		public FloatingPointValue(float P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private FloatingPointValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, float P_0)
			: base()
		{
			Kind = (VmValueKind)5;
			Value = P_0;
			PrimitiveType = (VmPrimitiveType)9;
		}

		public FloatingPointValue(float P_0, VmPrimitiveType P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private FloatingPointValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, float P_0, VmPrimitiveType P_1)
			: base()
		{
			Kind = (VmValueKind)5;
			Value = P_0;
			PrimitiveType = P_1;
		}

		public override bool IsZero()
		{
			return Value == 0.0;
		}

		public override bool IsNonZero()
		{
			return !IsZero();
		}

		public override string ToString()
		{
			return Value.ToString();
		}

		public override VmValue ConvertToPrimitiveType(VmPrimitiveType P_0)
		{
			VmDiagnosticCode VmDiagnosticCode = default(VmDiagnosticCode);
			while (true)
			{
				int num;
				switch (P_0)
				{
				default:
					num = 0;
					if (GetFloatingPointValueObfuscationSentinel() == null)
					{
						goto IL_0010;
					}
					goto IL_0022;
				case (VmPrimitiveType)1:
					return ToSByte();
				case (VmPrimitiveType)2:
					return ToByte();
				case (VmPrimitiveType)3:
					return ToInt16();
				case (VmPrimitiveType)4:
					return ToUInt16();
				case (VmPrimitiveType)5:
					return ToInt32();
				case (VmPrimitiveType)6:
					return ToUInt32();
				case (VmPrimitiveType)7:
					return ToInt64();
				case (VmPrimitiveType)8:
					return ToUInt64();
				case (VmPrimitiveType)9:
					goto IL_00ba;
				case (VmPrimitiveType)10:
					return ToDouble();
				case (VmPrimitiveType)11:
					{
						return ToBooleanValue();
					}
					IL_0022:
					switch (num)
					{
					case 1:
						goto end_IL_003c;
					case 2:
						throw new Exception(VmDiagnosticCode.ToString());
					case 3:
						goto IL_00ba;
					}
					goto IL_0010;
					IL_00ba:
					return ToSingle();
					IL_0010:
					do
					{
						VmDiagnosticCode = (VmDiagnosticCode)4;
						num = 2;
					}
					while (!IsFloatingPointValueObfuscationSentinelNull());
					goto IL_0022;
					end_IL_003c:
					break;
				}
			}
		}

		internal override object ToObject(Type P_0)
		{
			while (true)
			{
				if (P_0 != null)
				{
					goto IL_0025;
				}
				goto IL_00be;
				IL_00be:
				if (P_0 == typeof(float))
				{
					break;
				}
				goto IL_009c;
				IL_009c:
				if (!(P_0 == typeof(double)))
				{
					if (!(P_0 == null))
					{
						goto IL_007a;
					}
					goto IL_00f2;
				}
				int num = 4;
				if (GetFloatingPointValueObfuscationSentinel() != null)
				{
					goto IL_003c;
				}
				goto IL_00e6;
				IL_00b6:
				P_0 = P_0.GetElementType();
				goto IL_00be;
				IL_00f2:
				if (PrimitiveType == (VmPrimitiveType)9)
				{
					goto IL_00fc;
				}
				goto IL_0109;
				IL_007a:
				if (P_0 == typeof(object))
				{
					goto IL_00f2;
				}
				num = 0;
				if (IsFloatingPointValueObfuscationSentinelNull())
				{
					goto IL_003c;
				}
				goto IL_0109;
				IL_00fc:
				return (float)Value;
				IL_003c:
				switch (num)
				{
				case 10:
					break;
				case 1:
					goto IL_007a;
				case 12:
					goto IL_009c;
				case 2:
					goto IL_00b6;
				case 3:
				case 8:
					goto IL_00be;
				case 11:
					continue;
				case 7:
					goto IL_00e6;
				case 4:
				case 6:
					goto IL_00f2;
				case 9:
					goto IL_00fc;
				default:
					goto IL_0109;
				case 5:
					goto end_IL_00d2;
				}
				goto IL_0025;
				IL_00e6:
				return Value;
				IL_0025:
				if (P_0.IsByRef)
				{
					goto IL_00b6;
				}
				num = 0;
				if (GetFloatingPointValueObfuscationSentinel() != null)
				{
					goto IL_003c;
				}
				goto IL_00be;
				IL_0109:
				return Value;
				continue;
				end_IL_00d2:
				break;
			}
			return (float)Value;
		}

		public override Int32Value ToBooleanValue()
		{
			int num;
			while (true)
			{
				if (!IsZero())
				{
					if (GetFloatingPointValueObfuscationSentinel() == null)
					{
						switch (0)
						{
						case 1:
							continue;
						}
					}
					num = 0;
				}
				else
				{
					num = 1;
				}
				break;
			}
			return new Int32Value(num);
		}

		internal override bool IsTruthy()
		{
			return IsNonZero();
		}

		public override Int32Value ToSByte()
		{
			return new Int32Value((sbyte)Value, (VmPrimitiveType)1);
		}

		public override Int32Value ToByte()
		{
			return new Int32Value((uint)(byte)Value, (VmPrimitiveType)2);
		}

		public override Int32Value ToInt16()
		{
			return new Int32Value((short)Value, (VmPrimitiveType)3);
		}

		public override Int32Value ToUInt16()
		{
			return new Int32Value((uint)(ushort)Value, (VmPrimitiveType)4);
		}

		public override Int32Value ToInt32()
		{
			return new Int32Value((int)Value, (VmPrimitiveType)5);
		}

		public override Int32Value ToUInt32()
		{
			return new Int32Value((uint)Value, (VmPrimitiveType)6);
		}

		public override Int64Value ToInt64()
		{
			return new Int64Value((long)Value, (VmPrimitiveType)7);
		}

		public override Int64Value ToUInt64()
		{
			return new Int64Value((ulong)Value, (VmPrimitiveType)8);
		}

		public override Int32Value ConvertToSByte()
		{
			return ToSByte();
		}

		public override Int32Value ConvertToInt16()
		{
			return ToInt16();
		}

		public override Int32Value ConvertToInt32()
		{
			return ToInt32();
		}

		public override Int64Value ConvertToInt64()
		{
			return ToInt64();
		}

		public override Int32Value ConvertToByte()
		{
			return ToByte();
		}

		public override Int32Value ConvertToUInt16()
		{
			return ToUInt16();
		}

		public override Int32Value ConvertToUInt32()
		{
			return ToUInt32();
		}

		public override Int64Value ConvertToUInt64()
		{
			return ToUInt64();
		}

		public override Int32Value ToSByteChecked()
		{
			return new Int32Value(checked((sbyte)Value), (VmPrimitiveType)1);
		}

		public override Int32Value ToSByteCheckedUnsigned()
		{
			return new Int32Value(checked((sbyte)Value), (VmPrimitiveType)1);
		}

		public override Int32Value ToInt16Checked()
		{
			return new Int32Value(checked((short)Value), (VmPrimitiveType)3);
		}

		public override Int32Value ToInt16CheckedUnsigned()
		{
			return new Int32Value(checked((short)Value), (VmPrimitiveType)3);
		}

		public override Int32Value ToInt32Checked()
		{
			return new Int32Value(checked((int)Value), (VmPrimitiveType)5);
		}

		public override Int32Value ToInt32CheckedUnsigned()
		{
			return new Int32Value(checked((int)Value), (VmPrimitiveType)5);
		}

		public override Int64Value ToInt64Checked()
		{
			return new Int64Value(checked((long)Value), (VmPrimitiveType)7);
		}

		public override Int64Value ToInt64CheckedUnsigned()
		{
			return new Int64Value(checked((long)Value), (VmPrimitiveType)7);
		}

		public override Int32Value ToByteChecked()
		{
			return new Int32Value(checked((byte)Value), (VmPrimitiveType)2);
		}

		public override Int32Value ToByteCheckedUnsigned()
		{
			return new Int32Value(checked((byte)Value), (VmPrimitiveType)2);
		}

		public override Int32Value ToUInt16Checked()
		{
			return new Int32Value(checked((ushort)Value), (VmPrimitiveType)4);
		}

		public override Int32Value ToUInt16CheckedUnsigned()
		{
			return new Int32Value(checked((ushort)Value), (VmPrimitiveType)4);
		}

		public override Int32Value ToUInt32Checked()
		{
			return new Int32Value(checked((uint)Value), (VmPrimitiveType)6);
		}

		public override Int32Value ToUInt32CheckedUnsigned()
		{
			return new Int32Value(checked((uint)Value), (VmPrimitiveType)6);
		}

		public override Int64Value ToUInt64Checked()
		{
			return new Int64Value(checked((ulong)Value), (VmPrimitiveType)8);
		}

		public override Int64Value ToUInt64CheckedUnsigned()
		{
			return new Int64Value(checked((ulong)Value), (VmPrimitiveType)8);
		}

		public override FloatingPointValue ToSingle()
		{
			return new FloatingPointValue((float)Value, (VmPrimitiveType)9);
		}

		public override FloatingPointValue ToDouble()
		{
			return new FloatingPointValue(Value, (VmPrimitiveType)10);
		}

		public override FloatingPointValue ToDoubleUnsigned()
		{
			return new FloatingPointValue(Value);
		}

		public override NativeIntegerValue ToNativeInt()
		{
			while (IntPtr.Size == 8)
			{
				if (!IsFloatingPointValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ConvertToInt64().Bits.Int64);
			}
			return new NativeIntegerValue(ConvertToInt32().Bits.Int32);
		}

		public override NativeIntegerValue ToNativeUInt()
		{
			while (IntPtr.Size == 8)
			{
				if (GetFloatingPointValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ConvertToUInt64().Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)ConvertToUInt32().Bits.UInt32);
		}

		public override NativeIntegerValue ToNativeIntChecked()
		{
			while (IntPtr.Size == 8)
			{
				if (GetFloatingPointValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToInt64Checked().Bits.Int64);
			}
			return new NativeIntegerValue(ToInt32Checked().Bits.Int32);
		}

		public override NativeIntegerValue ToNativeUIntChecked()
		{
			while (IntPtr.Size == 8)
			{
				if (GetFloatingPointValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToUInt64Checked().Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)ToUInt32Checked().Bits.UInt32);
		}

		public override NativeIntegerValue ToNativeIntCheckedUnsigned()
		{
			while (IntPtr.Size == 8)
			{
				if (IsFloatingPointValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToInt64CheckedUnsigned().Bits.Int64);
			}
			return new NativeIntegerValue(ToInt32CheckedUnsigned().Bits.Int32);
		}

		public override NativeIntegerValue ToNativeUIntCheckedUnsigned()
		{
			while (IntPtr.Size == 8)
			{
				if (IsFloatingPointValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new NativeIntegerValue(ToUInt64CheckedUnsigned().Bits.UInt64);
			}
			return new NativeIntegerValue((ulong)ToUInt32CheckedUnsigned().Bits.UInt32);
		}

		public override VmValue Negate()
		{
			while (PrimitiveType == (VmPrimitiveType)9)
			{
				if (GetFloatingPointValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new FloatingPointValue((float)(0.0 - Value));
			}
			return new FloatingPointValue(0.0 - Value);
		}

		public override VmValue Add(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!P_0.IsManagedReference())
				{
					num = 0;
					if (GetFloatingPointValueObfuscationSentinel() != null)
					{
						goto IL_000f;
					}
					goto IL_0028;
				}
				goto IL_0032;
				IL_000f:
				switch (num)
				{
				case 4:
					goto IL_0032;
				case 1:
					continue;
				case 2:
					goto end_IL_004b;
				}
				goto IL_0028;
				IL_0032:
				P_0 = P_0.ReadValue();
				goto IL_0028;
				IL_0028:
				if (P_0.IsFloatingPointValue())
				{
					num = 0;
					if (GetFloatingPointValueObfuscationSentinel() == null)
					{
						break;
					}
					goto IL_000f;
				}
				throw new VmOperandException();
				continue;
				end_IL_004b:
				break;
			}
			return new FloatingPointValue(Value + ((FloatingPointValue)P_0).Value);
		}

		public override VmValue AddChecked(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0003;
				}
				goto IL_0044;
				IL_0003:
				P_0 = P_0.ReadValue();
				int num = 0;
				if (GetFloatingPointValueObfuscationSentinel() == null)
				{
					goto IL_0017;
				}
				goto IL_0044;
				IL_0044:
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				num = 2;
				if (GetFloatingPointValueObfuscationSentinel() != null)
				{
					break;
				}
				goto IL_0017;
				IL_0017:
				switch (num)
				{
				case 5:
					break;
				default:
					goto IL_0044;
				case 4:
					continue;
				case 2:
					throw new VmOperandException();
				case 1:
					goto end_IL_0051;
				}
				goto IL_0003;
				continue;
				end_IL_0051:
				break;
			}
			return new FloatingPointValue(Value + ((FloatingPointValue)P_0).Value);
		}

		public override VmValue AddCheckedUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (IsFloatingPointValueObfuscationSentinelNull())
					{
						goto IL_0010;
					}
					goto IL_0024;
				}
				goto IL_0046;
				IL_0046:
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				throw new VmOperandException();
				IL_0024:
				switch (num)
				{
				case 1:
					break;
				case 2:
					continue;
				default:
					goto IL_0046;
				case 3:
					goto end_IL_003b;
				}
				goto IL_0010;
				IL_0010:
				P_0 = P_0.ReadValue();
				num = 0;
				if (GetFloatingPointValueObfuscationSentinel() != null)
				{
					goto IL_0024;
				}
				goto IL_0046;
				continue;
				end_IL_003b:
				break;
			}
			return new FloatingPointValue(Value + ((FloatingPointValue)P_0).Value);
		}

		public override VmValue Subtract(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0003;
				}
				int num = 0;
				if (GetFloatingPointValueObfuscationSentinel() != null)
				{
					goto IL_0017;
				}
				goto IL_004e;
				IL_004e:
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				throw new VmOperandException();
				IL_0003:
				P_0 = P_0.ReadValue();
				num = 0;
				if (IsFloatingPointValueObfuscationSentinelNull())
				{
					goto IL_0017;
				}
				goto IL_004e;
				IL_0017:
				switch (num)
				{
				case 3:
					break;
				case 2:
					continue;
				default:
					goto IL_004e;
				case 4:
					goto end_IL_0040;
				}
				goto IL_0003;
				continue;
				end_IL_0040:
				break;
			}
			return new FloatingPointValue(Value - ((FloatingPointValue)P_0).Value);
		}

		public override VmValue SubtractChecked(VmValue P_0)
		{
			while (true)
			{
				if (!P_0.IsManagedReference())
				{
					if (IsFloatingPointValueObfuscationSentinelNull())
					{
						switch (1)
						{
						case 2:
							break;
						case 5:
							goto IL_003b;
						case 1:
						case 4:
							goto IL_0043;
						case 3:
							goto IL_004b;
						default:
							goto end_IL_002e;
						}
						continue;
					}
					break;
				}
				goto IL_003b;
				IL_0043:
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				goto IL_004b;
				IL_004b:
				throw new VmOperandException();
				IL_003b:
				P_0 = P_0.ReadValue();
				goto IL_0043;
				continue;
				end_IL_002e:
				break;
			}
			return new FloatingPointValue(Value - ((FloatingPointValue)P_0).Value);
		}

		public override VmValue SubtractCheckedUnsigned(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0003;
				}
				int num = 0;
				if (!IsFloatingPointValueObfuscationSentinelNull())
				{
					goto IL_0017;
				}
				goto IL_0052;
				IL_005a:
				throw new VmOperandException();
				IL_0003:
				P_0 = P_0.ReadValue();
				num = 2;
				if (IsFloatingPointValueObfuscationSentinelNull())
				{
					goto IL_0017;
				}
				goto IL_0052;
				IL_0017:
				switch (num)
				{
				case 5:
					break;
				case 1:
					continue;
				default:
					goto IL_0052;
				case 4:
					goto IL_005a;
				case 3:
					goto end_IL_0044;
				}
				goto IL_0003;
				IL_0052:
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				goto IL_005a;
				continue;
				end_IL_0044:
				break;
			}
			return new FloatingPointValue(Value - ((FloatingPointValue)P_0).Value);
		}

		public override VmValue Multiply(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0003;
				}
				goto IL_0057;
				IL_0003:
				P_0 = P_0.ReadValue();
				int num = 0;
				if (GetFloatingPointValueObfuscationSentinel() != null)
				{
					goto IL_0026;
				}
				goto IL_0057;
				IL_0026:
				switch (num)
				{
				case 4:
					break;
				case 7:
					goto IL_004d;
				default:
					goto IL_0057;
				case 6:
					continue;
				case 1:
				case 3:
					goto IL_006c;
				case 2:
					goto end_IL_0061;
				}
				goto IL_0003;
				IL_0057:
				if (P_0.IsFloatingPointValue())
				{
					goto IL_004d;
				}
				goto IL_006c;
				IL_004d:
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				num = 1;
				if (GetFloatingPointValueObfuscationSentinel() == null)
				{
					goto IL_0026;
				}
				goto IL_006c;
				IL_006c:
				throw new VmOperandException();
				continue;
				end_IL_0061:
				break;
			}
			return new FloatingPointValue(Value * ((FloatingPointValue)P_0).Value);
		}

		public override VmValue MultiplyChecked(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 1;
					if (!IsFloatingPointValueObfuscationSentinelNull())
					{
						goto IL_0011;
					}
					goto IL_0025;
				}
				goto IL_003d;
				IL_0025:
				switch (num)
				{
				case 1:
					break;
				case 3:
					goto IL_003d;
				case 2:
					continue;
				default:
					goto end_IL_0056;
				}
				goto IL_0011;
				IL_003d:
				if (P_0.IsFloatingPointValue())
				{
					num = 0;
					if (GetFloatingPointValueObfuscationSentinel() == null)
					{
						break;
					}
					goto IL_0025;
				}
				throw new VmOperandException();
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 3;
				if (IsFloatingPointValueObfuscationSentinelNull())
				{
					goto IL_0025;
				}
				goto IL_003d;
				continue;
				end_IL_0056:
				break;
			}
			return new FloatingPointValue(Value * ((FloatingPointValue)P_0).Value);
		}

		public override VmValue MultiplyCheckedUnsigned(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0012;
				}
				goto IL_003e;
				IL_003e:
				int num;
				do
				{
					if (P_0.IsFloatingPointValue())
					{
						num = 1;
						continue;
					}
					throw new VmOperandException();
				}
				while (!IsFloatingPointValueObfuscationSentinelNull());
				goto IL_0026;
				IL_0026:
				switch (num)
				{
				case 2:
					break;
				default:
					goto IL_003e;
				case 3:
					continue;
				case 1:
					return new FloatingPointValue(Value * ((FloatingPointValue)P_0).Value);
				}
				goto IL_0012;
				IL_0012:
				P_0 = P_0.ReadValue();
				num = 0;
				if (IsFloatingPointValueObfuscationSentinelNull())
				{
					goto IL_0026;
				}
				goto IL_003e;
			}
		}

		public override VmValue Divide(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (!IsFloatingPointValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_0024;
				}
				goto IL_002c;
				IL_000f:
				switch (num)
				{
				case 3:
					goto IL_002c;
				case 1:
					continue;
				case 2:
					goto end_IL_0045;
				}
				goto IL_0024;
				IL_002c:
				if (P_0.IsFloatingPointValue())
				{
					num = 0;
					if (IsFloatingPointValueObfuscationSentinelNull())
					{
						break;
					}
					goto IL_000f;
				}
				throw new VmOperandException();
				IL_0024:
				P_0 = P_0.ReadValue();
				goto IL_002c;
				continue;
				end_IL_0045:
				break;
			}
			return new FloatingPointValue(Value / ((FloatingPointValue)P_0).Value);
		}

		public override VmValue DivideUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (GetFloatingPointValueObfuscationSentinel() != null)
					{
						goto IL_000f;
					}
					goto IL_0028;
				}
				goto IL_0030;
				IL_0028:
				P_0 = P_0.ReadValue();
				goto IL_0030;
				IL_0030:
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				num = 2;
				if (!IsFloatingPointValueObfuscationSentinelNull())
				{
					goto IL_000f;
				}
				goto IL_0054;
				IL_0054:
				throw new VmOperandException();
				IL_000f:
				switch (num)
				{
				case 4:
					goto IL_0030;
				case 1:
					continue;
				case 2:
					goto IL_0054;
				case 3:
					goto end_IL_0049;
				}
				goto IL_0028;
				continue;
				end_IL_0049:
				break;
			}
			return new FloatingPointValue(Value / ((FloatingPointValue)P_0).Value);
		}

		public override VmValue Remainder(VmValue P_0)
		{
			while (true)
			{
				IL_0057:
				int num;
				if (P_0.IsManagedReference())
				{
					num = 2;
					if (IsFloatingPointValueObfuscationSentinelNull())
					{
						goto IL_0026;
					}
				}
				goto IL_003e;
				IL_0026:
				while (true)
				{
					switch (num)
					{
					case 2:
						P_0 = P_0.ReadValue();
						num = 0;
						if (!IsFloatingPointValueObfuscationSentinelNull())
						{
							continue;
						}
						break;
					case 3:
						goto IL_0057;
					case 1:
						return new FloatingPointValue(Value % ((FloatingPointValue)P_0).Value);
					}
					break;
				}
				goto IL_003e;
				IL_003e:
				do
				{
					if (P_0.IsFloatingPointValue())
					{
						num = 1;
						continue;
					}
					throw new VmOperandException();
				}
				while (GetFloatingPointValueObfuscationSentinel() != null);
				goto IL_0026;
			}
		}

		public override VmValue RemainderUnsigned(VmValue P_0)
		{
			while (true)
			{
				IL_0042:
				if (!P_0.IsManagedReference())
				{
					goto IL_002e;
				}
				goto IL_0038;
				IL_0038:
				P_0 = P_0.ReadValue();
				goto IL_002e;
				IL_002e:
				while (true)
				{
					if (!P_0.IsFloatingPointValue())
					{
						if (GetFloatingPointValueObfuscationSentinel() != null)
						{
							switch (0)
							{
							case 2:
							case 4:
								break;
							case 5:
								goto end_IL_002e;
							case 3:
								goto IL_0042;
							default:
								goto IL_004d;
							case 1:
								goto IL_0053;
							}
							continue;
						}
						goto IL_004d;
					}
					goto IL_0053;
					IL_004d:
					throw new VmOperandException();
					IL_0053:
					return new FloatingPointValue(Value % ((FloatingPointValue)P_0).Value);
					continue;
					end_IL_002e:
					break;
				}
				goto IL_0038;
			}
		}

		public override VmValue BitwiseAnd(VmValue P_0)
		{
			throw new VmOperandException();
		}

		public override VmValue BitwiseOr(VmValue P_0)
		{
			throw new VmOperandException();
		}

		public override VmValue BitwiseNot()
		{
			throw new VmOperandException();
		}

		public override VmValue BitwiseXor(VmValue P_0)
		{
			throw new VmOperandException();
		}

		public override VmValue ShiftLeft(VmValue P_0)
		{
			throw new VmOperandException();
		}

		public override VmValue ShiftRight(VmValue P_0)
		{
			throw new VmOperandException();
		}

		public override VmValue ShiftRightUnsigned(VmValue P_0)
		{
			throw new VmOperandException();
		}

		internal override VmValue ReadValue()
		{
			return this;
		}

		internal override bool IsEqualTo(VmValue P_0)
		{
			VmValue VmValue = default(VmValue);
			while (!P_0.IsObjectValue())
			{
				int num = 0;
				if (IsFloatingPointValueObfuscationSentinelNull())
				{
					goto IL_0028;
				}
				goto IL_0032;
				IL_0012:
				VmValue = P_0.ReadValue();
				num = 5;
				if (GetFloatingPointValueObfuscationSentinel() != null)
				{
					break;
				}
				goto IL_0032;
				IL_0032:
				switch (num)
				{
				case 4:
					break;
				default:
					goto IL_0028;
				case 1:
					continue;
				case 5:
					if (VmValue.IsFloatingPointValue())
					{
						return Value == ((FloatingPointValue)VmValue).Value;
					}
					goto case 3;
				case 3:
					return false;
				case 6:
					goto IL_0082;
				case 2:
					goto end_IL_0055;
				}
				goto IL_0012;
				IL_0028:
				if (!P_0.IsManagedReference())
				{
					goto IL_0012;
				}
				goto IL_0082;
				IL_0082:
				return ((ReferenceValue)P_0).IsEqualTo(this);
				continue;
				end_IL_0055:
				break;
			}
			return false;
		}

		internal override bool IsNotEqualTo(VmValue P_0)
		{
			while (!P_0.IsObjectValue())
			{
				while (true)
				{
					if (!P_0.IsManagedReference())
					{
						while (true)
						{
							VmValue VmValue = P_0.ReadValue();
							int num = 2;
							if (IsFloatingPointValueObfuscationSentinelNull())
							{
								goto IL_0003;
							}
							goto IL_0018;
							IL_0018:
							switch (num)
							{
							case 2:
								break;
							case 4:
								continue;
							case 5:
								goto end_IL_003b;
							case 6:
								goto end_IL_0052;
							default:
								goto IL_0067;
							case 1:
								goto IL_0081;
							case 3:
								goto end_IL_005c;
							}
							goto IL_0003;
							IL_0003:
							if (!VmValue.IsFloatingPointValue())
							{
								num = 0;
								if (IsFloatingPointValueObfuscationSentinelNull())
								{
									goto IL_0018;
								}
								goto IL_0067;
							}
							return Value != ((FloatingPointValue)VmValue).Value;
							IL_0067:
							return false;
							continue;
							end_IL_003b:
							break;
						}
						continue;
					}
					goto IL_0081;
					IL_0081:
					return ((ReferenceValue)P_0).IsNotEqualTo(this);
					continue;
					end_IL_0052:
					break;
				}
				continue;
				end_IL_005c:
				break;
			}
			return false;
		}

		public override bool GreaterThanOrEqual(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (IsFloatingPointValueObfuscationSentinelNull())
					{
						goto IL_000f;
					}
					goto IL_0024;
				}
				goto IL_002c;
				IL_000f:
				switch (num)
				{
				case 3:
					goto IL_002c;
				case 1:
					continue;
				case 2:
					goto end_IL_0045;
				}
				goto IL_0024;
				IL_002c:
				if (P_0.IsFloatingPointValue())
				{
					num = 0;
					if (IsFloatingPointValueObfuscationSentinelNull())
					{
						break;
					}
					goto IL_000f;
				}
				throw new VmOperandException();
				IL_0024:
				P_0 = P_0.ReadValue();
				goto IL_002c;
				continue;
				end_IL_0045:
				break;
			}
			return Value >= ((FloatingPointValue)P_0).Value;
		}

		public override bool GreaterThanOrEqualUnsigned(VmValue P_0)
		{
			while (true)
			{
				IL_0032:
				if (P_0.IsManagedReference())
				{
					while (true)
					{
						P_0 = P_0.ReadValue();
						if (IsFloatingPointValueObfuscationSentinelNull())
						{
							break;
						}
						switch (0)
						{
						case 1:
							break;
						case 3:
							goto IL_0032;
						default:
							goto end_IL_0003;
						case 4:
							goto end_IL_0032;
						}
						continue;
						end_IL_0003:
						break;
					}
				}
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				throw new VmOperandException();
				continue;
				end_IL_0032:
				break;
			}
			return Value >= ((FloatingPointValue)P_0).Value;
		}

		public override bool GreaterThan(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0011;
				}
				goto IL_003d;
				IL_003d:
				int num;
				if (P_0.IsFloatingPointValue())
				{
					num = 0;
					if (GetFloatingPointValueObfuscationSentinel() == null)
					{
						break;
					}
					goto IL_0025;
				}
				throw new VmOperandException();
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 1;
				if (IsFloatingPointValueObfuscationSentinelNull())
				{
					goto IL_0025;
				}
				goto IL_003d;
				IL_0025:
				switch (num)
				{
				case 2:
					break;
				case 1:
					goto IL_003d;
				case 3:
					continue;
				default:
					goto end_IL_0047;
				}
				goto IL_0011;
				continue;
				end_IL_0047:
				break;
			}
			return Value > ((FloatingPointValue)P_0).Value;
		}

		public override bool GreaterThanUnsigned(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 0;
					if (IsFloatingPointValueObfuscationSentinelNull())
					{
						goto IL_0010;
					}
					goto IL_0024;
				}
				goto IL_0046;
				IL_0046:
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				throw new VmOperandException();
				IL_0024:
				switch (num)
				{
				case 1:
					continue;
				case 2:
					goto IL_0046;
				case 3:
					goto end_IL_003b;
				}
				goto IL_0010;
				IL_0010:
				P_0 = P_0.ReadValue();
				num = 1;
				if (GetFloatingPointValueObfuscationSentinel() != null)
				{
					goto IL_0024;
				}
				goto IL_0046;
				continue;
				end_IL_003b:
				break;
			}
			return Value > ((FloatingPointValue)P_0).Value;
		}

		public override bool LessThanOrEqual(VmValue P_0)
		{
			while (true)
			{
				IL_0032:
				if (P_0.IsManagedReference())
				{
					while (true)
					{
						P_0 = P_0.ReadValue();
						if (!IsFloatingPointValueObfuscationSentinelNull())
						{
							break;
						}
						switch (1)
						{
						case 3:
							goto IL_0032;
						case 1:
						case 2:
							goto end_IL_0003;
						case 4:
							goto end_IL_0032;
						}
						continue;
						end_IL_0003:
						break;
					}
				}
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				throw new VmOperandException();
				continue;
				end_IL_0032:
				break;
			}
			return Value <= ((FloatingPointValue)P_0).Value;
		}

		public override bool LessThanOrEqualUnsigned(VmValue P_0)
		{
			while (true)
			{
				IL_0036:
				if (P_0.IsManagedReference())
				{
					while (true)
					{
						P_0 = P_0.ReadValue();
						if (IsFloatingPointValueObfuscationSentinelNull())
						{
							break;
						}
						switch (0)
						{
						case 3:
							break;
						case 5:
							goto IL_0036;
						default:
							goto end_IL_0003;
						case 2:
							goto IL_0049;
						case 1:
							goto end_IL_0036;
						}
						continue;
						end_IL_0003:
						break;
					}
				}
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				goto IL_0049;
				IL_0049:
				throw new VmOperandException();
				continue;
				end_IL_0036:
				break;
			}
			return Value <= ((FloatingPointValue)P_0).Value;
		}

		public override bool LessThan(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (P_0.IsManagedReference())
				{
					num = 1;
					if (!IsFloatingPointValueObfuscationSentinelNull())
					{
						goto IL_0011;
					}
					goto IL_0025;
				}
				goto IL_0040;
				IL_0069:
				throw new VmOperandException();
				IL_0040:
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				num = 4;
				if (GetFloatingPointValueObfuscationSentinel() != null)
				{
					goto IL_0025;
				}
				goto IL_0069;
				IL_0011:
				P_0 = P_0.ReadValue();
				num = 3;
				if (!IsFloatingPointValueObfuscationSentinelNull())
				{
					break;
				}
				goto IL_0025;
				IL_0025:
				switch (num)
				{
				case 1:
					break;
				case 3:
					goto IL_0040;
				case 2:
					continue;
				case 4:
					goto IL_0069;
				default:
					goto end_IL_0059;
				}
				goto IL_0011;
				continue;
				end_IL_0059:
				break;
			}
			return Value < ((FloatingPointValue)P_0).Value;
		}

		public override bool LessThanUnsigned(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0037;
				}
				if (GetFloatingPointValueObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						break;
					case 2:
						goto IL_0037;
					default:
						goto IL_003f;
					case 4:
						goto end_IL_002a;
					}
					continue;
				}
				goto IL_003f;
				IL_0037:
				P_0 = P_0.ReadValue();
				goto IL_003f;
				IL_003f:
				if (P_0.IsFloatingPointValue())
				{
					break;
				}
				throw new VmOperandException();
				continue;
				end_IL_002a:
				break;
			}
			return Value < ((FloatingPointValue)P_0).Value;
		}

		internal static bool IsFloatingPointValueObfuscationSentinelNull()
		{
			return FloatingPointValueObfuscationSentinel == null;
		}

		internal static FloatingPointValue GetFloatingPointValueObfuscationSentinel()
		{
			return FloatingPointValueObfuscationSentinel;
		}
	}

	internal enum VmPrimitiveType : byte
	{

	}

	internal enum VmRuntimeTypeCode : byte
	{

	}

	private class VmMessageException : Exception
	{
		public VmMessageException(string P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private VmMessageException(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string P_0)
			: base(P_0)
		{
		}
	}

	private class VmOperandException : Exception
	{
		public VmOperandException()
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
		{
		}

		private VmOperandException(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
			: base()
		{
		}

		public VmOperandException(string P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private VmOperandException(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string P_0)
			: base(P_0)
		{
		}
	}

	internal class VmInstruction
	{
		internal VmOpCode OpCode;

		internal object Operand;

		internal static VmInstruction VmInstructionObfuscationSentinel;

		public override string ToString()
		{
			object obj;
			while (true)
			{
				obj = OpCode;
				if (!IsVmInstructionObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						goto IL_0032;
					case 2:
						goto IL_003a;
					case 3:
						goto end_IL_0001;
					}
					continue;
				}
				goto IL_0032;
				IL_003a:
				return obj.ToString() + 'H' + Operand.ToString();
				IL_0032:
				if (Operand == null)
				{
					break;
				}
				goto IL_003a;
				continue;
				end_IL_0001:
				break;
			}
			return obj.ToString();
		}

		public VmInstruction()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			OpCode = (VmOpCode)126;
		}

		internal static bool IsVmInstructionObfuscationSentinelNull()
		{
			return VmInstructionObfuscationSentinel == null;
		}

		internal static VmInstruction GetVmInstructionObfuscationSentinel()
		{
			return VmInstructionObfuscationSentinel;
		}
	}

	internal abstract class ReferenceValue : VmValue
	{
		public ReferenceValue()
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
		{
		}

		private ReferenceValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
			: base()
		{
		}

		internal override bool IsManagedReference()
		{
			return true;
		}

		internal abstract IntPtr GetReferenceAddress();

		internal abstract void StoreValue(VmValue P_0);

		internal override bool IsAddressValue()
		{
			return true;
		}
	}

	internal class LocalReferenceValue : ReferenceValue
	{
		private ExecutionFrame Frame;

		internal int LocalIndex;

		internal static LocalReferenceValue LocalReferenceValueObfuscationSentinel;

		public LocalReferenceValue(int P_0, ExecutionFrame P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private LocalReferenceValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, int P_0, ExecutionFrame P_1)
			: base()
		{
			Frame = P_1;
			LocalIndex = P_0;
			Kind = (VmValueKind)7;
		}

		internal override void CopyFrom(VmValue P_0)
		{
			VmLocalDefinition VmLocalDefinition = default(VmLocalDefinition);
			VmValue VmValue = default(VmValue);
			while (true)
			{
				IL_0125:
				int num;
				if (!(P_0 is LocalReferenceValue))
				{
					VmLocalDefinition = Frame.MethodBody.Locals[LocalIndex];
					num = 0;
					if (GetLocalReferenceValueObfuscationSentinel() == null)
					{
						goto IL_00d1;
					}
					goto IL_00eb;
				}
				goto IL_0038;
				IL_0099:
				StoreValue(VmValue);
				num = 9;
				if (GetLocalReferenceValueObfuscationSentinel() == null)
				{
					break;
				}
				goto IL_00eb;
				IL_00eb:
				while (true)
				{
					switch (num)
					{
					case 8:
						break;
					default:
						goto IL_0059;
					case 2:
						goto IL_006d;
					case 4:
						goto IL_008c;
					case 5:
						goto IL_0099;
					case 7:
						LocalIndex = ((LocalReferenceValue)P_0).LocalIndex;
						num = 1;
						if (GetLocalReferenceValueObfuscationSentinel() != null)
						{
							continue;
						}
						return;
					case 3:
						goto IL_00d1;
					case 9:
						goto IL_0125;
					case 1:
						return;
					case 10:
						return;
					case 11:
						return;
					}
					break;
				}
				goto IL_0038;
				IL_00d1:
				if (P_0 is ReferenceValue)
				{
					goto IL_006d;
				}
				num = 0;
				if (GetLocalReferenceValueObfuscationSentinel() != null)
				{
					goto IL_0059;
				}
				goto IL_00eb;
				IL_008c:
				VmValue = (P_0 as ReferenceValue).ReadValue();
				goto IL_0099;
				IL_0038:
				Frame = ((LocalReferenceValue)P_0).Frame;
				num = 7;
				if (GetLocalReferenceValueObfuscationSentinel() != null)
				{
					goto IL_0059;
				}
				goto IL_00eb;
				IL_0059:
				StoreValue(P_0);
				num = 10;
				if (GetLocalReferenceValueObfuscationSentinel() != null)
				{
					goto IL_006d;
				}
				goto IL_00eb;
				IL_006d:
				if ((int)(VmLocalDefinition.PrimitiveType & (VmPrimitiveType)226) > 0)
				{
					goto IL_008c;
				}
				num = 5;
				if (IsLocalReferenceValueObfuscationSentinelNull())
				{
					goto IL_0059;
				}
				goto IL_00eb;
			}
		}

		internal override void Assign(VmValue P_0)
		{
			while (true)
			{
				StoreValue(P_0);
				if (GetLocalReferenceValueObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		internal override IntPtr GetReferenceAddress()
		{
			throw new NotImplementedException();
		}

		internal override void StoreValue(VmValue P_0)
		{
			while (true)
			{
				Frame.Locals[LocalIndex] = P_0;
				if (GetLocalReferenceValueObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		internal override object ToObject(Type P_0)
		{
			while (Frame.Locals[LocalIndex] == null)
			{
				if (GetLocalReferenceValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return null;
			}
			return ReadValue().ToObject(P_0);
		}

		internal override VmValue ReadValue()
		{
			while (Frame.Locals[LocalIndex] == null)
			{
				if (GetLocalReferenceValueObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new ObjectValue(null);
			}
			return Frame.Locals[LocalIndex].ReadValue();
		}

		internal override bool IsIntegerValue()
		{
			return ReadValue().IsIntegerValue();
		}

		internal override bool IsEqualTo(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_002a;
				}
				int num = 1;
				if (GetLocalReferenceValueObfuscationSentinel() == null)
				{
					goto IL_000f;
				}
				goto IL_004d;
				IL_0050:
				if (((LocalReferenceValue)P_0).LocalIndex != LocalIndex)
				{
					return false;
				}
				goto IL_0063;
				IL_002a:
				if (!(P_0 is LocalReferenceValue))
				{
					break;
				}
				num = 0;
				if (GetLocalReferenceValueObfuscationSentinel() != null)
				{
					goto IL_000f;
				}
				goto IL_0050;
				IL_0063:
				return true;
				IL_000f:
				switch (num)
				{
				case 4:
					break;
				case 2:
					continue;
				case 1:
					goto IL_004d;
				default:
					goto IL_0050;
				case 3:
					goto IL_0063;
				}
				goto IL_002a;
				IL_004d:
				return false;
			}
			return false;
		}

		internal override bool IsNotEqualTo(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0048;
				}
				int num = 2;
				if (GetLocalReferenceValueObfuscationSentinel() != null)
				{
					goto IL_002f;
				}
				goto IL_0074;
				IL_0010:
				if (((LocalReferenceValue)P_0).LocalIndex != LocalIndex)
				{
					num = 2;
					if (!IsLocalReferenceValueObfuscationSentinelNull())
					{
						goto IL_002f;
					}
					goto IL_0072;
				}
				return false;
				IL_0048:
				if (!(P_0 is LocalReferenceValue))
				{
					break;
				}
				num = 1;
				if (IsLocalReferenceValueObfuscationSentinelNull())
				{
					goto IL_0010;
				}
				goto IL_002f;
				IL_0072:
				return true;
				IL_002f:
				switch (num)
				{
				case 1:
					break;
				default:
					goto IL_0048;
				case 4:
					continue;
				case 2:
					goto IL_0072;
				case 3:
					goto IL_0074;
				}
				goto IL_0010;
				IL_0074:
				return true;
			}
			return true;
		}

		internal override bool IsTruthy()
		{
			return ReadValue().IsTruthy();
		}

		internal static bool IsLocalReferenceValueObfuscationSentinelNull()
		{
			return LocalReferenceValueObfuscationSentinel == null;
		}

		internal static LocalReferenceValue GetLocalReferenceValueObfuscationSentinel()
		{
			return LocalReferenceValueObfuscationSentinel;
		}
	}

	internal class ArrayElementReferenceValue : ReferenceValue
	{
		private Array Array;

		internal int ElementIndex;

		internal static ArrayElementReferenceValue ArrayElementReferenceValueObfuscationSentinel;

		public ArrayElementReferenceValue(int P_0, Array P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private ArrayElementReferenceValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, int P_0, Array P_1)
			: base()
		{
			Array = P_1;
			ElementIndex = P_0;
			Kind = (VmValueKind)7;
		}

		internal override IntPtr GetReferenceAddress()
		{
			throw new NotImplementedException();
		}

		internal override void CopyFrom(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!(P_0 is ArrayElementReferenceValue))
				{
					StoreValue(P_0);
					num = 0;
					if (GetArrayElementReferenceValueObfuscationSentinel() == null)
					{
						break;
					}
				}
				else
				{
					num = 3;
					if (!IsArrayElementReferenceValueObfuscationSentinelNull())
					{
						goto IL_0029;
					}
				}
				goto IL_0046;
				IL_0046:
				switch (num)
				{
				case 3:
					Array = ((ArrayElementReferenceValue)P_0).Array;
					break;
				case 2:
					break;
				default:
					return;
				case 4:
					continue;
				case 0:
					return;
				case 1:
					return;
				}
				goto IL_0029;
				IL_0029:
				ElementIndex = ((ArrayElementReferenceValue)P_0).ElementIndex;
				num = 1;
				if (IsArrayElementReferenceValueObfuscationSentinelNull())
				{
					break;
				}
				goto IL_0046;
			}
		}

		internal override void Assign(VmValue P_0)
		{
			while (true)
			{
				StoreValue(P_0);
				if (IsArrayElementReferenceValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		internal override void StoreValue(VmValue P_0)
		{
			while (true)
			{
				Array.SetValue(P_0.ToObject(null), ElementIndex);
				if (GetArrayElementReferenceValueObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		internal override object ToObject(Type P_0)
		{
			return ReadValue().ToObject(P_0);
		}

		internal override VmValue ReadValue()
		{
			return VmValue.CreateValue(Array.GetType().GetElementType(), Array.GetValue(ElementIndex));
		}

		internal override bool IsIntegerValue()
		{
			return ReadValue().IsIntegerValue();
		}

		internal override bool IsEqualTo(VmValue P_0)
		{
			ArrayElementReferenceValue e6H1otI03OdjftY1nuN = default(ArrayElementReferenceValue);
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0085;
				}
				int num = 5;
				if (IsArrayElementReferenceValueObfuscationSentinelNull())
				{
					goto IL_0050;
				}
				goto IL_00ae;
				IL_001c:
				if (e6H1otI03OdjftY1nuN.ElementIndex != ElementIndex)
				{
					break;
				}
				if (e6H1otI03OdjftY1nuN.Array != Array)
				{
					num = 1;
					if (IsArrayElementReferenceValueObfuscationSentinelNull())
					{
						goto IL_0050;
					}
					goto IL_00b1;
				}
				return true;
				IL_0085:
				if (P_0 is ArrayElementReferenceValue)
				{
					goto IL_0006;
				}
				num = 0;
				if (IsArrayElementReferenceValueObfuscationSentinelNull())
				{
					goto IL_0050;
				}
				goto IL_00ae;
				IL_00b1:
				return false;
				IL_0006:
				e6H1otI03OdjftY1nuN = (ArrayElementReferenceValue)P_0;
				num = 0;
				if (GetArrayElementReferenceValueObfuscationSentinel() == null)
				{
					goto IL_001c;
				}
				goto IL_0050;
				IL_0050:
				switch (num)
				{
				case 3:
					break;
				case 2:
					goto IL_001c;
				case 4:
					goto IL_0085;
				case 6:
					continue;
				default:
					goto IL_00ae;
				case 1:
					goto IL_00b1;
				case 5:
					return false;
				case 7:
					goto end_IL_00a0;
				}
				goto IL_0006;
				IL_00ae:
				return false;
				continue;
				end_IL_00a0:
				break;
			}
			return false;
		}

		internal override bool IsNotEqualTo(VmValue P_0)
		{
			while (P_0.IsManagedReference())
			{
				while (true)
				{
					if (P_0 is ArrayElementReferenceValue)
					{
						while (true)
						{
							ArrayElementReferenceValue e6H1otI03OdjftY1nuN = (ArrayElementReferenceValue)P_0;
							while (true)
							{
								int num;
								if (e6H1otI03OdjftY1nuN.ElementIndex == ElementIndex)
								{
									if (e6H1otI03OdjftY1nuN.Array == Array)
									{
										return false;
									}
									num = 0;
									if (GetArrayElementReferenceValueObfuscationSentinel() == null)
									{
										goto IL_001e;
									}
								}
								else
								{
									num = 1;
									if (IsArrayElementReferenceValueObfuscationSentinelNull())
									{
										goto IL_001e;
									}
								}
								goto IL_0080;
								IL_0080:
								return true;
								IL_001e:
								switch (num)
								{
								case 3:
									break;
								case 2:
									goto end_IL_004b;
								case 4:
									goto end_IL_005c;
								case 5:
									goto end_IL_0066;
								case 1:
									return true;
								default:
									goto IL_0080;
								}
								continue;
								end_IL_004b:
								break;
							}
							continue;
							end_IL_005c:
							break;
						}
						continue;
					}
					return true;
					continue;
					end_IL_0066:
					break;
				}
			}
			return true;
		}

		internal override bool IsTruthy()
		{
			return ReadValue().IsTruthy();
		}

		internal static bool IsArrayElementReferenceValueObfuscationSentinelNull()
		{
			return ArrayElementReferenceValueObfuscationSentinel == null;
		}

		internal static ArrayElementReferenceValue GetArrayElementReferenceValueObfuscationSentinel()
		{
			return ArrayElementReferenceValueObfuscationSentinel;
		}
	}

	internal class FieldReferenceValue : ReferenceValue
	{
		internal FieldInfo Field;

		internal object Target;

		internal static FieldReferenceValue FieldReferenceValueObfuscationSentinel;

		public FieldReferenceValue(FieldInfo P_0, object P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private FieldReferenceValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, FieldInfo P_0, object P_1)
			: base()
		{
			Field = P_0;
			Target = P_1;
			Kind = (VmValueKind)7;
		}

		internal override IntPtr GetReferenceAddress()
		{
			throw new NotImplementedException();
		}

		internal override void StoreValue(VmValue P_0)
		{
			while (Target != null)
			{
				while (true)
				{
					if (Target is VmValue)
					{
						goto IL_0003;
					}
					int num = 0;
					if (IsFieldReferenceValueObfuscationSentinelNull())
					{
						break;
					}
					goto IL_0032;
					IL_0032:
					switch (num)
					{
					case 1:
						continue;
					case 5:
						goto IL_0075;
					case 3:
						return;
					case 2:
					case 4:
						goto end_IL_0075;
					case 6:
						return;
					}
					goto IL_0003;
					IL_0003:
					Field.SetValue(((VmValue)Target).ToObject(null), P_0.ToObject(null));
					num = 3;
					if (GetFieldReferenceValueObfuscationSentinel() != null)
					{
						continue;
					}
					goto IL_0032;
				}
				break;
				continue;
				end_IL_0075:
				break;
				IL_0075:;
			}
			Field.SetValue(Target, P_0.ToObject(null));
		}

		internal override void CopyFrom(VmValue P_0)
		{
			while (true)
			{
				IL_0073:
				if (!(P_0 is FieldReferenceValue))
				{
					goto IL_0003;
				}
				goto IL_0054;
				IL_0054:
				Field = ((FieldReferenceValue)P_0).Field;
				int num = 1;
				if (GetFieldReferenceValueObfuscationSentinel() != null)
				{
					break;
				}
				goto IL_0035;
				IL_0035:
				while (true)
				{
					switch (num)
					{
					case 4:
						break;
					case 1:
						Target = ((FieldReferenceValue)P_0).Target;
						num = 1;
						if (!IsFieldReferenceValueObfuscationSentinelNull())
						{
							continue;
						}
						return;
					default:
						return;
					case 2:
						goto IL_0054;
					case 5:
						goto IL_0073;
					case 3:
						return;
					case 0:
						return;
					}
					break;
				}
				goto IL_0003;
				IL_0003:
				StoreValue(P_0);
				num = 0;
				if (!IsFieldReferenceValueObfuscationSentinelNull())
				{
					break;
				}
				goto IL_0035;
			}
		}

		internal override void Assign(VmValue P_0)
		{
			while (true)
			{
				StoreValue(P_0);
				if (IsFieldReferenceValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		internal override object ToObject(Type P_0)
		{
			return ReadValue().ToObject(P_0);
		}

		internal override VmValue ReadValue()
		{
			while (Target != null)
			{
				if (GetFieldReferenceValueObfuscationSentinel() == null)
				{
					switch (1)
					{
					case 2:
						break;
					case 1:
						goto IL_0031;
					default:
						goto IL_003e;
					case 3:
						goto end_IL_0026;
					}
					continue;
				}
				goto IL_0031;
				IL_0031:
				if (!(Target is VmValue))
				{
					break;
				}
				goto IL_003e;
				IL_003e:
				return VmValue.CreateValue(Field.FieldType, Field.GetValue(((VmValue)Target).ToObject(null)));
				continue;
				end_IL_0026:
				break;
			}
			return VmValue.CreateValue(Field.FieldType, Field.GetValue(Target));
		}

		internal override bool IsIntegerValue()
		{
			return ReadValue().IsIntegerValue();
		}

		internal override bool IsEqualTo(VmValue P_0)
		{
			FieldReferenceValue FieldReferenceValue = default(FieldReferenceValue);
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0052;
				}
				int num = 0;
				if (IsFieldReferenceValueObfuscationSentinelNull())
				{
					goto IL_000f;
				}
				goto IL_0075;
				IL_0078:
				return false;
				IL_0052:
				if (!(P_0 is FieldReferenceValue))
				{
					break;
				}
				goto IL_0048;
				IL_0048:
				FieldReferenceValue = (FieldReferenceValue)P_0;
				goto IL_0032;
				IL_0032:
				if (FieldReferenceValue.Field != Field)
				{
					num = 2;
					if (GetFieldReferenceValueObfuscationSentinel() != null)
					{
						goto IL_000f;
					}
					goto IL_0078;
				}
				if (FieldReferenceValue.Target == Target)
				{
					return true;
				}
				goto IL_0089;
				IL_0089:
				return false;
				IL_000f:
				switch (num)
				{
				case 6:
					break;
				case 4:
					goto IL_0048;
				case 5:
					goto IL_0052;
				case 1:
					continue;
				default:
					goto IL_0075;
				case 2:
					goto IL_0078;
				case 3:
					goto IL_0089;
				}
				goto IL_0032;
				IL_0075:
				return false;
			}
			return false;
		}

		internal override bool IsNotEqualTo(VmValue P_0)
		{
			FieldReferenceValue FieldReferenceValue = default(FieldReferenceValue);
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_003e;
				}
				int num = 3;
				if (IsFieldReferenceValueObfuscationSentinelNull())
				{
					goto IL_0017;
				}
				goto IL_0061;
				IL_0089:
				return true;
				IL_003e:
				if (P_0 is FieldReferenceValue)
				{
					goto IL_0003;
				}
				goto IL_0063;
				IL_0003:
				FieldReferenceValue = (FieldReferenceValue)P_0;
				num = 0;
				if (!IsFieldReferenceValueObfuscationSentinelNull())
				{
					goto IL_0017;
				}
				goto IL_0066;
				IL_0017:
				switch (num)
				{
				case 2:
					break;
				case 1:
					goto IL_003e;
				case 4:
					continue;
				case 3:
					goto IL_0061;
				case 5:
					goto IL_0063;
				default:
					goto IL_0066;
				case 7:
					goto IL_0089;
				case 6:
					goto end_IL_0056;
				}
				goto IL_0003;
				IL_0063:
				return true;
				IL_0061:
				return true;
				IL_0066:
				if (FieldReferenceValue.Field != Field)
				{
					break;
				}
				if (FieldReferenceValue.Target == Target)
				{
					return false;
				}
				goto IL_0089;
				continue;
				end_IL_0056:
				break;
			}
			return true;
		}

		internal override bool IsTruthy()
		{
			return ReadValue().IsTruthy();
		}

		internal static bool IsFieldReferenceValueObfuscationSentinelNull()
		{
			return FieldReferenceValueObfuscationSentinel == null;
		}

		internal static FieldReferenceValue GetFieldReferenceValueObfuscationSentinel()
		{
			return FieldReferenceValueObfuscationSentinel;
		}
	}

	internal class ArgumentReferenceValue : ReferenceValue
	{
		private ExecutionFrame Frame;

		internal int ArgumentIndex;

		private static ArgumentReferenceValue ArgumentReferenceValueObfuscationSentinel;

		public ArgumentReferenceValue(int P_0, ExecutionFrame P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private ArgumentReferenceValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, int P_0, ExecutionFrame P_1)
			: base()
		{
			Frame = P_1;
			ArgumentIndex = P_0;
			Kind = (VmValueKind)7;
		}

		internal override IntPtr GetReferenceAddress()
		{
			throw new NotImplementedException();
		}

		internal override void CopyFrom(VmValue P_0)
		{
			while (true)
			{
				int num;
				if (!(P_0 is ArgumentReferenceValue))
				{
					num = 3;
					if (GetArgumentReferenceValueObfuscationSentinel() == null)
					{
						break;
					}
					goto IL_0020;
				}
				goto IL_003f;
				IL_0003:
				ArgumentIndex = ((ArgumentReferenceValue)P_0).ArgumentIndex;
				num = 0;
				if (GetArgumentReferenceValueObfuscationSentinel() != null)
				{
					return;
				}
				goto IL_0020;
				IL_003f:
				Frame = ((ArgumentReferenceValue)P_0).Frame;
				num = 0;
				if (IsArgumentReferenceValueObfuscationSentinelNull())
				{
					goto IL_0003;
				}
				goto IL_0020;
				IL_0020:
				switch (num)
				{
				case 1:
					break;
				default:
					return;
				case 2:
					goto IL_003f;
				case 4:
					continue;
				case 0:
					return;
				case 3:
					goto end_IL_006d;
				case 5:
					return;
				}
				goto IL_0003;
				continue;
				end_IL_006d:
				break;
			}
			StoreValue(P_0);
		}

		internal override void Assign(VmValue P_0)
		{
			while (true)
			{
				StoreValue(P_0);
				if (IsArgumentReferenceValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		internal override void StoreValue(VmValue P_0)
		{
			while (true)
			{
				Frame.Arguments[ArgumentIndex] = P_0;
				if (IsArgumentReferenceValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		internal override object ToObject(Type P_0)
		{
			while (Frame.Arguments[ArgumentIndex] == null)
			{
				if (GetArgumentReferenceValueObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return null;
			}
			return ReadValue().ToObject(P_0);
		}

		internal override VmValue ReadValue()
		{
			while (Frame.Arguments[ArgumentIndex] == null)
			{
				if (!IsArgumentReferenceValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new ObjectValue(null);
			}
			return Frame.Arguments[ArgumentIndex].ReadValue();
		}

		internal override bool IsIntegerValue()
		{
			return ReadValue().IsIntegerValue();
		}

		internal override bool IsEqualTo(VmValue P_0)
		{
			while (P_0.IsManagedReference())
			{
				int num = 0;
				if (GetArgumentReferenceValueObfuscationSentinel() == null)
				{
					goto IL_0010;
				}
				goto IL_0024;
				IL_0046:
				return ((ArgumentReferenceValue)P_0).ArgumentIndex == ArgumentIndex;
				IL_0024:
				switch (num)
				{
				case 1:
					continue;
				case 2:
					goto IL_0046;
				}
				goto IL_0010;
				IL_0010:
				if (P_0 is ArgumentReferenceValue)
				{
					num = 1;
					if (GetArgumentReferenceValueObfuscationSentinel() != null)
					{
						goto IL_0024;
					}
					goto IL_0046;
				}
				return false;
			}
			return false;
		}

		internal override bool IsNotEqualTo(VmValue P_0)
		{
			while (true)
			{
				if (P_0.IsManagedReference())
				{
					goto IL_0026;
				}
				int num = 2;
				if (GetArgumentReferenceValueObfuscationSentinel() == null)
				{
					goto IL_000f;
				}
				goto IL_004e;
				IL_000f:
				switch (num)
				{
				case 1:
					break;
				case 3:
					continue;
				case 2:
					return true;
				default:
					goto IL_004e;
				}
				goto IL_0026;
				IL_0026:
				if (!(P_0 is ArgumentReferenceValue))
				{
					break;
				}
				num = 0;
				if (GetArgumentReferenceValueObfuscationSentinel() != null)
				{
					goto IL_000f;
				}
				goto IL_004e;
				IL_004e:
				return ((ArgumentReferenceValue)P_0).ArgumentIndex != ArgumentIndex;
			}
			return true;
		}

		internal override bool IsTruthy()
		{
			return ReadValue().IsTruthy();
		}

		internal static bool IsArgumentReferenceValueObfuscationSentinelNull()
		{
			return ArgumentReferenceValueObfuscationSentinel == null;
		}

		internal static ArgumentReferenceValue GetArgumentReferenceValueObfuscationSentinel()
		{
			return ArgumentReferenceValueObfuscationSentinel;
		}
	}

	internal class ValueCellReference : ReferenceValue
	{
		private VmValue ReferencedValue;

		private Type ReferencedType;

		internal static ValueCellReference ValueCellReferenceObfuscationSentinel;

		public ValueCellReference(VmValue P_0, Type P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private ValueCellReference(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, VmValue P_0, Type P_1)
			: base()
		{
			ReferencedValue = P_0;
			ReferencedType = P_1;
			Kind = (VmValueKind)7;
		}

		internal override IntPtr GetReferenceAddress()
		{
			throw new NotImplementedException();
		}

		internal override void CopyFrom(VmValue P_0)
		{
			while (P_0 is ValueCellReference)
			{
				int num = 0;
				if (IsValueCellReferenceObfuscationSentinelNull())
				{
					goto IL_0031;
				}
				goto IL_0044;
				IL_0012:
				ReferencedValue = ((ValueCellReference)P_0).ReferencedValue;
				num = 2;
				if (GetValueCellReferenceObfuscationSentinel() == null)
				{
					return;
				}
				goto IL_0044;
				IL_0044:
				switch (num)
				{
				case 3:
					break;
				default:
					goto IL_0031;
				case 1:
					continue;
				case 2:
					return;
				case 4:
					return;
				}
				goto IL_0012;
				IL_0031:
				ReferencedType = ((ValueCellReference)P_0).ReferencedType;
				goto IL_0012;
			}
			ReferencedValue.CopyFrom(P_0);
		}

		internal override void Assign(VmValue P_0)
		{
			while (true)
			{
				StoreValue(P_0);
				if (IsValueCellReferenceObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		internal override void StoreValue(VmValue P_0)
		{
			while (true)
			{
				ReferencedValue = P_0;
				if (!IsValueCellReferenceObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		internal override object ToObject(Type P_0)
		{
			while (ReferencedValue != null)
			{
				int num = 1;
				if (!IsValueCellReferenceObfuscationSentinelNull())
				{
					goto IL_006d;
				}
				while (true)
				{
					switch (num)
					{
					case 6:
						if (!(P_0 == typeof(object)))
						{
							num = 0;
							if (GetValueCellReferenceObfuscationSentinel() == null)
							{
								continue;
							}
							goto IL_006d;
						}
						goto case 3;
					case 1:
						if (!(P_0 == null))
						{
							goto case 6;
						}
						goto case 3;
					case 2:
						break;
					default:
						goto IL_006d;
					case 3:
					case 4:
						return ReferencedValue.ToObject(ReferencedType);
					case 5:
						goto end_IL_005f;
					}
					break;
				}
				continue;
				IL_006d:
				return ReferencedValue.ToObject(P_0);
				continue;
				end_IL_005f:
				break;
			}
			return new ObjectValue(null);
		}

		internal override VmValue ReadValue()
		{
			while (ReferencedValue == null)
			{
				if (GetValueCellReferenceObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return new ObjectValue(null);
			}
			return ReferencedValue.ReadValue();
		}

		internal override bool IsIntegerValue()
		{
			return ReadValue().IsIntegerValue();
		}

		internal override bool IsEqualTo(VmValue P_0)
		{
			ValueCellReference eYhYAuIexObEGmR5h3h = default(ValueCellReference);
			while (P_0.IsManagedReference())
			{
				int num = 2;
				if (IsValueCellReferenceObfuscationSentinelNull())
				{
					goto IL_0070;
				}
				goto IL_0086;
				IL_0070:
				if (P_0 is ValueCellReference)
				{
					num = 0;
					if (IsValueCellReferenceObfuscationSentinelNull())
					{
						goto IL_0059;
					}
					goto IL_0086;
				}
				return false;
				IL_0086:
				switch (num)
				{
				case 5:
					break;
				case 2:
					goto IL_0034;
				default:
					goto IL_0059;
				case 3:
					goto IL_0070;
				case 4:
					continue;
				case 1:
					goto IL_00ce;
				case 6:
					return true;
				}
				goto IL_0015;
				IL_0015:
				if (!(eYhYAuIexObEGmR5h3h.ReferencedType != ReferencedType))
				{
					if (ReferencedValue != null)
					{
						return ReferencedValue.IsEqualTo(eYhYAuIexObEGmR5h3h.ReferencedValue);
					}
					goto IL_0034;
				}
				num = 1;
				if (!IsValueCellReferenceObfuscationSentinelNull())
				{
					goto IL_0086;
				}
				goto IL_00ce;
				IL_00ce:
				return false;
				IL_0034:
				if (eYhYAuIexObEGmR5h3h.ReferencedValue == null)
				{
					num = 6;
					if (!IsValueCellReferenceObfuscationSentinelNull())
					{
						goto IL_0070;
					}
					goto IL_0086;
				}
				return false;
				IL_0059:
				eYhYAuIexObEGmR5h3h = (ValueCellReference)P_0;
				num = 3;
				if (GetValueCellReferenceObfuscationSentinel() == null)
				{
					goto IL_0015;
				}
				goto IL_0086;
			}
			return false;
		}

		internal override bool IsNotEqualTo(VmValue P_0)
		{
			ValueCellReference eYhYAuIexObEGmR5h3h = default(ValueCellReference);
			while (P_0.IsManagedReference())
			{
				while (true)
				{
					IL_0096:
					if (P_0 is ValueCellReference)
					{
						goto IL_0071;
					}
					int num = 2;
					if (!IsValueCellReferenceObfuscationSentinelNull())
					{
						goto IL_0044;
					}
					goto IL_00ab;
					IL_00ab:
					return true;
					IL_0071:
					do
					{
						eYhYAuIexObEGmR5h3h = (ValueCellReference)P_0;
						num = 4;
					}
					while (!IsValueCellReferenceObfuscationSentinelNull());
					goto IL_0044;
					IL_0044:
					while (true)
					{
						switch (num)
						{
						case 6:
							if (eYhYAuIexObEGmR5h3h.ReferencedValue == null)
							{
								num = 1;
								if (GetValueCellReferenceObfuscationSentinel() != null)
								{
									continue;
								}
								goto case 1;
							}
							goto case 5;
						case 4:
							if (!(eYhYAuIexObEGmR5h3h.ReferencedType != ReferencedType))
							{
								goto case 2;
							}
							goto case 9;
						case 2:
							if (ReferencedValue == null)
							{
								goto case 6;
							}
							return ReferencedValue.IsNotEqualTo(eYhYAuIexObEGmR5h3h.ReferencedValue);
						case 7:
							goto IL_0096;
						case 8:
							goto end_IL_0096;
						case 3:
							goto IL_00ab;
						case 1:
							return false;
						case 5:
							return true;
						case 9:
							return true;
						}
						break;
					}
					goto IL_0071;
					continue;
					end_IL_0096:
					break;
				}
			}
			return true;
		}

		internal override bool IsTruthy()
		{
			return ReadValue().IsTruthy();
		}

		internal static bool IsValueCellReferenceObfuscationSentinelNull()
		{
			return ValueCellReferenceObfuscationSentinel == null;
		}

		internal static ValueCellReference GetValueCellReferenceObfuscationSentinel()
		{
			return ValueCellReferenceObfuscationSentinel;
		}
	}

	internal class VmParameterDefinition
	{
		public int ParameterIndex;

		public bool IsByRef;

		public VmPrimitiveType PrimitiveType;

		public VmParameterDefinition()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	internal class VmLocalDefinition
	{
		public int LocalIndex;

		public VmPrimitiveType PrimitiveType;

		public bool PreserveReference;

		public Type DeclaredType;

		public VmLocalDefinition()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			DeclaredType = typeof(object);
		}
	}

	internal class VmExceptionRegion
	{
		public int TryStart;

		public int TryEnd;

		public VmExceptionHandler Handler;

		public VmExceptionRegion()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	internal class VmExceptionHandler
	{
		public int HandlerStart;

		public int HandlerEnd;

		public byte qdvI84hxno;

		public Type CatchType;

		public int FilterStart;

		public int ClauseKind;

		public VmExceptionHandler()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	internal class VmMethodBody
	{
		internal MethodBase Method;

		internal List<VmInstruction> Instructions;

		internal VmParameterDefinition[] Parameters;

		internal List<VmLocalDefinition> Locals;

		internal List<VmExceptionRegion> ExceptionRegions;

		public VmMethodBody()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}
	}

	private class FieldArgumentBinding
	{
		internal FieldInfo Field;

		internal int ArgumentIndex;

		public FieldArgumentBinding(FieldInfo P_0, int P_1)
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			Field = P_0;
			ArgumentIndex = P_1;
		}
	}

	private class FieldArgumentCallKey
	{
		private List<FieldArgumentBinding> FieldArguments;

		private MethodBase Method;

		internal static FieldArgumentCallKey FieldArgumentCallKeyObfuscationSentinel;

		public FieldArgumentCallKey(MethodBase P_0, List<FieldArgumentBinding> P_1)
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			FieldArguments = new List<FieldArgumentBinding>();
			FieldArguments = P_1;
			Method = P_0;
		}

		public FieldArgumentCallKey(MethodBase P_0, FieldArgumentBinding[] P_1)
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			FieldArguments = new List<FieldArgumentBinding>();
			FieldArguments.AddRange(P_1);
		}

		public override bool Equals(object P_0)
		{
			int num2 = default(int);
			while (true)
			{
				FieldArgumentCallKey FieldArgumentCallKey = P_0 as FieldArgumentCallKey;
				int num = 0;
				if (GetFieldArgumentCallKeyObfuscationSentinel() == null)
				{
					goto IL_0006;
				}
				goto IL_0053;
				IL_0053:
				switch (num)
				{
				case 1:
					continue;
				case 2:
					return false;
				case 7:
					goto IL_00b2;
				case 8:
					goto IL_00de;
				case 6:
				case 9:
					goto IL_010d;
				case 3:
					goto IL_011c;
				case 4:
					goto IL_011e;
				case 5:
					goto IL_0120;
				case 10:
					goto IL_0122;
				case 11:
					goto end_IL_008e;
				}
				goto IL_0006;
				IL_0006:
				if (P_0 != null)
				{
					if (Method != FieldArgumentCallKey.Method)
					{
						num = 4;
						if (IsFieldArgumentCallKeyObfuscationSentinelNull())
						{
							break;
						}
					}
					else
					{
						if (FieldArguments.Count == FieldArgumentCallKey.FieldArguments.Count)
						{
							num2 = 0;
							goto IL_010d;
						}
						num = 2;
						if (!IsFieldArgumentCallKeyObfuscationSentinelNull())
						{
							continue;
						}
					}
					goto IL_0053;
				}
				goto IL_0122;
				IL_00b2:
				if (!(FieldArguments[num2].Field != FieldArgumentCallKey.FieldArguments[num2].Field))
				{
					goto IL_00de;
				}
				goto IL_0120;
				IL_00de:
				if (FieldArguments[num2].ArgumentIndex != FieldArgumentCallKey.FieldArguments[num2].ArgumentIndex)
				{
					goto IL_011e;
				}
				num2++;
				goto IL_010d;
				IL_011e:
				return false;
				IL_0122:
				return false;
				IL_0120:
				return false;
				IL_010d:
				if (num2 < FieldArguments.Count)
				{
					goto IL_00b2;
				}
				goto IL_011c;
				IL_011c:
				return true;
				continue;
				end_IL_008e:
				break;
			}
			return false;
		}

		public override int GetHashCode()
		{
			int num = Method.GetHashCode();
			foreach (FieldArgumentBinding item in FieldArguments)
			{
				int num2 = item.Field.GetHashCode() + item.ArgumentIndex;
				num = (num ^ num2) + num2;
			}
			return num;
		}

		public FieldArgumentBinding FindFieldArgument(int P_0)
		{
			foreach (FieldArgumentBinding item in FieldArguments)
			{
				if (item.ArgumentIndex == P_0)
				{
					return item;
				}
			}
			return null;
		}

		public bool HasFieldArgument(int P_0)
		{
			foreach (FieldArgumentBinding item in FieldArguments)
			{
				if (item.ArgumentIndex == P_0)
				{
					return true;
				}
			}
			return false;
		}

		internal static bool IsFieldArgumentCallKeyObfuscationSentinelNull()
		{
			return FieldArgumentCallKeyObfuscationSentinel == null;
		}

		internal static FieldArgumentCallKey GetFieldArgumentCallKeyObfuscationSentinel()
		{
			return FieldArgumentCallKeyObfuscationSentinel;
		}
	}

	private delegate object MethodInvoker(object target, object[] paramters);

	private delegate object BoxedValueCopier(object target);

	private delegate void MemoryBlockInitializer(IntPtr a, byte b, int c);

	private delegate void MemoryBlockCopier(IntPtr s, IntPtr t, uint c);

	internal class ExecutionFrame
	{
		[Serializable]
		[CompilerGenerated]
		private sealed class NUryNFO61QEwT2y2PeD
		{
			public static readonly NUryNFO61QEwT2y2PeD _003C_003E9;

			public static Comparison<VmExceptionRegion> _003C_003E9__12_0;

			static NUryNFO61QEwT2y2PeD()
			{
				AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
				ReactorTrialGuard.CheckTrialPeriodOnce();
				_003C_003E9 = new NUryNFO61QEwT2y2PeD();
			}

			public NUryNFO61QEwT2y2PeD()
			{
				AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
				ReactorTrialGuard.CheckTrialPeriodOnce();
			}

			internal int CompareHandlerStartOffsets(VmExceptionRegion x, VmExceptionRegion y)
			{
				return x.Handler.HandlerStart.CompareTo(y.Handler.HandlerStart);
			}
		}

		internal VmMethodBody MethodBody;

		internal VmValue[] Arguments;

		internal VmValue[] Locals;

		internal EvaluationStack Stack;

		internal VmValue ReturnValue;

		internal Exception CurrentException;

		internal List<IntPtr> AllocatedMemoryBlocks;

		private int InstructionPointer;

		private int CurrentInstructionOffset;

		private int PendingHandlerOffset;

		private object CurrentOperand;

		private bool HasPendingLeave;

		private bool EndFilterRequested;

		private bool EndFinallyRequested;

		private bool FilterAccepted;

		private static Dictionary<Type, int> ManagedSizeCache;

		private static object ManagedSizeCacheLock;

		private static Dictionary<object, VmValue> ObjectValueAssociations;

		private static object ObjectValueAssociationsLock;

		private static Dictionary<MethodBase, MethodInvoker> DirectMethodInvokers;

		private static Dictionary<MethodBase, MethodInvoker> VirtualMethodInvokers;

		private static object MethodInvokerCacheLock;

		private static Dictionary<FieldArgumentCallKey, MethodInvoker> DirectFieldArgumentInvokers;

		private static Dictionary<FieldArgumentCallKey, MethodInvoker> VirtualFieldArgumentInvokers;

		private static object FieldArgumentInvokerCacheLock;

		private static Dictionary<FieldArgumentCallKey, MethodInvoker> ConstructorInvokers;

		private static object ConstructorInvokerCacheLock;

		private static Dictionary<Type, BoxedValueCopier> BoxedValueCopiers;

		private static object BoxedValueCopierCacheLock;

		private static MemoryBlockInitializer InitializeBlock;

		private static MemoryBlockCopier CopyBlock;

		internal static ExecutionFrame ExecutionFrameObfuscationSentinel;

		internal void Execute()
		{
			while (true)
			{
				bool flag = false;
				int num = 0;
				if (GetExecutionFrameObfuscationSentinel() == null)
				{
					goto IL_0003;
				}
				goto IL_0017;
				IL_0017:
				switch (num)
				{
				case 1:
					continue;
				case 2:
					return;
				}
				goto IL_0003;
				IL_0003:
				ExecuteWithExceptionTracking(ref flag);
				num = 2;
				if (!IsExecutionFrameObfuscationSentinelNull())
				{
					continue;
				}
				goto IL_0017;
			}
		}

		internal void ReleaseFrameResources()
		{
			Stack.Clear();
			Locals = null;
			if (AllocatedMemoryBlocks == null)
			{
				return;
			}
			foreach (IntPtr item in AllocatedMemoryBlocks)
			{
				try
				{
					Marshal.FreeHGlobal(item);
				}
				catch
				{
				}
			}
			AllocatedMemoryBlocks.Clear();
			AllocatedMemoryBlocks = null;
		}

		internal void ExecuteWithExceptionTracking(ref bool P_0)
		{
			while (InstructionPointer > -2)
			{
				if (HasPendingLeave)
				{
					HasPendingLeave = false;
					int num = CurrentInstructionOffset;
					int num2 = InstructionPointer;
					ExecuteFinallyHandlersOnLeave(CurrentInstructionOffset, InstructionPointer);
					InstructionPointer = num2;
					CurrentInstructionOffset = num;
				}
				if (!EndFinallyRequested)
				{
					if (!EndFilterRequested)
					{
						CurrentInstructionOffset = InstructionPointer;
						VmInstruction wBIN9NIsKdSI0BaaGHL = MethodBody.Instructions[InstructionPointer];
						CurrentOperand = wBIN9NIsKdSI0BaaGHL.Operand;
						try
						{
							ExecuteInstruction(wBIN9NIsKdSI0BaaGHL);
						}
						catch (Exception innerException)
						{
							if (innerException is TargetInvocationException)
							{
								TargetInvocationException ex = (TargetInvocationException)innerException;
								if (ex.InnerException != null)
								{
									innerException = ex.InnerException;
								}
							}
							CurrentException = innerException;
							P_0 = true;
							Stack.Clear();
							int num3 = CurrentInstructionOffset;
							VmExceptionRegion oxLuEqINxfsgMm9eyQA = FindMatchingExceptionHandler(num3, innerException);
							List<VmExceptionRegion> list = FindFilterRegions(num3, false);
							List<VmExceptionRegion> list2 = new List<VmExceptionRegion>();
							if (oxLuEqINxfsgMm9eyQA != null)
							{
								list2.Add(oxLuEqINxfsgMm9eyQA);
							}
							if (list != null && list.Count > 0)
							{
								list2.AddRange(list);
							}
							list2.Sort((VmExceptionRegion x, VmExceptionRegion y) => x.Handler.HandlerStart.CompareTo(y.Handler.HandlerStart));
							VmExceptionRegion oxLuEqINxfsgMm9eyQA2 = null;
							foreach (VmExceptionRegion item in list2)
							{
								if (item.Handler.ClauseKind != 0)
								{
									Stack.Push(new ObjectValue(innerException));
									CurrentInstructionOffset = item.Handler.FilterStart;
									InstructionPointer = CurrentInstructionOffset;
									Execute();
									if (FilterAccepted)
									{
										FilterAccepted = false;
										oxLuEqINxfsgMm9eyQA2 = item;
										break;
									}
									continue;
								}
								oxLuEqINxfsgMm9eyQA2 = item;
								break;
							}
							if (oxLuEqINxfsgMm9eyQA2 == null)
							{
								throw innerException;
							}
							PendingHandlerOffset = oxLuEqINxfsgMm9eyQA2.Handler.HandlerStart;
							ExecuteFaultAndFinallyHandlers(num3, oxLuEqINxfsgMm9eyQA2.Handler.HandlerStart);
							if (PendingHandlerOffset >= 0)
							{
								Stack.Push(new ObjectValue(innerException));
								CurrentInstructionOffset = PendingHandlerOffset;
								InstructionPointer = CurrentInstructionOffset;
								PendingHandlerOffset = -1;
								Execute();
							}
							return;
						}
						InstructionPointer++;
						continue;
					}
					EndFilterRequested = false;
					return;
				}
				EndFinallyRequested = false;
				return;
			}
			Stack.Clear();
		}

		internal void ExecuteFaultAndFinallyHandlers(int P_0, int P_1)
		{
			if (MethodBody.ExceptionRegions == null)
			{
				return;
			}
			foreach (VmExceptionRegion item in MethodBody.ExceptionRegions)
			{
				if ((item.Handler.ClauseKind == 4 || item.Handler.ClauseKind == 2) && item.Handler.HandlerStart >= P_0 && item.Handler.HandlerEnd <= P_1)
				{
					CurrentInstructionOffset = item.Handler.HandlerStart;
					InstructionPointer = CurrentInstructionOffset;
					bool flag = false;
					ExecuteWithExceptionTracking(ref flag);
					if (flag)
					{
						break;
					}
				}
			}
		}

		internal void ExecuteFinallyHandlersOnLeave(int P_0, int P_1)
		{
			List<VmExceptionRegion>.Enumerator enumerator = default(List<VmExceptionRegion>.Enumerator);
			bool flag = default(bool);
			while (MethodBody.ExceptionRegions != null)
			{
				int num = 1;
				if (!IsExecutionFrameObfuscationSentinelNull())
				{
					goto IL_0010;
				}
				goto IL_002e;
				IL_0059:
				try
				{
					while (enumerator.MoveNext())
					{
						while (true)
						{
							IL_0141:
							VmExceptionRegion current = enumerator.Current;
							while (current.Handler.ClauseKind == 2)
							{
								int num2 = 13;
								if (!IsExecutionFrameObfuscationSentinelNull())
								{
									continue;
								}
								while (true)
								{
									switch (num2)
									{
									case 13:
										if (current.Handler.HandlerStart < P_0)
										{
											goto end_IL_012d;
										}
										goto case 6;
									case 6:
										if (current.Handler.HandlerEnd > P_1)
										{
											goto end_IL_012d;
										}
										num2 = 6;
										if (GetExecutionFrameObfuscationSentinel() != null)
										{
											continue;
										}
										goto case 7;
									case 7:
										CurrentInstructionOffset = current.Handler.HandlerStart;
										num2 = 1;
										if (GetExecutionFrameObfuscationSentinel() != null)
										{
											continue;
										}
										goto case 1;
									case 1:
										do
										{
											InstructionPointer = CurrentInstructionOffset;
											num2 = 3;
										}
										while (GetExecutionFrameObfuscationSentinel() != null);
										continue;
									case 3:
										flag = false;
										num2 = 0;
										if (GetExecutionFrameObfuscationSentinel() != null)
										{
											continue;
										}
										goto default;
									case 4:
										break;
									case 5:
										goto IL_0141;
									default:
										ExecuteWithExceptionTracking(ref flag);
										goto case 8;
									case 8:
										if (flag)
										{
											return;
										}
										goto end_IL_012d;
									case 10:
									case 11:
									case 12:
										goto end_IL_012d;
									case 2:
										return;
									case 9:
										return;
									}
									break;
								}
								continue;
								end_IL_012d:
								break;
							}
							break;
						}
					}
					break;
				}
				finally
				{
					((IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
					if (!IsExecutionFrameObfuscationSentinelNull())
					{
						switch (0)
						{
						}
					}
				}
				IL_002e:
				switch (num)
				{
				case 1:
					break;
				case 2:
					continue;
				default:
					goto IL_0059;
				case 3:
					return;
				}
				goto IL_0010;
				IL_0010:
				enumerator = MethodBody.ExceptionRegions.GetEnumerator();
				num = 0;
				if (GetExecutionFrameObfuscationSentinel() != null)
				{
					goto IL_002e;
				}
				goto IL_0059;
			}
		}

		internal VmExceptionRegion FindMatchingExceptionHandler(int P_0, Exception P_1)
		{
			List<VmExceptionRegion>.Enumerator enumerator = default(List<VmExceptionRegion>.Enumerator);
			VmExceptionRegion current = default(VmExceptionRegion);
			while (true)
			{
				VmExceptionRegion oxLuEqINxfsgMm9eyQA = null;
				while (true)
				{
					int num;
					if (MethodBody.ExceptionRegions == null)
					{
						num = 2;
						if (GetExecutionFrameObfuscationSentinel() == null)
						{
							goto IL_0021;
						}
					}
					goto IL_0003;
					IL_026c:
					return oxLuEqINxfsgMm9eyQA;
					IL_0003:
					enumerator = MethodBody.ExceptionRegions.GetEnumerator();
					num = 0;
					if (!IsExecutionFrameObfuscationSentinelNull())
					{
						goto IL_0021;
					}
					goto IL_0065;
					IL_0021:
					switch (num)
					{
					case 3:
						continue;
					case 4:
						goto end_IL_004f;
					case 1:
						goto IL_0065;
					case 2:
					case 5:
						goto IL_026c;
					}
					goto IL_0003;
					IL_0065:
					try
					{
						while (true)
						{
							IL_0238:
							if (enumerator.MoveNext())
							{
								goto IL_01e7;
							}
							int num2 = 2;
							if (IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_0182;
							}
							goto IL_01f2;
							IL_0206:
							if (current.Handler.HandlerStart >= oxLuEqINxfsgMm9eyQA.Handler.HandlerStart)
							{
								continue;
							}
							goto IL_0220;
							IL_01e7:
							current = enumerator.Current;
							goto IL_01d9;
							IL_01d9:
							if (current.Handler == null)
							{
								continue;
							}
							num2 = 9;
							if (!IsExecutionFrameObfuscationSentinelNull())
							{
								break;
							}
							goto IL_0182;
							IL_0220:
							oxLuEqINxfsgMm9eyQA = current;
							continue;
							IL_0182:
							while (true)
							{
								switch (num2)
								{
								case 19:
									if (!(current.Handler.CatchType.FullName == typeof(Exception).FullName))
									{
										goto IL_0238;
									}
									num2 = 1;
									if (GetExecutionFrameObfuscationSentinel() != null)
									{
										continue;
									}
									goto case 1;
								case 4:
									if (!(current.Handler.CatchType.FullName == typeof(object).FullName))
									{
										num2 = 15;
										if (GetExecutionFrameObfuscationSentinel() != null)
										{
											continue;
										}
										goto case 19;
									}
									goto case 1;
								case 1:
									if (P_0 < current.TryStart)
									{
										num2 = 16;
										if (IsExecutionFrameObfuscationSentinelNull())
										{
											continue;
										}
										goto case 13;
									}
									goto IL_01f2;
								case 13:
									if (!(current.Handler.CatchType.FullName == P_1.GetType().FullName))
									{
										goto case 4;
									}
									goto case 1;
								case 10:
									if (!(current.Handler.CatchType != null))
									{
										goto IL_0238;
									}
									goto case 13;
								case 9:
									if (current.Handler.ClauseKind != 0)
									{
										goto IL_0238;
									}
									goto case 7;
								case 7:
									if (current.Handler.CatchType == P_1.GetType())
									{
										goto case 1;
									}
									goto case 10;
								case 5:
									break;
								case 8:
									goto IL_01e7;
								default:
									goto IL_01f2;
								case 3:
									goto IL_01fc;
								case 14:
									goto IL_0200;
								case 15:
									goto IL_0206;
								case 11:
									goto IL_0220;
								case 6:
								case 12:
								case 16:
								case 17:
								case 18:
									goto IL_0238;
								case 2:
									goto end_IL_0238;
								}
								break;
							}
							goto IL_01d9;
							IL_01f2:
							if (P_0 > current.TryEnd)
							{
								continue;
							}
							goto IL_01fc;
							IL_01fc:
							if (oxLuEqINxfsgMm9eyQA == null)
							{
								goto IL_0200;
							}
							goto IL_0206;
							IL_0200:
							oxLuEqINxfsgMm9eyQA = current;
							continue;
							end_IL_0238:
							break;
						}
					}
					finally
					{
						((IDisposable)enumerator/*cast due to .constrained prefix*/).Dispose();
						if (IsExecutionFrameObfuscationSentinelNull())
						{
							switch (0)
							{
							}
						}
					}
					goto IL_026c;
					continue;
					end_IL_004f:
					break;
				}
			}
		}

		internal List<VmExceptionRegion> FindFilterRegions(int P_0, bool P_1)
		{
			if (MethodBody.ExceptionRegions == null)
			{
				return null;
			}
			List<VmExceptionRegion> list = new List<VmExceptionRegion>();
			foreach (VmExceptionRegion item in MethodBody.ExceptionRegions)
			{
				if ((item.Handler.ClauseKind & 1) == 1 && P_0 >= item.TryStart && P_0 <= item.TryEnd)
				{
					list.Add(item);
				}
			}
			if (list.Count == 0)
			{
				return null;
			}
			return list;
		}

		private unsafe void ExecuteInstruction(VmInstruction P_0)
		{
			switch (P_0.OpCode)
			{
			case (VmOpCode)0:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToSByteCheckedUnsigned());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)1:
			{
				int metadataToken = (int)CurrentOperand;
				ConstructorInfo constructorInfo = (ConstructorInfo)typeof(VirtualMachineRuntime).Module.ResolveMethod(metadataToken);
				ParameterInfo[] parameters = constructorInfo.GetParameters();
				object[] array3 = new object[parameters.Length];
				VmValue[] array4 = new VmValue[parameters.Length];
				List<FieldArgumentBinding> list = null;
				FieldArgumentCallKey FieldArgumentCallKey = null;
				for (int i = 0; i < parameters.Length; i++)
				{
					VmValue VmValue = Stack.Pop();
					Type elementType = parameters[parameters.Length - 1 - i].ParameterType;
					object key = null;
					bool flag = false;
					if (elementType.IsByRef && VmValue is FieldReferenceValue FieldReferenceValue)
					{
						if (list == null)
						{
							list = new List<FieldArgumentBinding>();
						}
						list.Add(new FieldArgumentBinding(FieldReferenceValue.Field, parameters.Length - 1 - i));
						key = FieldReferenceValue.Target;
						if (key is VmValue)
						{
							VmValue = key as VmValue;
						}
						else
						{
							flag = true;
						}
					}
					if (!flag)
					{
						if (VmValue != null)
						{
							key = VmValue.ToObject(elementType);
						}
						if (key == null)
						{
							if (elementType.IsByRef)
							{
								elementType = elementType.GetElementType();
							}
							if (elementType.IsValueType)
							{
								key = Activator.CreateInstance(elementType);
								if (VmValue is LocalReferenceValue)
								{
									((ReferenceValue)VmValue).StoreValue(VmValue.CreateValue(elementType, key));
								}
							}
						}
					}
					array4[array3.Length - 1 - i] = VmValue;
					array3[array3.Length - 1 - i] = key;
				}
				MethodInvoker iC9Dj5Go7DZA3kLFH = null;
				if (list != null)
				{
					FieldArgumentCallKey = new FieldArgumentCallKey(constructorInfo, list);
					iC9Dj5Go7DZA3kLFH = GetOrCreateConstructorInvoker(constructorInfo, true, FieldArgumentCallKey);
				}
				object obj2 = null;
				obj2 = ((iC9Dj5Go7DZA3kLFH != null) ? iC9Dj5Go7DZA3kLFH(null, array3) : constructorInfo.Invoke(array3));
				for (int j = 0; j < parameters.Length; j++)
				{
					if (parameters[j].ParameterType.IsByRef && (FieldArgumentCallKey == null || !FieldArgumentCallKey.HasFieldArgument(j)))
					{
						if (array4[j].IsNativeIntegerValue())
						{
							((NativeIntegerValue)array4[j]).CopyNativeIntegerPayload(VmValue.CreateValue(parameters[j].ParameterType, array3[j]));
						}
						else if (array4[j] is LocalReferenceValue)
						{
							array4[j].CopyFrom(VmValue.CreateValue(parameters[j].ParameterType.GetElementType(), array3[j]));
						}
						else
						{
							array4[j].CopyFrom(VmValue.CreateValue(parameters[j].ParameterType, array3[j]));
						}
					}
				}
				Stack.Push(VmValue.CreateValue(constructorInfo.DeclaringType, obj2));
				break;
			}
			case (VmOpCode)2:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.AddCheckedUnsigned(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)3:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.Divide(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)4:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToUInt16());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)5:
				lock (ObjectValueAssociationsLock)
				{
					VmValue VmValue = Stack.Pop();
					object key = Stack.Pop().ToObject(null);
					ObjectValueAssociations[key] = VmValue;
					break;
				}
			case (VmOpCode)6:
				if (StringLiterals.Count == 0)
				{
					Module module = typeof(VirtualMachineRuntime).Module;
					Stack.Push(new StringValue(module.ResolveString((int)CurrentOperand | 0x70000000)));
				}
				else
				{
					Stack.Push(new StringValue(StringLiterals[(int)CurrentOperand]));
				}
				break;
			case (VmOpCode)8:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(VmValue);
				if (VmValue != null && VmValue.IsManagedReference() && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToUInt32());
					break;
				}
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs.IsNativeIntegerValue())
				{
					IntPtr intPtr = ((NativeIntegerValue)c7EoQ7IJLjSNsQhooYs).ToIntPtr();
					Stack.Push(new Int32Value(*(uint*)(void*)intPtr, (VmPrimitiveType)6));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)9:
			{
				VmValue VmValue = Stack.Pop();
				if (VmValue.IsNumericValue())
				{
					VmValue = ((NumericValue)VmValue).ToSingle();
				}
				Stack.Pop().Assign(VmValue);
				break;
			}
			case (VmOpCode)10:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(VmValue);
				if (VmValue != null && VmValue.IsManagedReference() && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToDouble());
					break;
				}
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs.IsNativeIntegerValue())
				{
					IntPtr intPtr = ((NativeIntegerValue)c7EoQ7IJLjSNsQhooYs).ToIntPtr();
					Stack.Push(new FloatingPointValue(*(double*)(void*)intPtr, (VmPrimitiveType)10));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)11:
			{
				VmValue VmValue = Stack.Pop();
				if (VmValue != null && VmValue.IsTruthy())
				{
					InstructionPointer = (int)CurrentOperand - 1;
				}
				break;
			}
			case (VmOpCode)12:
			{
				int metadataToken = (int)CurrentOperand;
				typeof(VirtualMachineRuntime).Module.ResolveType(metadataToken);
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				Array array = (Array)Stack.Pop().ToObject(null);
				Stack.Push(new ArrayElementReferenceValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32, array));
				break;
			}
			case (VmOpCode)13:
			{
				int metadataToken = (int)CurrentOperand;
				Module module = typeof(VirtualMachineRuntime).Module;
				Stack.Push(new NativeIntegerValue(module.ResolveMethod(metadataToken).MethodHandle.GetFunctionPointer()));
				break;
			}
			case (VmOpCode)14:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToUInt64CheckedUnsigned());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)16:
			{
				VmValue VmValue = Stack.Pop();
				if (AsNumericValue(Stack.Pop()).LessThanUnsigned(VmValue))
				{
					Stack.Push(new Int32Value(1));
				}
				else
				{
					Stack.Push(new Int32Value(0));
				}
				break;
			}
			case (VmOpCode)17:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.ShiftRight(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)19:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				Array obj6 = (Array)Stack.Pop().ToObject(null);
				object obj2 = obj6.GetValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Type elementType = obj6.GetType().GetElementType();
				Stack.Push(VmValue.CreateValue(elementType, obj2));
				break;
			}
			case (VmOpCode)21:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				object obj2 = ((Array)Stack.Pop().ToObject(null)).GetValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Stack.Push(VmValue.CreateValue(typeof(long), obj2));
				break;
			}
			case (VmOpCode)22:
			{
				VmValue VmValue = Stack.Pop();
				if (AsNumericValue(Stack.Pop()).GreaterThanOrEqualUnsigned(VmValue))
				{
					InstructionPointer = (int)CurrentOperand - 1;
				}
				break;
			}
			case (VmOpCode)23:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(VmValue);
				VmValue zaY0HTeglK55vqASp0b2 = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(zaY0HTeglK55vqASp0b2);
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					if (c7EoQ7IJLjSNsQhooYs2.GreaterThanUnsigned(VmValue))
					{
						InstructionPointer = (int)CurrentOperand - 1;
					}
				}
				else if (VmValue.IsNotEqualTo(zaY0HTeglK55vqASp0b2))
				{
					InstructionPointer = (int)CurrentOperand - 1;
				}
				break;
			}
			case (VmOpCode)24:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = Stack.Pop() as NumericValue;
				NumericValue c7EoQ7IJLjSNsQhooYs2 = Stack.Pop() as NumericValue;
				IntPtr intPtr = GetNativeAddress(Stack.Pop());
				if (intPtr != IntPtr.Zero)
				{
					byte Byte = c7EoQ7IJLjSNsQhooYs2.ToByte().Bits.Byte;
					uint num2 = c7EoQ7IJLjSNsQhooYs.ToUInt32().Bits.UInt32;
					InitializeMemoryBlock(intPtr, Byte, (int)num2);
				}
				break;
			}
			case (VmOpCode)25:
			{
				int metadataToken = (int)CurrentOperand;
				Module module = typeof(VirtualMachineRuntime).Module;
				object obj2 = null;
				try
				{
					obj2 = module.ResolveType(metadataToken);
				}
				catch
				{
					try
					{
						obj2 = module.ResolveMethod(metadataToken);
					}
					catch
					{
						try
						{
							obj2 = module.ResolveField(metadataToken);
							goto end_IL_0bd4;
						}
						catch
						{
							obj2 = module.ResolveMember(metadataToken);
							goto end_IL_0bd4;
						}
						end_IL_0bd4:;
					}
				}
				Stack.Push(new ObjectValue(obj2));
				break;
			}
			case (VmOpCode)26:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToSByteChecked());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)27:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.MultiplyCheckedUnsigned(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)28:
			{
				int metadataToken = (int)CurrentOperand;
				Type elementType = typeof(VirtualMachineRuntime).Module.ResolveType(metadataToken);
				object obj2 = ((Stack.Pop() as ReferenceValue) ?? throw new VmOperandException()).ToObject(elementType);
				VmValue VmValue;
				if (obj2 == null)
				{
					if (!elementType.IsValueType)
					{
						VmValue = new ObjectValue(null);
					}
					else
					{
						obj2 = Activator.CreateInstance(elementType);
						VmValue = VmValue.CreateValue(elementType, obj2);
					}
				}
				else
				{
					if (elementType.IsValueType)
					{
						obj2 = CopyBoxedValue(obj2);
					}
					VmValue = VmValue.CreateValue(elementType, obj2);
				}
				Stack.Push(VmValue);
				break;
			}
			case (VmOpCode)29:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.RemainderUnsigned(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)30:
			{
				int metadataToken = (int)CurrentOperand;
				Type elementType = typeof(VirtualMachineRuntime).Module.ResolveType(metadataToken);
				if (Stack.Pop() is ReferenceValue ReferenceValue)
				{
					if (!elementType.IsValueType)
					{
						ReferenceValue.StoreValue(new ObjectValue(null));
						break;
					}
					object obj2 = Activator.CreateInstance(elementType);
					ReferenceValue.StoreValue(VmValue.CreateValue(elementType, obj2));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)31:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.DivideUnsigned(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)32:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToInt64CheckedUnsigned());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)33:
			{
				int metadataToken = (int)CurrentOperand;
				FieldInfo fieldInfo = typeof(VirtualMachineRuntime).Module.ResolveField(metadataToken);
				object obj2 = Stack.Pop().ToObject(fieldInfo.FieldType);
				fieldInfo.SetValue(null, obj2);
				break;
			}
			case (VmOpCode)34:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToUInt32());
				break;
			}
			case (VmOpCode)35:
				Stack.Push(((NumericValue)Stack.Pop()).Negate());
				break;
			case (VmOpCode)36:
				InvokeMethod(false);
				break;
			case (VmOpCode)37:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToNativeInt());
				break;
			}
			case (VmOpCode)38:
				Stack.Push(Stack.Pop().ReadValue());
				break;
			case (VmOpCode)39:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToUInt16Checked());
				break;
			}
			case (VmOpCode)40:
				InstructionPointer = (int)CurrentOperand - 1;
				break;
			case (VmOpCode)41:
			{
				int[] array2 = (int[])CurrentOperand;
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				long num3 = c7EoQ7IJLjSNsQhooYs.ToInt64().Bits.Int64;
				if ((num3 < 0L || c7EoQ7IJLjSNsQhooYs.IsFloatingPointValue()) && IntPtr.Size == 4)
				{
					num3 = (int)num3;
				}
				if (c7EoQ7IJLjSNsQhooYs.IsInt32Value())
				{
					Int32Value sUIUMOxihqMaAjqQaU = (Int32Value)c7EoQ7IJLjSNsQhooYs;
					if (sUIUMOxihqMaAjqQaU.PrimitiveType == (VmPrimitiveType)6)
					{
						num3 = sUIUMOxihqMaAjqQaU.Bits.UInt32;
					}
				}
				if (num3 < array2.Length && num3 >= 0L)
				{
					InstructionPointer = array2[num3] - 1;
				}
				break;
			}
			case (VmOpCode)42:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.ShiftRightUnsigned(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)43:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToUInt32Checked());
				break;
			}
			case (VmOpCode)44:
				Stack.Push(new ObjectValue(null));
				break;
			case (VmOpCode)45:
				Stack.Push(new Int64Value((long)CurrentOperand));
				break;
			case (VmOpCode)46:
			{
				VmValue VmValue = Stack.Pop();
				if (AsNumericValue(Stack.Pop()).GreaterThanOrEqual(VmValue))
				{
					InstructionPointer = (int)CurrentOperand - 1;
				}
				break;
			}
			case (VmOpCode)47:
				if ((AsNumericValue(Stack.Peek()) ?? throw new ArithmeticException(((VmDiagnosticCode)0/*cast due to .constrained prefix*/).ToString())) is FloatingPointValue FloatingPointValue)
				{
					if (double.IsNaN(FloatingPointValue.Value))
					{
						throw new OverflowException(((VmDiagnosticCode)2/*cast due to .constrained prefix*/).ToString());
					}
					if (double.IsInfinity(FloatingPointValue.Value))
					{
						throw new OverflowException(((VmDiagnosticCode)1/*cast due to .constrained prefix*/).ToString());
					}
				}
				break;
			case (VmOpCode)48:
			{
				VmValue VmValue = Stack.Pop();
				if (VmValue.IsNumericValue())
				{
					VmValue = ((NumericValue)VmValue).ToDouble();
				}
				Stack.Pop().Assign(VmValue);
				break;
			}
			case (VmOpCode)49:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToUInt64Checked());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)50:
			{
				int metadataToken = (int)CurrentOperand;
				Locals[metadataToken] = CoerceStoredValue(Stack.Pop(), MethodBody.Locals[metadataToken].PrimitiveType, MethodBody.Locals[metadataToken].PreserveReference);
				break;
			}
			case (VmOpCode)51:
			{
				VmValue VmValue = Stack.Pop();
				if (AsNumericValue(Stack.Pop()).LessThanOrEqual(VmValue))
				{
					InstructionPointer = (int)CurrentOperand - 1;
				}
				break;
			}
			case (VmOpCode)52:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.Remainder(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)53:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToNativeUInt());
				break;
			}
			case (VmOpCode)56:
				Stack.Push(Arguments[(int)CurrentOperand]);
				break;
			case (VmOpCode)57:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToInt16Checked());
				break;
			}
			case (VmOpCode)58:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToNativeUIntCheckedUnsigned());
				break;
			}
			case (VmOpCode)59:
			{
				bool flag = false;
				VmValue VmValue = Stack.Pop();
				if (VmValue == null || !VmValue.IsTruthy())
				{
					InstructionPointer = (int)CurrentOperand - 1;
				}
				break;
			}
			case (VmOpCode)60:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				object obj2 = ((Array)Stack.Pop().ToObject(null)).GetValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Stack.Push(VmValue.CreateValue(typeof(IntPtr), obj2));
				break;
			}
			case (VmOpCode)61:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToUInt32CheckedUnsigned());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)62:
			{
				Array array = (Array)Stack.Pop().ToObject(null);
				Stack.Push(new Int32Value(array.Length, (VmPrimitiveType)5));
				break;
			}
			case (VmOpCode)63:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToNativeIntChecked());
				break;
			}
			case (VmOpCode)65:
			{
				int metadataToken = (int)CurrentOperand;
				Type elementType = typeof(VirtualMachineRuntime).Module.ResolveType(metadataToken);
				object obj2 = Stack.Pop().ReadValue().ToObject(elementType);
				VmValue VmValue = VmValue.CreateValue(elementType, obj2);
				Stack.Push(VmValue);
				break;
			}
			case (VmOpCode)66:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToInt64());
				break;
			}
			case (VmOpCode)68:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToInt16());
				break;
			}
			case (VmOpCode)69:
				Stack.Pop();
				break;
			case (VmOpCode)70:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(VmValue);
				if (VmValue != null && VmValue.IsManagedReference() && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToByte());
					break;
				}
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs.IsNativeIntegerValue())
				{
					IntPtr intPtr = ((NativeIntegerValue)c7EoQ7IJLjSNsQhooYs).ToIntPtr();
					Stack.Push(new Int32Value(*(byte*)(void*)intPtr, (VmPrimitiveType)2));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)71:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs2 != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.BitwiseAnd(c7EoQ7IJLjSNsQhooYs2));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)72:
				Stack.Push(Stack.Peek());
				break;
			case (VmOpCode)73:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				object obj2 = ((Array)Stack.Pop().ToObject(null)).GetValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Stack.Push(VmValue.CreateValue(typeof(short), obj2));
				break;
			}
			case (VmOpCode)74:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				object obj2 = ((Array)Stack.Pop().ToObject(null)).GetValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Stack.Push(VmValue.CreateValue(typeof(double), obj2));
				break;
			}
			case (VmOpCode)75:
				InvokeMethod(true);
				break;
			case (VmOpCode)76:
			{
				int metadataToken = (int)CurrentOperand;
				uint num2 = (uint)GetManagedSize(typeof(VirtualMachineRuntime).Module.ResolveType(metadataToken));
				Stack.Push(new Int32Value(num2, (VmPrimitiveType)6));
				break;
			}
			case (VmOpCode)77:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToInt32CheckedUnsigned());
				break;
			}
			case (VmOpCode)78:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.Subtract(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)79:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToInt64Checked());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)80:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				object obj2 = ((Array)Stack.Pop().ToObject(null)).GetValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Stack.Push(VmValue.CreateValue(typeof(float), obj2));
				break;
			}
			case (VmOpCode)82:
			{
				int metadataToken = (int)CurrentOperand;
				MethodBase methodBase = typeof(VirtualMachineRuntime).Module.ResolveMethod(metadataToken);
				Type elementType = Stack.Pop().ToObject(null).GetType();
				List<Type> list2 = new List<Type>();
				do
				{
					list2.Add(elementType);
					elementType = elementType.BaseType;
				}
				while (elementType != null && elementType != methodBase.DeclaringType);
				list2.Reverse();
				MethodBase methodBase2 = methodBase;
				foreach (Type item in list2)
				{
					MethodInfo[] methods = item.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					foreach (MethodInfo methodInfo in methods)
					{
						if (methodInfo.GetBaseDefinition() == methodBase2)
						{
							methodBase2 = methodInfo;
							break;
						}
					}
				}
				Stack.Push(new NativeIntegerValue(methodBase2.MethodHandle.GetFunctionPointer()));
				break;
			}
			case (VmOpCode)83:
				Stack.Push(new FloatingPointValue((float)CurrentOperand));
				break;
			case (VmOpCode)86:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(VmValue);
				if (VmValue != null && VmValue.IsManagedReference() && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToSingle());
					break;
				}
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs.IsNativeIntegerValue())
				{
					IntPtr intPtr = ((NativeIntegerValue)c7EoQ7IJLjSNsQhooYs).ToIntPtr();
					Stack.Push(new FloatingPointValue(*(float*)(void*)intPtr, (VmPrimitiveType)9));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)87:
				EndFinallyRequested = true;
				break;
			case (VmOpCode)88:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToUInt64());
				break;
			}
			case (VmOpCode)89:
			{
				int metadataToken = (int)CurrentOperand;
				if (MethodBody.Method.IsStatic)
				{
					Arguments[metadataToken] = CoerceStoredValue(Stack.Pop(), MethodBody.Parameters[metadataToken].PrimitiveType);
				}
				else
				{
					Arguments[metadataToken] = CoerceStoredValue(Stack.Pop(), MethodBody.Parameters[metadataToken - 1].PrimitiveType);
				}
				break;
			}
			case (VmOpCode)90:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(VmValue);
				if (VmValue != null && VmValue.IsManagedReference() && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToNativeInt());
					break;
				}
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs.IsNativeIntegerValue())
				{
					IntPtr intPtr = ((NativeIntegerValue)c7EoQ7IJLjSNsQhooYs).ToIntPtr();
					if (IntPtr.Size == 8)
					{
						long num3 = *(long*)(void*)intPtr;
						Stack.Push(new NativeIntegerValue(num3, (VmPrimitiveType)12));
					}
					else
					{
						int metadataToken = *(int*)(void*)intPtr;
						Stack.Push(new NativeIntegerValue(metadataToken, (VmPrimitiveType)12));
					}
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)91:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToDouble());
				break;
			}
			case (VmOpCode)92:
			{
				VmValue VmValue = Stack.Pop();
				if (VmValue.IsNumericValue())
				{
					VmValue = ((NumericValue)VmValue).ConvertToInt32();
				}
				Stack.Pop().Assign(VmValue);
				break;
			}
			case (VmOpCode)93:
				Stack.Push(new ArgumentReferenceValue((int)CurrentOperand, this));
				break;
			case (VmOpCode)94:
			{
				IntPtr intPtr = Marshal.AllocHGlobal((Stack.Pop() as NumericValue).ToInt32().Bits.Int32);
				if (AllocatedMemoryBlocks == null)
				{
					AllocatedMemoryBlocks = new List<IntPtr>();
				}
				AllocatedMemoryBlocks.Add(intPtr);
				Stack.Push(new NativeIntegerValue(intPtr));
				break;
			}
			case (VmOpCode)95:
			{
				int metadataToken = (int)CurrentOperand;
				FieldInfo fieldInfo = typeof(VirtualMachineRuntime).Module.ResolveField(metadataToken);
				VmValue zaY0HTeglK55vqASp0b4 = Stack.Pop();
				zaY0HTeglK55vqASp0b4.ReadValue();
				object obj2 = zaY0HTeglK55vqASp0b4.ToObject(null);
				Stack.Push(new FieldReferenceValue(fieldInfo, obj2));
				break;
			}
			case (VmOpCode)96:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToDoubleUnsigned());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)97:
				FilterAccepted = (bool)Stack.Pop().ToObject(typeof(bool));
				EndFilterRequested = true;
				break;
			case (VmOpCode)98:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToNativeIntCheckedUnsigned());
				break;
			}
			case (VmOpCode)99:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToSingle());
				break;
			}
			case (VmOpCode)67:
			case (VmOpCode)100:
			{
				int metadataToken = (int)CurrentOperand;
				Type elementType = typeof(VirtualMachineRuntime).Module.ResolveType(metadataToken);
				VmValue VmValue = Stack.Pop();
				object obj2 = VmValue.ToObject(elementType);
				if (obj2 != null)
				{
					if (elementType.IsValueType)
					{
						obj2 = CopyBoxedValue(obj2);
					}
					VmValue = VmValue.CreateValue(elementType, obj2);
				}
				else if (!elementType.IsValueType)
				{
					VmValue = new ObjectValue(null);
				}
				else
				{
					obj2 = Activator.CreateInstance(elementType);
					VmValue = VmValue.CreateValue(elementType, obj2);
				}
				((Stack.Pop() as ReferenceValue) ?? throw new VmOperandException()).CopyFrom(VmValue);
				break;
			}
			case (VmOpCode)101:
			{
				int metadataToken = (int)CurrentOperand;
				Type elementType = typeof(VirtualMachineRuntime).Module.ResolveType(metadataToken);
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				((Array)Stack.Pop().ToObject(null)).SetValue(VmValue.ToObject(elementType), c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				break;
			}
			case (VmOpCode)103:
			{
				VmValue VmValue = Stack.Pop();
				if (!VmValue.IsManagedReference())
				{
					throw new VmOperandException();
				}
				object obj2 = VmValue.ToObject(null);
				VmValue = ((obj2 != null) ? VmValue.CreateValue(obj2.GetType(), obj2) : new ObjectValue(null));
				Stack.Push(VmValue);
				break;
			}
			case (VmOpCode)104:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.MultiplyChecked(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)105:
				throw (Exception)Stack.Pop().ToObject(null);
			case (VmOpCode)106:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				object obj2 = ((Array)Stack.Pop().ToObject(null)).GetValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Stack.Push(VmValue.CreateValue(typeof(byte), obj2));
				break;
			}
			case (VmOpCode)107:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToByteCheckedUnsigned());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)108:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToByte());
				break;
			}
			case (VmOpCode)109:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				object obj2 = ((Array)Stack.Pop().ToObject(null)).GetValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Stack.Push(VmValue.CreateValue(typeof(uint), obj2));
				break;
			}
			case (VmOpCode)110:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs2 != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.BitwiseOr(c7EoQ7IJLjSNsQhooYs2));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)112:
				Stack.Push(new LocalReferenceValue((int)CurrentOperand, this));
				break;
			case (VmOpCode)113:
			{
				VmValue VmValue = Stack.Pop();
				if (VmValue.IsNumericValue())
				{
					VmValue = ((NumericValue)VmValue).ConvertToSByte();
				}
				Stack.Pop().Assign(VmValue);
				break;
			}
			case (VmOpCode)114:
			{
				VmValue VmValue = Stack.Pop();
				if (!AsNumericValue(Stack.Pop()).GreaterThan(VmValue))
				{
					Stack.Push(new Int32Value(0));
				}
				else
				{
					Stack.Push(new Int32Value(1));
				}
				break;
			}
			case (VmOpCode)115:
			{
				int metadataToken = (int)CurrentOperand;
				Type elementType = typeof(VirtualMachineRuntime).Module.ResolveType(metadataToken);
				VmValue VmValue = Stack.Pop();
				object obj2 = VmValue.ToObject(null);
				if (obj2 == null)
				{
					Stack.Push(new ObjectValue(null));
				}
				else if (elementType.IsAssignableFrom(obj2.GetType()))
				{
					Stack.Push(VmValue);
				}
				else
				{
					Stack.Push(new ObjectValue(null));
				}
				break;
			}
			case (VmOpCode)117:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.AddChecked(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)118:
				Stack.Push(new FloatingPointValue((double)CurrentOperand));
				break;
			case (VmOpCode)119:
			{
				VmValue VmValue = Stack.Pop();
				if (AsNumericValue(Stack.Pop()).LessThan(VmValue))
				{
					InstructionPointer = (int)CurrentOperand - 1;
				}
				break;
			}
			case (VmOpCode)121:
				if (Stack.Pop().IsNotEqualTo(Stack.Pop()))
				{
					InstructionPointer = (int)CurrentOperand - 1;
				}
				break;
			case (VmOpCode)122:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToSByte());
				break;
			}
			case (VmOpCode)124:
			{
				VmValue zaY0HTeglK55vqASp0b5 = ConvertBoxedEnumValue(Stack.Pop());
				VmValue VmValue = ConvertBoxedEnumValue(Stack.Pop());
				if (zaY0HTeglK55vqASp0b5.IsEqualTo(VmValue))
				{
					Stack.Push(new Int32Value(1));
				}
				else
				{
					Stack.Push(new Int32Value(0));
				}
				break;
			}
			case (VmOpCode)125:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(VmValue);
				if (VmValue != null && VmValue.IsManagedReference() && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToUInt16());
					break;
				}
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs.IsNativeIntegerValue())
				{
					IntPtr intPtr = ((NativeIntegerValue)c7EoQ7IJLjSNsQhooYs).ToIntPtr();
					Stack.Push(new Int32Value(*(ushort*)(void*)intPtr, (VmPrimitiveType)4));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)126:
				Stack.Push(new Int32Value((int)CurrentOperand));
				break;
			case (VmOpCode)127:
			{
				VmValue VmValue = Stack.Pop();
				Stack.Pop().Assign(VmValue);
				break;
			}
			case (VmOpCode)128:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.SubtractCheckedUnsigned(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)129:
			{
				VmValue VmValue = Stack.Pop();
				if (AsNumericValue(Stack.Pop()).LessThanOrEqualUnsigned(VmValue))
				{
					InstructionPointer = (int)CurrentOperand - 1;
				}
				break;
			}
			case (VmOpCode)130:
			{
				int metadataToken = (int)CurrentOperand;
				FieldInfo fieldInfo = typeof(VirtualMachineRuntime).Module.ResolveField(metadataToken);
				object obj2 = Stack.Pop().ToObject(fieldInfo.FieldType);
				VmValue VmValue = Stack.Pop();
				object key = VmValue.ToObject(null);
				if (key == null)
				{
					Type elementType = fieldInfo.DeclaringType;
					if (elementType.IsByRef)
					{
						elementType = elementType.GetElementType();
					}
					if (!elementType.IsValueType)
					{
						throw new NullReferenceException();
					}
					key = Activator.CreateInstance(elementType);
					if (VmValue is LocalReferenceValue)
					{
						((ReferenceValue)VmValue).StoreValue(VmValue.CreateValue(elementType, key));
					}
				}
				fieldInfo.SetValue(key, obj2);
				break;
			}
			case (VmOpCode)131:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToInt16CheckedUnsigned());
				break;
			}
			case (VmOpCode)132:
				throw CurrentException;
			case (VmOpCode)133:
				lock (ObjectValueAssociationsLock)
				{
					object key = Stack.Pop().ToObject(null);
					VmValue VmValue = null;
					if (!ObjectValueAssociations.TryGetValue(key, out VmValue))
					{
						Stack.Push(new ObjectValue(null));
					}
					else
					{
						Stack.Push(VmValue);
					}
					break;
				}
			case (VmOpCode)134:
				InstructionPointer = (int)CurrentOperand - 1;
				HasPendingLeave = true;
				break;
			case (VmOpCode)135:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(VmValue);
				VmValue zaY0HTeglK55vqASp0b2 = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(zaY0HTeglK55vqASp0b2);
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					if (c7EoQ7IJLjSNsQhooYs2.GreaterThanUnsigned(VmValue))
					{
						Stack.Push(new Int32Value(1));
					}
					else
					{
						Stack.Push(new Int32Value(0));
					}
				}
				else if (!VmValue.IsNotEqualTo(zaY0HTeglK55vqASp0b2))
				{
					Stack.Push(new Int32Value(0));
				}
				else
				{
					Stack.Push(new Int32Value(1));
				}
				break;
			}
			case (VmOpCode)136:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToInt32());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)137:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				object obj2 = ((Array)Stack.Pop().ToObject(null)).GetValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Stack.Push(VmValue.CreateValue(typeof(int), obj2));
				break;
			}
			case (VmOpCode)138:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				object obj2 = ((Array)Stack.Pop().ToObject(null)).GetValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Stack.Push(VmValue.CreateValue(typeof(ushort), obj2));
				break;
			}
			case (VmOpCode)139:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.ShiftLeft(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)140:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToByteChecked());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)141:
			{
				VmValue VmValue = Stack.Pop();
				if (VmValue.IsNumericValue())
				{
					VmValue = ((NumericValue)VmValue).ConvertToInt64();
				}
				Stack.Pop().Assign(VmValue);
				break;
			}
			case (VmOpCode)143:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToUInt16CheckedUnsigned());
				break;
			}
			case (VmOpCode)144:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.BitwiseNot());
				break;
			}
			case (VmOpCode)146:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.Multiply(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)147:
			{
				VmValue VmValue = Locals[(int)CurrentOperand];
				Stack.Push(VmValue);
				break;
			}
			case (VmOpCode)148:
			{
				VmValue zaY0HTeglK55vqASp0b3 = Stack.Pop();
				VmValue VmValue = Stack.Pop();
				if (zaY0HTeglK55vqASp0b3.IsEqualTo(VmValue))
				{
					InstructionPointer = (int)CurrentOperand - 1;
				}
				break;
			}
			case (VmOpCode)149:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs2 != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.BitwiseXor(c7EoQ7IJLjSNsQhooYs2));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)15:
			case (VmOpCode)20:
			case (VmOpCode)81:
			case (VmOpCode)111:
			case (VmOpCode)123:
			case (VmOpCode)150:
				throw new VmOperandException();
			case (VmOpCode)151:
			{
				Type elementType = typeof(VirtualMachineRuntime).Module.ResolveType((int)CurrentOperand);
				object obj2 = Stack.Pop().ToObject(elementType);
				if (obj2 == null)
				{
					obj2 = Activator.CreateInstance(elementType);
				}
				ObjectValue hBptEKeyCu6KEtAdaw = new ObjectValue(VmValue.CreateValue(elementType, CopyBoxedValue(obj2)));
				Stack.Push(hBptEKeyCu6KEtAdaw);
				break;
			}
			case (VmOpCode)152:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = (NumericValue)Stack.Pop();
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.SubtractChecked(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)153:
			{
				VmValue VmValue = Stack.Pop();
				if (AsNumericValue(Stack.Pop()).GreaterThan(VmValue))
				{
					InstructionPointer = (int)CurrentOperand - 1;
				}
				break;
			}
			case (VmOpCode)154:
				InstructionPointer = -3;
				if (Stack.GetCount() > 0)
				{
					ReturnValue = Stack.Pop();
				}
				break;
			case (VmOpCode)155:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(VmValue);
				if (VmValue != null && VmValue.IsManagedReference() && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToSByte());
					break;
				}
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs.IsNativeIntegerValue())
				{
					IntPtr intPtr = ((NativeIntegerValue)c7EoQ7IJLjSNsQhooYs).ToIntPtr();
					Stack.Push(new Int32Value(*(sbyte*)(void*)intPtr, (VmPrimitiveType)1));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)156:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(VmValue);
				if (VmValue != null && VmValue.IsManagedReference() && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToInt16());
					break;
				}
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs.IsNativeIntegerValue())
				{
					IntPtr intPtr = ((NativeIntegerValue)c7EoQ7IJLjSNsQhooYs).ToIntPtr();
					Stack.Push(new Int32Value(*(short*)(void*)intPtr, (VmPrimitiveType)3));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)157:
			{
				VmValue VmValue = Stack.Pop();
				if (VmValue.IsNumericValue())
				{
					VmValue = ((NumericValue)VmValue).ToNativeInt();
				}
				Stack.Pop().Assign(VmValue);
				break;
			}
			case (VmOpCode)7:
			case (VmOpCode)18:
			case (VmOpCode)55:
			case (VmOpCode)120:
			case (VmOpCode)142:
			case (VmOpCode)158:
				break;
			case (VmOpCode)159:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = Stack.Pop() as NumericValue;
				IntPtr intPtr = GetNativeAddress(Stack.Pop());
				IntPtr intPtr2 = GetNativeAddress(Stack.Pop());
				if (intPtr != IntPtr.Zero && intPtr2 != IntPtr.Zero)
				{
					uint num2 = c7EoQ7IJLjSNsQhooYs.ToUInt32().Bits.UInt32;
					CopyMemoryBlock(intPtr2, intPtr, num2);
				}
				break;
			}
			case (VmOpCode)160:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				object obj2 = ((Array)Stack.Pop().ToObject(null)).GetValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Stack.Push(VmValue.CreateValue(typeof(sbyte), obj2));
				break;
			}
			case (VmOpCode)161:
			{
				int metadataToken = (int)CurrentOperand;
				Type elementType2 = typeof(VirtualMachineRuntime).Module.ResolveType(metadataToken);
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				Array array = Array.CreateInstance(elementType2, c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Stack.Push(new ObjectValue(array));
				break;
			}
			case (VmOpCode)162:
			{
				int metadataToken = (int)CurrentOperand;
				FieldInfo fieldInfo = typeof(VirtualMachineRuntime).Module.ResolveField(metadataToken);
				object obj2 = Stack.Pop().ToObject(null);
				Stack.Push(VmValue.CreateValue(fieldInfo.FieldType, fieldInfo.GetValue(obj2)));
				break;
			}
			case (VmOpCode)163:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ToInt32Checked());
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)164:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					throw new VmOperandException();
				}
				Stack.Push(c7EoQ7IJLjSNsQhooYs.ToNativeUIntChecked());
				break;
			}
			case (VmOpCode)165:
				break;
			case (VmOpCode)166:
			{
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				NumericValue c7EoQ7IJLjSNsQhooYs2 = AsNumericValue(Stack.Pop());
				if (c7EoQ7IJLjSNsQhooYs2 != null && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs2.Add(c7EoQ7IJLjSNsQhooYs));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)167:
			{
				VmValue VmValue = Stack.Pop();
				if (VmValue.IsNumericValue())
				{
					VmValue = ((NumericValue)VmValue).ConvertToInt16();
				}
				Stack.Pop().Assign(VmValue);
				break;
			}
			case (VmOpCode)168:
			{
				VmValue VmValue = Stack.Pop();
				if (!AsNumericValue(Stack.Pop()).LessThan(VmValue))
				{
					Stack.Push(new Int32Value(0));
				}
				else
				{
					Stack.Push(new Int32Value(1));
				}
				break;
			}
			case (VmOpCode)169:
			{
				int metadataToken = (int)CurrentOperand;
				Type elementType = typeof(VirtualMachineRuntime).Module.ResolveType(metadataToken);
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				object obj2 = ((Array)Stack.Pop().ToObject(null)).GetValue(c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				Stack.Push(VmValue.CreateValue(elementType, obj2));
				break;
			}
			case (VmOpCode)170:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(VmValue);
				if (VmValue != null && VmValue.IsManagedReference() && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToInt64());
					break;
				}
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs.IsNativeIntegerValue())
				{
					IntPtr intPtr = ((NativeIntegerValue)c7EoQ7IJLjSNsQhooYs).ToIntPtr();
					Stack.Push(new Int64Value(*(long*)(void*)intPtr, (VmPrimitiveType)7));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)171:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(VmValue);
				if (VmValue != null && VmValue.IsManagedReference() && c7EoQ7IJLjSNsQhooYs != null)
				{
					Stack.Push(c7EoQ7IJLjSNsQhooYs.ConvertToInt32());
					break;
				}
				if (c7EoQ7IJLjSNsQhooYs != null && c7EoQ7IJLjSNsQhooYs.IsNativeIntegerValue())
				{
					IntPtr intPtr = ((NativeIntegerValue)c7EoQ7IJLjSNsQhooYs).ToIntPtr();
					Stack.Push(new Int32Value(*(int*)(void*)intPtr, (VmPrimitiveType)5));
					break;
				}
				throw new VmOperandException();
			}
			case (VmOpCode)172:
			{
				VmValue VmValue = Stack.Pop();
				bool num = AsNumericValue(Stack.Pop()).LessThanUnsigned(VmValue);
				if (num)
				{
					Stack.Push(new Int32Value(1));
				}
				else
				{
					Stack.Push(new Int32Value(0));
				}
				if (num)
				{
					InstructionPointer = (int)CurrentOperand - 1;
				}
				break;
			}
			case (VmOpCode)54:
			case (VmOpCode)64:
			case (VmOpCode)84:
			case (VmOpCode)85:
			case (VmOpCode)102:
			case (VmOpCode)116:
			case (VmOpCode)145:
			case (VmOpCode)173:
			{
				VmValue VmValue = Stack.Pop();
				NumericValue c7EoQ7IJLjSNsQhooYs = AsNumericValue(Stack.Pop());
				Array obj = (Array)Stack.Pop().ToObject(null);
				Type elementType = obj.GetType().GetElementType();
				obj.SetValue(VmValue.ToObject(elementType), c7EoQ7IJLjSNsQhooYs.ToInt32().Bits.Int32);
				break;
			}
			case (VmOpCode)174:
			{
				int metadataToken = (int)CurrentOperand;
				FieldInfo fieldInfo = typeof(VirtualMachineRuntime).Module.ResolveField(metadataToken);
				Stack.Push(new FieldReferenceValue(fieldInfo, null));
				break;
			}
			case (VmOpCode)175:
			{
				int metadataToken = (int)CurrentOperand;
				FieldInfo fieldInfo = typeof(VirtualMachineRuntime).Module.ResolveField(metadataToken);
				Stack.Push(VmValue.CreateValue(fieldInfo.FieldType, fieldInfo.GetValue(null)));
				break;
			}
			}
		}

		private VmValue CoerceStoredValue(VmValue P_0, VmPrimitiveType P_1, bool P_2 = false)
		{
			while (true)
			{
				IL_0081:
				if (P_2)
				{
					goto IL_006d;
				}
				goto IL_0077;
				IL_0077:
				if (P_0.IsManagedReference())
				{
					goto IL_0003;
				}
				goto IL_006d;
				IL_0003:
				P_0 = P_0.ReadValue();
				goto IL_006d;
				IL_006d:
				while (true)
				{
					int num;
					if (!P_0.IsInt32Value())
					{
						num = 0;
						if (IsExecutionFrameObfuscationSentinelNull())
						{
							goto IL_001a;
						}
						goto IL_002e;
					}
					goto IL_00c2;
					IL_0091:
					return ((Int64Value)P_0).ConvertToPrimitiveType(P_1);
					IL_002e:
					switch (num)
					{
					case 10:
						break;
					case 2:
						goto IL_001a;
					case 3:
					case 4:
					case 12:
						continue;
					case 1:
						goto IL_0077;
					case 5:
						goto IL_0081;
					default:
						goto IL_0087;
					case 6:
						goto IL_0091;
					case 9:
						goto IL_009e;
					case 8:
						goto IL_00a6;
					case 11:
						goto IL_00a8;
					case 13:
						goto IL_00b5;
					case 7:
						goto IL_00c2;
					}
					break;
					IL_001a:
					if (!P_0.IsInt64Value())
					{
						num = 0;
						if (!IsExecutionFrameObfuscationSentinelNull())
						{
							goto IL_002e;
						}
						goto IL_0087;
					}
					goto IL_0091;
					IL_00c2:
					return ((Int32Value)P_0).ConvertToPrimitiveType(P_1);
					IL_0087:
					if (!P_0.IsFloatingPointValue())
					{
						goto IL_009e;
					}
					goto IL_00a8;
					IL_009e:
					if (!P_0.IsNativeIntegerValue())
					{
						goto IL_00a6;
					}
					goto IL_00b5;
					IL_00a6:
					return P_0;
					IL_00b5:
					return ((NativeIntegerValue)P_0).ConvertToPrimitiveType(P_1);
					IL_00a8:
					return ((FloatingPointValue)P_0).ConvertToPrimitiveType(P_1);
				}
				goto IL_0003;
			}
		}

		private VmValue LoadLocal(int P_0)
		{
			return Locals[P_0];
		}

		private void PopIntoLocal(int P_0)
		{
			while (true)
			{
				StoreLocal(P_0, Stack.Pop());
				if (GetExecutionFrameObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		private static int GetManagedSize(Type P_0)
		{
			bool lockTaken = default(bool);
			int result = default(int);
			while (true)
			{
				object obj = ManagedSizeCacheLock;
				if (IsExecutionFrameObfuscationSentinelNull())
				{
					switch (1)
					{
					case 2:
						break;
					case 1:
						goto IL_002f;
					case 3:
						goto IL_0033;
					default:
						goto end_IL_0001;
					}
					continue;
				}
				goto IL_002f;
				IL_0033:
				try
				{
					Monitor.Enter(obj, ref lockTaken);
					while (true)
					{
						if (ManagedSizeCache == null)
						{
							goto IL_003e;
						}
						goto IL_006c;
						IL_006c:
						int num = 0;
						if (GetExecutionFrameObfuscationSentinel() != null)
						{
							break;
						}
						goto IL_0054;
						IL_0054:
						switch (num)
						{
						case 3:
							break;
						case 1:
							goto IL_006c;
						case 2:
							continue;
						default:
							goto end_IL_007a;
						}
						goto IL_003e;
						IL_003e:
						ManagedSizeCache = new Dictionary<Type, int>();
						num = 1;
						if (!IsExecutionFrameObfuscationSentinelNull())
						{
							goto IL_0054;
						}
						goto IL_006c;
						continue;
						end_IL_007a:
						break;
					}
					try
					{
						int value = 0;
						int num2 = 1;
						if (GetExecutionFrameObfuscationSentinel() == null)
						{
							goto IL_0110;
						}
						goto IL_012e;
						IL_0110:
						if (!ManagedSizeCache.TryGetValue(P_0, out value))
						{
							goto IL_00b8;
						}
						num2 = 2;
						if (IsExecutionFrameObfuscationSentinelNull())
						{
							goto IL_00fe;
						}
						goto IL_012e;
						IL_0155:
						result = value;
						goto end_IL_0085;
						IL_00fe:
						result = value;
						num2 = 2;
						if (IsExecutionFrameObfuscationSentinelNull())
						{
							goto IL_012e;
						}
						goto end_IL_0085;
						IL_012e:
						switch (num2)
						{
						case 6:
							break;
						case 5:
							goto IL_00b8;
						case 4:
							goto IL_00fe;
						case 1:
							goto IL_0110;
						case 2:
							goto end_IL_0085;
						default:
							goto IL_0155;
						case 3:
							goto end_IL_0085;
						}
						goto IL_009a;
						IL_00b8:
						DynamicMethod dynamicMethod = new DynamicMethod(string.Empty, typeof(int), Type.EmptyTypes, restrictedSkipVisibility: true);
						ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
						iLGenerator.Emit(OpCodes.Sizeof, P_0);
						iLGenerator.Emit(OpCodes.Ret);
						value = (int)dynamicMethod.Invoke(null, null);
						goto IL_009a;
						IL_009a:
						ManagedSizeCache[P_0] = value;
						num2 = 0;
						if (GetExecutionFrameObfuscationSentinel() != null)
						{
							goto IL_012e;
						}
						goto IL_0155;
						end_IL_0085:;
					}
					catch
					{
						int num3 = 0;
						if (IsExecutionFrameObfuscationSentinelNull())
						{
							goto IL_0169;
						}
						goto IL_017b;
						IL_0169:
						do
						{
							result = 0;
							num3 = 1;
						}
						while (!IsExecutionFrameObfuscationSentinelNull());
						goto IL_017b;
						IL_017b:
						switch (num3)
						{
						case 1:
							goto end_IL_015b;
						}
						goto IL_0169;
						end_IL_015b:;
					}
				}
				finally
				{
					if (lockTaken)
					{
						goto IL_01b2;
					}
					int num4 = 1;
					if (GetExecutionFrameObfuscationSentinel() == null)
					{
						goto IL_019f;
					}
					goto end_IL_018c;
					IL_01b2:
					Monitor.Exit(obj);
					num4 = 0;
					if (IsExecutionFrameObfuscationSentinelNull())
					{
						goto IL_019f;
					}
					goto end_IL_018c;
					IL_019f:
					switch (num4)
					{
					default:
						goto end_IL_018c;
					case 2:
						break;
					case 0:
					case 1:
						goto end_IL_018c;
					}
					goto IL_01b2;
					end_IL_018c:;
				}
				break;
				IL_002f:
				lockTaken = false;
				goto IL_0033;
				continue;
				end_IL_0001:
				break;
			}
			return result;
		}

		private void StoreLocal(int P_0, VmValue P_1)
		{
			while (true)
			{
				Locals[P_0] = CoerceStoredValue(P_1, MethodBody.Locals[P_0].PrimitiveType, MethodBody.Locals[P_0].PreserveReference);
				if (GetExecutionFrameObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		private static NumericValue AsNumericValue(object P_0)
		{
			NumericValue c7EoQ7IJLjSNsQhooYs;
			while (true)
			{
				c7EoQ7IJLjSNsQhooYs = P_0 as NumericValue;
				int num = 0;
				if (IsExecutionFrameObfuscationSentinelNull())
				{
					goto IL_0028;
				}
				goto IL_0038;
				IL_0038:
				switch (num)
				{
				case 5:
					break;
				case 4:
					goto IL_001e;
				case 1:
					goto IL_0028;
				case 2:
					continue;
				default:
					goto end_IL_005b;
				}
				goto IL_0003;
				IL_0028:
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					goto IL_001e;
				}
				num = 6;
				if (IsExecutionFrameObfuscationSentinelNull())
				{
					break;
				}
				goto IL_0038;
				IL_0003:
				c7EoQ7IJLjSNsQhooYs = ((VmValue)P_0).ReadValue() as NumericValue;
				num = 0;
				if (GetExecutionFrameObfuscationSentinel() == null)
				{
					break;
				}
				goto IL_0038;
				IL_001e:
				if (!((VmValue)P_0).IsManagedReference())
				{
					break;
				}
				goto IL_0003;
				continue;
				end_IL_005b:
				break;
			}
			return c7EoQ7IJLjSNsQhooYs;
		}

		private void InvokeMethod(bool P_0)
		{
			int num3 = default(int);
			FieldArgumentCallKey FieldArgumentCallKey = default(FieldArgumentCallKey);
			VmValue[] array = default(VmValue[]);
			object[] array2 = default(object[]);
			object obj2 = default(object);
			FieldReferenceValue FieldReferenceValue = default(FieldReferenceValue);
			VmValue VmValue = default(VmValue);
			bool flag = default(bool);
			Type type2 = default(Type);
			Type type = default(Type);
			object obj = default(object);
			VmValue zaY0HTeglK55vqASp0b2 = default(VmValue);
			List<FieldArgumentBinding> list = default(List<FieldArgumentBinding>);
			int num2 = default(int);
			object obj3 = default(object);
			MethodInvoker iC9Dj5Go7DZA3kLFH = default(MethodInvoker);
			while (true)
			{
				int metadataToken = (int)CurrentOperand;
				while (true)
				{
					MethodBase methodBase = typeof(VirtualMachineRuntime).Module.ResolveMethod(metadataToken);
					while (true)
					{
						MethodInfo methodInfo = methodBase as MethodInfo;
						while (true)
						{
							IL_0815:
							ParameterInfo[] parameters = methodBase.GetParameters();
							int num = 35;
							if (GetExecutionFrameObfuscationSentinel() == null)
							{
								goto IL_05b9;
							}
							goto IL_0617;
							IL_0617:
							while (true)
							{
								switch (num)
								{
								case 95:
								case 124:
									if (num3 < parameters.Length)
									{
										goto case 58;
									}
									goto case 112;
								case 58:
								case 69:
									if (parameters[num3].ParameterType.IsByRef)
									{
										goto case 121;
									}
									goto case 28;
								case 121:
									if (FieldArgumentCallKey == null)
									{
										goto case 19;
									}
									num = 83;
									if (IsExecutionFrameObfuscationSentinelNull())
									{
										continue;
									}
									goto case 24;
								case 19:
									if (array[num3].IsNativeIntegerValue())
									{
										goto case 47;
									}
									goto case 120;
								case 47:
									((NativeIntegerValue)array[num3]).CopyNativeIntegerPayload(VmValue.CreateValue(parameters[num3].ParameterType, array2[num3]));
									num = 14;
									if (GetExecutionFrameObfuscationSentinel() != null)
									{
										continue;
									}
									goto case 28;
								case 120:
									if (array[num3] is LocalReferenceValue)
									{
										num = 41;
										if (GetExecutionFrameObfuscationSentinel() != null)
										{
											continue;
										}
										goto case 100;
									}
									goto case 55;
								case 100:
									array[num3].CopyFrom(VmValue.CreateValue(parameters[num3].ParameterType.GetElementType(), array2[num3]));
									goto case 28;
								case 55:
									array[num3].CopyFrom(VmValue.CreateValue(parameters[num3].ParameterType, array2[num3]));
									goto case 28;
								case 28:
								case 45:
								case 80:
								case 88:
									num3++;
									goto case 95;
								case 24:
									obj2 = FieldReferenceValue.Target;
									goto case 23;
								case 23:
									if (obj2 is VmValue)
									{
										goto case 32;
									}
									goto case 53;
								case 32:
									VmValue = obj2 as VmValue;
									goto IL_04d0;
								case 53:
									flag = true;
									num = 53;
									if (GetExecutionFrameObfuscationSentinel() != null)
									{
										continue;
									}
									goto case 59;
								case 59:
									if (obj2 == null)
									{
										num = 7;
										if (GetExecutionFrameObfuscationSentinel() != null)
										{
											continue;
										}
										goto case 41;
									}
									goto IL_04d0;
								case 41:
									if (type2.IsByRef)
									{
										goto case 98;
									}
									goto case 18;
								case 98:
									type2 = type2.GetElementType();
									goto case 18;
								case 18:
									if (type2.IsValueType)
									{
										goto case 34;
									}
									goto IL_04d0;
								case 34:
									if (FieldReferenceValue.Field.IsStatic)
									{
										goto case 109;
									}
									num = 22;
									if (GetExecutionFrameObfuscationSentinel() != null)
									{
										continue;
									}
									goto IL_0476;
								case 109:
									obj2 = FieldReferenceValue.Field.GetValue(null);
									goto IL_047f;
								case 111:
									break;
								case 38:
									goto IL_018a;
								case 20:
									goto IL_01a5;
								case 87:
									goto IL_01c0;
								case 35:
									goto IL_01d8;
								case 27:
								case 29:
								case 31:
								case 64:
								case 82:
									goto IL_01df;
								case 65:
									goto IL_01fd;
								case 118:
									goto IL_020b;
								case 103:
									goto IL_0216;
								case 102:
									goto IL_0223;
								case 52:
									goto IL_0236;
								case 106:
									goto IL_0239;
								case 115:
									goto IL_024c;
								case 1:
									goto IL_0268;
								case 3:
									goto IL_026b;
								case 51:
									goto IL_0273;
								case 36:
									goto IL_0276;
								case 48:
									goto IL_028a;
								case 6:
									goto IL_0295;
								case 108:
									goto IL_02a3;
								case 33:
									goto IL_02ad;
								case 16:
									goto IL_02bb;
								case 12:
								case 101:
									goto IL_02d5;
								case 39:
									goto IL_02e8;
								default:
									goto IL_02fb;
								case 57:
									goto IL_0304;
								case 93:
									goto IL_0311;
								case 9:
									goto IL_0315;
								case 67:
									goto IL_0319;
								case 10:
								case 90:
									goto IL_0332;
								case 2:
								case 37:
								case 68:
								case 123:
									goto IL_0354;
								case 99:
									goto IL_0357;
								case 74:
									goto IL_0372;
								case 11:
								case 42:
								case 63:
									goto IL_0386;
								case 70:
								case 104:
									goto IL_039a;
								case 62:
									goto IL_03a9;
								case 8:
									goto IL_03b8;
								case 91:
									goto IL_03be;
								case 26:
									goto IL_03c2;
								case 81:
									goto IL_03cb;
								case 40:
								case 78:
								case 92:
								case 105:
								case 119:
									goto IL_03ea;
								case 94:
									goto IL_0402;
								case 77:
								case 113:
									goto IL_040e;
								case 110:
									goto IL_041a;
								case 7:
									goto IL_0433;
								case 46:
									goto IL_0451;
								case 14:
								case 25:
									goto IL_0476;
								case 71:
								case 117:
									goto IL_047f;
								case 4:
									goto IL_0488;
								case 73:
									goto IL_04b2;
								case 5:
								case 49:
								case 96:
									goto IL_04d0;
								case 116:
									if (Nullable.GetUnderlyingType(type) != null)
									{
										goto case 114;
									}
									goto case 44;
								case 114:
									obj = FormatterServices.GetUninitializedObject(type);
									num = 14;
									if (GetExecutionFrameObfuscationSentinel() != null)
									{
										continue;
									}
									goto case 44;
								case 44:
								case 60:
									if (!(zaY0HTeglK55vqASp0b2 is LocalReferenceValue))
									{
										goto IL_0354;
									}
									goto case 89;
								case 89:
									((ReferenceValue)zaY0HTeglK55vqASp0b2).StoreValue(VmValue.CreateValue(type, obj));
									num = 68;
									if (IsExecutionFrameObfuscationSentinelNull())
									{
										continue;
									}
									goto case 32;
								case 83:
									if (!FieldArgumentCallKey.HasFieldArgument(num3))
									{
										goto case 19;
									}
									goto case 28;
								case 79:
									type = type.GetElementType();
									goto case 17;
								case 17:
								case 72:
									if (type.IsValueType)
									{
										goto case 61;
									}
									goto case 21;
								case 61:
									obj = Activator.CreateInstance(type);
									goto case 13;
								case 13:
									if (obj == null)
									{
										goto case 116;
									}
									goto case 44;
								case 75:
									if (FieldReferenceValue == null)
									{
										num = 49;
										if (IsExecutionFrameObfuscationSentinelNull())
										{
											continue;
										}
										goto IL_028a;
									}
									goto case 56;
								case 56:
									if (list == null)
									{
										goto case 22;
									}
									goto case 50;
								case 22:
									list = new List<FieldArgumentBinding>();
									goto case 50;
								case 50:
									list.Add(new FieldArgumentBinding(FieldReferenceValue.Field, parameters.Length - 1 - num2));
									goto case 24;
								case 54:
									goto IL_05b9;
								case 43:
									if (type.IsByRef)
									{
										goto case 79;
									}
									goto case 17;
								case 30:
									obj = zaY0HTeglK55vqASp0b2.ToObject(methodBase.DeclaringType);
									num = 9;
									if (IsExecutionFrameObfuscationSentinelNull())
									{
										continue;
									}
									goto IL_0433;
								case 15:
									FieldReferenceValue = VmValue as FieldReferenceValue;
									num = 75;
									if (IsExecutionFrameObfuscationSentinelNull())
									{
										continue;
									}
									goto IL_039a;
								case 107:
									goto IL_0815;
								case 86:
									goto end_IL_0815;
								case 84:
									goto end_IL_0833;
								case 85:
									goto end_IL_083e;
								case 21:
									throw new NullReferenceException();
								case 112:
									if (!(methodInfo != null))
									{
										return;
									}
									goto case 66;
								case 66:
									if (!(methodInfo.ReturnType != typeof(void)))
									{
										return;
									}
									goto case 122;
								case 122:
									Stack.Push(VmValue.CreateValue(methodInfo.ReturnType, obj3));
									return;
								case 76:
								case 97:
									return;
								}
								break;
							}
							goto IL_0186;
							IL_05b9:
							array2 = new object[parameters.Length];
							goto IL_01a5;
							IL_04d0:
							if (!flag)
							{
								goto IL_0186;
							}
							goto IL_01df;
							IL_0186:
							if (VmValue != null)
							{
								goto IL_018a;
							}
							goto IL_01d8;
							IL_018a:
							obj2 = VmValue.ToObject(type2);
							num = 35;
							if (!IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_01a5;
							}
							goto IL_0617;
							IL_01a5:
							array = new VmValue[parameters.Length];
							num = 72;
							if (GetExecutionFrameObfuscationSentinel() == null)
							{
								goto IL_01c0;
							}
							goto IL_0617;
							IL_01c0:
							list = null;
							num = 0;
							if (IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_0268;
							}
							goto IL_0617;
							IL_01d8:
							if (obj2 != null)
							{
								goto IL_01df;
							}
							goto IL_0402;
							IL_0402:
							if (!type2.IsByRef)
							{
								goto IL_040e;
							}
							goto IL_04b2;
							IL_040e:
							if (!type2.IsValueType)
							{
								goto IL_01df;
							}
							goto IL_041a;
							IL_041a:
							obj2 = Activator.CreateInstance(type2);
							num = 2;
							if (GetExecutionFrameObfuscationSentinel() == null)
							{
								goto IL_0433;
							}
							goto IL_0617;
							IL_0433:
							if (VmValue is LocalReferenceValue)
							{
								goto IL_0451;
							}
							num = 38;
							if (GetExecutionFrameObfuscationSentinel() == null)
							{
								goto IL_01df;
							}
							goto IL_0617;
							IL_024c:
							if (!type2.IsByRef)
							{
								goto IL_04d0;
							}
							num = 15;
							if (!IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_0268;
							}
							goto IL_0617;
							IL_0451:
							((ReferenceValue)VmValue).StoreValue(VmValue.CreateValue(type2, obj2));
							num = 27;
							if (GetExecutionFrameObfuscationSentinel() != null)
							{
								goto IL_0476;
							}
							goto IL_0617;
							IL_0476:
							obj2 = Activator.CreateInstance(type2);
							goto IL_047f;
							IL_047f:
							if (VmValue is LocalReferenceValue)
							{
								goto IL_0488;
							}
							goto IL_04d0;
							IL_0488:
							((ReferenceValue)VmValue).StoreValue(VmValue.CreateValue(type2, obj2));
							num = 5;
							if (!IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_0354;
							}
							goto IL_0617;
							IL_01df:
							array[array2.Length - 1 - num2] = VmValue;
							num = 22;
							if (IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_01fd;
							}
							goto IL_0617;
							IL_01fd:
							array2[array2.Length - 1 - num2] = obj2;
							goto IL_020b;
							IL_020b:
							num2++;
							goto IL_0332;
							IL_04b2:
							type2 = type2.GetElementType();
							num = 77;
							if (!IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_0319;
							}
							goto IL_0617;
							IL_0319:
							type = methodBase.DeclaringType;
							num = 43;
							if (!IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_0332;
							}
							goto IL_0617;
							IL_0332:
							if (num2 < parameters.Length)
							{
								goto IL_0216;
							}
							goto IL_0273;
							IL_0273:
							iC9Dj5Go7DZA3kLFH = null;
							goto IL_0276;
							IL_0276:
							if (list == null)
							{
								goto IL_02a3;
							}
							num = 41;
							if (IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_028a;
							}
							goto IL_0617;
							IL_0236:
							obj2 = null;
							goto IL_0239;
							IL_028a:
							FieldArgumentCallKey = new FieldArgumentCallKey(methodBase, list);
							goto IL_0295;
							IL_0295:
							iC9Dj5Go7DZA3kLFH = GetOrCreateFieldArgumentInvoker(methodBase, P_0, FieldArgumentCallKey);
							goto IL_02d5;
							IL_02a3:
							if (methodInfo != null)
							{
								goto IL_02ad;
							}
							goto IL_02d5;
							IL_02ad:
							if (methodInfo.ReturnType.IsByRef)
							{
								goto IL_02bb;
							}
							goto IL_02d5;
							IL_02bb:
							iC9Dj5Go7DZA3kLFH = GetOrCreateMethodInvoker(methodBase, P_0);
							num = 11;
							if (GetExecutionFrameObfuscationSentinel() == null)
							{
								goto IL_02d5;
							}
							goto IL_0617;
							IL_02d5:
							obj = null;
							num = 30;
							if (IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_02e8;
							}
							goto IL_0617;
							IL_02e8:
							zaY0HTeglK55vqASp0b2 = null;
							num = 0;
							if (!IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_02fb;
							}
							goto IL_0617;
							IL_02fb:
							if (!methodBase.IsStatic)
							{
								goto IL_0304;
							}
							goto IL_0354;
							IL_0304:
							zaY0HTeglK55vqASp0b2 = Stack.Pop();
							goto IL_0311;
							IL_0311:
							if (zaY0HTeglK55vqASp0b2 == null)
							{
								goto IL_0315;
							}
							num = 30;
							if (GetExecutionFrameObfuscationSentinel() != null)
							{
								goto IL_03b8;
							}
							goto IL_0617;
							IL_0315:
							if (obj == null)
							{
								goto IL_0319;
							}
							goto IL_0354;
							IL_0239:
							flag = false;
							num = 72;
							if (GetExecutionFrameObfuscationSentinel() == null)
							{
								goto IL_024c;
							}
							goto IL_0617;
							IL_0354:
							obj3 = null;
							goto IL_0357;
							IL_0357:
							if (methodBase is ConstructorInfo)
							{
								goto IL_0372;
							}
							num = 7;
							if (GetExecutionFrameObfuscationSentinel() == null)
							{
								goto IL_0386;
							}
							goto IL_0617;
							IL_0268:
							FieldArgumentCallKey = null;
							goto IL_026b;
							IL_0372:
							if (!(Nullable.GetUnderlyingType(methodBase.DeclaringType) != null))
							{
								goto IL_0386;
							}
							goto IL_03b8;
							IL_0386:
							if (iC9Dj5Go7DZA3kLFH != null)
							{
								goto IL_03a9;
							}
							num = 97;
							if (GetExecutionFrameObfuscationSentinel() == null)
							{
								goto IL_039a;
							}
							goto IL_0617;
							IL_026b:
							num2 = 0;
							goto IL_0332;
							IL_039a:
							obj3 = methodBase.Invoke(obj, array2);
							goto IL_03ea;
							IL_03a9:
							obj3 = iC9Dj5Go7DZA3kLFH(obj, array2);
							goto IL_03ea;
							IL_03b8:
							obj3 = array2[0];
							goto IL_03be;
							IL_03be:
							if (zaY0HTeglK55vqASp0b2 != null)
							{
								goto IL_03c2;
							}
							goto IL_03ea;
							IL_03c2:
							if (zaY0HTeglK55vqASp0b2 is LocalReferenceValue)
							{
								goto IL_03cb;
							}
							goto IL_03ea;
							IL_03cb:
							((ReferenceValue)zaY0HTeglK55vqASp0b2).StoreValue(VmValue.CreateValue(Nullable.GetUnderlyingType(methodBase.DeclaringType), obj3));
							goto IL_03ea;
							IL_03ea:
							num3 = 0;
							num = 95;
							if (GetExecutionFrameObfuscationSentinel() != null)
							{
								goto IL_01d8;
							}
							goto IL_0617;
							IL_0216:
							VmValue = Stack.Pop();
							goto IL_0223;
							IL_0223:
							type2 = parameters[parameters.Length - 1 - num2].ParameterType;
							goto IL_0236;
							continue;
							end_IL_0815:
							break;
						}
						continue;
						end_IL_0833:
						break;
					}
					continue;
					end_IL_083e:
					break;
				}
			}
		}

		private static MethodInvoker GetOrCreateMethodInvoker(object P_0, bool P_1)
		{
			lock (MethodInvokerCacheLock)
			{
				MethodInvoker value = null;
				if (!P_1)
				{
					if (VirtualMethodInvokers.TryGetValue((MethodBase)P_0, out value))
					{
						return value;
					}
				}
				else if (DirectMethodInvokers.TryGetValue((MethodBase)P_0, out value))
				{
					return value;
				}
				MethodInfo methodInfo = P_0 as MethodInfo;
				DynamicMethod dynamicMethod = new DynamicMethod(string.Empty, typeof(object), new Type[2]
				{
					typeof(object),
					typeof(object[])
				}, restrictedSkipVisibility: true);
				ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
				ParameterInfo[] parameters = ((MethodBase)P_0).GetParameters();
				Type[] array = new Type[parameters.Length];
				for (int i = 0; i < array.Length; i++)
				{
					if (!parameters[i].ParameterType.IsByRef)
					{
						array[i] = parameters[i].ParameterType;
					}
					else
					{
						array[i] = parameters[i].ParameterType.GetElementType();
					}
				}
				int num = array.Length;
				if (((MemberInfo)P_0).DeclaringType.IsValueType)
				{
					num++;
				}
				LocalBuilder[] array2 = new LocalBuilder[num];
				for (int j = 0; j < array.Length; j++)
				{
					array2[j] = iLGenerator.DeclareLocal(array[j]);
				}
				if (((MemberInfo)P_0).DeclaringType.IsValueType)
				{
					array2[array2.Length - 1] = iLGenerator.DeclareLocal(((MemberInfo)P_0).DeclaringType.MakeByRefType());
				}
				for (int k = 0; k < array.Length; k++)
				{
					iLGenerator.Emit(OpCodes.Ldarg_1);
					EmitInt32Constant(iLGenerator, k);
					iLGenerator.Emit(OpCodes.Ldelem_Ref);
					if (!array[k].IsValueType)
					{
						if (array[k] != typeof(object))
						{
							iLGenerator.Emit(OpCodes.Castclass, array[k]);
						}
					}
					else
					{
						iLGenerator.Emit(OpCodes.Unbox_Any, array[k]);
					}
					iLGenerator.Emit(OpCodes.Stloc, array2[k]);
				}
				if (!((MethodBase)P_0).IsStatic)
				{
					iLGenerator.Emit(OpCodes.Ldarg_0);
					if (((MemberInfo)P_0).DeclaringType.IsValueType)
					{
						iLGenerator.Emit(OpCodes.Unbox, ((MemberInfo)P_0).DeclaringType);
						iLGenerator.Emit(OpCodes.Stloc, array2[array2.Length - 1]);
						iLGenerator.Emit(OpCodes.Ldloc_S, array2[array2.Length - 1]);
					}
					else
					{
						iLGenerator.Emit(OpCodes.Castclass, ((MemberInfo)P_0).DeclaringType);
					}
				}
				for (int l = 0; l < array.Length; l++)
				{
					if (!parameters[l].ParameterType.IsByRef)
					{
						iLGenerator.Emit(OpCodes.Ldloc, array2[l]);
					}
					else
					{
						iLGenerator.Emit(OpCodes.Ldloca_S, array2[l]);
					}
				}
				if (P_1)
				{
					if (methodInfo != null)
					{
						iLGenerator.EmitCall(OpCodes.Call, methodInfo, null);
					}
					else
					{
						iLGenerator.Emit(OpCodes.Call, P_0 as ConstructorInfo);
					}
				}
				else if (!(methodInfo != null))
				{
					iLGenerator.Emit(OpCodes.Callvirt, P_0 as ConstructorInfo);
				}
				else
				{
					iLGenerator.EmitCall(OpCodes.Callvirt, methodInfo, null);
				}
				if (!(methodInfo == null) && !(methodInfo.ReturnType == typeof(void)))
				{
					if (!methodInfo.ReturnType.IsByRef)
					{
						if (methodInfo.ReturnType.IsValueType)
						{
							iLGenerator.Emit(OpCodes.Box, methodInfo.ReturnType);
						}
					}
					else
					{
						Type elementType = methodInfo.ReturnType.GetElementType();
						if (!elementType.IsValueType)
						{
							iLGenerator.Emit(OpCodes.Ldind_Ref, elementType);
						}
						else
						{
							iLGenerator.Emit(OpCodes.Ldobj, elementType);
						}
						if (elementType.IsValueType)
						{
							iLGenerator.Emit(OpCodes.Box, elementType);
						}
					}
				}
				else
				{
					iLGenerator.Emit(OpCodes.Ldnull);
				}
				for (int m = 0; m < array.Length; m++)
				{
					if (parameters[m].ParameterType.IsByRef)
					{
						iLGenerator.Emit(OpCodes.Ldarg_1);
						EmitInt32Constant(iLGenerator, m);
						iLGenerator.Emit(OpCodes.Ldloc, array2[m]);
						if (array2[m].LocalType.IsValueType)
						{
							iLGenerator.Emit(OpCodes.Box, array2[m].LocalType);
						}
						iLGenerator.Emit(OpCodes.Stelem_Ref);
					}
				}
				iLGenerator.Emit(OpCodes.Ret);
				MethodInvoker iC9Dj5Go7DZA3kLFH = (MethodInvoker)dynamicMethod.CreateDelegate(typeof(MethodInvoker));
				if (P_1)
				{
					DirectMethodInvokers.Add((MethodBase)P_0, iC9Dj5Go7DZA3kLFH);
				}
				else
				{
					VirtualMethodInvokers.Add((MethodBase)P_0, iC9Dj5Go7DZA3kLFH);
				}
				return iC9Dj5Go7DZA3kLFH;
			}
		}

		private static MethodInvoker GetOrCreateFieldArgumentInvoker(object P_0, bool P_1, object P_2)
		{
			lock (FieldArgumentInvokerCacheLock)
			{
				MethodInvoker value = null;
				if (!P_1)
				{
					if (VirtualFieldArgumentInvokers.TryGetValue((FieldArgumentCallKey)P_2, out value))
					{
						return value;
					}
				}
				else if (DirectFieldArgumentInvokers.TryGetValue((FieldArgumentCallKey)P_2, out value))
				{
					return value;
				}
				MethodInfo methodInfo = P_0 as MethodInfo;
				DynamicMethod dynamicMethod = new DynamicMethod(string.Empty, typeof(object), new Type[2]
				{
					typeof(object),
					typeof(object[])
				}, typeof(VirtualMachineRuntime), skipVisibility: true);
				ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
				ParameterInfo[] parameters = ((MethodBase)P_0).GetParameters();
				Type[] array = new Type[parameters.Length];
				for (int i = 0; i < array.Length; i++)
				{
					if (parameters[i].ParameterType.IsByRef)
					{
						array[i] = parameters[i].ParameterType.GetElementType();
					}
					else
					{
						array[i] = parameters[i].ParameterType;
					}
				}
				int num = array.Length;
				if (((MemberInfo)P_0).DeclaringType.IsValueType)
				{
					num++;
				}
				LocalBuilder[] array2 = new LocalBuilder[num];
				for (int j = 0; j < array.Length; j++)
				{
					if (((FieldArgumentCallKey)P_2).HasFieldArgument(j))
					{
						array2[j] = iLGenerator.DeclareLocal(typeof(object));
					}
					else
					{
						array2[j] = iLGenerator.DeclareLocal(array[j]);
					}
				}
				if (((MemberInfo)P_0).DeclaringType.IsValueType)
				{
					array2[array2.Length - 1] = iLGenerator.DeclareLocal(((MemberInfo)P_0).DeclaringType.MakeByRefType());
				}
				for (int k = 0; k < array.Length; k++)
				{
					iLGenerator.Emit(OpCodes.Ldarg_1);
					EmitInt32Constant(iLGenerator, k);
					iLGenerator.Emit(OpCodes.Ldelem_Ref);
					if (!((FieldArgumentCallKey)P_2).HasFieldArgument(k))
					{
						if (array[k].IsValueType)
						{
							iLGenerator.Emit(OpCodes.Unbox_Any, array[k]);
						}
						else if (array[k] != typeof(object))
						{
							iLGenerator.Emit(OpCodes.Castclass, array[k]);
						}
					}
					iLGenerator.Emit(OpCodes.Stloc, array2[k]);
				}
				if (!((MethodBase)P_0).IsStatic)
				{
					iLGenerator.Emit(OpCodes.Ldarg_0);
					if (!((MemberInfo)P_0).DeclaringType.IsValueType)
					{
						iLGenerator.Emit(OpCodes.Castclass, ((MemberInfo)P_0).DeclaringType);
					}
					else
					{
						iLGenerator.Emit(OpCodes.Unbox, ((MemberInfo)P_0).DeclaringType);
						iLGenerator.Emit(OpCodes.Stloc, array2[array2.Length - 1]);
						iLGenerator.Emit(OpCodes.Ldloc_S, array2[array2.Length - 1]);
					}
				}
				for (int l = 0; l < array.Length; l++)
				{
					if (((FieldArgumentCallKey)P_2).HasFieldArgument(l))
					{
						FieldArgumentBinding FieldArgumentBinding = ((FieldArgumentCallKey)P_2).FindFieldArgument(l);
						if (!FieldArgumentBinding.Field.IsStatic)
						{
							if (!FieldArgumentBinding.Field.DeclaringType.IsValueType)
							{
								iLGenerator.Emit(OpCodes.Ldloc, array2[l]);
								iLGenerator.Emit(OpCodes.Castclass, FieldArgumentBinding.Field.DeclaringType);
								iLGenerator.Emit(OpCodes.Ldflda, FieldArgumentBinding.Field);
							}
							else
							{
								iLGenerator.Emit(OpCodes.Ldloc, array2[l]);
								iLGenerator.Emit(OpCodes.Unbox, FieldArgumentBinding.Field.DeclaringType);
								iLGenerator.Emit(OpCodes.Ldflda, FieldArgumentBinding.Field);
							}
						}
						else
						{
							iLGenerator.Emit(OpCodes.Ldsflda, FieldArgumentBinding.Field);
						}
					}
					else if (parameters[l].ParameterType.IsByRef)
					{
						iLGenerator.Emit(OpCodes.Ldloca_S, array2[l]);
					}
					else
					{
						iLGenerator.Emit(OpCodes.Ldloc, array2[l]);
					}
				}
				if (!P_1)
				{
					if (methodInfo != null)
					{
						iLGenerator.EmitCall(OpCodes.Callvirt, methodInfo, null);
					}
					else
					{
						iLGenerator.Emit(OpCodes.Callvirt, P_0 as ConstructorInfo);
					}
				}
				else if (methodInfo != null)
				{
					iLGenerator.EmitCall(OpCodes.Call, methodInfo, null);
				}
				else
				{
					iLGenerator.Emit(OpCodes.Call, P_0 as ConstructorInfo);
				}
				if (!(methodInfo == null) && !(methodInfo.ReturnType == typeof(void)))
				{
					if (!methodInfo.ReturnType.IsByRef)
					{
						if (methodInfo.ReturnType.IsValueType)
						{
							iLGenerator.Emit(OpCodes.Box, methodInfo.ReturnType);
						}
					}
					else
					{
						Type elementType = methodInfo.ReturnType.GetElementType();
						if (!elementType.IsValueType)
						{
							iLGenerator.Emit(OpCodes.Ldind_Ref, elementType);
						}
						else
						{
							iLGenerator.Emit(OpCodes.Ldobj, elementType);
						}
						if (elementType.IsValueType)
						{
							iLGenerator.Emit(OpCodes.Box, elementType);
						}
					}
				}
				else
				{
					iLGenerator.Emit(OpCodes.Ldnull);
				}
				for (int m = 0; m < array.Length; m++)
				{
					if (!parameters[m].ParameterType.IsByRef)
					{
						continue;
					}
					if (!((FieldArgumentCallKey)P_2).HasFieldArgument(m))
					{
						iLGenerator.Emit(OpCodes.Ldarg_1);
						EmitInt32Constant(iLGenerator, m);
						iLGenerator.Emit(OpCodes.Ldloc, array2[m]);
						if (array2[m].LocalType.IsValueType)
						{
							iLGenerator.Emit(OpCodes.Box, array2[m].LocalType);
						}
						iLGenerator.Emit(OpCodes.Stelem_Ref);
						continue;
					}
					FieldArgumentBinding ddCD7oGpH12LRZ0KBlB2 = ((FieldArgumentCallKey)P_2).FindFieldArgument(m);
					if (!ddCD7oGpH12LRZ0KBlB2.Field.IsStatic)
					{
						iLGenerator.Emit(OpCodes.Ldarg_1);
						EmitInt32Constant(iLGenerator, m);
						iLGenerator.Emit(OpCodes.Ldloc, array2[m]);
						if (array2[m].LocalType.IsValueType)
						{
							iLGenerator.Emit(OpCodes.Box, ddCD7oGpH12LRZ0KBlB2.Field.FieldType);
						}
						iLGenerator.Emit(OpCodes.Stelem_Ref);
					}
					else
					{
						iLGenerator.Emit(OpCodes.Ldarg_1);
						EmitInt32Constant(iLGenerator, m);
						iLGenerator.Emit(OpCodes.Ldsfld, ddCD7oGpH12LRZ0KBlB2.Field);
						if (ddCD7oGpH12LRZ0KBlB2.Field.FieldType.IsValueType)
						{
							iLGenerator.Emit(OpCodes.Box, ddCD7oGpH12LRZ0KBlB2.Field.FieldType);
						}
						iLGenerator.Emit(OpCodes.Stelem_Ref);
					}
				}
				iLGenerator.Emit(OpCodes.Ret);
				MethodInvoker iC9Dj5Go7DZA3kLFH = (MethodInvoker)dynamicMethod.CreateDelegate(typeof(MethodInvoker));
				if (P_1)
				{
					DirectFieldArgumentInvokers.Add((FieldArgumentCallKey)P_2, iC9Dj5Go7DZA3kLFH);
				}
				else
				{
					VirtualFieldArgumentInvokers.Add((FieldArgumentCallKey)P_2, iC9Dj5Go7DZA3kLFH);
				}
				return iC9Dj5Go7DZA3kLFH;
			}
		}

		private static MethodInvoker GetOrCreateConstructorInvoker(object P_0, bool P_1, object P_2)
		{
			lock (ConstructorInvokerCacheLock)
			{
				MethodInvoker value = null;
				if (!ConstructorInvokers.TryGetValue((FieldArgumentCallKey)P_2, out value))
				{
					ConstructorInfo constructorInfo = P_0 as ConstructorInfo;
					DynamicMethod dynamicMethod = new DynamicMethod(string.Empty, typeof(object), new Type[2]
					{
						typeof(object),
						typeof(object[])
					}, typeof(VirtualMachineRuntime), skipVisibility: true);
					ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
					ParameterInfo[] parameters = ((MethodBase)P_0).GetParameters();
					Type[] array = new Type[parameters.Length];
					for (int i = 0; i < array.Length; i++)
					{
						if (!parameters[i].ParameterType.IsByRef)
						{
							array[i] = parameters[i].ParameterType;
						}
						else
						{
							array[i] = parameters[i].ParameterType.GetElementType();
						}
					}
					int num = array.Length;
					if (((MemberInfo)P_0).DeclaringType.IsValueType)
					{
						num++;
					}
					LocalBuilder[] array2 = new LocalBuilder[num];
					for (int j = 0; j < array.Length; j++)
					{
						if (!((FieldArgumentCallKey)P_2).HasFieldArgument(j))
						{
							array2[j] = iLGenerator.DeclareLocal(array[j]);
						}
						else
						{
							array2[j] = iLGenerator.DeclareLocal(typeof(object));
						}
					}
					if (((MemberInfo)P_0).DeclaringType.IsValueType)
					{
						array2[array2.Length - 1] = iLGenerator.DeclareLocal(((MemberInfo)P_0).DeclaringType.MakeByRefType());
					}
					for (int k = 0; k < array.Length; k++)
					{
						iLGenerator.Emit(OpCodes.Ldarg_1);
						EmitInt32Constant(iLGenerator, k);
						iLGenerator.Emit(OpCodes.Ldelem_Ref);
						if (!((FieldArgumentCallKey)P_2).HasFieldArgument(k))
						{
							if (!array[k].IsValueType)
							{
								if (array[k] != typeof(object))
								{
									iLGenerator.Emit(OpCodes.Castclass, array[k]);
								}
							}
							else
							{
								iLGenerator.Emit(OpCodes.Unbox_Any, array[k]);
							}
						}
						iLGenerator.Emit(OpCodes.Stloc, array2[k]);
					}
					for (int l = 0; l < array.Length; l++)
					{
						if (((FieldArgumentCallKey)P_2).HasFieldArgument(l))
						{
							FieldArgumentBinding FieldArgumentBinding = ((FieldArgumentCallKey)P_2).FindFieldArgument(l);
							if (FieldArgumentBinding.Field.IsStatic)
							{
								iLGenerator.Emit(OpCodes.Ldsflda, FieldArgumentBinding.Field);
							}
							else if (!FieldArgumentBinding.Field.DeclaringType.IsValueType)
							{
								iLGenerator.Emit(OpCodes.Ldloc, array2[l]);
								iLGenerator.Emit(OpCodes.Castclass, FieldArgumentBinding.Field.DeclaringType);
								iLGenerator.Emit(OpCodes.Ldflda, FieldArgumentBinding.Field);
							}
							else
							{
								iLGenerator.Emit(OpCodes.Ldloc, array2[l]);
								iLGenerator.Emit(OpCodes.Unbox, FieldArgumentBinding.Field.DeclaringType);
								iLGenerator.Emit(OpCodes.Ldflda, FieldArgumentBinding.Field);
							}
						}
						else if (parameters[l].ParameterType.IsByRef)
						{
							iLGenerator.Emit(OpCodes.Ldloca_S, array2[l]);
						}
						else
						{
							iLGenerator.Emit(OpCodes.Ldloc, array2[l]);
						}
					}
					iLGenerator.Emit(OpCodes.Newobj, P_0 as ConstructorInfo);
					if (constructorInfo.DeclaringType.IsValueType)
					{
						iLGenerator.Emit(OpCodes.Box, constructorInfo.DeclaringType);
					}
					for (int m = 0; m < array.Length; m++)
					{
						if (!parameters[m].ParameterType.IsByRef)
						{
							continue;
						}
						if (!((FieldArgumentCallKey)P_2).HasFieldArgument(m))
						{
							iLGenerator.Emit(OpCodes.Ldarg_1);
							EmitInt32Constant(iLGenerator, m);
							iLGenerator.Emit(OpCodes.Ldloc, array2[m]);
							if (array2[m].LocalType.IsValueType)
							{
								iLGenerator.Emit(OpCodes.Box, array2[m].LocalType);
							}
							iLGenerator.Emit(OpCodes.Stelem_Ref);
							continue;
						}
						FieldArgumentBinding ddCD7oGpH12LRZ0KBlB2 = ((FieldArgumentCallKey)P_2).FindFieldArgument(m);
						if (!ddCD7oGpH12LRZ0KBlB2.Field.IsStatic)
						{
							iLGenerator.Emit(OpCodes.Ldarg_1);
							EmitInt32Constant(iLGenerator, m);
							iLGenerator.Emit(OpCodes.Ldloc, array2[m]);
							if (array2[m].LocalType.IsValueType)
							{
								iLGenerator.Emit(OpCodes.Box, array2[m].LocalType);
							}
							iLGenerator.Emit(OpCodes.Stelem_Ref);
						}
						else
						{
							iLGenerator.Emit(OpCodes.Ldarg_1);
							EmitInt32Constant(iLGenerator, m);
							iLGenerator.Emit(OpCodes.Ldsfld, ddCD7oGpH12LRZ0KBlB2.Field);
							if (ddCD7oGpH12LRZ0KBlB2.Field.FieldType.IsValueType)
							{
								iLGenerator.Emit(OpCodes.Box, array2[m].LocalType);
							}
							iLGenerator.Emit(OpCodes.Stelem_Ref);
						}
					}
					iLGenerator.Emit(OpCodes.Ret);
					MethodInvoker iC9Dj5Go7DZA3kLFH = (MethodInvoker)dynamicMethod.CreateDelegate(typeof(MethodInvoker));
					ConstructorInvokers.Add((FieldArgumentCallKey)P_2, iC9Dj5Go7DZA3kLFH);
					return iC9Dj5Go7DZA3kLFH;
				}
				return value;
			}
		}

		private static void EmitInt32Constant(object P_0, int P_1)
		{
			while (true)
			{
				IL_00ef:
				int num;
				switch (P_1)
				{
				case 8:
					((ILGenerator)P_0).Emit(OpCodes.Ldc_I4_8);
					num = 11;
					if (IsExecutionFrameObfuscationSentinelNull())
					{
						return;
					}
					goto IL_006f;
				default:
					while (P_1 > -129)
					{
						num = 12;
						if (!IsExecutionFrameObfuscationSentinelNull())
						{
							continue;
						}
						goto IL_006f;
					}
					goto IL_003b;
				case 3:
					((ILGenerator)P_0).Emit(OpCodes.Ldc_I4_3);
					num = 0;
					if (GetExecutionFrameObfuscationSentinel() == null)
					{
						return;
					}
					goto IL_006f;
				case 2:
					((ILGenerator)P_0).Emit(OpCodes.Ldc_I4_2);
					num = 1;
					if (!IsExecutionFrameObfuscationSentinelNull())
					{
						return;
					}
					goto IL_006f;
				case 0:
					((ILGenerator)P_0).Emit(OpCodes.Ldc_I4_0);
					return;
				case 1:
					((ILGenerator)P_0).Emit(OpCodes.Ldc_I4_1);
					return;
				case 4:
					((ILGenerator)P_0).Emit(OpCodes.Ldc_I4_4);
					return;
				case 5:
					((ILGenerator)P_0).Emit(OpCodes.Ldc_I4_5);
					return;
				case 6:
					((ILGenerator)P_0).Emit(OpCodes.Ldc_I4_6);
					return;
				case 7:
					((ILGenerator)P_0).Emit(OpCodes.Ldc_I4_7);
					return;
				case -1:
					break;
					IL_006f:
					while (true)
					{
						switch (num)
						{
						case 12:
							break;
						case 5:
							goto end_IL_006f;
						default:
							return;
						case 6:
							goto IL_00ef;
						case 0:
							return;
						case 1:
							return;
						case 4:
							((ILGenerator)P_0).Emit(OpCodes.Ldc_I4_S, (sbyte)P_1);
							return;
						case 3:
							return;
						case 7:
							return;
						case 8:
							return;
						case 10:
							return;
						case 11:
							return;
						case 2:
							return;
						case 13:
							return;
						case 14:
							return;
						case 15:
							return;
						case 16:
							goto end_IL_00f2;
						case 9:
							return;
						}
						if (P_1 < 128)
						{
							num = 4;
							if (GetExecutionFrameObfuscationSentinel() != null)
							{
								return;
							}
							continue;
						}
						goto IL_003b;
						continue;
						end_IL_006f:
						break;
					}
					goto default;
					IL_003b:
					((ILGenerator)P_0).Emit(OpCodes.Ldc_I4, P_1);
					num = 14;
					if (!IsExecutionFrameObfuscationSentinelNull())
					{
						return;
					}
					goto IL_006f;
					end_IL_00f2:
					break;
				}
				break;
			}
			((ILGenerator)P_0).Emit(OpCodes.Ldc_I4_M1);
		}

		private static VmValue ConvertBoxedEnumValue(object P_0)
		{
			VmValue VmValue = default(VmValue);
			object obj = default(object);
			Type underlyingType = default(Type);
			object obj2 = default(object);
			while (((VmValue)P_0).ReadValue().IsObjectValue())
			{
				int num = 6;
				if (IsExecutionFrameObfuscationSentinelNull())
				{
					goto IL_006e;
				}
				goto IL_007d;
				IL_00c3:
				return VmValue as NumericValue;
				IL_007d:
				switch (num)
				{
				case 10:
					break;
				case 1:
					goto IL_0026;
				case 4:
					goto IL_0041;
				case 2:
					goto IL_004c;
				case 8:
					goto IL_005c;
				case 6:
					goto IL_006e;
				case 3:
					goto IL_0077;
				case 7:
					continue;
				default:
					goto IL_00c3;
				case 5:
				case 9:
					goto end_IL_00b0;
				}
				goto IL_0015;
				IL_006e:
				obj = ((VmValue)P_0).ToObject((Type)null);
				goto IL_0077;
				IL_0077:
				if (obj == null)
				{
					break;
				}
				goto IL_0015;
				IL_0015:
				if (!obj.GetType().IsEnum)
				{
					break;
				}
				goto IL_0026;
				IL_0026:
				underlyingType = Enum.GetUnderlyingType(obj.GetType());
				num = 3;
				if (IsExecutionFrameObfuscationSentinelNull())
				{
					goto IL_0041;
				}
				goto IL_007d;
				IL_0041:
				obj2 = Convert.ChangeType(obj, underlyingType);
				goto IL_004c;
				IL_004c:
				VmValue = AsEnumNumericValue(VmValue.CreateValue(underlyingType, obj2));
				goto IL_005c;
				IL_005c:
				if (VmValue == null)
				{
					break;
				}
				num = 0;
				if (IsExecutionFrameObfuscationSentinelNull())
				{
					goto IL_007d;
				}
				goto IL_00c3;
				continue;
				end_IL_00b0:
				break;
			}
			return (VmValue)P_0;
		}

		private static NumericValue AsEnumNumericValue(object P_0)
		{
			NumericValue c7EoQ7IJLjSNsQhooYs;
			while (true)
			{
				c7EoQ7IJLjSNsQhooYs = P_0 as NumericValue;
				int num = 0;
				if (GetExecutionFrameObfuscationSentinel() == null)
				{
					goto IL_0003;
				}
				goto IL_001b;
				IL_001b:
				switch (num)
				{
				case 2:
					break;
				case 1:
					goto IL_0007;
				case 3:
					continue;
				default:
					goto IL_0052;
				case 4:
				case 5:
					goto end_IL_003a;
				}
				goto IL_0003;
				IL_0003:
				if (c7EoQ7IJLjSNsQhooYs != null)
				{
					break;
				}
				goto IL_0007;
				IL_0007:
				if (!((VmValue)P_0).IsManagedReference())
				{
					break;
				}
				num = 0;
				if (GetExecutionFrameObfuscationSentinel() != null)
				{
					goto IL_001b;
				}
				goto IL_0052;
				IL_0052:
				c7EoQ7IJLjSNsQhooYs = ((VmValue)P_0).ReadValue() as NumericValue;
				break;
				continue;
				end_IL_003a:
				break;
			}
			return c7EoQ7IJLjSNsQhooYs;
		}

		private static IntPtr GetNativeAddress(object P_0)
		{
			object obj = default(object);
			ReferenceValue ReferenceValue = default(ReferenceValue);
			while (true)
			{
				int num;
				if (P_0 != null)
				{
					if (((VmValue)P_0).IsNativeIntegerValue())
					{
						break;
					}
					num = 0;
					if (IsExecutionFrameObfuscationSentinelNull())
					{
						goto IL_0036;
					}
				}
				else
				{
					num = 2;
					if (IsExecutionFrameObfuscationSentinelNull())
					{
						goto IL_00e0;
					}
				}
				goto IL_004a;
				IL_00e6:
				obj = ((VmValue)P_0).ToObject(typeof(IntPtr));
				goto IL_00f8;
				IL_00f8:
				if (obj != null)
				{
					goto IL_00fc;
				}
				goto IL_0114;
				IL_00fc:
				if (!(obj.GetType() == typeof(IntPtr)))
				{
					goto IL_0114;
				}
				goto IL_011a;
				IL_004a:
				switch (num)
				{
				case 3:
					break;
				default:
					goto IL_0036;
				case 6:
					continue;
				case 4:
					goto IL_009f;
				case 5:
					goto IL_00e0;
				case 1:
				case 7:
					goto IL_00e6;
				case 2:
					goto IL_00f8;
				case 8:
					goto IL_00fc;
				case 9:
					goto IL_0114;
				case 11:
					goto IL_011a;
				case 10:
					goto end_IL_008f;
				}
				goto IL_0020;
				IL_00e0:
				return IntPtr.Zero;
				IL_0036:
				if (((VmValue)P_0).IsManagedReference())
				{
					goto IL_0020;
				}
				num = 0;
				if (!IsExecutionFrameObfuscationSentinelNull())
				{
					goto IL_004a;
				}
				goto IL_00e6;
				IL_011a:
				return (IntPtr)obj;
				IL_0020:
				ReferenceValue = (ReferenceValue)P_0;
				num = 4;
				if (IsExecutionFrameObfuscationSentinelNull())
				{
					goto IL_004a;
				}
				goto IL_009f;
				IL_009f:
				IntPtr result;
				try
				{
					result = ReferenceValue.GetReferenceAddress();
					if (!IsExecutionFrameObfuscationSentinelNull())
					{
						switch (0)
						{
						}
					}
				}
				catch
				{
					if (IsExecutionFrameObfuscationSentinelNull())
					{
						switch (0)
						{
						}
					}
					goto IL_00e6;
				}
				return result;
				IL_0114:
				throw new VmOperandException();
				continue;
				end_IL_008f:
				break;
			}
			return ((NativeIntegerValue)P_0).ToIntPtr();
		}

		private static object CopyBoxedValue(object P_0)
		{
			lock (BoxedValueCopierCacheLock)
			{
				if (BoxedValueCopiers == null)
				{
					BoxedValueCopiers = new Dictionary<Type, BoxedValueCopier>();
				}
				if (P_0 != null)
				{
					try
					{
						Type type = P_0.GetType();
						if (!BoxedValueCopiers.TryGetValue(type, out var value))
						{
							DynamicMethod dynamicMethod = new DynamicMethod(string.Empty, typeof(object), new Type[1] { typeof(object) }, restrictedSkipVisibility: true);
							ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
							iLGenerator.Emit(OpCodes.Ldarg_0);
							iLGenerator.Emit(OpCodes.Unbox_Any, type);
							iLGenerator.Emit(OpCodes.Box, type);
							iLGenerator.Emit(OpCodes.Ret);
							BoxedValueCopier BoxedValueCopier = (BoxedValueCopier)dynamicMethod.CreateDelegate(typeof(BoxedValueCopier));
							BoxedValueCopiers.Add(type, BoxedValueCopier);
							return BoxedValueCopier(P_0);
						}
						return value(P_0);
					}
					catch
					{
						return null;
					}
				}
				return null;
			}
		}

		private static void InitializeMemoryBlock(IntPtr P_0, byte P_1, int P_2)
		{
			bool lockTaken = default(bool);
			while (true)
			{
				object pNQeYoCqAb = BoxedValueCopierCacheLock;
				int num = 0;
				if (GetExecutionFrameObfuscationSentinel() == null)
				{
					goto IL_0003;
				}
				goto IL_0015;
				IL_0015:
				switch (num)
				{
				case 1:
					continue;
				case 3:
					try
					{
						Monitor.Enter(pNQeYoCqAb, ref lockTaken);
						while (true)
						{
							if (InitializeBlock != null)
							{
								goto IL_0051;
							}
							int num2 = 0;
							if (IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_006d;
							}
							goto IL_0082;
							IL_0082:
							DynamicMethod dynamicMethod = new DynamicMethod(string.Empty, typeof(void), new Type[3]
							{
								typeof(IntPtr),
								typeof(byte),
								typeof(int)
							}, typeof(VirtualMachineRuntime), skipVisibility: true);
							ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
							iLGenerator.Emit(OpCodes.Ldarg_0);
							iLGenerator.Emit(OpCodes.Ldarg_1);
							iLGenerator.Emit(OpCodes.Ldarg_2);
							iLGenerator.Emit(OpCodes.Initblk);
							iLGenerator.Emit(OpCodes.Ret);
							InitializeBlock = (MemoryBlockInitializer)dynamicMethod.CreateDelegate(typeof(MemoryBlockInitializer));
							goto IL_0051;
							IL_0051:
							InitializeBlock(P_0, P_1, P_2);
							num2 = 1;
							if (GetExecutionFrameObfuscationSentinel() != null)
							{
								break;
							}
							goto IL_006d;
							IL_006d:
							switch (num2)
							{
							case 3:
								break;
							default:
								goto IL_0082;
							case 2:
								continue;
							case 1:
								return;
							}
							goto IL_0051;
						}
						return;
					}
					finally
					{
						int num3;
						if (lockTaken)
						{
							num3 = 1;
							if (IsExecutionFrameObfuscationSentinelNull())
							{
								goto IL_015d;
							}
							goto IL_0170;
						}
						goto end_IL_014c;
						IL_0170:
						switch (num3)
						{
						case 1:
							break;
						default:
							goto end_IL_014c;
						case 0:
							goto end_IL_014c;
						}
						goto IL_015d;
						IL_015d:
						Monitor.Exit(pNQeYoCqAb);
						num3 = 0;
						if (!IsExecutionFrameObfuscationSentinelNull())
						{
							goto IL_0170;
						}
						end_IL_014c:;
					}
				case 2:
					return;
				}
				goto IL_0003;
				IL_0003:
				do
				{
					lockTaken = false;
					num = 3;
				}
				while (GetExecutionFrameObfuscationSentinel() != null);
				goto IL_0015;
			}
		}

		private static void CopyMemoryBlock(IntPtr P_0, IntPtr P_1, uint P_2)
		{
			while (true)
			{
				if (CopyBlock == null)
				{
					goto IL_0006;
				}
				goto IL_00cb;
				IL_00cb:
				int num;
				do
				{
					CopyBlock(P_0, P_1, P_2);
					num = 1;
				}
				while (!IsExecutionFrameObfuscationSentinelNull());
				goto IL_00b3;
				IL_00b3:
				switch (num)
				{
				case 2:
					break;
				default:
					goto IL_00cb;
				case 3:
					continue;
				case 1:
					return;
				}
				goto IL_0006;
				IL_0006:
				DynamicMethod dynamicMethod = new DynamicMethod(string.Empty, typeof(void), new Type[3]
				{
					typeof(IntPtr),
					typeof(IntPtr),
					typeof(uint)
				}, typeof(VirtualMachineRuntime), skipVisibility: true);
				ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
				iLGenerator.Emit(OpCodes.Ldarg_0);
				iLGenerator.Emit(OpCodes.Ldarg_1);
				iLGenerator.Emit(OpCodes.Ldarg_2);
				iLGenerator.Emit(OpCodes.Cpblk);
				iLGenerator.Emit(OpCodes.Ret);
				CopyBlock = (MemoryBlockCopier)dynamicMethod.CreateDelegate(typeof(MemoryBlockCopier));
				num = 0;
				if (GetExecutionFrameObfuscationSentinel() != null)
				{
					goto IL_00b3;
				}
				goto IL_00cb;
			}
		}

		public ExecutionFrame()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			Arguments = new VmValue[0];
			Locals = new VmValue[0];
			Stack = new EvaluationStack();
			PendingHandlerOffset = -1;
		}

		static ExecutionFrame()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			ManagedSizeCacheLock = new object();
			ObjectValueAssociations = new Dictionary<object, VmValue>();
			ObjectValueAssociationsLock = new object();
			DirectMethodInvokers = new Dictionary<MethodBase, MethodInvoker>();
			VirtualMethodInvokers = new Dictionary<MethodBase, MethodInvoker>();
			MethodInvokerCacheLock = new object();
			DirectFieldArgumentInvokers = new Dictionary<FieldArgumentCallKey, MethodInvoker>();
			VirtualFieldArgumentInvokers = new Dictionary<FieldArgumentCallKey, MethodInvoker>();
			FieldArgumentInvokerCacheLock = new object();
			ConstructorInvokers = new Dictionary<FieldArgumentCallKey, MethodInvoker>();
			ConstructorInvokerCacheLock = new object();
			BoxedValueCopierCacheLock = new object();
		}

		internal static bool IsExecutionFrameObfuscationSentinelNull()
		{
			return ExecutionFrameObfuscationSentinel == null;
		}

		internal static ExecutionFrame GetExecutionFrameObfuscationSentinel()
		{
			return ExecutionFrameObfuscationSentinel;
		}
	}

	internal enum VmOpCode : byte
	{

	}

	internal enum VmValueKind : byte
	{

	}

	internal abstract class VmValue
	{
		internal VmValueKind Kind;

		internal static VmValue VmValueObfuscationSentinel;

		public VmValue()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal bool IsObjectValue()
		{
			return Kind == (VmValueKind)0;
		}

		internal bool IsInt32Value()
		{
			return Kind == (VmValueKind)1;
		}

		internal bool IsNativeIntegerValue()
		{
			while (Kind != (VmValueKind)3)
			{
				if (!IsVmValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return Kind == (VmValueKind)4;
			}
			return true;
		}

		internal bool IsInt64Value()
		{
			return Kind == (VmValueKind)2;
		}

		internal bool IsFloatingPointValue()
		{
			return Kind == (VmValueKind)5;
		}

		internal bool IsStringValue()
		{
			return Kind == (VmValueKind)6;
		}

		internal virtual bool IsManagedReference()
		{
			return false;
		}

		internal virtual bool IsAddressValue()
		{
			return false;
		}

		internal abstract void Assign(VmValue P_0);

		internal virtual bool IsNumericValue()
		{
			return false;
		}

		internal VmValue(VmValueKind P_0)
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			Kind = P_0;
		}

		internal abstract object ToObject(Type P_0);

		internal abstract bool IsEqualTo(VmValue P_0);

		internal abstract bool IsNotEqualTo(VmValue P_0);

		internal abstract bool IsTruthy();

		internal abstract VmValue ReadValue();

		internal virtual bool IsIntegerValue()
		{
			return false;
		}

		internal abstract void CopyFrom(VmValue P_0);

		internal static VmRuntimeTypeCode GetRuntimeTypeCode(Type P_0)
		{
			while (true)
			{
				Type type = P_0;
				while (true)
				{
					if (type != null)
					{
						while (true)
						{
							IL_02ba:
							if (!type.IsByRef)
							{
								goto IL_02a3;
							}
							goto IL_02af;
							IL_02af:
							type = type.GetElementType();
							goto IL_02a3;
							IL_02a3:
							while (true)
							{
								int num;
								if (type != null)
								{
									num = 0;
									if (GetVmValueObfuscationSentinel() != null)
									{
										goto IL_01dc;
									}
									goto IL_0261;
								}
								goto IL_0279;
								IL_0270:
								type = Nullable.GetUnderlyingType(type);
								goto IL_0279;
								IL_0279:
								if (!(type == typeof(string)))
								{
									if (!(type == typeof(byte)))
									{
										if (type == typeof(sbyte))
										{
											num = 5;
											if (GetVmValueObfuscationSentinel() == null)
											{
												goto IL_01dc;
											}
											goto IL_02e4;
										}
										if (!(type == typeof(short)))
										{
											goto IL_0059;
										}
										goto IL_030d;
									}
									goto IL_030f;
								}
								goto IL_02eb;
								IL_0149:
								if (type == typeof(IntPtr))
								{
									num = 19;
									if (GetVmValueObfuscationSentinel() == null)
									{
										goto IL_01dc;
									}
								}
								else
								{
									if (type == typeof(UIntPtr))
									{
										goto IL_02f3;
									}
									if (!(type == typeof(char)))
									{
										if (!(type == typeof(object)))
										{
											goto IL_0129;
										}
										goto IL_02ee;
									}
								}
								goto IL_02f0;
								IL_0304:
								return (VmRuntimeTypeCode)9;
								IL_0307:
								return (VmRuntimeTypeCode)8;
								IL_0309:
								return (VmRuntimeTypeCode)7;
								IL_0059:
								if (!(type == typeof(ushort)))
								{
									if (type == typeof(int))
									{
										num = 2;
										if (IsVmValueObfuscationSentinelNull())
										{
											goto IL_02e2;
										}
									}
									else
									{
										if (!(type == typeof(uint)))
										{
											if (!(type == typeof(long)))
											{
												if (!(type == typeof(ulong)))
												{
													goto IL_00e9;
												}
												goto IL_0307;
											}
											goto IL_0309;
										}
										num = 0;
										if (GetVmValueObfuscationSentinel() == null)
										{
											goto IL_02e4;
										}
									}
									goto IL_01dc;
								}
								goto IL_030b;
								IL_030b:
								return (VmRuntimeTypeCode)4;
								IL_02ee:
								return (VmRuntimeTypeCode)0;
								IL_02f0:
								return (VmRuntimeTypeCode)15;
								IL_02f3:
								return (VmRuntimeTypeCode)13;
								IL_0129:
								if (type.IsEnum)
								{
									num = 7;
									if (!IsVmValueObfuscationSentinelNull())
									{
										goto IL_01dc;
									}
									goto IL_02fa;
								}
								return (VmRuntimeTypeCode)17;
								IL_030d:
								return (VmRuntimeTypeCode)3;
								IL_00e9:
								if (!(type == typeof(float)))
								{
									goto IL_00ff;
								}
								goto IL_0304;
								IL_00ff:
								if (type == typeof(double))
								{
									num = 25;
									if (GetVmValueObfuscationSentinel() == null)
									{
										goto IL_0301;
									}
								}
								else
								{
									if (type == typeof(bool))
									{
										goto IL_02e6;
									}
									num = 1;
									if (GetVmValueObfuscationSentinel() != null)
									{
										goto IL_0149;
									}
								}
								goto IL_01dc;
								IL_02fa:
								return (VmRuntimeTypeCode)16;
								IL_030f:
								return (VmRuntimeTypeCode)2;
								IL_02e6:
								return (VmRuntimeTypeCode)11;
								IL_01dc:
								switch (num)
								{
								case 23:
									break;
								case 9:
									goto IL_00e9;
								case 14:
									goto IL_00ff;
								case 24:
									goto IL_0129;
								case 1:
									goto IL_0149;
								default:
									goto IL_0261;
								case 28:
									goto IL_0270;
								case 6:
									goto IL_0279;
								case 16:
								case 21:
									continue;
								case 31:
									goto end_IL_02a3;
								case 22:
									goto IL_02ba;
								case 10:
									goto end_IL_02ba;
								case 11:
									goto end_IL_02c5;
								case 2:
									goto IL_02e2;
								case 3:
									goto IL_02e4;
								case 4:
									goto IL_02e6;
								case 5:
									return (VmRuntimeTypeCode)1;
								case 13:
									goto IL_02eb;
								case 15:
									goto IL_02ee;
								case 17:
									goto IL_02f0;
								case 18:
									goto IL_02f3;
								case 19:
									return (VmRuntimeTypeCode)12;
								case 7:
									goto IL_02fa;
								case 26:
									goto IL_0301;
								case 27:
									goto IL_0304;
								case 8:
									goto IL_0307;
								case 30:
									goto IL_0309;
								case 20:
									goto IL_030b;
								case 12:
									goto IL_030d;
								case 25:
									goto IL_030f;
								case 29:
									goto IL_0311;
								}
								goto IL_0059;
								IL_0301:
								return (VmRuntimeTypeCode)10;
								IL_02eb:
								return (VmRuntimeTypeCode)14;
								IL_02e4:
								return (VmRuntimeTypeCode)6;
								IL_02e2:
								return (VmRuntimeTypeCode)5;
								IL_0261:
								if (Nullable.GetUnderlyingType(type) != null)
								{
									goto IL_0270;
								}
								goto IL_0279;
								continue;
								end_IL_02a3:
								break;
							}
							goto IL_02af;
							continue;
							end_IL_02ba:
							break;
						}
						continue;
					}
					goto IL_0311;
					IL_0311:
					return (VmRuntimeTypeCode)18;
					continue;
					end_IL_02c5:
					break;
				}
			}
		}

		internal static VmValue CreateValue(Type P_0, object P_1)
		{
			VmValue VmValue = default(VmValue);
			while (true)
			{
				VmRuntimeTypeCode VmRuntimeTypeCode = GetRuntimeTypeCode(P_0);
				while (true)
				{
					VmRuntimeTypeCode tOjM3cIQ1SG40P4Qhql2 = (VmRuntimeTypeCode)18;
					while (true)
					{
						IL_0ce4:
						if (P_1 != null)
						{
							goto IL_09be;
						}
						goto IL_0ccb;
						IL_0ccb:
						VmValue = null;
						int num = 89;
						if (GetVmValueObfuscationSentinel() != null)
						{
							goto IL_0006;
						}
						goto IL_09dd;
						IL_09dd:
						do
						{
							IL_09dd_2:
							switch (num)
							{
							case 180:
								goto IL_0025;
							case 3:
								goto IL_0044;
							case 79:
								goto IL_005e;
							case 178:
								if ((bool)P_1)
								{
									goto case 86;
								}
								goto case 54;
							case 86:
								VmValue = new Int32Value(1, (VmPrimitiveType)3);
								break;
							case 54:
								VmValue = new Int32Value(0, (VmPrimitiveType)3);
								break;
							case 176:
								goto IL_008a;
							case 174:
								goto IL_00a8;
							case 23:
								goto IL_00c6;
							case 152:
								goto IL_00e0;
							case 171:
								goto IL_00f3;
							case 170:
								if (tOjM3cIQ1SG40P4Qhql2 != (VmRuntimeTypeCode)15)
								{
									num = 77;
									if (GetVmValueObfuscationSentinel() == null)
									{
										goto IL_09dd_2;
									}
									goto IL_0126;
								}
								goto IL_0139;
							case 46:
								goto IL_0126;
							case 84:
								goto IL_0139;
							case 169:
								goto IL_014d;
							case 166:
								VmValue = new Int32Value((byte)P_1, (VmPrimitiveType)2);
								break;
							case 165:
								goto IL_0174;
							case 164:
								VmValue = new Int32Value(1, (VmPrimitiveType)1);
								num = 58;
								if (IsVmValueObfuscationSentinelNull())
								{
									goto IL_09dd_2;
								}
								goto case 32;
							case 32:
								if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)15)
								{
									goto case 160;
								}
								goto case 126;
							case 160:
								VmValue = new Int32Value((char)P_1, (VmPrimitiveType)5);
								num = 28;
								if (!IsVmValueObfuscationSentinelNull())
								{
									goto IL_09dd_2;
								}
								break;
							case 163:
								VmValue = new Int64Value(1L, (VmPrimitiveType)7);
								break;
							case 159:
								goto IL_01df;
							case 72:
								goto IL_01ea;
							case 21:
							case 130:
								goto IL_01f2;
							case 156:
								goto IL_0200;
							case 125:
								goto IL_0218;
							case 111:
								goto IL_023e;
							case 154:
								goto IL_0254;
							case 100:
								goto IL_026e;
							case 153:
								goto IL_0292;
							case 150:
								goto IL_02a0;
							case 71:
								goto IL_02ba;
							case 146:
								goto IL_02dd;
							case 140:
								goto IL_02f2;
							case 78:
								goto IL_02fd;
							case 104:
								goto IL_031a;
							case 139:
								goto IL_0330;
							case 137:
								goto IL_0354;
							case 132:
								goto IL_0369;
							case 4:
								goto IL_0372;
							case 8:
								goto IL_037d;
							case 131:
								goto IL_0391;
							case 64:
								goto IL_039a;
							case 128:
								goto IL_03bb;
							case 127:
								goto IL_03d8;
							case 124:
								if (tOjM3cIQ1SG40P4Qhql2 != (VmRuntimeTypeCode)1)
								{
									num = 120;
									if (GetVmValueObfuscationSentinel() == null)
									{
										goto IL_09dd_2;
									}
									goto IL_040b;
								}
								goto case 66;
							case 94:
								goto IL_040b;
							case 66:
								VmValue = new Int32Value((byte)(sbyte)P_1, (VmPrimitiveType)2);
								break;
							case 121:
								goto IL_0443;
							case 120:
								if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)2)
								{
									goto case 166;
								}
								goto IL_0d1a;
							case 119:
								goto IL_0475;
							case 117:
								goto IL_0483;
							case 116:
								goto IL_0490;
							case 114:
								goto IL_04ad;
							case 108:
								goto IL_04c2;
							case 39:
								goto IL_04dd;
							case 29:
								goto IL_04e8;
							case 106:
								goto IL_04f6;
							case 51:
								goto IL_04fc;
							case 67:
								goto IL_0507;
							case 22:
								goto IL_052b;
							case 81:
								goto IL_0533;
							case 101:
								goto IL_0541;
							case 105:
								if (tOjM3cIQ1SG40P4Qhql2 != (VmRuntimeTypeCode)11)
								{
									num = 170;
									if (GetVmValueObfuscationSentinel() == null)
									{
										goto IL_09dd_2;
									}
									break;
								}
								goto case 178;
							case 93:
								goto IL_057d;
							case 91:
								VmValue = new Int32Value((sbyte)(byte)P_1, (VmPrimitiveType)1);
								num = 36;
								if (GetVmValueObfuscationSentinel() != null)
								{
									goto IL_09dd_2;
								}
								break;
							case 89:
								goto IL_05d8;
							case 70:
								goto IL_063b;
							case 74:
								goto IL_0660;
							case 75:
								goto IL_067a;
							case 65:
								goto IL_069d;
							case 25:
								goto IL_06b7;
							case 73:
								goto IL_06f1;
							case 34:
								goto IL_071e;
							case 68:
								goto IL_0743;
							case 88:
								goto IL_0751;
							case 10:
								goto IL_076f;
							case 44:
								goto IL_078a;
							case 76:
								goto IL_07c3;
							case 47:
								goto IL_07de;
							case 1:
								goto IL_0846;
							case 63:
								goto IL_086b;
							case 11:
								goto IL_0890;
							case 2:
								goto IL_08b7;
							case 82:
								goto IL_08f6;
							case 7:
								goto IL_093e;
							case 19:
								goto IL_095d;
							case 9:
								goto IL_0971;
							case 16:
								goto IL_0992;
							case 83:
								if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)2)
								{
									goto case 91;
								}
								goto IL_0d48;
							case 48:
								VmValue = new NativeIntegerValue((IntPtr)P_1);
								break;
							case 6:
								goto IL_09be;
							case 179:
								goto IL_0ccb;
							case 43:
								goto IL_0ce4;
							case 122:
								goto end_IL_0ce4;
							case 123:
								goto end_IL_0cec;
							case 49:
								goto IL_0cfc;
							case 59:
								goto IL_0d02;
							case 113:
								goto IL_0d08;
							case 129:
								goto IL_0d0e;
							case 50:
								goto IL_0d14;
							case 15:
							case 55:
							case 134:
								goto IL_0d1a;
							case 77:
							case 143:
								throw new InvalidCastException();
							case 144:
								goto IL_0d26;
							case 110:
							case 147:
								goto IL_0d2c;
							case 148:
								goto IL_0d32;
							case 38:
							case 149:
								throw new InvalidCastException();
							case 41:
							case 42:
								goto IL_0d42;
							case 33:
							case 155:
							case 172:
								goto IL_0d48;
							case 126:
							case 173:
								throw new InvalidCastException();
							case 107:
							case 181:
								goto IL_0d55;
							case 5:
								goto IL_0d5c;
							case 95:
								goto IL_0d6b;
							}
							break;
							IL_05d8:
							while (true)
							{
								switch (VmRuntimeTypeCode)
								{
								case (VmRuntimeTypeCode)3:
									goto IL_05c0;
								case (VmRuntimeTypeCode)0:
									goto IL_0630;
								case (VmRuntimeTypeCode)1:
									goto IL_0648;
								case (VmRuntimeTypeCode)2:
									goto IL_069d;
								case (VmRuntimeTypeCode)4:
									goto IL_06ca;
								case (VmRuntimeTypeCode)5:
									goto IL_06e7;
								case (VmRuntimeTypeCode)6:
									goto IL_0704;
								case (VmRuntimeTypeCode)7:
									goto IL_0731;
								case (VmRuntimeTypeCode)8:
									goto IL_073e;
								case (VmRuntimeTypeCode)9:
									goto IL_07aa;
								case (VmRuntimeTypeCode)10:
									goto IL_07d5;
								case (VmRuntimeTypeCode)11:
									goto IL_07f0;
								case (VmRuntimeTypeCode)12:
									goto IL_0890;
								case (VmRuntimeTypeCode)13:
									goto IL_08ae;
								case (VmRuntimeTypeCode)14:
									goto IL_08d9;
								case (VmRuntimeTypeCode)15:
									goto IL_0918;
								case (VmRuntimeTypeCode)16:
								case (VmRuntimeTypeCode)17:
									goto IL_0992;
								case (VmRuntimeTypeCode)18:
									goto IL_0d26;
								}
								break;
								IL_05c0:
								if (tOjM3cIQ1SG40P4Qhql2 != (VmRuntimeTypeCode)3)
								{
									num = 105;
									if (!IsVmValueObfuscationSentinelNull())
									{
										continue;
									}
									goto IL_09dd_2;
								}
								goto IL_06b7;
							}
							break;
							IL_0d26:
							throw new InvalidCastException();
							IL_0992:
							VmValue = CreateEnumOrObjectValue(P_1);
							break;
							IL_0918:
							switch (tOjM3cIQ1SG40P4Qhql2)
							{
							case (VmRuntimeTypeCode)6:
								break;
							case (VmRuntimeTypeCode)4:
								goto IL_0330;
							default:
								goto IL_0391;
							case (VmRuntimeTypeCode)3:
								goto IL_040b;
							case (VmRuntimeTypeCode)2:
								goto IL_093e;
							case (VmRuntimeTypeCode)1:
								goto IL_095d;
							case (VmRuntimeTypeCode)5:
								goto IL_0971;
							}
							goto IL_014d;
							IL_0971:
							VmValue = new Int32Value((int)P_1, (VmPrimitiveType)15);
							num = 0;
							if (!IsVmValueObfuscationSentinelNull())
							{
								break;
							}
							goto IL_09dd_2;
							IL_093e:
							VmValue = new Int32Value((byte)P_1, (VmPrimitiveType)15);
							num = 35;
							if (IsVmValueObfuscationSentinelNull())
							{
								goto IL_09dd_2;
							}
							goto IL_095d;
							IL_095d:
							VmValue = new Int32Value((sbyte)P_1, (VmPrimitiveType)15);
							break;
							IL_08d9:
							VmValue = new StringValue(P_1 as string);
							num = 85;
							if (IsVmValueObfuscationSentinelNull())
							{
								goto IL_09dd_2;
							}
							goto IL_08f6;
							IL_08ae:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)13)
							{
								goto IL_08b7;
							}
							goto IL_0d02;
							IL_0d02:
							throw new InvalidCastException();
							IL_0890:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)12)
							{
								num = 48;
								if (GetVmValueObfuscationSentinel() != null)
								{
									break;
								}
								goto IL_09dd_2;
							}
							goto IL_0cfc;
							IL_0126:
							VmValue = new Int64Value((long)P_1, (VmPrimitiveType)7);
							break;
							IL_0cfc:
							throw new InvalidCastException();
							IL_07f0:
							switch (tOjM3cIQ1SG40P4Qhql2)
							{
							case (VmRuntimeTypeCode)7:
								break;
							case (VmRuntimeTypeCode)1:
								goto IL_02dd;
							case (VmRuntimeTypeCode)3:
								goto IL_0354;
							case (VmRuntimeTypeCode)18:
								goto IL_03bb;
							case (VmRuntimeTypeCode)6:
								goto IL_0443;
							case (VmRuntimeTypeCode)8:
								goto IL_0490;
							case (VmRuntimeTypeCode)2:
								goto IL_04ad;
							default:
								goto IL_057d;
							case (VmRuntimeTypeCode)4:
								goto IL_0846;
							case (VmRuntimeTypeCode)5:
								goto IL_086b;
							case (VmRuntimeTypeCode)11:
								goto IL_08f6;
							case (VmRuntimeTypeCode)9:
							case (VmRuntimeTypeCode)10:
							case (VmRuntimeTypeCode)12:
							case (VmRuntimeTypeCode)13:
							case (VmRuntimeTypeCode)14:
							case (VmRuntimeTypeCode)15:
							case (VmRuntimeTypeCode)16:
								goto IL_0d32;
							}
							goto IL_00f3;
							IL_0d32:
							throw new InvalidCastException();
							IL_08f6:
							VmValue = new Int32Value((bool)P_1);
							num = 23;
							if (GetVmValueObfuscationSentinel() == null)
							{
								break;
							}
							goto IL_09dd_2;
							IL_086b:
							VmValue = new Int32Value((int)P_1 != 0);
							num = 32;
							if (GetVmValueObfuscationSentinel() == null)
							{
								break;
							}
							goto IL_09dd_2;
							IL_0846:
							VmValue = new Int32Value((ushort)P_1 != 0);
							num = 60;
							if (GetVmValueObfuscationSentinel() == null)
							{
								break;
							}
							goto IL_09dd_2;
							IL_07d5:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)10)
							{
								goto IL_07de;
							}
							goto IL_0d0e;
							IL_07de:
							VmValue = new FloatingPointValue((double)P_1);
							break;
							IL_0d0e:
							throw new InvalidCastException();
							IL_07aa:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)9)
							{
								num = 35;
								if (!IsVmValueObfuscationSentinelNull())
								{
									goto IL_09dd_2;
								}
								goto IL_07c3;
							}
							goto IL_0d08;
							IL_01ea:
							if ((bool)P_1)
							{
								goto IL_0174;
							}
							goto IL_01f2;
							IL_07c3:
							VmValue = new FloatingPointValue((float)P_1);
							break;
							IL_0d08:
							throw new InvalidCastException();
							IL_073e:
							if (tOjM3cIQ1SG40P4Qhql2 != (VmRuntimeTypeCode)8)
							{
								goto IL_0743;
							}
							goto IL_0751;
							IL_0743:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)11)
							{
								goto IL_0200;
							}
							goto IL_0475;
							IL_0751:
							VmValue = new Int64Value((ulong)P_1, (VmPrimitiveType)8);
							num = 31;
							if (GetVmValueObfuscationSentinel() == null)
							{
								goto IL_09dd_2;
							}
							goto IL_076f;
							IL_076f:
							if (tOjM3cIQ1SG40P4Qhql2 != (VmRuntimeTypeCode)11)
							{
								num = 12;
								if (GetVmValueObfuscationSentinel() != null)
								{
									goto IL_09dd_2;
								}
								goto IL_02a0;
							}
							goto IL_078a;
							IL_00f3:
							VmValue = new Int32Value((ulong)(long)P_1 > 0uL);
							break;
							IL_078a:
							if (!(bool)P_1)
							{
								goto IL_008a;
							}
							num = 49;
							if (GetVmValueObfuscationSentinel() != null)
							{
								goto IL_09dd_2;
							}
							goto IL_0292;
							IL_00a8:
							VmValue = new Int32Value((ushort)P_1, (VmPrimitiveType)4);
							num = 36;
							if (GetVmValueObfuscationSentinel() == null)
							{
								goto IL_09dd_2;
							}
							goto IL_00c6;
							IL_0731:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)7)
							{
								goto IL_0126;
							}
							goto IL_02f2;
							IL_0704:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)6)
							{
								goto IL_071e;
							}
							num = 10;
							if (IsVmValueObfuscationSentinelNull())
							{
								goto IL_09dd_2;
							}
							goto IL_08b7;
							IL_01f2:
							VmValue = new Int32Value(0, (VmPrimitiveType)4);
							break;
							IL_08b7:
							VmValue = new NativeIntegerValue((UIntPtr)P_1);
							num = 38;
							if (IsVmValueObfuscationSentinelNull())
							{
								break;
							}
							goto IL_09dd_2;
							IL_071e:
							VmValue = new Int32Value((uint)P_1, (VmPrimitiveType)6);
							break;
							IL_06e7:
							if (tOjM3cIQ1SG40P4Qhql2 != (VmRuntimeTypeCode)5)
							{
								goto IL_04c2;
							}
							goto IL_06f1;
							IL_06ca:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)4)
							{
								goto IL_00a8;
							}
							num = 142;
							if (!IsVmValueObfuscationSentinelNull())
							{
								goto IL_09dd_2;
							}
							goto IL_01df;
							IL_0174:
							VmValue = new Int32Value(1, (VmPrimitiveType)4);
							break;
							IL_069d:
							if (tOjM3cIQ1SG40P4Qhql2 > (VmRuntimeTypeCode)2)
							{
								goto IL_04f6;
							}
							num = 124;
							if (IsVmValueObfuscationSentinelNull())
							{
								goto IL_09dd_2;
							}
							goto IL_06f1;
							IL_00c6:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)15)
							{
								goto IL_00e0;
							}
							num = 68;
							if (!IsVmValueObfuscationSentinelNull())
							{
								goto IL_09dd_2;
							}
							goto IL_0d55;
							IL_06f1:
							VmValue = new Int32Value((int)P_1, (VmPrimitiveType)5);
							break;
							IL_0648:
							if (tOjM3cIQ1SG40P4Qhql2 > (VmRuntimeTypeCode)2)
							{
								goto IL_0369;
							}
							num = 50;
							if (GetVmValueObfuscationSentinel() != null)
							{
								goto IL_09dd_2;
							}
							goto IL_0660;
							IL_014d:
							VmValue = new Int32Value((int)(uint)P_1, (VmPrimitiveType)15);
							break;
							IL_0660:
							if (tOjM3cIQ1SG40P4Qhql2 != (VmRuntimeTypeCode)1)
							{
								num = 83;
								if (GetVmValueObfuscationSentinel() != null)
								{
									break;
								}
								goto IL_09dd_2;
							}
							goto IL_067a;
							IL_005e:
							VmValue = new Int32Value(0, (VmPrimitiveType)1);
							break;
							IL_067a:
							VmValue = new Int32Value((sbyte)P_1, (VmPrimitiveType)1);
							num = 27;
							if (GetVmValueObfuscationSentinel() == null)
							{
								break;
							}
							goto IL_09dd_2;
							IL_0630:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)15)
							{
								goto IL_0483;
							}
							goto IL_063b;
							IL_063b:
							VmValue = CreateEnumOrObjectValue(P_1);
							break;
							IL_06b7:
							VmValue = new Int32Value((short)P_1, (VmPrimitiveType)3);
							break;
							IL_057d:
							VmValue = new Int32Value(P_1 != null);
							num = 99;
							if (IsVmValueObfuscationSentinelNull())
							{
								goto IL_09dd_2;
							}
							goto IL_0ce4;
							IL_04f6:
							if (tOjM3cIQ1SG40P4Qhql2 != (VmRuntimeTypeCode)11)
							{
								goto IL_04fc;
							}
							goto IL_052b;
							IL_04fc:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)15)
							{
								goto IL_0507;
							}
							goto IL_0d1a;
							IL_0507:
							VmValue = new Int32Value((byte)(char)P_1, (VmPrimitiveType)2);
							num = 28;
							if (IsVmValueObfuscationSentinelNull())
							{
								break;
							}
							goto IL_09dd_2;
							IL_052b:
							if ((bool)P_1)
							{
								goto IL_0533;
							}
							goto IL_0541;
							IL_0533:
							VmValue = new Int32Value(1, (VmPrimitiveType)2);
							break;
							IL_0541:
							VmValue = new Int32Value(0, (VmPrimitiveType)2);
							num = 56;
							if (GetVmValueObfuscationSentinel() == null)
							{
								goto IL_09dd_2;
							}
							goto IL_0372;
							IL_04c2:
							if (tOjM3cIQ1SG40P4Qhql2 != (VmRuntimeTypeCode)11)
							{
								num = 32;
								if (!IsVmValueObfuscationSentinelNull())
								{
									break;
								}
								goto IL_09dd_2;
							}
							goto IL_04dd;
							IL_0d55:
							throw new InvalidCastException();
							IL_04dd:
							if ((bool)P_1)
							{
								goto IL_03d8;
							}
							goto IL_04e8;
							IL_04e8:
							VmValue = new Int32Value(0, (VmPrimitiveType)5);
							break;
							IL_04ad:
							VmValue = new Int32Value((byte)P_1 != 0);
							break;
							IL_0490:
							VmValue = new Int32Value((ulong)P_1 > 0L);
							break;
							IL_0483:
							VmValue = new ObjectValue(P_1);
							break;
							IL_0475:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)15)
							{
								goto IL_0025;
							}
							goto IL_0d2c;
							IL_0d2c:
							throw new InvalidCastException();
							IL_0d1a:
							throw new InvalidCastException();
							IL_0443:
							VmValue = new Int32Value((uint)P_1 != 0);
							num = 4;
							if (GetVmValueObfuscationSentinel() == null)
							{
								break;
							}
							goto IL_09dd_2;
							IL_040b:
							VmValue = new Int32Value((short)P_1, (VmPrimitiveType)15);
							num = 9;
							if (GetVmValueObfuscationSentinel() == null)
							{
								break;
							}
							goto IL_09dd_2;
							IL_03d8:
							VmValue = new Int32Value(1, (VmPrimitiveType)5);
							num = 184;
							if (IsVmValueObfuscationSentinelNull())
							{
								goto IL_09dd_2;
							}
							goto IL_02fd;
							IL_03bb:
							VmValue = new Int32Value(false);
							num = 24;
							if (IsVmValueObfuscationSentinelNull())
							{
								break;
							}
							goto IL_09dd_2;
							IL_0391:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)15)
							{
								goto IL_039a;
							}
							goto IL_0d14;
							IL_039a:
							VmValue = new Int32Value((char)P_1, (VmPrimitiveType)15);
							num = 20;
							if (IsVmValueObfuscationSentinelNull())
							{
								goto IL_09dd_2;
							}
							goto IL_037d;
							IL_0d14:
							throw new InvalidCastException();
							IL_0369:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)11)
							{
								goto IL_0044;
							}
							goto IL_0372;
							IL_0372:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)15)
							{
								goto IL_037d;
							}
							goto IL_0d48;
							IL_0d48:
							throw new InvalidCastException();
							IL_037d:
							VmValue = new Int32Value((sbyte)(char)P_1, (VmPrimitiveType)1);
							break;
							IL_0354:
							VmValue = new Int32Value((short)P_1 != 0);
							break;
							IL_0330:
							VmValue = new Int32Value((ushort)P_1, (VmPrimitiveType)15);
							num = 30;
							if (GetVmValueObfuscationSentinel() == null)
							{
								break;
							}
							goto IL_09dd_2;
							IL_02f2:
							if (tOjM3cIQ1SG40P4Qhql2 != (VmRuntimeTypeCode)11)
							{
								goto IL_0254;
							}
							goto IL_02fd;
							IL_02fd:
							if ((bool)P_1)
							{
								num = 163;
								if (!IsVmValueObfuscationSentinelNull())
								{
									break;
								}
								goto IL_09dd_2;
							}
							goto IL_031a;
							IL_00e0:
							VmValue = new Int32Value((char)P_1, (VmPrimitiveType)4);
							break;
							IL_031a:
							VmValue = new Int64Value(0L, (VmPrimitiveType)7);
							break;
							IL_02dd:
							VmValue = new Int32Value((sbyte)P_1 != 0);
							break;
							IL_02a0:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)15)
							{
								goto IL_02ba;
							}
							num = 149;
							if (IsVmValueObfuscationSentinelNull())
							{
								goto IL_09dd_2;
							}
							goto IL_0d6b;
							IL_008a:
							VmValue = new Int32Value(0u, (VmPrimitiveType)6);
							num = 26;
							if (GetVmValueObfuscationSentinel() == null)
							{
								break;
							}
							goto IL_09dd_2;
							IL_02ba:
							VmValue = new Int32Value((uint)(char)P_1, (VmPrimitiveType)6);
							num = 26;
							if (!IsVmValueObfuscationSentinelNull())
							{
								break;
							}
							goto IL_09dd_2;
							IL_0292:
							VmValue = new Int32Value(1u, (VmPrimitiveType)6);
							break;
							IL_0254:
							if (tOjM3cIQ1SG40P4Qhql2 == (VmRuntimeTypeCode)15)
							{
								goto IL_026e;
							}
							num = 7;
							if (GetVmValueObfuscationSentinel() != null)
							{
								goto IL_09dd_2;
							}
							goto IL_0d42;
							IL_0025:
							VmValue = new Int64Value((ulong)(char)P_1, (VmPrimitiveType)8);
							num = 14;
							if (GetVmValueObfuscationSentinel() == null)
							{
								goto IL_09dd_2;
							}
							goto IL_0044;
							IL_0d42:
							throw new InvalidCastException();
							IL_026e:
							VmValue = new Int64Value((char)P_1, (VmPrimitiveType)7);
							num = 97;
							if (GetVmValueObfuscationSentinel() != null)
							{
								break;
							}
							goto IL_09dd_2;
							IL_0200:
							if ((bool)P_1)
							{
								num = 23;
								if (!IsVmValueObfuscationSentinelNull())
								{
									goto IL_09dd_2;
								}
								goto IL_0218;
							}
							goto IL_023e;
							IL_0044:
							if ((bool)P_1)
							{
								num = 164;
								if (!IsVmValueObfuscationSentinelNull())
								{
									break;
								}
								goto IL_09dd_2;
							}
							goto IL_005e;
							IL_0218:
							VmValue = new Int64Value(1uL, (VmPrimitiveType)8);
							num = 104;
							continue;
							IL_023e:
							VmValue = new Int64Value(0uL, (VmPrimitiveType)8);
							break;
							IL_01df:
							if (tOjM3cIQ1SG40P4Qhql2 != (VmRuntimeTypeCode)11)
							{
								goto IL_00c6;
							}
							goto IL_01ea;
						}
						while (!IsVmValueObfuscationSentinelNull());
						goto IL_0006;
						IL_09be:
						tOjM3cIQ1SG40P4Qhql2 = GetRuntimeTypeCode(P_1.GetType());
						num = 179;
						if (!IsVmValueObfuscationSentinelNull())
						{
							goto IL_0139;
						}
						goto IL_09dd;
						IL_0139:
						VmValue = new Int32Value((short)(char)P_1, (VmPrimitiveType)3);
						goto IL_0006;
						IL_0006:
						if (P_0.IsByRef)
						{
							num = 4;
							if (!IsVmValueObfuscationSentinelNull())
							{
								goto IL_09dd;
							}
							goto IL_0d5c;
						}
						goto IL_0d6b;
						IL_0d6b:
						return VmValue;
						IL_0d5c:
						VmValue = new ValueCellReference(VmValue, P_0.GetElementType());
						goto IL_0d6b;
						continue;
						end_IL_0ce4:
						break;
					}
					continue;
					end_IL_0cec:
					break;
				}
			}
		}

		private static VmValue CreateEnumOrObjectValue(object P_0)
		{
			VmValue VmValue = default(VmValue);
			Type underlyingType = default(Type);
			object obj = default(object);
			while (true)
			{
				int num;
				if (P_0 == null)
				{
					num = 1;
					if (!IsVmValueObfuscationSentinelNull())
					{
						break;
					}
					goto IL_0022;
				}
				goto IL_0075;
				IL_009f:
				return VmValue as NumericValue;
				IL_0075:
				if (!P_0.GetType().IsEnum)
				{
					break;
				}
				goto IL_0066;
				IL_0066:
				underlyingType = Enum.GetUnderlyingType(P_0.GetType());
				goto IL_004d;
				IL_004d:
				obj = Convert.ChangeType(P_0, underlyingType);
				num = 0;
				if (IsVmValueObfuscationSentinelNull())
				{
					goto IL_0006;
				}
				goto IL_0022;
				IL_0022:
				switch (num)
				{
				case 4:
					break;
				case 8:
					goto IL_004d;
				case 5:
					goto IL_0066;
				case 6:
					goto IL_0075;
				case 2:
					continue;
				default:
					goto IL_009b;
				case 3:
					goto IL_009f;
				case 1:
				case 7:
					goto end_IL_0092;
				}
				goto IL_0006;
				IL_0006:
				VmValue = AsNumericFactoryValue(CreateValue(underlyingType, obj));
				num = 0;
				if (GetVmValueObfuscationSentinel() != null)
				{
					goto IL_0022;
				}
				goto IL_009b;
				IL_009b:
				if (VmValue == null)
				{
					break;
				}
				goto IL_009f;
				continue;
				end_IL_0092:
				break;
			}
			return new ObjectValue(P_0);
		}

		private static NumericValue AsNumericFactoryValue(object P_0)
		{
			NumericValue c7EoQ7IJLjSNsQhooYs;
			while (true)
			{
				c7EoQ7IJLjSNsQhooYs = P_0 as NumericValue;
				int num = 2;
				if (!IsVmValueObfuscationSentinelNull())
				{
					goto IL_0026;
				}
				goto IL_0036;
				IL_0036:
				switch (num)
				{
				case 5:
					break;
				case 4:
					goto IL_000b;
				case 2:
					goto IL_0026;
				case 3:
					continue;
				default:
					goto end_IL_0055;
				}
				goto IL_0003;
				IL_0026:
				if (c7EoQ7IJLjSNsQhooYs == null)
				{
					goto IL_0003;
				}
				num = 0;
				if (IsVmValueObfuscationSentinelNull())
				{
					break;
				}
				goto IL_0036;
				IL_000b:
				c7EoQ7IJLjSNsQhooYs = ((VmValue)P_0).ReadValue() as NumericValue;
				num = 0;
				if (IsVmValueObfuscationSentinelNull())
				{
					break;
				}
				goto IL_0036;
				IL_0003:
				if (!((VmValue)P_0).IsManagedReference())
				{
					break;
				}
				goto IL_000b;
				continue;
				end_IL_0055:
				break;
			}
			return c7EoQ7IJLjSNsQhooYs;
		}

		internal static bool IsVmValueObfuscationSentinelNull()
		{
			return VmValueObfuscationSentinel == null;
		}

		internal static VmValue GetVmValueObfuscationSentinel()
		{
			return VmValueObfuscationSentinel;
		}
	}

	private class ObjectValue : VmValue
	{
		public object Value;

		public Type DeclaredType;

		internal static ObjectValue ObjectValueObfuscationSentinel;

		public ObjectValue()
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate())
		{
		}

		private ObjectValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards)
			: this(null)
		{
		}

		internal override void CopyFrom(VmValue P_0)
		{
			while (true)
			{
				IL_0079:
				if (!(P_0 is ObjectValue))
				{
					goto IL_0003;
				}
				goto IL_005a;
				IL_005a:
				Value = ((ObjectValue)P_0).Value;
				int num = 2;
				if (!IsObjectValueObfuscationSentinelNull())
				{
					break;
				}
				goto IL_003a;
				IL_003a:
				while (true)
				{
					switch (num)
					{
					case 4:
						break;
					case 2:
						DeclaredType = ((ObjectValue)P_0).DeclaredType;
						num = 0;
						if (GetObjectValueObfuscationSentinel() != null)
						{
							continue;
						}
						return;
					default:
						return;
					case 1:
						goto IL_005a;
					case 5:
						goto IL_0079;
					case 0:
						return;
					case 3:
						return;
					}
					break;
				}
				goto IL_0003;
				IL_0003:
				Value = P_0.ReadValue();
				num = 3;
				if (GetObjectValueObfuscationSentinel() == null)
				{
					goto IL_003a;
				}
				goto IL_005a;
			}
		}

		internal override void Assign(VmValue P_0)
		{
			while (true)
			{
				CopyFrom(P_0);
				if (IsObjectValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		public ObjectValue(object P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private ObjectValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, object P_0)
			: base((VmValueKind)0)
		{
			Value = P_0;
			DeclaredType = null;
		}

		public ObjectValue(object P_0, Type P_1)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
		{
		}

		private ObjectValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, object P_0, Type P_1)
			: base((VmValueKind)0)
		{
			Value = P_0;
			DeclaredType = P_1;
		}

		public override string ToString()
		{
			VmDiagnosticCode VmDiagnosticCode = default(VmDiagnosticCode);
			while (true)
			{
				int num;
				if (Value == null)
				{
					VmDiagnosticCode = (VmDiagnosticCode)5;
					num = 1;
					if (GetObjectValueObfuscationSentinel() == null)
					{
						break;
					}
				}
				else
				{
					num = 0;
					if (GetObjectValueObfuscationSentinel() != null)
					{
						goto IL_003e;
					}
				}
				switch (num)
				{
				case 1:
					break;
				default:
					goto IL_003e;
				case 2:
					goto end_IL_0033;
				}
				continue;
				IL_003e:
				return Value.ToString();
				continue;
				end_IL_0033:
				break;
			}
			return VmDiagnosticCode.ToString();
		}

		internal override object ToObject(Type P_0)
		{
			object obj = default(object);
			object obj2 = default(object);
			while (Value != null)
			{
				int num = 18;
				if (GetObjectValueObfuscationSentinel() == null)
				{
					goto IL_0261;
				}
				goto IL_0272;
				IL_03c5:
				obj = ((Type)obj).TypeHandle;
				goto IL_03f4;
				IL_0272:
				while (true)
				{
					switch (num)
					{
					case 47:
						if (obj2.GetType() != P_0)
						{
							goto case 35;
						}
						num = 16;
						if (GetObjectValueObfuscationSentinel() != null)
						{
							continue;
						}
						goto IL_03f7;
					case 35:
						if (P_0 == typeof(RuntimeFieldHandle))
						{
							goto case 46;
						}
						num = 7;
						if (GetObjectValueObfuscationSentinel() == null)
						{
							continue;
						}
						goto IL_03f7;
					case 46:
						if (obj2 is FieldInfo)
						{
							break;
						}
						num = 5;
						if (GetObjectValueObfuscationSentinel() != null)
						{
							continue;
						}
						goto case 7;
					case 7:
					case 14:
					case 31:
						if (P_0 == typeof(RuntimeTypeHandle))
						{
							goto case 9;
						}
						goto case 25;
					case 9:
						if (obj2 is Type)
						{
							goto case 36;
						}
						goto case 25;
					case 36:
						obj2 = ((Type)obj2).TypeHandle;
						num = 17;
						if (GetObjectValueObfuscationSentinel() != null)
						{
							continue;
						}
						goto IL_03f7;
					case 1:
					case 21:
					case 26:
						goto IL_00e8;
					case 18:
						goto IL_00f5;
					case 29:
						goto IL_011a;
					case 27:
						goto IL_0132;
					case 15:
						goto IL_0139;
					case 41:
						goto IL_0159;
					case 5:
						goto IL_0174;
					case 17:
						goto IL_0196;
					case 16:
						goto IL_01a8;
					case 19:
						goto IL_01c5;
					case 22:
						goto IL_01d7;
					case 6:
					case 12:
						goto IL_01f4;
					case 13:
						goto IL_021a;
					case 39:
						if (obj2 != null)
						{
							goto case 34;
						}
						goto IL_03f7;
					case 34:
						if (P_0 != null)
						{
							goto case 47;
						}
						goto IL_03f7;
					case 32:
						obj2 = ((VmValue)Value).ToObject(P_0);
						goto case 39;
					case 23:
						goto IL_0261;
					case 24:
						goto IL_0340;
					case 30:
						goto IL_0351;
					case 43:
						goto IL_036d;
					case 33:
						goto IL_0376;
					case 25:
					case 38:
						if (P_0 == typeof(RuntimeMethodHandle))
						{
							goto case 20;
						}
						goto IL_03f7;
					case 20:
						if (obj2 is MethodBase)
						{
							goto case 3;
						}
						goto IL_03f7;
					case 3:
						obj2 = ((MethodBase)obj2).MethodHandle;
						goto IL_03f7;
					case 8:
						goto IL_03c5;
					case 28:
						goto IL_03da;
					case 2:
					case 10:
					case 45:
						goto IL_03f4;
					case 4:
					case 11:
					case 37:
					case 40:
					case 42:
					case 48:
						goto IL_03f7;
					case 44:
						goto end_IL_0340;
					}
					break;
				}
				goto IL_00c5;
				IL_0261:
				if (!(P_0 != null))
				{
					goto IL_00e8;
				}
				goto IL_0159;
				IL_0159:
				if (P_0.IsByRef)
				{
					goto IL_021a;
				}
				num = 21;
				if (!IsObjectValueObfuscationSentinelNull())
				{
					goto IL_0174;
				}
				goto IL_0272;
				IL_03da:
				return ((VmValue)Value).ToObject(DeclaredType);
				IL_0174:
				if (obj.GetType() != P_0)
				{
					num = 12;
					if (GetObjectValueObfuscationSentinel() == null)
					{
						goto IL_0196;
					}
					goto IL_0272;
				}
				goto IL_03f4;
				IL_011a:
				obj = Value;
				num = 27;
				if (IsObjectValueObfuscationSentinelNull())
				{
					goto IL_0132;
				}
				goto IL_0272;
				IL_0196:
				if (P_0 == typeof(RuntimeFieldHandle))
				{
					goto IL_01a8;
				}
				goto IL_01c5;
				IL_01a8:
				if (!(obj is FieldInfo))
				{
					goto IL_01c5;
				}
				num = 24;
				if (GetObjectValueObfuscationSentinel() != null)
				{
					goto IL_0272;
				}
				goto IL_0351;
				IL_0132:
				if (obj != null)
				{
					goto IL_0139;
				}
				goto IL_03f4;
				IL_0351:
				obj = ((FieldInfo)obj).FieldHandle;
				goto IL_03f4;
				IL_01c5:
				if (P_0 == typeof(RuntimeTypeHandle))
				{
					goto IL_01d7;
				}
				goto IL_01f4;
				IL_01d7:
				if (!(obj is Type))
				{
					goto IL_01f4;
				}
				num = 2;
				if (GetObjectValueObfuscationSentinel() != null)
				{
					goto IL_0272;
				}
				goto IL_03c5;
				IL_0139:
				if (!(P_0 != null))
				{
					goto IL_03f4;
				}
				num = 5;
				if (GetObjectValueObfuscationSentinel() == null)
				{
					goto IL_0272;
				}
				goto IL_03f7;
				IL_01f4:
				if (P_0 == typeof(RuntimeMethodHandle))
				{
					num = 24;
					if (GetObjectValueObfuscationSentinel() != null)
					{
						goto IL_0272;
					}
					goto IL_036d;
				}
				goto IL_03f4;
				IL_03f4:
				return obj;
				IL_036d:
				if (obj is MethodBase)
				{
					goto IL_0376;
				}
				goto IL_03f4;
				IL_0376:
				obj = ((MethodBase)obj).MethodHandle;
				goto IL_03f4;
				IL_021a:
				P_0 = P_0.GetElementType();
				num = 1;
				if (GetObjectValueObfuscationSentinel() != null)
				{
					goto IL_00c5;
				}
				goto IL_0272;
				IL_00c5:
				obj2 = ((FieldInfo)obj2).FieldHandle;
				num = 4;
				if (GetObjectValueObfuscationSentinel() != null)
				{
					goto IL_00e8;
				}
				goto IL_0272;
				IL_00e8:
				if (Value is VmValue)
				{
					goto IL_00f5;
				}
				goto IL_011a;
				IL_00f5:
				if (!(DeclaredType != null))
				{
					num = 32;
					if (GetObjectValueObfuscationSentinel() == null)
					{
						goto IL_0272;
					}
					goto IL_03c5;
				}
				goto IL_03da;
				IL_03f7:
				return obj2;
				continue;
				end_IL_0340:
				break;
				IL_0340:;
			}
			return null;
		}

		internal override bool IsEqualTo(VmValue P_0)
		{
			while (P_0.IsManagedReference())
			{
				if (GetObjectValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return ((ReferenceValue)P_0).IsEqualTo(this);
			}
			object obj = ToObject(null);
			object obj2 = P_0.ToObject(null);
			return obj == obj2;
		}

		internal override bool IsNotEqualTo(VmValue P_0)
		{
			while (P_0.IsManagedReference())
			{
				if (!IsObjectValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return ((ReferenceValue)P_0).IsNotEqualTo(this);
			}
			object obj = ToObject(null);
			object obj2 = P_0.ToObject(null);
			return obj != obj2;
		}

		internal override VmValue ReadValue()
		{
			while (true)
			{
				VmValue VmValue = Value as VmValue;
				int num = 1;
				if (GetObjectValueObfuscationSentinel() == null)
				{
					goto IL_0003;
				}
				goto IL_0013;
				IL_0013:
				switch (num)
				{
				case 2:
					break;
				case 3:
					continue;
				default:
					goto IL_0047;
				case 1:
					goto end_IL_002a;
				}
				goto IL_0003;
				IL_0003:
				if (VmValue == null)
				{
					break;
				}
				num = 0;
				if (GetObjectValueObfuscationSentinel() != null)
				{
					goto IL_0013;
				}
				goto IL_0047;
				IL_0047:
				return VmValue.ReadValue();
				continue;
				end_IL_002a:
				break;
			}
			return this;
		}

		internal override bool IsTruthy()
		{
			while (Value != null)
			{
				VmValue VmValue = Value as VmValue;
				int num = 0;
				if (IsObjectValueObfuscationSentinelNull())
				{
					goto IL_002c;
				}
				goto IL_0032;
				IL_002c:
				do
				{
					if (VmValue != null)
					{
						num = 1;
						continue;
					}
					return true;
				}
				while (!IsObjectValueObfuscationSentinelNull());
				goto IL_0032;
				IL_0032:
				switch (num)
				{
				case 3:
					continue;
				case 1:
					if (VmValue.ToObject(null) == null)
					{
						return false;
					}
					goto case 4;
				case 2:
					goto end_IL_004d;
				case 4:
					return true;
				}
				goto IL_002c;
				continue;
				end_IL_004d:
				break;
			}
			return false;
		}

		internal static bool IsObjectValueObfuscationSentinelNull()
		{
			return ObjectValueObfuscationSentinel == null;
		}

		internal static ObjectValue GetObjectValueObfuscationSentinel()
		{
			return ObjectValueObfuscationSentinel;
		}
	}

	private class StringValue : VmValue
	{
		public string Value;

		private static StringValue StringValueObfuscationSentinel;

		public StringValue(string P_0)
			: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
		{
		}

		private StringValue(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, string P_0)
			: base((VmValueKind)6)
		{
			Value = P_0;
		}

		internal override void CopyFrom(VmValue P_0)
		{
			while (true)
			{
				Value = ((StringValue)P_0).Value;
				if (GetStringValueObfuscationSentinel() == null)
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		internal override void Assign(VmValue P_0)
		{
			while (true)
			{
				CopyFrom(P_0);
				if (IsStringValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		public override string ToString()
		{
			char c = default(char);
			VmDiagnosticCode VmDiagnosticCode = default(VmDiagnosticCode);
			while (true)
			{
				if (Value == null)
				{
					goto IL_0003;
				}
				int num = 1;
				if (GetStringValueObfuscationSentinel() != null)
				{
					goto IL_0012;
				}
				goto IL_0046;
				IL_004a:
				return c + Value + '*';
				IL_0003:
				VmDiagnosticCode = (VmDiagnosticCode)5;
				num = 0;
				if (IsStringValueObfuscationSentinelNull())
				{
					break;
				}
				goto IL_0012;
				IL_0012:
				switch (num)
				{
				case 4:
					break;
				case 2:
					continue;
				case 1:
					goto IL_0046;
				case 3:
					goto IL_004a;
				default:
					goto end_IL_003b;
				}
				goto IL_0003;
				IL_0046:
				c = '*';
				goto IL_004a;
				continue;
				end_IL_003b:
				break;
			}
			return VmDiagnosticCode.ToString();
		}

		internal override bool IsTruthy()
		{
			return Value != null;
		}

		internal override object ToObject(Type P_0)
		{
			return Value;
		}

		internal override bool IsEqualTo(VmValue P_0)
		{
			while (P_0.IsManagedReference())
			{
				if (IsStringValueObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						continue;
					}
				}
				return ((ReferenceValue)P_0).IsEqualTo(this);
			}
			string text = Value;
			object obj = P_0.ToObject(null);
			return text == obj;
		}

		internal override bool IsNotEqualTo(VmValue P_0)
		{
			while (!P_0.IsManagedReference())
			{
				if (GetStringValueObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						break;
					default:
						goto IL_002d;
					case 2:
						goto end_IL_0022;
					}
					continue;
				}
				goto IL_002d;
				IL_002d:
				string text = Value;
				object obj = P_0.ToObject(null);
				return text != obj;
				continue;
				end_IL_0022:
				break;
			}
			return ((ReferenceValue)P_0).IsNotEqualTo(this);
		}

		internal override VmValue ReadValue()
		{
			return this;
		}

		internal static bool IsStringValueObfuscationSentinelNull()
		{
			return StringValueObfuscationSentinel == null;
		}

		internal static StringValue GetStringValueObfuscationSentinel()
		{
			return StringValueObfuscationSentinel;
		}
	}

	internal class EvaluationStack
	{
		private List<VmValue> Values;

		private static EvaluationStack EvaluationStackObfuscationSentinel;

		[SpecialName]
		public int GetCount()
		{
			return Values.Count;
		}

		public void Clear()
		{
			while (true)
			{
				Values.Clear();
				if (IsEvaluationStackObfuscationSentinelNull())
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		public void Push(VmValue P_0)
		{
			while (true)
			{
				Values.Add(P_0);
				if (GetEvaluationStackObfuscationSentinel() != null)
				{
					switch (0)
					{
					case 1:
						break;
					default:
						return;
					case 0:
						return;
					}
					continue;
				}
				break;
			}
		}

		public VmValue Peek()
		{
			return Values[Values.Count - 1];
		}

		public VmValue Pop()
		{
			VmValue result = Peek();
			if (Values.Count != 0)
			{
				Values.RemoveAt(Values.Count - 1);
			}
			return result;
		}

		public EvaluationStack()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			Values = new List<VmValue>();
		}

		internal static bool IsEvaluationStackObfuscationSentinelNull()
		{
			return EvaluationStackObfuscationSentinel == null;
		}

		internal static EvaluationStack GetEvaluationStackObfuscationSentinel()
		{
			return EvaluationStackObfuscationSentinel;
		}
	}

	internal enum VmDiagnosticCode
	{

	}

	[Serializable]
	[CompilerGenerated]
	private sealed class COF5KkOMw6Qnm8sv5I0<T>
	{
		public static readonly COF5KkOMw6Qnm8sv5I0<T> _003C_003E9;

		public static Comparison<VmExceptionRegion> _003C_003E9__45_0;

		internal static object MethodBodyRegionSortClosureObfuscationSentinel;

		static COF5KkOMw6Qnm8sv5I0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			_003C_003E9 = new COF5KkOMw6Qnm8sv5I0<T>();
		}

		public COF5KkOMw6Qnm8sv5I0()
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
		}

		internal int CompareMethodHandlerStartOffsets(VmExceptionRegion x, VmExceptionRegion y)
		{
			return x.Handler.HandlerStart.CompareTo(y.Handler.HandlerStart);
		}

		internal static bool IsMethodBodyRegionSortClosureObfuscationSentinelNull()
		{
			return MethodBodyRegionSortClosureObfuscationSentinel == null;
		}

		internal static object GetMethodBodyRegionSortClosureObfuscationSentinel()
		{
			return MethodBodyRegionSortClosureObfuscationSentinel;
		}
	}

	internal static VmMethodBody[] MethodBodies;

	internal static int[] MethodBodyOffsets;

	internal static List<string> StringLiterals;

	private static BinaryReader BytecodeReader;

	private static byte[] OperandEncodings;

	private static bool BytecodeInitializationStarted;

	private static object BytecodeLock;

	private static VirtualMachineRuntime VirtualMachineRuntimeObfuscationSentinel;

	internal static object[] CreateSingleSlotResult()
	{
		return new object[1];
	}

	internal static object[] ExecuteMethodWithThisReference<TInstance>(int P_0, object P_1, object P_2, ref TInstance P_3)
	{
		VmMethodBody oJ5L9cIz088j56stGWC = null;
		lock (BytecodeLock)
		{
			if (!BytecodeInitializationStarted)
			{
				BytecodeInitializationStarted = true;
				LoadBytecodeResource();
			}
			if (MethodBodies[P_0] != null)
			{
				oJ5L9cIz088j56stGWC = MethodBodies[P_0];
			}
			else
			{
				BytecodeReader.BaseStream.Position = MethodBodyOffsets[P_0];
				oJ5L9cIz088j56stGWC = new VmMethodBody();
				Module module = typeof(VirtualMachineRuntime).Module;
				int metadataToken = ReadCompressedSignedInt32(BytecodeReader);
				int num = ReadCompressedSignedInt32(BytecodeReader);
				int num2 = ReadCompressedSignedInt32(BytecodeReader);
				int num3 = ReadCompressedSignedInt32(BytecodeReader);
				oJ5L9cIz088j56stGWC.Method = module.ResolveMethod(metadataToken);
				ParameterInfo[] parameters = oJ5L9cIz088j56stGWC.Method.GetParameters();
				oJ5L9cIz088j56stGWC.Parameters = new VmParameterDefinition[parameters.Length];
				for (int i = 0; i < parameters.Length; i++)
				{
					Type type = parameters[i].ParameterType;
					VmParameterDefinition hTmbS4IKODuv8OAYYad = new VmParameterDefinition();
					hTmbS4IKODuv8OAYYad.IsByRef = type.IsByRef;
					hTmbS4IKODuv8OAYYad.ParameterIndex = i;
					oJ5L9cIz088j56stGWC.Parameters[i] = hTmbS4IKODuv8OAYYad;
					if (type.IsByRef)
					{
						type = type.GetElementType();
					}
					VmPrimitiveType VmPrimitiveType = (VmPrimitiveType)0;
					VmPrimitiveType = ((!(type == typeof(string))) ? ((!(type == typeof(byte))) ? ((type == typeof(sbyte)) ? ((VmPrimitiveType)1) : ((!(type == typeof(short))) ? ((!(type == typeof(ushort))) ? ((!(type == typeof(int))) ? ((!(type == typeof(uint))) ? ((!(type == typeof(long))) ? ((!(type == typeof(ulong))) ? ((!(type == typeof(float))) ? ((!(type == typeof(double))) ? ((!(type == typeof(bool))) ? ((!(type == typeof(IntPtr))) ? ((!(type == typeof(UIntPtr))) ? ((type == typeof(char)) ? ((VmPrimitiveType)15) : ((VmPrimitiveType)0)) : ((VmPrimitiveType)13)) : ((VmPrimitiveType)12)) : ((VmPrimitiveType)11)) : ((VmPrimitiveType)10)) : ((VmPrimitiveType)9)) : ((VmPrimitiveType)8)) : ((VmPrimitiveType)7)) : ((VmPrimitiveType)6)) : ((VmPrimitiveType)5)) : ((VmPrimitiveType)4)) : ((VmPrimitiveType)3))) : ((VmPrimitiveType)2)) : ((VmPrimitiveType)14));
					hTmbS4IKODuv8OAYYad.PrimitiveType = VmPrimitiveType;
				}
				oJ5L9cIz088j56stGWC.Locals = new List<VmLocalDefinition>(num);
				for (int j = 0; j < num; j++)
				{
					int num4 = ReadCompressedSignedInt32(BytecodeReader);
					VmLocalDefinition VmLocalDefinition = new VmLocalDefinition();
					VmLocalDefinition.DeclaredType = null;
					if (num4 >= 0 && num4 < 50)
					{
						VmLocalDefinition.PrimitiveType = (VmPrimitiveType)(num4 & 0x1F);
						VmLocalDefinition.PreserveReference = (num4 & 0x20) > 0;
					}
					VmLocalDefinition.LocalIndex = j;
					oJ5L9cIz088j56stGWC.Locals.Add(VmLocalDefinition);
				}
				oJ5L9cIz088j56stGWC.ExceptionRegions = new List<VmExceptionRegion>(num2);
				for (int k = 0; k < num2; k++)
				{
					int m1qIkPKOPD = ReadCompressedSignedInt32(BytecodeReader);
					int o2rIX1aZ7C = ReadCompressedSignedInt32(BytecodeReader);
					VmExceptionRegion oxLuEqINxfsgMm9eyQA = new VmExceptionRegion();
					oxLuEqINxfsgMm9eyQA.TryStart = m1qIkPKOPD;
					oxLuEqINxfsgMm9eyQA.TryEnd = o2rIX1aZ7C;
					VmExceptionHandler VmExceptionHandler = (oxLuEqINxfsgMm9eyQA.Handler = new VmExceptionHandler());
					m1qIkPKOPD = ReadCompressedSignedInt32(BytecodeReader);
					o2rIX1aZ7C = ReadCompressedSignedInt32(BytecodeReader);
					int num5 = ReadCompressedSignedInt32(BytecodeReader);
					VmExceptionHandler.HandlerStart = m1qIkPKOPD;
					VmExceptionHandler.HandlerEnd = o2rIX1aZ7C;
					VmExceptionHandler.ClauseKind = num5;
					switch (num5)
					{
					case 0:
						VmExceptionHandler.CatchType = module.ResolveType(ReadCompressedSignedInt32(BytecodeReader));
						break;
					case 1:
						VmExceptionHandler.FilterStart = ReadCompressedSignedInt32(BytecodeReader);
						break;
					default:
						ReadCompressedSignedInt32(BytecodeReader);
						break;
					}
					oJ5L9cIz088j56stGWC.ExceptionRegions.Add(oxLuEqINxfsgMm9eyQA);
				}
				oJ5L9cIz088j56stGWC.ExceptionRegions.Sort((VmExceptionRegion x, VmExceptionRegion y) => x.Handler.HandlerStart.CompareTo(y.Handler.HandlerStart));
				oJ5L9cIz088j56stGWC.Instructions = new List<VmInstruction>(num3);
				for (int num6 = 0; num6 < num3; num6++)
				{
					VmInstruction wBIN9NIsKdSI0BaaGHL = new VmInstruction();
					byte b = (byte)(wBIN9NIsKdSI0BaaGHL.OpCode = (VmOpCode)BytecodeReader.ReadByte());
					if (b < 176)
					{
						int num7 = OperandEncodings[b];
						if (num7 == 0)
						{
							wBIN9NIsKdSI0BaaGHL.Operand = null;
						}
						else
						{
							object obj = null;
							switch (num7)
							{
							case 1:
								obj = ReadCompressedSignedInt32(BytecodeReader);
								goto IL_0556;
							case 2:
								obj = BytecodeReader.ReadInt64();
								goto IL_0556;
							case 3:
								obj = BytecodeReader.ReadSingle();
								goto IL_0556;
							case 4:
								obj = BytecodeReader.ReadDouble();
								goto IL_0556;
							case 5:
							{
								int num8 = ReadCompressedSignedInt32(BytecodeReader);
								int[] array = new int[num8];
								for (int num9 = 0; num9 < num8; num9++)
								{
									array[num9] = ReadCompressedSignedInt32(BytecodeReader);
								}
								obj = array;
								goto IL_0556;
							}
							default:
								{
									throw new Exception();
								}
								IL_0556:
								wBIN9NIsKdSI0BaaGHL.Operand = obj;
								break;
							}
						}
						oJ5L9cIz088j56stGWC.Instructions.Add(wBIN9NIsKdSI0BaaGHL);
						continue;
					}
					throw new Exception();
				}
				MethodBodies[P_0] = oJ5L9cIz088j56stGWC;
			}
		}
		ExecutionFrame eQ2LJ8GAMxyiGPmO2h = new ExecutionFrame();
		eQ2LJ8GAMxyiGPmO2h.MethodBody = oJ5L9cIz088j56stGWC;
		ParameterInfo[] parameters2 = oJ5L9cIz088j56stGWC.Method.GetParameters();
		bool flag = false;
		int num10 = 0;
		if (oJ5L9cIz088j56stGWC.Method is MethodInfo && ((MethodInfo)oJ5L9cIz088j56stGWC.Method).ReturnType != typeof(void))
		{
			flag = true;
		}
		if (oJ5L9cIz088j56stGWC.Method.IsStatic)
		{
			eQ2LJ8GAMxyiGPmO2h.Arguments = new VmValue[parameters2.Length];
			for (int num11 = 0; num11 < parameters2.Length; num11++)
			{
				Type parameterType = parameters2[num11].ParameterType;
				eQ2LJ8GAMxyiGPmO2h.Arguments[num11] = VmValue.CreateValue(parameterType, ((object[])P_1)[num11]);
				if (parameterType.IsByRef)
				{
					num10++;
				}
			}
		}
		else
		{
			eQ2LJ8GAMxyiGPmO2h.Arguments = new VmValue[parameters2.Length + 1];
			if (oJ5L9cIz088j56stGWC.Method.DeclaringType.IsValueType)
			{
				eQ2LJ8GAMxyiGPmO2h.Arguments[0] = new ValueCellReference(new ObjectValue(P_2), oJ5L9cIz088j56stGWC.Method.DeclaringType);
			}
			else
			{
				eQ2LJ8GAMxyiGPmO2h.Arguments[0] = new ObjectValue(P_2);
			}
			for (int num12 = 0; num12 < parameters2.Length; num12++)
			{
				Type parameterType2 = parameters2[num12].ParameterType;
				if (parameterType2.IsByRef)
				{
					eQ2LJ8GAMxyiGPmO2h.Arguments[num12 + 1] = VmValue.CreateValue(parameterType2, ((object[])P_1)[num12]);
					num10++;
				}
				else
				{
					eQ2LJ8GAMxyiGPmO2h.Arguments[num12 + 1] = VmValue.CreateValue(parameterType2, ((object[])P_1)[num12]);
				}
			}
		}
		eQ2LJ8GAMxyiGPmO2h.Locals = new VmValue[oJ5L9cIz088j56stGWC.Locals.Count];
		for (int num13 = 0; num13 < oJ5L9cIz088j56stGWC.Locals.Count; num13++)
		{
			VmLocalDefinition kIX0MtI4Eum2V5w6L2D2 = oJ5L9cIz088j56stGWC.Locals[num13];
			switch (kIX0MtI4Eum2V5w6L2D2.PrimitiveType)
			{
			case (VmPrimitiveType)0:
				eQ2LJ8GAMxyiGPmO2h.Locals[num13] = null;
				break;
			case (VmPrimitiveType)7:
			case (VmPrimitiveType)8:
				eQ2LJ8GAMxyiGPmO2h.Locals[num13] = new Int64Value(0L, kIX0MtI4Eum2V5w6L2D2.PrimitiveType);
				break;
			case (VmPrimitiveType)9:
			case (VmPrimitiveType)10:
				eQ2LJ8GAMxyiGPmO2h.Locals[num13] = new FloatingPointValue(0.0, kIX0MtI4Eum2V5w6L2D2.PrimitiveType);
				break;
			case (VmPrimitiveType)12:
				eQ2LJ8GAMxyiGPmO2h.Locals[num13] = new NativeIntegerValue(IntPtr.Zero);
				break;
			case (VmPrimitiveType)13:
				eQ2LJ8GAMxyiGPmO2h.Locals[num13] = new NativeIntegerValue(UIntPtr.Zero);
				break;
			case (VmPrimitiveType)14:
				eQ2LJ8GAMxyiGPmO2h.Locals[num13] = null;
				break;
			case (VmPrimitiveType)1:
			case (VmPrimitiveType)2:
			case (VmPrimitiveType)3:
			case (VmPrimitiveType)4:
			case (VmPrimitiveType)5:
			case (VmPrimitiveType)6:
			case (VmPrimitiveType)11:
			case (VmPrimitiveType)15:
				eQ2LJ8GAMxyiGPmO2h.Locals[num13] = new Int32Value(0, kIX0MtI4Eum2V5w6L2D2.PrimitiveType);
				break;
			case (VmPrimitiveType)16:
				eQ2LJ8GAMxyiGPmO2h.Locals[num13] = new ObjectValue(null);
				break;
			}
		}
		try
		{
			eQ2LJ8GAMxyiGPmO2h.Execute();
		}
		finally
		{
			eQ2LJ8GAMxyiGPmO2h.ReleaseFrameResources();
		}
		int num14 = 0;
		if (flag)
		{
			num14 = 1;
		}
		num14 += num10;
		object[] array2 = new object[num14];
		if (flag)
		{
			array2[0] = null;
		}
		if (oJ5L9cIz088j56stGWC.Method is MethodInfo)
		{
			MethodInfo methodInfo = (MethodInfo)oJ5L9cIz088j56stGWC.Method;
			if (methodInfo.ReturnType != typeof(void) && eQ2LJ8GAMxyiGPmO2h.ReturnValue != null)
			{
				array2[0] = eQ2LJ8GAMxyiGPmO2h.ReturnValue.ToObject(methodInfo.ReturnType);
			}
		}
		if (num10 > 0)
		{
			int num15 = 0;
			if (flag)
			{
				num15++;
			}
			for (int num16 = 0; num16 < parameters2.Length; num16++)
			{
				Type parameterType3 = parameters2[num16].ParameterType;
				if (!parameterType3.IsByRef)
				{
					continue;
				}
				parameterType3 = parameterType3.GetElementType();
				if (eQ2LJ8GAMxyiGPmO2h.Arguments[num16] != null)
				{
					if (oJ5L9cIz088j56stGWC.Method.IsStatic)
					{
						array2[num15] = eQ2LJ8GAMxyiGPmO2h.Arguments[num16].ToObject(parameterType3);
					}
					else
					{
						array2[num15] = eQ2LJ8GAMxyiGPmO2h.Arguments[num16 + 1].ToObject(parameterType3);
					}
				}
				else
				{
					array2[num15] = null;
				}
				num15++;
			}
		}
		if (!oJ5L9cIz088j56stGWC.Method.IsStatic && oJ5L9cIz088j56stGWC.Method.DeclaringType.IsValueType)
		{
			P_3 = (TInstance)eQ2LJ8GAMxyiGPmO2h.Arguments[0].ToObject(oJ5L9cIz088j56stGWC.Method.DeclaringType);
		}
		return array2;
	}

	internal static object[] ExecuteMethod(int P_0, object P_1, object P_2)
	{
		int num;
		while (true)
		{
			num = 0;
			if (GetVirtualMachineRuntimeObfuscationSentinel() == null)
			{
				switch (0)
				{
				case 1:
					continue;
				}
			}
			break;
		}
		return ExecuteMethodWithThisReference(P_0, P_1, P_2, ref num);
	}

	internal static object[] ExecuteValueTypeMethod<TInstance>(int P_0, object P_1, ref TInstance P_2)
	{
		return ExecuteMethodWithThisReference(P_0, P_1, P_2, ref P_2);
	}

	internal static void LoadBytecodeResource()
	{
		byte[] array = default(byte[]);
		while (true)
		{
			int num;
			if (MethodBodyOffsets != null)
			{
				num = 1;
				if (GetVirtualMachineRuntimeObfuscationSentinel() == null)
				{
					break;
				}
				goto IL_001c;
			}
			goto IL_0035;
			IL_001c:
			switch (num)
			{
			case 4:
				break;
			default:
				goto IL_0035;
			case 2:
				continue;
			case 1:
			case 3:
				return;
			}
			goto IL_0006;
			IL_0035:
			BinaryReader binaryReader = new BinaryReader(typeof(VirtualMachineRuntime).Assembly.GetManifestResourceStream("WWH5aCBp0lgLkbvKDy.buhjcsUmGGy81pkpdj"));
			binaryReader.BaseStream.Position = 0L;
			array = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length);
			binaryReader.Close();
			goto IL_0006;
			IL_0006:
			InitializeBytecodeTables(array);
			num = 3;
			if (IsVirtualMachineRuntimeObfuscationSentinelNull())
			{
				break;
			}
			goto IL_001c;
		}
	}

	internal static void InitializeBytecodeTables(object P_0)
	{
		int num2 = default(int);
		int num7 = default(int);
		int num6 = default(int);
		int num4 = default(int);
		int num5 = default(int);
		int num3 = default(int);
		int num8 = default(int);
		int num9 = default(int);
		while (true)
		{
			BytecodeReader = new BinaryReader(new MemoryStream((byte[])P_0));
			while (true)
			{
				IL_02b4:
				OperandEncodings = new byte[255];
				int num = 0;
				if (IsVirtualMachineRuntimeObfuscationSentinelNull())
				{
					goto IL_01ff;
				}
				goto IL_021a;
				IL_021a:
				while (true)
				{
					switch (num)
					{
					case 33:
						break;
					case 2:
					case 4:
						goto IL_0022;
					case 14:
					case 20:
						goto IL_0028;
					case 12:
						goto IL_0031;
					case 8:
						goto IL_0053;
					case 1:
						goto IL_0069;
					case 23:
						goto IL_007b;
					case 22:
						goto IL_0086;
					case 6:
						goto IL_00a4;
					case 24:
					case 29:
						goto IL_00c6;
					case 15:
						goto IL_00fe;
					case 25:
						goto IL_010a;
					case 16:
						goto IL_0116;
					case 9:
					case 10:
						goto IL_013d;
					case 18:
						goto IL_0143;
					case 11:
					case 17:
						goto IL_0154;
					case 32:
						MethodBodyOffsets[num2] = num7;
						goto case 27;
					case 27:
						num7 += num6;
						num = 21;
						if (IsVirtualMachineRuntimeObfuscationSentinelNull())
						{
							continue;
						}
						goto case 7;
					case 7:
						MethodBodyOffsets = new int[num4];
						goto case 30;
					case 30:
						num5 = 0;
						num = 0;
						if (GetVirtualMachineRuntimeObfuscationSentinel() != null)
						{
							continue;
						}
						goto IL_0022;
					case 19:
					case 31:
						if (num2 >= num4)
						{
							num = 13;
							if (IsVirtualMachineRuntimeObfuscationSentinelNull())
							{
								continue;
							}
							goto IL_013d;
						}
						goto case 3;
					case 3:
						num6 = MethodBodyOffsets[num2];
						goto case 32;
					case 28:
						num3++;
						num = 8;
						if (!IsVirtualMachineRuntimeObfuscationSentinelNull())
						{
							continue;
						}
						goto IL_013d;
					case 26:
						num2 = 0;
						num = 19;
						if (IsVirtualMachineRuntimeObfuscationSentinelNull())
						{
							continue;
						}
						goto IL_0154;
					case 21:
						num2++;
						goto case 19;
					case 5:
						goto IL_01f7;
					default:
						goto IL_01ff;
					case 34:
						goto IL_02b4;
					case 35:
						goto end_IL_02b4;
					case 13:
						return;
					}
					break;
				}
				goto IL_0006;
				IL_01ff:
				num4 = ReadCompressedSignedInt32(BytecodeReader);
				num = 5;
				if (IsVirtualMachineRuntimeObfuscationSentinelNull())
				{
					goto IL_01f7;
				}
				goto IL_021a;
				IL_01f7:
				num8 = 0;
				goto IL_0154;
				IL_0006:
				MethodBodies = new VmMethodBody[num4];
				num = 7;
				if (!IsVirtualMachineRuntimeObfuscationSentinelNull())
				{
					goto IL_0022;
				}
				goto IL_021a;
				IL_0022:
				if (num5 < num4)
				{
					goto IL_0028;
				}
				goto IL_00a4;
				IL_0028:
				MethodBodies[num5] = null;
				goto IL_0031;
				IL_0031:
				MethodBodyOffsets[num5] = ReadCompressedSignedInt32(BytecodeReader);
				num = 1;
				if (GetVirtualMachineRuntimeObfuscationSentinel() == null)
				{
					goto IL_0053;
				}
				goto IL_021a;
				IL_0053:
				num5++;
				num = 2;
				if (GetVirtualMachineRuntimeObfuscationSentinel() != null)
				{
					goto IL_0069;
				}
				goto IL_021a;
				IL_0069:
				OperandEncodings[num9] = BytecodeReader.ReadByte();
				goto IL_007b;
				IL_007b:
				num8++;
				goto IL_0154;
				IL_0154:
				if (num8 < num4)
				{
					goto IL_0086;
				}
				goto IL_00fe;
				IL_0086:
				num9 = BytecodeReader.ReadByte();
				num = 1;
				if (!IsVirtualMachineRuntimeObfuscationSentinelNull())
				{
					goto IL_0069;
				}
				goto IL_021a;
				IL_00a4:
				num7 = (int)BytecodeReader.BaseStream.Position;
				num = 26;
				if (!IsVirtualMachineRuntimeObfuscationSentinelNull())
				{
					goto IL_00c6;
				}
				goto IL_021a;
				IL_00c6:
				StringLiterals.Add(Encoding.Unicode.GetString(BytecodeReader.ReadBytes(ReadCompressedSignedInt32(BytecodeReader))));
				num = 28;
				if (GetVirtualMachineRuntimeObfuscationSentinel() != null)
				{
					goto IL_00fe;
				}
				goto IL_021a;
				IL_00fe:
				num4 = ReadCompressedSignedInt32(BytecodeReader);
				goto IL_010a;
				IL_010a:
				StringLiterals = new List<string>(num4);
				goto IL_0116;
				IL_0116:
				num3 = 0;
				num = 6;
				if (IsVirtualMachineRuntimeObfuscationSentinelNull())
				{
					goto IL_013d;
				}
				goto IL_021a;
				IL_013d:
				if (num3 >= num4)
				{
					goto IL_0143;
				}
				num = 15;
				if (GetVirtualMachineRuntimeObfuscationSentinel() == null)
				{
					goto IL_00c6;
				}
				goto IL_021a;
				IL_0143:
				num4 = ReadCompressedSignedInt32(BytecodeReader);
				goto IL_0006;
				continue;
				end_IL_02b4:
				break;
			}
		}
	}

	internal static int ReadCompressedSignedInt32(object P_0)
	{
		uint num5 = default(uint);
		int num4 = default(int);
		uint num3 = default(uint);
		uint num2 = default(uint);
		while (true)
		{
			bool flag = false;
			int num = 14;
			if (GetVirtualMachineRuntimeObfuscationSentinel() == null)
			{
				while (true)
				{
					switch (num)
					{
					case 17:
						if (num5 >= 128)
						{
							num = 3;
							if (GetVirtualMachineRuntimeObfuscationSentinel() != null)
							{
								continue;
							}
							goto case 5;
						}
						goto case 16;
					case 5:
						num4 = 0;
						num = 13;
						if (GetVirtualMachineRuntimeObfuscationSentinel() == null)
						{
							continue;
						}
						goto case 3;
					case 3:
					case 13:
						num3 = ((BinaryReader)P_0).ReadByte();
						num = 7;
						if (IsVirtualMachineRuntimeObfuscationSentinelNull())
						{
							continue;
						}
						goto end_IL_0110;
					case 14:
						num2 = 0u;
						goto case 8;
					case 8:
						num5 = ((BinaryReader)P_0).ReadByte();
						num = 3;
						if (GetVirtualMachineRuntimeObfuscationSentinel() != null)
						{
							continue;
						}
						goto case 10;
					case 10:
						num2 |= num5 & 0x3F;
						goto case 2;
					case 2:
						if ((num5 & 0x40) == 0)
						{
							goto case 17;
						}
						num = 0;
						if (!IsVirtualMachineRuntimeObfuscationSentinelNull())
						{
							continue;
						}
						goto case 1;
					case 1:
						flag = true;
						goto case 17;
					case 11:
						if (num3 >= 128)
						{
							goto case 9;
						}
						num = 0;
						if (IsVirtualMachineRuntimeObfuscationSentinelNull())
						{
							continue;
						}
						goto IL_0123;
					case 9:
						num4++;
						goto case 3;
					case 7:
						num2 |= (num3 & 0x7F) << 7 * num4 + 6;
						goto case 11;
					case 15:
						break;
					default:
						goto IL_0123;
					case 6:
						goto end_IL_0110;
					case 16:
						if (!flag)
						{
							return (int)num2;
						}
						goto case 4;
					case 4:
						return (int)(~num2);
					}
					break;
				}
				continue;
			}
			goto IL_0123;
			IL_0123:
			if (flag)
			{
				break;
			}
			return (int)num2;
			continue;
			end_IL_0110:
			break;
		}
		return (int)(~num2);
	}

	public VirtualMachineRuntime()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
	}

	static VirtualMachineRuntime()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		MethodBodies = null;
		MethodBodyOffsets = null;
		BytecodeInitializationStarted = false;
		BytecodeLock = new object();
	}

	internal static bool IsVirtualMachineRuntimeObfuscationSentinelNull()
	{
		return VirtualMachineRuntimeObfuscationSentinel == null;
	}

	internal static VirtualMachineRuntime GetVirtualMachineRuntimeObfuscationSentinel()
	{
		return VirtualMachineRuntimeObfuscationSentinel;
	}
}
}
