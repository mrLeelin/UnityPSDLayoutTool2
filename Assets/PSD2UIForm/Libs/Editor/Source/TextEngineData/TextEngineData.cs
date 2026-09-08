using System.Collections;
using PsdProtectionGuards;
using PsdDescriptors;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdDescriptors
{

internal class TextEngineData : PsdPropertyBag
{
	public TextEngineData(PsdBigEndianReader P_0)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0)
	{
	}

	private TextEngineData(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0)
		: base()
	{
		P_0.ReadInt32();
		P_0.ExpectRepeatedCharacter('\n', 2);
		ReadEngineDictionary(P_0, 0, this);
	}

	private void ReadEngineDictionary(PsdBigEndianReader P_0, int P_1, PsdPropertyBag P_2)
	{
		P_0.ExpectRepeatedCharacter('\t', P_1);
		switch (P_0.ReadByteAsChar())
		{
		case ']':
			return;
		case '<':
			P_0.ExpectCharacter('<');
			break;
		}
		P_0.ExpectCharacter('\n');
		while (true)
		{
			P_0.ExpectRepeatedCharacter('\t', P_1);
			char c = P_0.ReadByteAsChar();
			if (c == '>')
			{
				break;
			}
			c = P_0.ReadByteAsChar();
			string text = string.Empty;
			while (true)
			{
				c = P_0.ReadByteAsChar();
				if (c == ' ' || c == '\n')
				{
					break;
				}
				text += c;
			}
			switch (c)
			{
			case '\n':
			{
				PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag();
				ReadEngineDictionary(P_0, P_1 + 1, wBX0tc6bIp34eGtoXLH);
				if (wBX0tc6bIp34eGtoXLH.Count > 0)
				{
					P_2.Add(text, wBX0tc6bIp34eGtoXLH);
				}
				P_0.ExpectCharacter('\n');
				break;
			}
			case ' ':
			{
				object value = ReadEngineValue(P_0, P_1 + 1);
				P_2.Add(text, value);
				break;
			}
			}
		}
		P_0.ExpectCharacter('>');
	}

	private object ReadEngineValue(PsdBigEndianReader P_0, int P_1)
	{
		char c = P_0.ReadByteAsChar();
		switch (c)
		{
		case ']':
			return null;
		case '(':
		{
			string text2 = string.Empty;
			P_0.ReadInt16();
			while (true)
			{
				char c2 = P_0.ReadByteAsChar();
				if (c2 == ')')
				{
					break;
				}
				char c3 = P_0.ReadByteAsChar();
				if (c3 == '\\')
				{
					c3 = P_0.ReadByteAsChar();
				}
				text2 = ((c3 != '\r') ? (text2 + (char)(((uint)c2 << 8) | c3)) : (text2 + "\n"));
			}
			P_0.ExpectCharacter('\n');
			return text2;
		}
		case '[':
		{
			ArrayList arrayList = new ArrayList();
			c = P_0.ReadByteAsChar();
			while (true)
			{
				switch (c)
				{
				case '\n':
				{
					PsdPropertyBag wBX0tc6bIp34eGtoXLH = new PsdPropertyBag();
					ReadEngineDictionary(P_0, P_1, wBX0tc6bIp34eGtoXLH);
					P_0.ExpectCharacter('\n');
					if (wBX0tc6bIp34eGtoXLH.Count != 0)
					{
						arrayList.Add(wBX0tc6bIp34eGtoXLH);
						break;
					}
					return arrayList;
				}
				case ' ':
				{
					object obj = ReadEngineValue(P_0, P_1);
					if (obj != null)
					{
						arrayList.Add(obj);
						break;
					}
					P_0.ExpectCharacter('\n');
					return arrayList;
				}
				}
			}
		}
		default:
		{
			string text = string.Empty;
			do
			{
				text += c;
				c = P_0.ReadByteAsChar();
			}
			while (c != '\n' && c != ' ');
			if (int.TryParse(text, out var result))
			{
				return result;
			}
			if (!float.TryParse(text, out var result2))
			{
				if (!bool.TryParse(text, out var result3))
				{
					return text;
				}
				return result3;
			}
			return result2;
		}
		}
	}
}
}
