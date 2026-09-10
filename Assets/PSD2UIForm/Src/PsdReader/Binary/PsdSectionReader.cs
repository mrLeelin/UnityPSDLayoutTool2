using System.Runtime.CompilerServices;
using InvalidPsdFileExceptionNamespace;
using PsdBinaryReaderNamespace;

namespace PsdSectionReaderNamespace
{
    internal abstract class PsdSectionReader<TValue>
    {
        private readonly PsdBinaryReader _reader;

        private readonly int _readerVersion;

        private readonly long _sectionStart;

        private readonly long _sectionLength;

        private readonly object _context;

        private TValue _value;

        private bool _isRead;

        private static object s_ObfuscationSentinel;

        public TValue Value
        {
            get
            {
                if (!_isRead && _sectionLength > 0L)
                {
                    long num = _reader.Position;
                    int num2 = _reader.Version;
                    Read();
                    _reader.Position = num;
                    _reader.Version = num2;
                }
                return _value;
            }
        }

        public long Position => _sectionStart;

        protected PsdSectionReader(PsdBinaryReader psdBinaryReader, bool enabled, object value)
        {
            if (enabled)
            {
                _sectionLength = ReadSectionLength(psdBinaryReader);
            }
            _reader = psdBinaryReader;
            _readerVersion = psdBinaryReader.Version;
            _sectionStart = psdBinaryReader.Position;
            _context = value;
            if (!enabled)
            {
                Read();
                _sectionLength = psdBinaryReader.Position - _sectionStart;
            }
            _reader.Position = _sectionStart + _sectionLength;
        }

        protected PsdSectionReader(PsdBinaryReader psdBinaryReader, long value, object value2)
        {
            if (value < 0L)
            {
                throw new InvalidPsdFileException();
            }
            _reader = psdBinaryReader;
            _sectionLength = value;
            _readerVersion = psdBinaryReader.Version;
            _sectionStart = psdBinaryReader.Position;
            _context = value2;
            if (_sectionLength == 0L)
            {
                Read();
                _sectionLength = psdBinaryReader.Position - _sectionStart;
            }
            _reader.Position = _sectionStart + _sectionLength;
        }

        public void Read()
        {
            _reader.Position = _sectionStart;
            _reader.Version = _readerVersion;
            ReadValue(_reader, _context, out _value);
            if (_sectionLength > 0L)
            {
                _reader.Position = _sectionStart + _sectionLength;
            }
            _isRead = true;
        }

        [SpecialName]
        public long GetSectionLength()
        {
            return _sectionLength;
        }

        [SpecialName]
        public long GetSectionEndPosition()
        {
            return _sectionStart + _sectionLength;
        }

        protected virtual long ReadSectionLength(PsdBinaryReader reader)
        {
            return reader.ReadVersionedLength();
        }

        protected abstract void ReadValue(PsdBinaryReader reader, object context, out TValue result);

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static object GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
