using PsdProtectionGuards;
using cn.efunstudio.psdreader.FullSerializer.Internal;
using PsdProtectionRuntime;

namespace cn.efunstudio.psdreader.FullSerializer
{

public struct fsAotVersionInfo
{
	public struct Member
	{
		public string MemberName;

		public string JsonName;

		public string StorageType;

		public string OverrideConverterType;

		internal static object a7Rb68ZdLrLeK6pIVc4R;

		public Member(fsMetaProperty property)
		{
			AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
			ReactorTrialGuard.CheckTrialPeriodOnce();
			MemberName = property.MemberName;
			JsonName = property.JsonName;
			StorageType = property.StorageType.CSharpName(includeNamespace: true);
			OverrideConverterType = null;
			if (property.OverrideConverterType != null)
			{
				OverrideConverterType = property.OverrideConverterType.CSharpName();
			}
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Member))
			{
				return false;
			}
			return this == (Member)obj;
		}

		public override int GetHashCode()
		{
			return MemberName.GetHashCode() + 17 * JsonName.GetHashCode() + 17 * StorageType.GetHashCode() + ((!string.IsNullOrEmpty(OverrideConverterType)) ? (17 * OverrideConverterType.GetHashCode()) : 0);
		}

		public static bool operator ==(Member a, Member b)
		{
			if (a.MemberName == b.MemberName && a.JsonName == b.JsonName && a.StorageType == b.StorageType)
			{
				return a.OverrideConverterType == b.OverrideConverterType;
			}
			return false;
		}

		public static bool operator !=(Member a, Member b)
		{
			return !(a == b);
		}

		internal static bool IUeDEQZd0m1Mqeu3oY01()
		{
			return a7Rb68ZdLrLeK6pIVc4R == null;
		}

		internal static object J4C1dSZdE0KtXufVGaf6()
		{
			return a7Rb68ZdLrLeK6pIVc4R;
		}
	}

	public bool IsConstructorPublic;

	public Member[] Members;
}
}
