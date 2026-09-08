using System;
using System.Collections;
using System.Collections.Generic;
using PsdProtectionGuards;
using PsdDescriptors;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;
using PsdBinaryUtilities;
using PsdSections;

namespace PsdSections
{

internal abstract class PropertySectionReader : LengthPrefixedSectionReader<IProperties>, IProperties, IEnumerable<KeyValuePair<string, object>>, IEnumerable
{
	public object this[string property] => GetPropertiesOrNull()?[property];

	public int Count => GetPropertiesOrNull()?.Count ?? 0;

	protected PropertySectionReader(PsdBigEndianReader P_0, object P_1)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1)
	{
	}

	private PropertySectionReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, object P_1)
		: base(P_0, P_1)
	{
	}

	protected PropertySectionReader(PsdBigEndianReader P_0, long P_1, object P_2)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1, P_2)
	{
	}

	private PropertySectionReader(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, PsdBigEndianReader P_0, long P_1, object P_2)
		: base(P_0, P_1, P_2)
	{
	}

	public bool Contains(string P_0)
	{
		return GetPropertiesOrNull()?.Contains(P_0) ?? false;
	}

	IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
	{
		IProperties properties = GetPropertiesOrNull();
		return (properties ?? new PsdPropertyBag()).GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		IProperties properties = GetPropertiesOrNull();
		return (properties ?? new PsdPropertyBag()).GetEnumerator();
	}

	private IProperties GetPropertiesOrNull()
	{
		try
		{
			return base.Value;
		}
		catch (ObjectDisposedException)
		{
			return null;
		}
		catch (NullReferenceException)
		{
			return null;
		}
	}
}
}
