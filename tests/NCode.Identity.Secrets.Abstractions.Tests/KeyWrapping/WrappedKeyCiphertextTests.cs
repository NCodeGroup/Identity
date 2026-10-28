#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;

namespace NCode.Identity.Secrets.KeyWrapping;

public class WrappedKeyCiphertextTests
{
    private const string EmptyMessage = "The wrapped key is empty.";
    private const string NotBase64UrlMessage = "The wrapped key is not valid base64url text.";

    #region GetByteCount Tests

    [Theory]
    [InlineData("AA", 1)]
    [InlineData("AAA", 2)]
    [InlineData("AAAA", 3)]
    [InlineData("AAAAAA", 4)]
    public void GetByteCount_ValidLength_ReturnsDecodedLength(string text, int expected)
    {
        var result = WrappedKeyCiphertext.GetByteCount(text);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetByteCount_Empty_ThrowsCryptographicException()
    {
        var exception = Assert.Throws<CryptographicException>(() =>
            WrappedKeyCiphertext.GetByteCount(string.Empty)
        );

        Assert.Equal(EmptyMessage, exception.Message);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AAAAA")]
    public void GetByteCount_ImpossibleLength_ThrowsCryptographicException(string text)
    {
        var exception = Assert.Throws<CryptographicException>(() =>
            WrappedKeyCiphertext.GetByteCount(text)
        );

        Assert.Equal(NotBase64UrlMessage, exception.Message);
    }

    #endregion

    #region Decode Tests

    [Fact]
    public void Decode_ValidText_WritesBytesAndReturnsCount()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var text = Base64Url.EncodeToString(bytes);
        var destination = new byte[WrappedKeyCiphertext.GetByteCount(text)];

        var result = WrappedKeyCiphertext.Decode(text, destination);

        Assert.Equal(32, result);
        Assert.Equal(bytes, destination);
    }

    [Fact]
    public void Decode_Empty_ThrowsCryptographicException()
    {
        var destination = new byte[4];

        var exception = Assert.Throws<CryptographicException>(() =>
            WrappedKeyCiphertext.Decode(string.Empty, destination)
        );

        Assert.Equal(EmptyMessage, exception.Message);
    }

    [Theory]
    [InlineData("AA!A")]
    [InlineData("!!!!")]
    [InlineData("AA+A")]
    [InlineData("AA/A")]
    public void Decode_IllegalCharacter_ThrowsCryptographicExceptionWithFormatException(string text)
    {
        var destination = new byte[4];

        var exception = Assert.Throws<CryptographicException>(() =>
            WrappedKeyCiphertext.Decode(text, destination)
        );

        Assert.Equal(NotBase64UrlMessage, exception.Message);
        Assert.IsType<FormatException>(exception.InnerException);
    }

    [Fact]
    public void Decode_Padding_ThrowsCryptographicException()
    {
        const string text = "AA==";
        var destination = new byte[4];

        var exception = Assert.Throws<CryptographicException>(() =>
            WrappedKeyCiphertext.Decode(text, destination)
        );

        Assert.Equal(NotBase64UrlMessage, exception.Message);
    }

    [Fact]
    public void Decode_ImpossibleLength_ThrowsCryptographicException()
    {
        const string text = "AAAAA";
        var destination = new byte[4];

        var exception = Assert.Throws<CryptographicException>(() =>
            WrappedKeyCiphertext.Decode(text, destination)
        );

        Assert.Equal(NotBase64UrlMessage, exception.Message);
    }

    [Fact]
    public void Decode_DestinationTooShort_ThrowsArgumentException()
    {
        var text = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var destination = new byte[31];

        var exception = Assert.Throws<ArgumentException>(() =>
            WrappedKeyCiphertext.Decode(text, destination)
        );

        Assert.Equal("destination", exception.ParamName);
    }

    [Fact]
    public void Decode_WriterValidText_WritesBytesAndAdvances()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var text = Base64Url.EncodeToString(bytes);
        var writer = new ArrayBufferWriter<byte>();

        WrappedKeyCiphertext.Decode(text, ref writer);

        Assert.Equal(32, writer.WrittenCount);
        Assert.Equal(bytes, writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void Decode_WriterEmpty_ThrowsCryptographicExceptionWithoutAdvancing()
    {
        var writer = new ArrayBufferWriter<byte>();

        var exception = Assert.Throws<CryptographicException>(() =>
        {
            WrappedKeyCiphertext.Decode(string.Empty, ref writer);
        });

        Assert.Equal(EmptyMessage, exception.Message);
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void Decode_WriterIllegalCharacter_ThrowsCryptographicExceptionWithoutAdvancing()
    {
        var writer = new ArrayBufferWriter<byte>();

        var exception = Assert.Throws<CryptographicException>(() =>
        {
            WrappedKeyCiphertext.Decode("AA!A", ref writer);
        });

        Assert.Equal(NotBase64UrlMessage, exception.Message);
        Assert.Equal(0, writer.WrittenCount);
    }

    #endregion
}
