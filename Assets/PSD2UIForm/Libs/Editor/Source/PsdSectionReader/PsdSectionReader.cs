using System.Runtime.CompilerServices;
using PsdProtectionGuards;
using PsdProtectionRuntime;
using PsdBinaryUtilities;

namespace PsdSections
{

internal abstract class PsdSectionReader<TSectionValue>
{
	private readonly PsdBigEndianReader _reader;

	private readonly int _readerVersion;

	private readonly long _sectionPosition;

	private readonly long _sectionLength;

	private readonly object _readContext;

	private TSectionValue _value;

	private bool _hasReadValue;

	internal static object _sectionSentinel;

	public TSectionValue Value
	{
		get
		{
			if (!_hasReadValue && _sectionLength > 0L)
			{
				long num = _reader.Position;
				int num2 = _reader.Version;
				ReadSection();
				_reader.Position = num;
				_reader.Version = num2;
			}
			return _value;
		}
	}

	public long Position => _sectionPosition;

	protected PsdSectionReader(PsdBigEndianReader P_0, bool P_1, object P_2)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		if (P_1)
		{
			_sectionLength = ReadSectionLength(P_0);
		}
		_reader = P_0;
		_readerVersion = P_0.Version;
		_sectionPosition = P_0.Position;
		_readContext = P_2;
		if (!P_1)
		{
			ReadSection();
			_sectionLength = P_0.Position - _sectionPosition;
		}
		_reader.Position = _sectionPosition + _sectionLength;
	}

	protected PsdSectionReader(PsdBigEndianReader P_0, long P_1, object P_2)
	{
		AssemblyProtectionRuntime.ThrowIfDebuggerAttached();
		ReactorTrialGuard.CheckTrialPeriodOnce();
		if (P_1 < 0L)
		{
			throw new PsdInvalidDataException();
		}
		_reader = P_0;
		_sectionLength = P_1;
		_readerVersion = P_0.Version;
		_sectionPosition = P_0.Position;
		_readContext = P_2;
		if (_sectionLength == 0L)
		{
			ReadSection();
			_sectionLength = P_0.Position - _sectionPosition;
		}
		_reader.Position = _sectionPosition + _sectionLength;
	}

	public void ReadSection()
	{
		_reader.Position = _sectionPosition;
		_reader.Version = _readerVersion;
		ReadSectionValue(_reader, _readContext, out _value);
		if (_sectionLength > 0L)
		{
			_reader.Position = _sectionPosition + _sectionLength;
		}
		_hasReadValue = true;
	}

	[SpecialName]
	public long GetSectionLength()
	{
		return _sectionLength;
	}

	[SpecialName]
	public long GetSectionEndPosition()
	{
		return _sectionPosition + _sectionLength;
	}

	protected virtual long ReadSectionLength(PsdBigEndianReader P_0)
	{
		return P_0.ReadVersionedLength();
	}

	protected abstract void ReadSectionValue(PsdBigEndianReader P_0, object P_1, out TSectionValue P_2);

	internal static bool IsSectionSentinelNull()
	{
		return _sectionSentinel == null;
	}

	internal static object GetSectionSentinel()
	{
		return _sectionSentinel;
	}
}
}
