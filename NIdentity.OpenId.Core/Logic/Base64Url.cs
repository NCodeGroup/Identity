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
using System.Runtime.InteropServices;
using System.Text;

namespace NIdentity.OpenId.Logic;

internal static class Base64Url
{
    // every 3 bytes is converted to 4 chars
    private const int ByteBlockSize = 3;
    private const int CharBlockSize = 4;

    public static int GetCharCountForEncode(int byteCount)
    {
        if (byteCount == 0) return 0;
        var (wholeBlocks, remainderBytes) = Math.DivRem((uint)byteCount, ByteBlockSize);
        Debug.Assert(remainderBytes is 0 or 1 or 2);
        var charCount = (wholeBlocks * CharBlockSize) + (remainderBytes > 0 ? remainderBytes + 1 : 0);
        if (charCount > int.MaxValue)
            throw new OutOfMemoryException();
        return (int)charCount;
    }

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

        // in the following, notice that:
        // '+' has been replaced with '-'
        // '/' has been replaced with '_'
        // '=' has been removed
        fixed (byte* alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"u8)
        {
            int bytePos;

            for (bytePos = 0; bytePos < wholeBlockBytes; bytePos += ByteBlockSize)
            {
                chars[charPos] = (char)alphabet[(bytes[bytePos] & 0xFC) >> 2];
                chars[charPos + 1] = (char)alphabet[((bytes[bytePos] & 0x03) << 4) | ((bytes[bytePos + 1] & 0xF0) >> 4)];
                chars[charPos + 2] = (char)alphabet[((bytes[bytePos + 1] & 0x0F) << 2) | ((bytes[bytePos + 2] & 0xC0) >> 6)];
                chars[charPos + 3] = (char)alphabet[bytes[bytePos + 2] & 0x3F];
                charPos += 4;
            }

            switch (remainderBytes)
            {
                case 1: // two character padding omitted
                    chars[charPos] = (char)alphabet[(bytes[bytePos] & 0xFC) >> 2];
                    chars[charPos + 1] = (char)alphabet[(bytes[bytePos] & 0x03) << 4];
                    charPos += 2;
                    break;

                case 2: // one character padding omitted
                    chars[charPos] = (char)alphabet[(bytes[bytePos] & 0xFC) >> 2];
                    chars[charPos + 1] = (char)alphabet[((bytes[bytePos] & 0x03) << 4) | ((bytes[bytePos + 1] & 0xF0) >> 4)];
                    chars[charPos + 2] = (char)alphabet[(bytes[bytePos + 1] & 0x0F) << 2];
                    charPos += 3;
                    break;
            }
        }

        return charPos;
    }

    public static byte[] Decode(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return Array.Empty<byte>();
        }

        var padding = 0;
        var charCount = input.Length;

        switch (charCount % 4)
        {
            case 0:
                break;

            case 2:
                padding = 2;
                break;

            case 3:
                padding = 1;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(input), "The input value is not a valid base64url string.");
        }

        charCount += padding;
        var byteCount = 3 * (charCount / 4) - padding;

        var bytes = new byte[byteCount];
        var result = TryDecode(input, bytes, out var bytesWritten);
        Debug.Assert(result && bytesWritten == byteCount);

        return bytes;
    }

    public static bool TryDecode(string input, Span<byte> bytes, out int bytesWritten)
    {
        if (string.IsNullOrEmpty(input))
        {
            bytesWritten = 0;
            return true;
        }

        var builder = new StringBuilder(input);

        switch (builder.Length % 4)
        {
            case 0:
                break;

            case 2:
                builder.Append("==");
                break;

            case 3:
                builder.Append('=');
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(input), "The input value is not a valid base64url string.");
        }

        builder.Replace('-', '+');
        builder.Replace('_', '/');

        return Convert.TryFromBase64String(builder.ToString(), bytes, out bytesWritten);
    }
}
