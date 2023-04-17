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
using System.Text;

namespace NIdentity.OpenId.Logic;

internal static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        var builder = new StringBuilder(Convert.ToBase64String(bytes).TrimEnd('='));

        builder.Replace('+', '-');
        builder.Replace('/', '_');

        return builder.ToString();
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
