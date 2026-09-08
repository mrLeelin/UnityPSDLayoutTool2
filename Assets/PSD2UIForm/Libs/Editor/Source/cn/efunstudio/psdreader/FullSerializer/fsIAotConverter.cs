using System;

namespace cn.efunstudio.psdreader.FullSerializer
{

public interface fsIAotConverter
{
	Type ModelType { get; }

	fsAotVersionInfo VersionInfo { get; }
}
}
