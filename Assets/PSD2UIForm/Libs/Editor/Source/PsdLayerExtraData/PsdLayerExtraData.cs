using System;
using System.Runtime.CompilerServices;
using PsdSections;
using PsdPropertyUtilities;
using PsdProtectionGuards;
using cn.efunstudio.psdreader.PsdParser;
using PsdProtectionRuntime;

namespace PsdLayerData
{

internal class PsdLayerExtraData
{
	private readonly LayerMaskReader _maskReader;

	private readonly LayerBlendingRangesReader _blendingRangesReader;

	private readonly LayerAdditionalInfoReader _additionalInfoReader;

	private readonly string _name;

	private SectionType _sectionType;

	private Guid _smartObjectId;

	public SectionType SectionType => _sectionType;

	public string Name => _name;

	public PsdLayerExtraData(LayerMaskReader P_0, LayerBlendingRangesReader P_1, LayerAdditionalInfoReader P_2, string P_3)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_maskReader = P_0;
		_blendingRangesReader = P_1;
		_additionalInfoReader = P_2;
		_name = P_3;
		_additionalInfoReader.TryReadPropertyValue(ref _name, "luni.Name");
		if (!_additionalInfoReader.TryReadPropertyValue(ref _sectionType, "lsct.SectionType"))
		{
			_additionalInfoReader.TryReadPropertyValue(ref _sectionType, "lsdk.SectionType");
		}
		if (_additionalInfoReader.Contains("SoLd.Idnt"))
		{
			_smartObjectId = _additionalInfoReader.GetGuidValue("SoLd.Idnt");
		}
		else if (_additionalInfoReader.Contains("SoLE.Idnt"))
		{
			_smartObjectId = _additionalInfoReader.GetGuidValue("SoLE.Idnt");
		}
	}

	[SpecialName]
	public Guid GetSmartObjectId()
	{
		return _smartObjectId;
	}

	[SpecialName]
	public LayerMask GetLayerMask()
	{
		return _maskReader.Value;
	}

	[SpecialName]
	public object GetBlendingRanges()
	{
		return _blendingRangesReader.Value;
	}

	[SpecialName]
	public IProperties GetAdditionalProperties()
	{
		return _additionalInfoReader.Value;
	}
}
}
