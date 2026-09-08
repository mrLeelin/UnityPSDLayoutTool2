using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.PsdParser
{

public struct PsdColor
{
	private readonly byte r;

	private readonly byte g;

	private readonly byte b;

	private readonly byte a;

	internal static object Os8j7kZcAjw0tbYiFuPc;

	public byte R => r;

	public byte G => g;

	public byte B => b;

	public byte A => a;

	public PsdColor(byte r, byte g, byte b, byte a = byte.MaxValue)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		this.r = r;
		this.g = g;
		this.b = b;
		this.a = a;
	}

	public PsdColor WithAlpha(byte alpha)
	{
		return new PsdColor(R, G, B, alpha);
	}

	public override string ToString()
	{
		return $"RGBA({R}, {G}, {B}, {A})";
	}

	internal static bool nQtqZaZcMlSmT6rwoe3g()
	{
		return Os8j7kZcAjw0tbYiFuPc == null;
	}

	internal static object aR8R4SZcQrQyDDInIm3C()
	{
		return Os8j7kZcAjw0tbYiFuPc;
	}
}
}
