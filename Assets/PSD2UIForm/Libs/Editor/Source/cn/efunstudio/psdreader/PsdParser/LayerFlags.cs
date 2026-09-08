using System;

namespace cn.efunstudio.psdreader.PsdParser
{

[Flags]
internal enum LayerFlags
{
	Transparency = 1,
	Visible = 2,
	Obsolete = 4,
	Unknown0 = 8,
	Unknown1 = 0x10
}
}
