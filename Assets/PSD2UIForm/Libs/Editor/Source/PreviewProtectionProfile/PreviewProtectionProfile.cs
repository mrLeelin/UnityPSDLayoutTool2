using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using PsdPreviewProtection;
using PsdProtectionRuntime;

namespace PsdPreviewProtection
{

internal sealed class PreviewProtectionProfile
{
	[CompilerGenerated]
	private int _version;

	[CompilerGenerated]
	private int _stableSeed;

	[CompilerGenerated]
	private int _sessionSeed;

	[CompilerGenerated]
	private byte qHdABm1f1J;

	[CompilerGenerated]
	private byte XcIAUZrBId;

	[CompilerGenerated]
	private byte L7vA91VwO3;

	[CompilerGenerated]
	private byte qUJAmMnU4d;

	[CompilerGenerated]
	private byte NlhAolrHJp;

	[CompilerGenerated]
	private byte VjRAJa7jE9;

	[CompilerGenerated]
	private byte F1fAuY4yM9;

	[CompilerGenerated]
	private byte G1eAT12WGX;

	[CompilerGenerated]
	private PreviewProtectionFlags _protectionFlags;

	[CompilerGenerated]
	private string LXGAMZS5LQ;

	[CompilerGenerated]
	private string RfyAQRTT0j;

	[CompilerGenerated]
	private string tarAHWgrGH;

	[CompilerGenerated]
	private string mQHA3ZSqIL;

	internal int Version
	{
		[CompilerGenerated]
		get
		{
			return _version;
		}
		[CompilerGenerated]
		set
		{
			_version = value;
		}
	}

	[SpecialName]
	[CompilerGenerated]
	internal int GetStableSeed()
	{
		return _stableSeed;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetStableSeed(int P_0)
	{
		_stableSeed = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal int GetSessionSeed()
	{
		return _sessionSeed;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetSessionSeed(int P_0)
	{
		_sessionSeed = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal byte GvsTSpIpvK()
	{
		return qHdABm1f1J;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void VuYTLVgV7C(byte P_0)
	{
		qHdABm1f1J = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal byte auwTE03s0j()
	{
		return XcIAUZrBId;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void iboTC3evT4(byte P_0)
	{
		XcIAUZrBId = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal byte xpdTxouQpO()
	{
		return L7vA91VwO3;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void OpETIfGOmw(byte P_0)
	{
		L7vA91VwO3 = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal byte w3RTbRHx9B()
	{
		return qUJAmMnU4d;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void TWHTwZyvi0(byte P_0)
	{
		qUJAmMnU4d = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal byte gjjTWI68cj()
	{
		return NlhAolrHJp;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void O6hTiEJGVm(byte P_0)
	{
		NlhAolrHJp = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal byte WFaTjh9q7a()
	{
		return VjRAJa7jE9;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void GD7TYSTCK6(byte P_0)
	{
		VjRAJa7jE9 = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal byte vL5T4Sw8Hv()
	{
		return F1fAuY4yM9;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void mm6TfcO4JQ(byte P_0)
	{
		F1fAuY4yM9 = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal byte naLTgkaVgS()
	{
		return G1eAT12WGX;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void ok1TlGf7Tr(byte P_0)
	{
		G1eAT12WGX = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal PreviewProtectionFlags GetProtectionFlags()
	{
		return _protectionFlags;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void SetProtectionFlags(PreviewProtectionFlags P_0)
	{
		_protectionFlags = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal string OFlTvUd2HH()
	{
		return LXGAMZS5LQ;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void d4ATdXT53l(string P_0)
	{
		LXGAMZS5LQ = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal string aurT8rbs9i()
	{
		return RfyAQRTT0j;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void q7ZTDNr52h(string P_0)
	{
		RfyAQRTT0j = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal string z9eTyqhVJb()
	{
		return tarAHWgrGH;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void ykaTzR5REh(string P_0)
	{
		tarAHWgrGH = P_0;
	}

	[SpecialName]
	[CompilerGenerated]
	internal string TnOAZhDjDu()
	{
		return mQHA3ZSqIL;
	}

	[SpecialName]
	[CompilerGenerated]
	internal void uGJAOSdA4J(string P_0)
	{
		mQHA3ZSqIL = P_0;
	}

	[SpecialName]
	internal bool HasVisibleProtection()
	{
		if (GetProtectionFlags() != 0)
		{
			if (GvsTSpIpvK() <= 0 && auwTE03s0j() <= 0 && gjjTWI68cj() <= 0)
			{
				return vL5T4Sw8Hv() > 0;
			}
			return true;
		}
		return false;
	}

	public PreviewProtectionProfile()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_version = 1;
		LXGAMZS5LQ = string.Empty;
		RfyAQRTT0j = string.Empty;
		tarAHWgrGH = string.Empty;
		mQHA3ZSqIL = string.Empty;
	}
}
}
