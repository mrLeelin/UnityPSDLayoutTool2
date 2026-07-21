namespace PhotoshopFile
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Reads primitive data types as binary values in in big-endian format
    /// </summary>
    public class BinaryReverseReader : BinaryReader
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryReverseReader"/> class using the given stream.
        /// </summary>
        /// <param name="stream">The stream to read through.</param>
        public BinaryReverseReader(Stream stream)
            : base(stream, Encoding.UTF7)
        {
        }

        /// <summary>
        /// Reads a 16 bit int (2 bytes) from the stream.
        /// </summary>
        /// <returns>The read 16 bit int.</returns>
        public override short ReadInt16()
        {
            short num = base.ReadInt16();
            num = ReverseBytes(num);
            return num;
        }

        /// <summary>
        /// Reads a 32 bit int (4 bytes) from the stream.
        /// </summary>
        /// <returns>The read 32 bit int.</returns>
        public override int ReadInt32()
        {
            int num = base.ReadInt32();
            num = ReverseBytes(num);
            return num;
        }

        /// <summary>
        /// Reads a 64 bit int (8 bytes) from the stream.
        /// </summary>
        /// <returns>The read 64 bit int.</returns>
        public override long ReadInt64()
        {
            long num = base.ReadInt64();
            num = ReverseBytes(num);
            return num;
        }

        /// <summary>
        /// Reads an unsigned 16 bit int (2 bytes) from the stream.
        /// </summary>
        /// <returns>The read unsigned 16 bit int.</returns>
        public override ushort ReadUInt16()
        {
            ushort num = base.ReadUInt16();
            num = ReverseBytes(num);
            return num;
        }

        /// <summary>
        /// Reads an unsigned 32 bit int (4 bytes) from the stream.
        /// </summary>
        /// <returns>The read unsigned 32 bit int.</returns>
        public override uint ReadUInt32()
        {
            uint num = base.ReadUInt32();
            num = ReverseBytes(num);
            return num;
        }

        /// <summary>
        /// Reads an unsigned 64 bit int (8 bytes) from the stream.
        /// </summary>
        /// <returns>The read unsigned 64 bit int.</returns>
        public override ulong ReadUInt64()
        {
            ulong num = base.ReadUInt64();
            num = ReverseBytes(num);
            return num;
        }

        /// <summary>
        /// Reads a pascal string from the stream.
        /// </summary>
        /// <returns>The read string.</returns>
        public string ReadPascalString()
        {
            byte num1 = ReadByte();
            byte[] bytes = ReadBytes(num1);
            if (num1 % 2 == 0)
            {
                ReadByte();
            }

            return new string(Encoding.ASCII.GetChars(bytes));
        }

        /// <summary>
        /// Reads one Photoshop descriptor floating-point token from the stream.
        /// </summary>
        /// <returns>The read floating point number.</returns>
        public float ReadFloat()
        {
            float value;
            return TryReadAsciiFloat(out value) ? value : 0.0f;
        }

        /// <summary>
        /// Reads one ASCII floating-point token from a Photoshop descriptor.
        /// Photoshop separates descriptor values with spaces, brackets, commas,
        /// and newlines; none of those are part of the numeric token.
        /// </summary>
        /// <param name="value">The parsed value.</param>
        /// <returns>True when a valid invariant-culture float was read.</returns>
        public bool TryReadAsciiFloat(out float value)
        {
            value = 0.0f;
            string token;
            if (!TryReadAsciiNumberToken(out token))
            {
                return false;
            }

            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private bool TryReadAsciiNumberToken(out string token)
        {
            token = string.Empty;
            while (BaseStream.Position < BaseStream.Length)
            {
                int current = BaseStream.ReadByte();
                if (current == 32 || current == 9 || current == 10 || current == 13 || current == '[' || current == ',')
                {
                    continue;
                }

                BaseStream.Position--;
                break;
            }

            StringBuilder builder = new StringBuilder();
            while (BaseStream.Position < BaseStream.Length)
            {
                int current = BaseStream.ReadByte();
                if ((current >= '0' && current <= '9') || current == '+' || current == '-' || current == '.' || current == 'e' || current == 'E')
                {
                    builder.Append((char)current);
                    continue;
                }

                break;
            }

            token = builder.ToString();
            return token.Length > 0;
        }

        /// <summary>
        /// Reads a string stored with a null byte preceding each character.
        /// </summary>
        /// <returns>The read string.</returns>
        public override string ReadString()
        {
            StringBuilder str = new StringBuilder();
            try
            {
                while (BaseStream.Position + 1 < BaseStream.Length)
                {
                    // PSD descriptor strings are UTF-16 Big Endian and null-terminated.
                    byte high = ReadByte();
                    byte low = ReadByte();
                    ushort codeUnit = (ushort)((high << 8) | low);
                    if (codeUnit == 0)
                    {
                        break;
                    }

                    str.Append((char)codeUnit);
                }
            }
            catch (ArgumentException)
            {
                UnityEngine.Debug.LogError("An invalid character was found in the string.");
            }

            return str.ToString();
        }

        /// <summary>
        /// Searches through the stream for the given string.  If found, the position in the stream
        /// will be the byte right AFTER the search string.  If it is not found, the position will be the
        /// end of the stream.
        /// </summary>
        /// <param name="search">The string to search for.</param>
        public void Seek(string search)
        {
            TrySeek(search);
        }

        /// <summary>
        /// Searches for a byte sequence without recursively skipping a possible
        /// overlapping match. Photoshop text descriptors contain many similar
        /// keys, so the old recursive search could run to EOF even when the key
        /// existed later in the descriptor.
        /// </summary>
        /// <param name="search">The ASCII string to search for.</param>
        /// <returns>True when the sequence was found.</returns>
        public bool TrySeek(string search)
        {
            return TrySeek(Encoding.ASCII.GetBytes(search));
        }

        /// <summary>
        /// Reverses the bytes in order to convert back and forth between Big and Little Endian.
        /// </summary>
        /// <returns>The number with the reversed bytes.</returns>
        /// <param name="value">The number to reverse the bytes of.</param>
        /// <remarks>
        /// See: http://www.csharp-examples.net/reverse-bytes/
        /// And: http://stackoverflow.com/questions/19560436/bitwise-endian-swap-for-various-types
        /// </remarks>
        private Int16 ReverseBytes(Int16 value)
        {
            return (Int16)ReverseBytes((UInt16)value);
        }

        /// <summary>
        /// Reverses the bytes in order to convert back and forth between Big and Little Endian.
        /// </summary>
        /// <returns>The number with the reversed bytes.</returns>
        /// <param name="value">The number to reverse the bytes of.</param>
        private Int32 ReverseBytes(Int32 value)
        {
            return (Int32)ReverseBytes((UInt32)value);
        }

        /// <summary>
        /// Reverses the bytes in order to convert back and forth between Big and Little Endian.
        /// </summary>
        /// <returns>The number with the reversed bytes.</returns>
        /// <param name="value">The number to reverse the bytes of.</param>
        private Int64 ReverseBytes(Int64 value)
        {
            return (Int64)ReverseBytes((UInt64)value);
        }

        /// <summary>
        /// Reverses the bytes in order to convert back and forth between Big and Little Endian.
        /// </summary>
        /// <returns>The number with the reversed bytes.</returns>
        /// <param name="value">The number to reverse the bytes of.</param>
        private UInt16 ReverseBytes(UInt16 value)
        {
            return (UInt16)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }

        /// <summary>
        /// Reverses the bytes in order to convert back and forth between Big and Little Endian.
        /// </summary>
        /// <returns>The number with the reversed bytes.</returns>
        /// <param name="value">The number to reverse the bytes of.</param>
        private UInt32 ReverseBytes(UInt32 value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
                (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;
        }

        /// <summary>
        /// Reverses the bytes in order to convert back and forth between Big and Little Endian.
        /// </summary>
        /// <returns>The number with the reversed bytes.</returns>
        /// <param name="value">The number to reverse the bytes of.</param>
        private UInt64 ReverseBytes(UInt64 value)
        {
            return (value & 0x00000000000000FFUL) << 56 | (value & 0x000000000000FF00UL) << 40 |
                (value & 0x0000000000FF0000UL) << 24 | (value & 0x00000000FF000000UL) << 8 |
                    (value & 0x000000FF00000000UL) >> 8 | (value & 0x0000FF0000000000UL) >> 24 |
                    (value & 0x00FF000000000000UL) >> 40 | (value & 0xFF00000000000000UL) >> 56;
        }

        /// <summary>
        /// Searches through the stream for the given byte array.  If found, the position in the stream
        /// will be the byte right AFTER the search array.  If it is not found, the position will be the
        /// end of the stream.
        /// </summary>
        /// <param name="search">The byte array sequence to search for in the stream</param>
        private bool TrySeek(byte[] search)
        {
            if (search == null || search.Length == 0)
            {
                return false;
            }

            long start = BaseStream.Position;
            long lastStart = BaseStream.Length - search.Length;
            for (long candidate = start; candidate <= lastStart; candidate++)
            {
                BaseStream.Position = candidate;
                bool match = true;
                for (int index = 0; index < search.Length; index++)
                {
                    if (ReadByte() != search[index])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    // Preserve the legacy reader contract: the stream points
                    // immediately after the search key.
                    BaseStream.Position = candidate + search.Length;
                    return true;
                }
            }

            BaseStream.Position = BaseStream.Length;
            return false;
        }
    }
}
