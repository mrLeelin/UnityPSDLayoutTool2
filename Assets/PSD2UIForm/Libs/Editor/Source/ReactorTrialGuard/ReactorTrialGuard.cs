using System;

namespace PsdProtectionGuards
{

internal class ReactorTrialGuard
{
	private static bool _trialCheckStarted;

	internal static ReactorTrialGuard TrialControlFlowSentinel;

	internal static void CheckTrialPeriodOnce()
	{
		TimeSpan timeSpan = default(TimeSpan);
		while (!_trialCheckStarted)
		{
			int num = 0;
			if (GetTrialSentinel() == null)
			{
				goto IL_0057;
			}
			goto IL_005f;
			IL_0084:
			throw new Exception("This assembly is protected by an unregistered version of Eziriz's \".NET Reactor\"! This assembly won't further work.");
			IL_005f:
			switch (num)
			{
			case 3:
				break;
			case 2:
				goto IL_0039;
			default:
				goto IL_0057;
			case 1:
				continue;
			case 4:
				goto IL_0084;
			}
			goto IL_0012;
			IL_0057:
			_trialCheckStarted = true;
			goto IL_0012;
			IL_0012:
			timeSpan = DateTime.Now - new DateTime(2026, 5, 28);
			num = 2;
			if (GetTrialSentinel() != null)
			{
				goto IL_0039;
			}
			goto IL_005f;
			IL_0039:
			if (Math.Sign(timeSpan.Days) >= 14)
			{
				num = 2;
				if (GetTrialSentinel() != null)
				{
					goto IL_005f;
				}
				goto IL_0084;
			}
			break;
		}
	}

	internal static bool IsTrialSentinelNull()
	{
		return TrialControlFlowSentinel == null;
	}

	internal static ReactorTrialGuard GetTrialSentinel()
	{
		return TrialControlFlowSentinel;
	}
}
}
