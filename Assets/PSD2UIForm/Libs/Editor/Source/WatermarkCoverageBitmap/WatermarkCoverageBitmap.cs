using System;
using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace PsdPreviewProtection
{

internal sealed class WatermarkCoverageBitmap
{
	[CompilerGenerated]
	private static readonly WatermarkCoverageBitmap _empty;

	private readonly int _width;

	private readonly int _height;

	private readonly int _samplesPerUnit;

	private readonly byte[] _coverage;

	[SpecialName]
	[CompilerGenerated]
	internal static WatermarkCoverageBitmap GetEmpty()
	{
		return _empty;
	}

	internal WatermarkCoverageBitmap(int P_0, int P_1, int P_2, byte[] P_3)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_width = Math.Max(0, P_0);
		_height = Math.Max(0, P_1);
		_samplesPerUnit = Math.Max(1, P_2);
		_coverage = P_3 ?? Array.Empty<byte>();
	}

	[SpecialName]
	internal int GetWidth()
	{
		return _width;
	}

	[SpecialName]
	internal int GetHeight()
	{
		return _height;
	}

	[SpecialName]
	internal bool HasCoverage()
	{
		if (_width > 0 && _height > 0)
		{
			return _coverage.Length != 0;
		}
		return false;
	}

	internal float SampleBilinearCoverage(float P_0, float P_1)
	{
		if (HasCoverage() && !(P_0 < 0f) && P_1 >= 0f)
		{
			float num = P_0 * (float)_samplesPerUnit;
			float num2 = P_1 * (float)_samplesPerUnit;
			if (!(num < 0f) && !(num2 < 0f) && !(num >= (float)_width) && num2 < (float)_height)
			{
				int num3 = (int)num;
				int num4 = (int)num2;
				int num5 = Math.Min(num3 + 1, _width - 1);
				int num6 = Math.Min(num4 + 1, _height - 1);
				float num7 = num - (float)num3;
				float num8 = num2 - (float)num4;
				int num9 = num4 * _width;
				int num10 = num6 * _width;
				float num11 = (float)(int)_coverage[num9 + num3] * 0.003921569f;
				float num12 = (float)(int)_coverage[num9 + num5] * 0.003921569f;
				float num13 = (float)(int)_coverage[num10 + num3] * 0.003921569f;
				float num14 = (float)(int)_coverage[num10 + num5] * 0.003921569f;
				float num15 = num11 + (num12 - num11) * num7;
				float num16 = num13 + (num14 - num13) * num7;
				return num15 + (num16 - num15) * num8;
			}
			return 0f;
		}
		return 0f;
	}

	static WatermarkCoverageBitmap()
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		_empty = new WatermarkCoverageBitmap(0, 0, 1, Array.Empty<byte>());
	}
}
}
