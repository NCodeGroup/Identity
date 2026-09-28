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

namespace NCode.Identity.Logic;

public class DefaultCryptoServiceTests
{
    private readonly DefaultCryptoService _service = new();

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

        var result = _service.EncodeBinary(data, BinaryEncodingType.Base64);

        Assert.Equal(Convert.ToBase64String(data), result);
    }

    [Fact]
    public void EncodeBinary_WhenHex_ReturnsHex()
    {
        byte[] data = [0xAB, 0xCD, 0xEF];

        var result = _service.EncodeBinary(data, BinaryEncodingType.Hex);

        Assert.Equal("ABCDEF", result);
    }

    [Fact]
    public void EncodeBinary_WhenBase64Url_ReturnsUrlSafe()
    {
        byte[] data = [0xFB, 0xFF, 0xFE];

        var result = _service.EncodeBinary(data, BinaryEncodingType.Base64Url);

        Assert.DoesNotContain('+', result);
        Assert.DoesNotContain('/', result);
        Assert.DoesNotContain('=', result);
    }

    [Fact]
    public void EncodeBinary_WhenUnsupported_Throws()
    {
        byte[] data = [1, 2, 3];

        Assert.Throws<ArgumentException>(() =>
            _service.EncodeBinary(data, BinaryEncodingType.Unspecified)
        );
    }

    #endregion

    #region GenerateKey Tests

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(128)]
    public void GenerateKey_ReturnsEncodedKeyOfExpectedLength(int byteLength)
    {
        var result = _service.GenerateKey(byteLength, BinaryEncodingType.Hex);

        Assert.Equal(byteLength * 2, result.Length);
    }

    [Fact]
    public void GenerateKey_ProducesDifferentKeys()
    {
        var first = _service.GenerateKey(32, BinaryEncodingType.Base64Url);
        var second = _service.GenerateKey(32, BinaryEncodingType.Base64Url);

        Assert.NotEqual(first, second);
    }

    #endregion

    #region HashValue (bytes) Tests

    [Theory]
    [InlineData(HashAlgorithmType.Sha1)]
    [InlineData(HashAlgorithmType.Sha256)]
    [InlineData(HashAlgorithmType.Sha512)]
    public void HashValue_Bytes_MatchesFrameworkHash(HashAlgorithmType hashAlgorithmType)
    {
        byte[] data = "hello world"u8.ToArray();

        var result = _service.HashValue(data, hashAlgorithmType, BinaryEncodingType.Hex);

        byte[] expected = hashAlgorithmType switch
        {
            HashAlgorithmType.Sha1 => SHA1.HashData(data),
            HashAlgorithmType.Sha256 => SHA256.HashData(data),
            _ => SHA512.HashData(data),
        };
        Assert.Equal(Convert.ToHexString(expected), result);
    }

    [Fact]
    public void HashValue_Bytes_WhenUnsupportedAlgorithm_Throws()
    {
        byte[] data = [1, 2, 3];

        Assert.Throws<ArgumentException>(() =>
            _service.HashValue(data, HashAlgorithmType.Unspecified, BinaryEncodingType.Hex)
        );
    }

    #endregion

    #region HashValue (string) Tests

    [Fact]
    public void HashValue_String_WithDefaultEncoding_MatchesUtf8()
    {
        const string data = "hello world";

        var result = _service.HashValue(data, HashAlgorithmType.Sha256, BinaryEncodingType.Hex);

        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void HashValue_String_WithLargeInput_MatchesUtf8()
    {
        var data = new string('x', 500);

        var result = _service.HashValue(data, HashAlgorithmType.Sha256, BinaryEncodingType.Hex);

        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void HashValue_String_WithCustomEncoding_UsesEncoding()
    {
        const string data = "hello";

        var result = _service.HashValue(
            data,
            HashAlgorithmType.Sha256,
            BinaryEncodingType.Hex,
            Encoding.Unicode
        );

        var expected = Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(data)));
        Assert.Equal(expected, result);
    }

    #endregion
}
