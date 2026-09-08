using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PsdPropertyUtilities;
using PsdProtectionGuards;
using PsdSections;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;

namespace PsdLayerData
{

internal class PsdLayerAndMaskData
{
	private readonly LayerInfoReader _layerListReader;

	private readonly GlobalLayerMaskSectionReader _globalMaskReader;

	private readonly IProperties _additionalLayerProperties;

	private ILinkedLayer[] _linkedLayers;

	public PsdLayer[] Layers
	{
		get
		{
			if (_layerListReader != null && _layerListReader.GetSectionLength() > 0L)
			{
				return _layerListReader.Value ?? Array.Empty<PsdLayer>();
			}
			if (_additionalLayerProperties != null)
			{
				string[] array = new string[2] { "Lr16", "Lr32" };
				foreach (string text in array)
				{
					if (_additionalLayerProperties.ContainsPropertyPath(text, "Layers"))
					{
						PsdLayer[] array2 = _additionalLayerProperties.GetPropertyValue<PsdLayer[]>(text, new string[1] { "Layers" });
						if (array2 != null)
						{
							return array2;
						}
					}
				}
			}
			return Array.Empty<PsdLayer>();
		}
	}

	public PsdLayerAndMaskData(LayerInfoReader P_0, GlobalLayerMaskSectionReader P_1, IProperties P_2)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_layerListReader = P_0;
		_globalMaskReader = P_1;
		_additionalLayerProperties = P_2;
	}

	[SpecialName]
	public ILinkedLayer[] GetLinkedLayers()
	{
		if (_linkedLayers == null)
		{
			List<ILinkedLayer> list = new List<ILinkedLayer>();
			string[] array = new string[4] { "lnk2", "lnk3", "lnkD", "lnkE" };
			foreach (string text in array)
			{
				if (_additionalLayerProperties.Contains(text))
				{
					ILinkedLayer[] collection = _additionalLayerProperties.GetPropertyValue<ILinkedLayer[]>(text, new string[1] { "Items" });
					list.AddRange(collection);
				}
			}
			_linkedLayers = list.ToArray();
		}
		return _linkedLayers;
	}

	[SpecialName]
	public IProperties GetAdditionalLayerProperties()
	{
		return _additionalLayerProperties;
	}
}
}
