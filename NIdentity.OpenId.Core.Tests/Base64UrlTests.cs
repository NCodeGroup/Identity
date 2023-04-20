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

using System.Security.Cryptography;
using NIdentity.OpenId.Logic;
using Xunit;

namespace NIdentity.OpenId.Core.Tests;

public class Base64UrlTests : BaseTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    [InlineData(4, 6)]
    [InlineData(5, 7)]
    [InlineData(6, 8)]
    [InlineData(7, 10)]
    [InlineData(8, 11)]
    [InlineData(9, 12)]
    [InlineData(10, 14)]
    public void GetCharCountForEncode_Valid(int byteCount, int expected)
    {
        var result = Base64Url.GetCharCountForEncode(byteCount);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(1024)]
    [InlineData(1024 + 1)]
    [InlineData(1024 + 2)]
    [InlineData(1024 + 3)]
    [InlineData(1024 + 4)]
    public void Encode_Valid(int byteCount)
    {
        var bytes = new byte[byteCount].AsSpan();
        RandomNumberGenerator.Fill(bytes);

        var expected = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var result = Base64Url.Encode(bytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(1024)]
    [InlineData(1024 + 1)]
    [InlineData(1024 + 2)]
    [InlineData(1024 + 3)]
    [InlineData(1024 + 4)]
    public void TryEncode_Valid(int byteCount)
    {
        var bytes = new byte[byteCount].AsSpan();
        RandomNumberGenerator.Fill(bytes);

        var expected = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var charLength = Base64Url.GetCharCountForEncode(bytes.Length);
        Assert.Equal(expected.Length, charLength);
        var chars = new char[charLength];

        var result = Base64Url.TryEncode(bytes, chars, out var charsWritten);
        Assert.True(result);
        Assert.Equal(charLength, charsWritten);
        Assert.Equal(expected, new string(chars));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, -1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, -1)]
    [InlineData(6, 4)]
    [InlineData(7, 5)]
    [InlineData(8, 6)]
    [InlineData(9, -1)]
    [InlineData(10, 7)]
    [InlineData(11, 8)]
    [InlineData(12, 9)]
    [InlineData(13, -1)]
    [InlineData(14, 10)]
    public void GetByteCountForDecode_Valid(int charCount, int expected)
    {
        if (expected == -1)
        {
            Assert.Throws<FormatException>(() =>
                Base64Url.GetByteCountForDecode(charCount));
        }
        else
        {
            var result = Base64Url.GetByteCountForDecode(charCount);
            Assert.Equal(expected, result);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(1024)]
    [InlineData(1024 + 1)]
    [InlineData(1024 + 2)]
    [InlineData(1024 + 3)]
    [InlineData(1024 + 4)]
    public void Decode_Valid(int byteCount)
    {
        var bytes = new byte[byteCount].AsSpan();
        RandomNumberGenerator.Fill(bytes);

        var base64 = Convert.ToBase64String(bytes);
        var encoded = base64.TrimEnd('=').Replace('+', '-').Replace('/', '_').AsSpan();

        var decoded = Base64Url.Decode(encoded);
        Assert.Equal(base64, Convert.ToBase64String(decoded));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(1024)]
    [InlineData(1024 + 1)]
    [InlineData(1024 + 2)]
    [InlineData(1024 + 3)]
    [InlineData(1024 + 4)]
    public void TryDecode_Valid(int byteCount)
    {
        var bytes = new byte[byteCount].AsSpan();
        RandomNumberGenerator.Fill(bytes);

        var base64 = Convert.ToBase64String(bytes);
        var encoded = base64.TrimEnd('=').Replace('+', '-').Replace('/', '_').AsSpan();

        var decoded = new byte[byteCount];
        var result = Base64Url.TryDecode(encoded, decoded, out var bytesWritten);
        Assert.True(result);
        Assert.Equal(byteCount, bytesWritten);
        Assert.Equal(base64, Convert.ToBase64String(decoded));
    }
}
