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
using NCode.Cryptography.Keys;
using NCode.Jose.KeyManagement;

namespace NCode.Jose.Tests.KeyManagement;

public class RsaKeyManagementAlgorithmTests : BaseTests
{
    public static IEnumerable<object[]> GetCommonRsaKeyParameterTestData()
    {
        yield return new object[] { 2048, RSAEncryptionPadding.Pkcs1 };
        yield return new object[] { 2048, RSAEncryptionPadding.OaepSHA256 };
        yield return new object[] { 2048, RSAEncryptionPadding.OaepSHA384 };
        yield return new object[] { 2048, RSAEncryptionPadding.OaepSHA512 };

        yield return new object[] { 3072, RSAEncryptionPadding.Pkcs1 };
        yield return new object[] { 3072, RSAEncryptionPadding.OaepSHA256 };
        yield return new object[] { 3072, RSAEncryptionPadding.OaepSHA384 };
        yield return new object[] { 3072, RSAEncryptionPadding.OaepSHA512 };
    }

    public static IEnumerable<object[]> GetLegalCekByteSizesTestData()
    {
        // https://crypto.stackexchange.com/a/42100

        yield return new object[] { 1024, RSAEncryptionPadding.Pkcs1, new[] { new KeySizes(1, 117, 1) } };
        yield return new object[] { 1024, RSAEncryptionPadding.OaepSHA256, new[] { new KeySizes(1, 62, 1) } };
        yield return new object[] { 1024, RSAEncryptionPadding.OaepSHA384, new[] { new KeySizes(1, 30, 1) } };

        yield return new object[] { 2048, RSAEncryptionPadding.Pkcs1, new[] { new KeySizes(1, 245, 1) } };
        yield return new object[] { 2048, RSAEncryptionPadding.OaepSHA256, new[] { new KeySizes(1, 190, 1) } };
        yield return new object[] { 2048, RSAEncryptionPadding.OaepSHA384, new[] { new KeySizes(1, 158, 1) } };
        yield return new object[] { 2048, RSAEncryptionPadding.OaepSHA512, new[] { new KeySizes(1, 126, 1) } };

        yield return new object[] { 3072, RSAEncryptionPadding.Pkcs1, new[] { new KeySizes(1, 373, 1) } };
        yield return new object[] { 3072, RSAEncryptionPadding.OaepSHA256, new[] { new KeySizes(1, 318, 1) } };
        yield return new object[] { 3072, RSAEncryptionPadding.OaepSHA384, new[] { new KeySizes(1, 286, 1) } };
        yield return new object[] { 3072, RSAEncryptionPadding.OaepSHA512, new[] { new KeySizes(1, 254, 1) } };
    }

    [Theory]
    [MemberData(nameof(GetLegalCekByteSizesTestData))]
    public void GetLegalCekByteSizes_Valid(int kekSizeBits, RSAEncryptionPadding padding, IEnumerable<KeySizes> expected)
    {
        const string code = nameof(code);
        var algorithm = new RsaKeyManagementAlgorithm(code, padding);
        var result = algorithm.GetLegalCekByteSizes(kekSizeBits);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData(1024, 128)]
    [InlineData(2048, 256)]
    [InlineData(3072, 384)]
    [InlineData(4096, 512)]
    [InlineData(4095, 512)]
    [InlineData(4094, 512)]
    [InlineData(4093, 512)]
    [InlineData(4092, 512)]
    [InlineData(4091, 512)]
    [InlineData(4090, 512)]
    [InlineData(4089, 512)]
    [InlineData(4088, 511)]
    [InlineData(4087, 511)]
    [InlineData(4086, 511)]
    [InlineData(4085, 511)]
    public void GetEncryptedContentKeySizeBytes_Valid(int kekSizeBits, int expected)
    {
        const string code = nameof(code);
        var anyPadding = RSAEncryptionPadding.Pkcs1;
        var algorithm = new RsaKeyManagementAlgorithm(code, anyPadding);
        var anyCekSizeBytes = RandomNumberGenerator.GetInt32(int.MaxValue);
        var result = algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, anyCekSizeBytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetCommonRsaKeyParameterTestData))]
    public void TryWrapKey_Valid(int kekSizeBits, RSAEncryptionPadding padding)
    {
        const string code = nameof(code);
        const string keyId = nameof(keyId);

        using var key = RSA.Create(kekSizeBits);
        using var secretKey = RsaSecretKey.Create(keyId, key);

        var algorithm = new RsaKeyManagementAlgorithm(code, padding);

        var cekSizeBytes = algorithm.GetLegalCekByteSizes(kekSizeBits).Single().MaxSize;
        var keySizeBytes = algorithm.GetEncryptedContentKeySizeBytes(secretKey.KeySizeBits, cekSizeBytes);

        var header = new Dictionary<string, object>();
        var contentKey = new byte[cekSizeBytes];
        var encryptedContentKey = new byte[keySizeBytes];

        RandomNumberGenerator.Fill(contentKey);

        var result = algorithm.TryWrapKey(secretKey, header, contentKey, encryptedContentKey, out var bytesWritten);
        Assert.True(result);
        Assert.Equal(keySizeBytes, bytesWritten);

        var decrypted = key.Decrypt(encryptedContentKey, padding);
        Assert.Equal(Convert.ToBase64String(contentKey), Convert.ToBase64String(decrypted));
    }

    [Fact]
    public void TryWrapKey_GivenTooSmallDestination_ThenValid()
    {
        const string code = nameof(code);
        const string keyId = nameof(keyId);
        const int kekSizeBits = 2048;
        const int kekSizeBytes = kekSizeBits >> 3;

        using var key = RSA.Create(kekSizeBits);
        using var secretKey = RsaSecretKey.Create(keyId, key);

        var anyPadding = RSAEncryptionPadding.Pkcs1;
        var algorithm = new RsaKeyManagementAlgorithm(code, anyPadding);

        var header = new Dictionary<string, object>();
        var contentKey = new byte[32];
        var encryptedContentKey = new byte[kekSizeBytes - 1];

        var result = algorithm.TryWrapKey(secretKey, header, contentKey, encryptedContentKey, out var bytesWritten);
        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    [Theory]
    [MemberData(nameof(GetCommonRsaKeyParameterTestData))]
    public void RoundTrip_Valid(int kekSizeBits, RSAEncryptionPadding padding)
    {
        const string code = nameof(code);
        const string keyId = nameof(keyId);

        using var key = RSA.Create(kekSizeBits);
        using var secretKey = RsaSecretKey.Create(keyId, key);

        var algorithm = new RsaKeyManagementAlgorithm(code, padding);

        var cekSizeBytes = algorithm.GetLegalCekByteSizes(kekSizeBits).Single().MaxSize;
        var keySizeBytes = algorithm.GetEncryptedContentKeySizeBytes(secretKey.KeySizeBits, cekSizeBytes);

        var header = new Dictionary<string, object>();
        var contentKey = new byte[cekSizeBytes];
        var encryptedContentKey = new byte[keySizeBytes + 16];

        RandomNumberGenerator.Fill(contentKey);

        var wrapResult = algorithm.TryWrapKey(
            secretKey,
            header,
            contentKey,
            encryptedContentKey,
            out var encryptedBytes);
        Assert.True(wrapResult);
        Assert.Equal(keySizeBytes, encryptedBytes);

        var decryptedContentKey = new byte[keySizeBytes + 32];
        var unwrapResult = algorithm.TryUnwrapKey(
            secretKey,
            header,
            encryptedContentKey.AsSpan(0, encryptedBytes),
            decryptedContentKey,
            out var decryptedBytes);
        Assert.True(unwrapResult);
        Assert.Equal(cekSizeBytes, decryptedBytes);
        Assert.Equal(Convert.ToBase64String(contentKey), Convert.ToBase64String(decryptedContentKey.AsSpan(0, decryptedBytes)));
    }
}
