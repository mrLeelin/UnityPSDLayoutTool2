using System;
using System.IO;
using PsdProtectionGuards;
using PsdProtectionRuntime;

namespace PsdBinaryUtilities
{

internal class ReadOnlySubstream : Stream
{
	private readonly Stream _baseStream;

	private readonly long _startOffset;

	private readonly long _length;

	public override bool CanRead => true;

	public override bool CanSeek => true;

	public override bool CanWrite => false;

	public override long Length => _length;

	public override long Position
	{
		get
		{
			return _baseStream.Position - _startOffset;
		}
		set
		{
			_baseStream.Position = _startOffset + value;
		}
	}

	public ReadOnlySubstream(Stream P_0, long P_1, long P_2)
		: this(global::Psd2UIForm.Reconstruction.ConstructorGuards.Evaluate(), P_0, P_1, P_2)
	{
	}

	private ReadOnlySubstream(global::Psd2UIForm.Reconstruction.ConstructorGuardToken constructorGuards, Stream P_0, long P_1, long P_2)
		: base()
	{
		_baseStream = P_0;
		_startOffset = P_1;
		_length = P_2;
	}

	public override void Flush()
	{
	}

	public override int Read(byte[] P_0, int P_1, int P_2)
	{
		return _baseStream.Read(P_0, P_1, P_2);
	}

	public override long Seek(long P_0, SeekOrigin P_1)
	{
		if (P_1 == SeekOrigin.Current)
		{
			return _baseStream.Seek(P_0, P_1) - _startOffset;
		}
		return _baseStream.Seek(_startOffset + P_0, P_1) - _startOffset;
	}

	public override void SetLength(long P_0)
	{
		throw new NotImplementedException();
	}

	public override void Write(byte[] P_0, int P_1, int P_2)
	{
		throw new NotImplementedException();
	}
}
}
