using System;

namespace cn.efunstudio.psdreader.PsdParser
{

public interface ILinkedLayer
{
	PsdDocument Document { get; }

	Uri AbsoluteUri { get; }

	bool HasDocument { get; }

	Guid ID { get; }

	string Name { get; }

	int Width { get; }

	int Height { get; }
}
}
