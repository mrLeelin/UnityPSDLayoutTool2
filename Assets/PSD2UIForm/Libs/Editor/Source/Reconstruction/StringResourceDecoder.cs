using System;
using System.IO;

namespace Psd2UIForm.Reconstruction
{

internal static class StringResourceDecoder
{
    internal static byte[] Decode(Stream stream)
    {
        var reader = new BinaryReader(stream);
        reader.BaseStream.Position = 0;
        byte[] encrypted = reader.ReadBytes(unchecked((int)reader.BaseStream.Length));
        reader.Close();
        return Decode(encrypted);
    }

    internal static byte[] Decode(byte[] encrypted)
    {
        return Decode(encrypted, CreateKey(null, out _));
    }

    internal static byte[] CreateKey(byte[] publicKeyToken, out byte[] initializationVector)
    {
        byte[] key =
        {
            167, 115, 50, 5, 6, 30, 210, 225,
            242, 22, 147, 223, 4, 212, 12, 201,
            177, 51, 25, 191, 107, 181, 74, 252,
            0, 81, 139, 99, 196, 175, 211, 23
        };
        initializationVector = new byte[]
        {
            5, 80, 90, 132, 252, 200, 22, 191,
            34, 86, 217, 86, 217, 59, 215, 15
        };
        Array.Reverse(initializationVector);
        if (publicKeyToken != null && publicKeyToken.Length != 0)
        {
            for (int tokenIndex = 0; tokenIndex < 8; tokenIndex++)
            {
                initializationVector[tokenIndex * 2 + 1] = publicKeyToken[tokenIndex];
            }
        }
        for (int vectorIndex = 0; vectorIndex < initializationVector.Length; vectorIndex++)
        {
            key[vectorIndex] ^= initializationVector[vectorIndex];
        }
        return key;
    }

    internal static byte[] Decode(byte[] encrypted, byte[] key)
    {
        byte[] decoded = new byte[encrypted.Length];
        uint accumulator = 0;
        unchecked
        {
            for (int offset = 0; offset < encrypted.Length; offset += 4)
            {
                int keyOffset = offset % key.Length;
                uint keyWord = ReadWord(key, keyOffset, 4);
                uint intermediate = accumulator + keyWord;
                uint mixed = (uint)((ulong)(intermediate * intermediate) % 3552659686UL);
                mixed ^= mixed << 13;
                mixed += 1554755323;
                mixed ^= mixed >> 3;
                mixed += 3428374719;
                mixed ^= mixed << 17;
                mixed += 3103922918;
                mixed += 1618460672;
                accumulator = intermediate + mixed;
                int remaining = Math.Min(4, encrypted.Length - offset);
                uint value = ReadWord(encrypted, offset, remaining) ^ accumulator;
                for (int byteIndex = 0; byteIndex < remaining; byteIndex++)
                {
                    decoded[offset + byteIndex] = (byte)(value >> (byteIndex * 8));
                }
            }
        }
        return decoded;
    }

    private static uint ReadWord(byte[] data, int offset, int count)
    {
        uint value = 0;
        for (int byteIndex = 0; byteIndex < count; byteIndex++)
        {
            value |= (uint)data[offset + byteIndex] << (byteIndex * 8);
        }
        return value;
    }
}
}
