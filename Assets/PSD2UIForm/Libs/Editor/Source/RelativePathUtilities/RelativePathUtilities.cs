using System.IO;

namespace PsdPathUtilities
{

internal static class RelativePathUtilities
{
	internal static string GetRelativePath(object P_0, object P_1)
	{
		return Path.GetRelativePath((string)P_0, (string)P_1);
	}
}
}
