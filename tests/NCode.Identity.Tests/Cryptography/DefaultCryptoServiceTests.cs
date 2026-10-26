#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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
using System.Text;
using NCode.Buffers;

namespace NCode.Identity.Cryptography;

public class DefaultCryptoServiceTests
{
    private readonly DefaultCryptoService _service = new([
        new Base64UrlBinaryEncoder(),
        new Base64BinaryEncoder(),
        new HexBinaryEncoder(),
    ]);

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenAnEncodingIsRegisteredTwice_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultCryptoService([new HexBinaryEncoder(), new HexBinaryEncoder()])
        );

        Assert.Contains("'hex'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region GenerateBytes Tests

    [Fact]
    public void GenerateBytes_FillsDestination()
    {
        var destination = new byte[32];

        _service.GenerateBytes(destination);

        // Extremely unlikely to be all zeros after a cryptographic fill.
        Assert.NotEqual(-1, destination.AsSpan().IndexOfAnyExcept((byte)0));
    }

    #endregion

    #region EncodeBinary Tests

    [Fact]
    public void EncodeBinary_WhenBase64_ReturnsBase64()
    {
        byte[] data = [1, 2, 3, 4];

        var result = _service.EncodeBinary(data, BinaryEncodings.Base64);

        Assert.Equal(Convert.ToBase64String(data), result);
    }

    [Fact]
    public void EncodeBinary_WhenHex_ReturnsHex()
    {
        byte[] data = [0xAB, 0xCD, 0xEF];

        var result = _service.EncodeBinary(data, BinaryEncodings.Hex);

        Assert.Equal("ABCDEF", result);
    }

    [Fact]
    public void EncodeBinary_WhenBase64Url_ReturnsUrlSafe()
    {
        byte[] data = [0xFB, 0xFF, 0xFE];

        var result = _service.EncodeBinary(data, BinaryEncodings.Base64Url);

        Assert.DoesNotContain('+', result);
        Assert.DoesNotContain('/', result);
        Assert.DoesNotContain('=', result);
    }

    [Theory]
    [InlineData("base32")]
    [InlineData("HEX")]
    [InlineData("")]
    public void EncodeBinary_WhenTheEncodingIsNotRegistered_Throws(string binaryEncoding)
    {
        byte[] data = [1, 2, 3];

        var exception = Assert.Throws<ArgumentException>(() =>
            _service.EncodeBinary(data, binaryEncoding)
        );

        Assert.Contains($"'{binaryEncoding}'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region GenerateKey Tests

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(128)]
    public void GenerateKey_ReturnsEncodedKeyOfExpectedLength(int byteLength)
    {
        var result = _service.GenerateKey(byteLength, BinaryEncodings.Hex);

        Assert.Equal(byteLength * 2, result.Length);
    }

    [Fact]
    public void GenerateKey_ProducesDifferentKeys()
    {
        var first = _service.GenerateKey(32, BinaryEncodings.Base64Url);
        var second = _service.GenerateKey(32, BinaryEncodings.Base64Url);

        Assert.NotEqual(first, second);
    }

    #endregion

    #region HashValue (bytes) Tests

    [Theory]
    [InlineData("SHA1")]
    [InlineData("SHA256")]
    [InlineData("SHA384")]
    [InlineData("SHA512")]
    public void HashValue_Bytes_MatchesFrameworkHash(string hashAlgorithm)
    {
        byte[] data = "hello world"u8.ToArray();

        var result = _service.HashValue(
            data,
            new HashAlgorithmName(hashAlgorithm),
            BinaryEncodings.Hex
        );

        byte[] expected = hashAlgorithm switch
        {
            // SHA-1 is a supported algorithm of the service under test, so the oracle must name it too.
#pragma warning disable CA5350
            "SHA1" => SHA1.HashData(data),
#pragma warning restore CA5350
            "SHA256" => SHA256.HashData(data),
            "SHA384" => SHA384.HashData(data),
            _ => SHA512.HashData(data),
        };
        Assert.Equal(Convert.ToHexString(expected), result);
    }

    [Fact]
    public void HashValue_Bytes_WhenUnsupportedAlgorithm_Throws()
    {
        byte[] data = [1, 2, 3];

        Assert.ThrowsAny<CryptographicException>(() =>
            _service.HashValue(data, new HashAlgorithmName("unknown"), BinaryEncodings.Hex)
        );
    }

    #endregion

    #region HashValue (string) Tests

    [Fact]
    public void HashValue_String_WithDefaultEncoding_MatchesUtf8()
    {
        const string data = "hello world";

        var result = _service.HashValue(data, HashAlgorithmName.SHA256, BinaryEncodings.Hex);

        var expected = Convert.ToHexString(SHA256.HashData(SecureEncoding.UTF8.GetBytes(data)));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void HashValue_String_WithLargeInput_MatchesUtf8()
    {
        var data = new string('x', 500);

        var result = _service.HashValue(data, HashAlgorithmName.SHA256, BinaryEncodings.Hex);

        var expected = Convert.ToHexString(SHA256.HashData(SecureEncoding.UTF8.GetBytes(data)));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void HashValue_String_WithCustomEncoding_UsesEncoding()
    {
        const string data = "hello";
        var utf16 = new UnicodeEncoding(
            bigEndian: false,
            byteOrderMark: false,
            throwOnInvalidBytes: true
        );

        var result = _service.HashValue(data, HashAlgorithmName.SHA256, BinaryEncodings.Hex, utf16);

        var expected = Convert.ToHexString(SHA256.HashData(utf16.GetBytes(data)));
        Assert.Equal(expected, result);
    }

    #endregion
}
