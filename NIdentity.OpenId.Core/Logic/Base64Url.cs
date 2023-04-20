#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NIdentity.OpenId.Logic;

internal static class Base64Url
{
    private const int MaxPadCount = 2;
    private const int ByteBlockSize = 3;
    private const int CharBlockSize = 4;

    private static ReadOnlySpan<byte> EncodingMap => "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"u8;

    private static ReadOnlySpan<sbyte> DecodingMap
    {
        get
        {
            const sbyte __ = -1;

            return new sbyte[]
            {
                __, __, __, __, __, __, __, __, __, __, __, __, __, __, __, __,
                __, __, __, __, __, __, __, __, __, __, __, __, __, __, __, __,
                __, __, __, __, __, __, __, __, __, __, __, __, __, 62, __, __,
                52, 53, 54, 55, 56, 57, 58, 59, 60, 61, __, __, __, __, __, __,
                __, 00, 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 13, 14,
                15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, __, __, __, __, 63,
                __, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40,
                41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, __, __, __, __, __,
                __, __, __, __, __, __, __, __, __, __, __, __, __, __, __, __,
                __, __, __, __, __, __, __, __, __, __, __, __, __, __, __, __,
                __, __, __, __, __, __, __, __, __, __, __, __, __, __, __, __,
                __, __, __, __, __, __, __, __, __, __, __, __, __, __, __, __,
                __, __, __, __, __, __, __, __, __, __, __, __, __, __, __, __,
                __, __, __, __, __, __, __, __, __, __, __, __, __, __, __, __,
                __, __, __, __, __, __, __, __, __, __, __, __, __, __, __, __,
                __, __, __, __, __, __, __, __, __, __, __, __, __, __, __, __
            };
        }
    }

    public static int GetCharCountForEncode(int byteCount) =>
        (byteCount * CharBlockSize + MaxPadCount) / ByteBlockSize;

    public static unsafe string Encode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        var byteCount = bytes.Length;
        var charCount = GetCharCountForEncode(byteCount);

        fixed (byte* bytesPtr = &MemoryMarshal.GetReference(bytes))
        {
            return string.Create(charCount, (byteCount, bytesPtr: (IntPtr)bytesPtr), static (chars, state) =>
            {
                fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
                {
                    var charsWritten = Encode(state.byteCount, (byte*)state.bytesPtr, charsPtr);
                    Debug.Assert(charsWritten == chars.Length);
                }
            });
        }
    }

    public static unsafe bool TryEncode(ReadOnlySpan<byte> bytes, Span<char> chars, out int charsWritten)
    {
        var byteCount = bytes.Length;
        if (byteCount == 0)
        {
            charsWritten = 0;
            return true;
        }

        var minCharCount = GetCharCountForEncode(bytes.Length);
        if (chars.Length < minCharCount)
        {
            charsWritten = 0;
            return false;
        }

        fixed (byte* bytesPtr = &MemoryMarshal.GetReference(bytes))
        fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
        {
            charsWritten = Encode(byteCount, bytesPtr, charsPtr);
            return true;
        }
    }

    private static unsafe int Encode(int byteCount, byte* bytes, char* chars)
    {
        var remainderBytes = byteCount % ByteBlockSize;
        var wholeBlockBytes = byteCount - remainderBytes;
        var charPos = 0;

        fixed (byte* map = EncodingMap)
        {
            int bytePos;

            for (bytePos = 0; bytePos < wholeBlockBytes; bytePos += ByteBlockSize)
            {
                chars[charPos] = (char)map[(bytes[bytePos] & 0xFC) >> 2];
                chars[charPos + 1] = (char)map[((bytes[bytePos] & 0x03) << 4) | ((bytes[bytePos + 1] & 0xF0) >> 4)];
                chars[charPos + 2] = (char)map[((bytes[bytePos + 1] & 0x0F) << 2) | ((bytes[bytePos + 2] & 0xC0) >> 6)];
                chars[charPos + 3] = (char)map[bytes[bytePos + 2] & 0x3F];
                charPos += 4;
            }

            switch (remainderBytes)
            {
                case 1: // two character padding omitted
                    chars[charPos] = (char)map[(bytes[bytePos] & 0xFC) >> 2];
                    chars[charPos + 1] = (char)map[(bytes[bytePos] & 0x03) << 4];
                    charPos += 2;
                    break;

                case 2: // one character padding omitted
                    chars[charPos] = (char)map[(bytes[bytePos] & 0xFC) >> 2];
                    chars[charPos + 1] = (char)map[((bytes[bytePos] & 0x03) << 4) | ((bytes[bytePos + 1] & 0xF0) >> 4)];
                    chars[charPos + 2] = (char)map[(bytes[bytePos + 1] & 0x0F) << 2];
                    charPos += 3;
                    break;
            }
        }

        return charPos;
    }

    public static int GetByteCountForDecode(int charCount) =>
        GetByteCountForDecode(charCount, out _);

    private static int GetByteCountForDecode(int charCount, out int remainder)
    {
        var (byteCount, tempRemainder) = Math.DivRem(charCount * ByteBlockSize, CharBlockSize);
        if (tempRemainder > MaxPadCount)
            throw new FormatException("Invalid length for a base64url char array or string.");

        remainder = tempRemainder;
        return byteCount;
    }

    public static byte[] Decode(ReadOnlySpan<char> chars)
    {
        var minDestLength = GetByteCountForDecode(chars.Length, out var remainder);

        var bytes = new byte[minDestLength];
        var result = TryDecode(chars, bytes, minDestLength, remainder, out var bytesWritten);
        Debug.Assert(result && bytesWritten == minDestLength);

        return bytes;
    }

    public static bool TryDecode(ReadOnlySpan<char> chars, Span<byte> bytes, out int bytesWritten)
    {
        var minDestLength = GetByteCountForDecode(chars.Length, out var remainder);
        return TryDecode(chars, bytes, minDestLength, remainder, out bytesWritten);
    }

    private static bool TryDecode(ReadOnlySpan<char> chars, Span<byte> bytes, int minDestLength, int remainder, out int bytesWritten)
    {
        var destLength = bytes.Length;
        if (destLength < minDestLength)
        {
            bytesWritten = 0;
            return false;
        }

        var srcIndex = 0;
        var destIndex = 0;

        ref var src = ref MemoryMarshal.GetReference(chars);
        ref var dest = ref MemoryMarshal.GetReference(bytes);
        ref var map = ref MemoryMarshal.GetReference(DecodingMap);

        // only decode entire blocks
        var srcLengthBlocks = chars.Length & ~0x03;
        while (srcIndex < srcLengthBlocks)
        {
            var result = DecodeFour(ref src, ref srcIndex, ref map);
            if (result < 0)
            {
                throw new FormatException("The input is not a valid Base64Url string as it contains an illegal character.");
            }

            WriteThreeLowOrderBytes(ref dest, ref destIndex, result);
        }

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (remainder == 1)
        {
            var result = DecodeThree(ref src, ref srcIndex, ref map);
            if (result < 0)
            {
                throw new FormatException("The input is not a valid Base64Url string as it contains an illegal character.");
            }

            WriteTwoLowOrderBytes(ref dest, ref destIndex, result);
        }
        else if (remainder == 2)
        {
            var result = DecodeTwo(ref src, ref srcIndex, ref map);
            if (result < 0)
            {
                throw new FormatException("The input is not a valid Base64Url string as it contains an illegal character.");
            }

            WriteOneLowOrderByte(ref dest, ref destIndex, result);
        }

        bytesWritten = destIndex;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DecodeFour(ref char src, ref int srcIndex, ref sbyte map)
    {
        int i0 = Unsafe.Add(ref src, srcIndex++);
        int i1 = Unsafe.Add(ref src, srcIndex++);
        int i2 = Unsafe.Add(ref src, srcIndex++);
        int i3 = Unsafe.Add(ref src, srcIndex++);

        var isInvalid = ((i0 | i1 | i2 | i3) & ~0xFF) != 0;
        if (isInvalid) return -1;

        i0 = Unsafe.Add(ref map, i0);
        i1 = Unsafe.Add(ref map, i1);
        i2 = Unsafe.Add(ref map, i2);
        i3 = Unsafe.Add(ref map, i3);

        i0 <<= 18;
        i1 <<= 12;
        i2 <<= 6;

        i0 |= i3;
        i1 |= i2;

        i0 |= i1;

        return i0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DecodeThree(ref char src, ref int srcIndex, ref sbyte map)
    {
        int i0 = Unsafe.Add(ref src, srcIndex++);
        int i1 = Unsafe.Add(ref src, srcIndex++);
        int i2 = Unsafe.Add(ref src, srcIndex++);

        var isInvalid = ((i0 | i1 | i2) & ~0xFF) != 0;
        if (isInvalid) return -1;

        i0 = Unsafe.Add(ref map, i0);
        i1 = Unsafe.Add(ref map, i1);
        i2 = Unsafe.Add(ref map, i2);

        i0 <<= 18;
        i1 <<= 12;
        i2 <<= 6;

        i0 |= i2;
        i0 |= i1;

        return i0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DecodeTwo(ref char src, ref int srcIndex, ref sbyte map)
    {
        int i0 = Unsafe.Add(ref src, srcIndex++);
        int i1 = Unsafe.Add(ref src, srcIndex++);

        var isInvalid = ((i0 | i1) & ~0xFF) != 0;
        if (isInvalid) return -1;

        i0 = Unsafe.Add(ref map, i0);
        i1 = Unsafe.Add(ref map, i1);

        i0 <<= 18;
        i1 <<= 12;

        i0 |= i1;

        return i0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteThreeLowOrderBytes(ref byte dest, ref int destIndex, int value)
    {
        Unsafe.Add(ref dest, destIndex++) = (byte)(value >> 16);
        Unsafe.Add(ref dest, destIndex++) = (byte)(value >> 8);
        Unsafe.Add(ref dest, destIndex++) = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTwoLowOrderBytes(ref byte dest, ref int destIndex, int value)
    {
        Unsafe.Add(ref dest, destIndex++) = (byte)(value >> 16);
        Unsafe.Add(ref dest, destIndex++) = (byte)(value >> 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteOneLowOrderByte(ref byte dest, ref int destIndex, int value)
    {
        Unsafe.Add(ref dest, destIndex++) = (byte)(value >> 16);
    }
}
