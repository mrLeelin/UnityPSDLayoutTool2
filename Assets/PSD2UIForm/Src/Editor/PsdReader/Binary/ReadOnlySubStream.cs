using System;
using System.IO;

namespace ReadOnlySubStreamNamespace
{
    internal class ReadOnlySubStream : Stream
    {
        private readonly Stream _baseStream;

        private readonly long _startOffset;

        private readonly long _length;

        private static ReadOnlySubStream s_ObfuscationSentinel;

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

        public ReadOnlySubStream(Stream stream, long value, long value2)
        {
            _baseStream = stream;
            _startOffset = value;
            _length = value2;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _baseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Current)
            {
                return _baseStream.Seek(offset, origin) - _startOffset;
            }
            return _baseStream.Seek(_startOffset + offset, origin) - _startOffset;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ReadOnlySubStream GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
