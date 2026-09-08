using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

[Serializable]
internal sealed class PsdReaderDeviceShardPayloadDocument
{
	public const int CurrentSchema = 1;

	public int Schema;

	public string Kind;

	public string ProductCode;

	public string Prefix;

	public int Revision;

	public string[] BlockedTargets;

	public long IssuedUtcTicks;

	public PsdReaderDeviceShardPayloadDocument()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		Schema = 1;
		Kind = "DeviceShard";
		ProductCode = string.Empty;
		Prefix = string.Empty;
		BlockedTargets = Array.Empty<string>();
	}
}
}
