using System;
using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using PsdLicensing;
using PsdProtectionRuntime;

namespace PsdLicensing
{

internal sealed class LicenseStatusSnapshot
{
	[CompilerGenerated]
	private LicenseValidationStatus _validationStatus;

	[CompilerGenerated]
	private string _message;

	[CompilerGenerated]
	private string _licenseId;

	[CompilerGenerated]
	private string _lookupId;

	[CompilerGenerated]
	private string _statusText;

	[CompilerGenerated]
	private string[] _features;

	[CompilerGenerated]
	private DateTime _lastVerifiedUtc;

	[CompilerGenerated]
	private DateTime _supportUntilUtc;

	[CompilerGenerated]
	private int _revision;

	public string Message
	{
		[CompilerGenerated]
		get
		{
			return _message;
		}
		[CompilerGenerated]
		internal set
		{
			_message = value;
		}
	}

	[SpecialName]
	[CompilerGenerated]
	public LicenseValidationStatus GetValidationStatus()
	{
		return _validationStatus;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetValidationStatus(LicenseValidationStatus P_0)
	{
		_validationStatus = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	public string GetLicenseId()
	{
		return _licenseId;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetLicenseId(string P_0)
	{
		_licenseId = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	public string GetLookupId()
	{
		return _lookupId;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetLookupId(string P_0)
	{
		_lookupId = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	public string GetStatusText()
	{
		return _statusText;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetStatusText(string P_0)
	{
		_statusText = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	public string[] GetFeatures()
	{
		return _features;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetFeatures(string[] P_0)
	{
		_features = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	public DateTime GetLastVerifiedUtc()
	{
		return _lastVerifiedUtc;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetLastVerifiedUtc(DateTime P_0)
	{
		_lastVerifiedUtc = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	public DateTime GetSupportUntilUtc()
	{
		return _supportUntilUtc;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetSupportUntilUtc(DateTime P_0)
	{
		_supportUntilUtc = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	public int GetRevision()
	{
		return _revision;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetRevision(int P_0)
	{
		_revision = P_0;
	}

	public LicenseStatusSnapshot()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_message = string.Empty;
		_licenseId = string.Empty;
		_lookupId = string.Empty;
		_statusText = string.Empty;
		_features = Array.Empty<string>();
	}
}
}
