using System;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader
{

[Serializable]
internal sealed class PsdReaderSignedEnvelopeDocument
{
	public const int CurrentSchema = 1;

	public const string CurrentKdf = "EFUN-HMACSHA256-V1";

	public int Schema;

	public string Kind;

	public string Kid;

	public string Alg;

	public string Enc;

	public string Kdf;

	public string Scope;

	public string Iv;

	public string Payload;

	public string Sig;

	public PsdReaderSignedEnvelopeDocument()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		Schema = 1;
		Kind = "SecureEnvelope";
		Kid = string.Empty;
		Alg = "RS256";
		Enc = "AES256-CBC";
		Kdf = "EFUN-HMACSHA256-V1";
		Scope = string.Empty;
		Iv = string.Empty;
		Payload = string.Empty;
		Sig = string.Empty;
	}
}
}
