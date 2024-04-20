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
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Encoders;
using NCode.Jose.Algorithms.KeyManagement;
using NCode.Jose.DataProtection;
using NCode.Jose.Exceptions;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Tests.Algorithms.KeyManagement;

public class AesGcmKeyManagementAlgorithmTests
{
    private DefaultSecretKeyFactory SecretKeyFactory { get; } = new(NoneSecureDataProtector.Singleton);
    private static AesGcmKeyManagementAlgorithm CreateAlgorithm(int? kekSizeBits = null) =>
        new("code", kekSizeBits ?? Random.Shared.Next());

    [Fact]
    public void Code_Valid()
    {
        var algorithm = CreateAlgorithm();
        Assert.Equal("code", algorithm.Code);
    }

    [Fact]
    public void KeyType_Valid()
    {
        var algorithm = CreateAlgorithm();
        Assert.Equal(typeof(SymmetricSecretKey), algorithm.KeyType);
    }

    [Fact]
    public void KeyBitSizes_Valid()
    {
        const int kekSizeBits = 256;

        var algorithm = CreateAlgorithm(kekSizeBits);
        var result = Assert.Single(algorithm.KeyBitSizes);
        Assert.Equal(kekSizeBits, result.MinSize);
        Assert.Equal(kekSizeBits, result.MaxSize);
        Assert.Equal(0, result.SkipSize);
    }

    [Fact]
    public void GetLegalCekByteSizes_Valid()
    {
        var algorithm = CreateAlgorithm();
        var results = algorithm.GetLegalCekByteSizes(Random.Shared.Next());
        var result = Assert.Single(results);
        Assert.Equal(1, result.MinSize);
        Assert.Equal(int.MaxValue, result.MaxSize);
        Assert.Equal(1, result.SkipSize);
    }

    [Fact]
    public void GetEncryptedContentKeySizeBytes_Valid()
    {
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();
        var algorithm = CreateAlgorithm();
        var result = algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);
        Assert.Equal(cekSizeBytes, result);
    }

    [Theory]
    [InlineData(128, 128)]
    [InlineData(128, 256)]
    [InlineData(192, 128)]
    [InlineData(192, 256)]
    [InlineData(256, 128)]
    [InlineData(256, 256)]
    public void RoundTrip_Valid(int kekSizeBits, int cekSizeBits)
    {
        const string keyId = nameof(keyId);

        var algorithm = CreateAlgorithm(kekSizeBits);

        var kekSizeBytes = kekSizeBits >> 3;
        Span<byte> kek = new byte[kekSizeBytes];
        RandomNumberGenerator.Fill(kek);

        var metadata = new KeyMetadata { KeyId = keyId };
        var secretKey = SecretKeyFactory.CreateSymmetric(metadata, kek);
        var headerForWrap = new Dictionary<string, object>();

        var cekSizeBytes = cekSizeBits >> 3;
        Span<byte> cek = new byte[cekSizeBytes];
        Span<byte> encryptedCek = new byte[cekSizeBytes];
        RandomNumberGenerator.Fill(cek);

        var wrapResult = algorithm.TryWrapKey(secretKey, headerForWrap, cek, encryptedCek, out var wrapBytesWritten);
        Assert.True(wrapResult);
        Assert.Equal(cekSizeBytes, wrapBytesWritten);

        var encodedNonce = Assert.IsType<string>(Assert.Contains("iv", headerForWrap));
        var nonceSizeBytes = Base64Url.GetByteCountForDecode(encodedNonce.Length);
        Assert.Equal(96 >> 3, nonceSizeBytes);

        var encodedTag = Assert.IsType<string>(Assert.Contains("tag", headerForWrap));
        var tagSizeBytes = Base64Url.GetByteCountForDecode(encodedTag.Length);
        Assert.Equal(128 >> 3, tagSizeBytes);

        var controlAlgorithm = new global::Jose.AesGcmKeyWrapManagement(kekSizeBits);
        var controlResult = controlAlgorithm.Unwrap(encryptedCek.ToArray(), kek.ToArray(), cekSizeBits, headerForWrap);
        Assert.Equal(controlResult, cek.ToArray());

        var headerForUnwrap = JsonSerializer.SerializeToElement(headerForWrap);
        var unwrapResult = algorithm.TryUnwrapKey(secretKey, headerForUnwrap, encryptedCek, cek, out var unwrapBytesWritten);
        Assert.True(unwrapResult);
        Assert.Equal(cekSizeBytes, unwrapBytesWritten);
        Assert.Equal(controlResult, cek.ToArray());
    }

    [Fact]
    public void ValidateHeaderForUnwrap_MissingIV()
    {
        var iv = new byte[1];
        var tag = new byte[1];
        var header = new JsonObject();

        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            AesGcmKeyManagementAlgorithm.ValidateHeaderForUnwrap(headerForUnwrap, iv, tag));

        Assert.Equal("The JWT header is missing the 'iv' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_InvalidIVSize()
    {
        var iv = new byte[1];
        var tag = new byte[1];
        var header = new JsonObject
        {
            ["iv"] = Base64Url.Encode(iv)
        };

        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            AesGcmKeyManagementAlgorithm.ValidateHeaderForUnwrap(headerForUnwrap, iv, tag));

        Assert.Equal("The 'iv' field in the JWT header has an invalid size.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_InvalidIVValue()
    {
        var iv = new byte[1];
        var tag = new byte[1];
        var header = new JsonObject
        {
            ["iv"] = new string('!', Base64Url.GetCharCountForEncode(96 >> 3))
        };

        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            AesGcmKeyManagementAlgorithm.ValidateHeaderForUnwrap(headerForUnwrap, iv, tag));

        Assert.Equal("Failed to deserialize the 'iv' field from the JWT header.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_MissingTag()
    {
        var iv = new byte[96 >> 3];
        var tag = new byte[1];
        var header = new JsonObject
        {
            ["iv"] = Base64Url.Encode(iv)
        };

        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            AesGcmKeyManagementAlgorithm.ValidateHeaderForUnwrap(headerForUnwrap, iv, tag));

        Assert.Equal("The JWT header is missing the 'tag' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_InvalidTagSize()
    {
        var iv = new byte[96 >> 3];
        var tag = new byte[1];
        var header = new JsonObject
        {
            ["iv"] = Base64Url.Encode(iv),
            ["tag"] = Base64Url.Encode(tag)
        };

        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            AesGcmKeyManagementAlgorithm.ValidateHeaderForUnwrap(headerForUnwrap, iv, tag));

        Assert.Equal("The 'tag' field in the JWT header has an invalid size.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_InvalidTagValue()
    {
        var iv = new byte[96 >> 3];
        var tag = new byte[1];
        var header = new JsonObject
        {
            ["iv"] = Base64Url.Encode(iv),
            ["tag"] = new string('!', Base64Url.GetCharCountForEncode(128 >> 3))
        };

        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            AesGcmKeyManagementAlgorithm.ValidateHeaderForUnwrap(headerForUnwrap, iv, tag));

        Assert.Equal("Failed to deserialize the 'tag' field from the JWT header.", exception.Message);
    }
}
