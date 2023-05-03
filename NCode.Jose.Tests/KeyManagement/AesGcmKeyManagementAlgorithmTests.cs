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
using NCode.Cryptography.Keys;
using NCode.Jose.Exceptions;
using NCode.Jose.KeyManagement;

namespace NCode.Jose.Tests.KeyManagement;

public class AesGcmKeyManagementAlgorithmTests : BaseTests
{
    private Mock<IAesKeyWrap> MockAesKeyWrap { get; }

    public AesGcmKeyManagementAlgorithmTests()
    {
        MockAesKeyWrap = CreateStrictMock<IAesKeyWrap>();
    }

    private Pbes2KeyManagementAlgorithm Create(
        IAesKeyWrap? aesKeyWrap = null,
        string? code = null,
        HashAlgorithmName? hashAlgorithmName = null,
        int? keySizeBits = null,
        int? maxIterationCount = null) =>
        new(
            aesKeyWrap ?? MockAesKeyWrap.Object,
            code ?? "code",
            hashAlgorithmName ?? HashAlgorithmName.SHA256,
            keySizeBits ?? 256,
            maxIterationCount ?? 310000);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    public void KeySizeBytes_Valid(int keySizeBits, int keySizeBytes)
    {
        var algorithm = Create(keySizeBits: keySizeBits);
        Assert.Equal(keySizeBytes, algorithm.KeySizeBytes);
    }

    [Fact]
    public void KekBitSizes_Valid()
    {
        var algorithm = Create();
        var kekBitSizes = Assert.Single(algorithm.KekBitSizes);
        Assert.Equal(8, kekBitSizes.MinSize);
        Assert.Equal(int.MaxValue, kekBitSizes.MaxSize);
        Assert.Equal(8, kekBitSizes.SkipSize);
    }

    [Fact]
    public void GetLegalCekByteSizes_Valid()
    {
        var expected = Random.Shared.Next();
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();

        MockAesKeyWrap
            .Setup(_ => _.GetEncryptedContentKeySizeBytes(cekSizeBytes))
            .Returns(expected)
            .Verifiable();

        var algorithm = Create();
        var result = algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetEncryptedContentKeySizeBytes_Valid()
    {
        var expected = Enumerable.Empty<KeySizes>();

        MockAesKeyWrap
            .Setup(_ => _.LegalCekByteSizes)
            .Returns(expected)
            .Verifiable();

        var algorithm = Create();
        var kekSizeBits = Random.Shared.Next();
        Assert.Same(expected, algorithm.GetLegalCekByteSizes(kekSizeBits));
    }

    [Fact]
    public void TryWrapKey_GivenMissingAlgHeader_ThenThrows()
    {
        const string keyId = nameof(keyId);
        const string password = nameof(password);

        var algorithm = Create();

        using var secretKey = new SymmetricSecretKey(keyId, password);

        var header = new Dictionary<string, object>();

        var contentKey = Array.Empty<byte>();
        var encryptedContentKey = Array.Empty<byte>();

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.TryWrapKey(secretKey, header, contentKey, encryptedContentKey, out _));

        Assert.Equal("The JWT header is missing the 'alg' field.", exception.Message);
    }

    [Fact]
    public void TryWrapKey_GivenTooSmallIterationCount_ThenThrows()
    {
        const string keyId = nameof(keyId);
        const string password = nameof(password);

        var algorithm = Create();

        using var secretKey = new SymmetricSecretKey(keyId, password);

        var header = new Dictionary<string, object>
        {
            ["alg"] = "anything",
            ["p2c"] = 1
        };

        var contentKey = Array.Empty<byte>();
        var encryptedContentKey = Array.Empty<byte>();

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.TryWrapKey(secretKey, header, contentKey, encryptedContentKey, out _));

        Assert.Equal("The 'p2c' field in the JWT header must be at least 1000", exception.Message);
    }

    [Fact]
    public void TryWrapKey_GivenTooLargeIterationCount_ThenThrows()
    {
        const string keyId = nameof(keyId);
        const string password = nameof(password);

        var algorithm = Create();

        using var secretKey = new SymmetricSecretKey(keyId, password);

        var header = new Dictionary<string, object>
        {
            ["alg"] = "anything",
            ["p2c"] = int.MaxValue
        };

        var contentKey = Array.Empty<byte>();
        var encryptedContentKey = Array.Empty<byte>();

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.TryWrapKey(secretKey, header, contentKey, encryptedContentKey, out _));

        Assert.Equal("The 'p2c' field in the JWT header must be at most 310000", exception.Message);
    }


    [Fact]
    public void TryWrapKey_GivenDestinationTooSmall_ThenValid()
    {
        const string keyId = nameof(keyId);
        const string password = nameof(password);

        var algorithm = Create();

        using var secretKey = new SymmetricSecretKey(keyId, password);

        var header = new Dictionary<string, object>
        {
            ["alg"] = "anything"
        };

        var contentKey = new byte[512];
        var encryptedContentKey = Array.Empty<byte>();

        MockAesKeyWrap
            .Setup(_ => _.LegalCekByteSizes)
            .Returns(new[] { new KeySizes(1, int.MaxValue, 1) })
            .Verifiable();

        MockAesKeyWrap
            .Setup(_ => _.GetEncryptedContentKeySizeBytes(contentKey.Length))
            .Returns(1)
            .Verifiable();

        var result = algorithm.TryWrapKey(secretKey, header, contentKey, encryptedContentKey, out var bytesWritten);
        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void RoundTrip_Valid()
    {
        const string keyId = nameof(keyId);
        const string password = nameof(password);
        const string alg = nameof(alg);
        const int cekSize = 512;

        var algorithm = Create(aesKeyWrap: AesKeyWrap.Default);

        using var secretKey = new SymmetricSecretKey(keyId, password);

        // ReSharper disable once InconsistentNaming
        var p2c = Random.Shared.Next(Pbes2KeyManagementAlgorithm.MinIterationCount, 310000);
        var saltSize = alg.Length + 1 + Pbes2KeyManagementAlgorithm.SaltInputSizeBytes;

        IDictionary<string, object> header = new Dictionary<string, object>
        {
            [nameof(alg)] = alg,
            [nameof(p2c)] = p2c
        };

        var contentKey = new byte[cekSize];
        RandomNumberGenerator.Fill(contentKey);

        var encryptedContentKey = new byte[cekSize + 8];
        var decryptedContentKey = new byte[cekSize];

        var wrapResult = algorithm.TryWrapKey(secretKey, header, contentKey, encryptedContentKey, out var wrapBytesWritten);
        Assert.True(wrapResult);
        Assert.Equal(encryptedContentKey.Length, wrapBytesWritten);
        Assert.Equal(p2c, Assert.Contains(nameof(p2c), header));
        Assert.Equal(saltSize, Assert.IsType<string>(Assert.Contains("p2s", header)).Length);

        var unwrapResult = algorithm.TryUnwrapKey(secretKey, header, encryptedContentKey, decryptedContentKey, out var unwrapBytesWritten);
        Assert.True(unwrapResult);
        Assert.Equal(contentKey.Length, unwrapBytesWritten);
        Assert.Equal(Convert.ToBase64String(contentKey), Convert.ToBase64String(decryptedContentKey));
    }
}
