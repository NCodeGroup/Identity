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
using System.Text;
using System.Text.Json;
using NCode.Jose.Algorithms;
using NCode.Jose.Algorithms.KeyManagement;
using NCode.Jose.Exceptions;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Tests.Algorithms.KeyManagement;

public class Pbes2KeyManagementAlgorithmTests : BaseTests
{
    private Mock<IAesKeyWrap> MockAesKeyWrap { get; }

    public Pbes2KeyManagementAlgorithmTests()
    {
        MockAesKeyWrap = CreateStrictMock<IAesKeyWrap>();
    }

    private Pbes2KeyManagementAlgorithm Create(
        IAesKeyWrap? aesKeyWrap = null,
        string? code = null,
        int? keySizeBits = null,
        int? maxIterationCount = null)
    {
        var actualKeySizeBits = keySizeBits ?? 128;
        var hashAlgorithmName = actualKeySizeBits switch
        {
            128 => HashAlgorithmName.SHA256,
            192 => HashAlgorithmName.SHA384,
            256 => HashAlgorithmName.SHA512,
            _ => throw new ArgumentOutOfRangeException(nameof(keySizeBits))
        };
        return new Pbes2KeyManagementAlgorithm(
            aesKeyWrap ?? MockAesKeyWrap.Object,
            code ?? "code",
            hashAlgorithmName,
            actualKeySizeBits,
            maxIterationCount ?? 310000);
    }

    [Fact]
    public void Code_Valid()
    {
        var algorithm = Create();
        Assert.Equal("code", algorithm.Code);
    }

    [Fact]
    public void KeyType_Valid()
    {
        var algorithm = Create();
        Assert.Equal(typeof(SymmetricSecretKey), algorithm.KeyType);
    }

    [Fact]
    public void KekBitSizes_Valid()
    {
        var algorithm = Create();
        var kekBitSizes = Assert.Single(algorithm.KeyBitSizes);
        Assert.Equal(8, kekBitSizes.MinSize);
        Assert.Equal(int.MaxValue, kekBitSizes.MaxSize);
        Assert.Equal(8, kekBitSizes.SkipSize);
    }

    [Theory]
    [InlineData(128, 16)]
    [InlineData(192, 24)]
    [InlineData(256, 32)]
    public void KeySizeBytes_Valid(int keySizeBits, int keySizeBytes)
    {
        var algorithm = Create(keySizeBits: keySizeBits);
        Assert.Equal(keySizeBytes, algorithm.KeySizeBytes);
    }

    [Fact]
    public void GetLegalCekByteSizes_Valid()
    {
        var expected = Random.Shared.Next();
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();

        MockAesKeyWrap
            .Setup(x => x.GetEncryptedContentKeySizeBytes(cekSizeBytes))
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
            .Setup(x => x.LegalCekByteSizes)
            .Returns(expected)
            .Verifiable();

        var algorithm = Create();
        var kekSizeBits = Random.Shared.Next();
        Assert.Same(expected, algorithm.GetLegalCekByteSizes(kekSizeBits));
    }

    [Fact]
    public void TryWrapKey_GivenMissingAlgHeader_ThenThrows()
    {
        const string password = nameof(password);

        var algorithm = Create();

        using var secretKey = new DefaultSymmetricSecretKey(default, Encoding.UTF8.GetBytes(password));

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
        const string password = nameof(password);

        var algorithm = Create();

        using var secretKey = new DefaultSymmetricSecretKey(default, Encoding.UTF8.GetBytes(password));

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
        const string password = nameof(password);

        var algorithm = Create();

        using var secretKey = new DefaultSymmetricSecretKey(default, Encoding.UTF8.GetBytes(password));

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
        const string password = nameof(password);

        var algorithm = Create();

        using var secretKey = new DefaultSymmetricSecretKey(default, Encoding.UTF8.GetBytes(password));

        var header = new Dictionary<string, object>
        {
            ["alg"] = "anything"
        };

        var contentKey = new byte[512];
        var encryptedContentKey = Array.Empty<byte>();

        MockAesKeyWrap
            .Setup(x => x.LegalCekByteSizes)
            .Returns(new[] { new KeySizes(1, int.MaxValue, 1) })
            .Verifiable();

        MockAesKeyWrap
            .Setup(x => x.GetEncryptedContentKeySizeBytes(contentKey.Length))
            .Returns(1)
            .Verifiable();

        var result = algorithm.TryWrapKey(secretKey, header, contentKey, encryptedContentKey, out var bytesWritten);
        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    [Theory]
    [InlineData(128, 384)]
    [InlineData(128, 512)]
    [InlineData(192, 384)]
    [InlineData(192, 512)]
    [InlineData(256, 384)]
    [InlineData(256, 512)]
    public void RoundTrip_Valid(int keySizeBits, int cekSizeBits)
    {
        const string keyId = nameof(keyId);
        const string alg = nameof(alg);

        var password = Guid.NewGuid().ToString("N");
        var keyBytes = Encoding.UTF8.GetBytes(password);
        var cekSizeBytes = cekSizeBits >> 3;

        var algorithm = Create(
            keySizeBits: keySizeBits,
            aesKeyWrap: AesKeyWrap.Singleton);

        var metadata = new KeyMetadata { KeyId = keyId };
        using var secretKey = new DefaultSymmetricSecretKey(metadata, keyBytes);

        // ReSharper disable once InconsistentNaming
        var p2c = Random.Shared.Next(Pbes2KeyManagementAlgorithm.MinIterationCount, 310000);
        var saltSize = alg.Length + 1 + Pbes2KeyManagementAlgorithm.SaltInputSizeBytes;

        var header = new Dictionary<string, object>
        {
            [nameof(alg)] = alg,
            [nameof(p2c)] = p2c
        };

        var contentKey = new byte[cekSizeBytes];
        RandomNumberGenerator.Fill(contentKey);

        var encryptedContentKey = new byte[cekSizeBytes + 8];
        var decryptedContentKey = new byte[cekSizeBytes];

        var wrapResult = algorithm.TryWrapKey(secretKey, header, contentKey, encryptedContentKey, out var wrapBytesWritten);
        Assert.True(wrapResult);
        Assert.Equal(encryptedContentKey.Length, wrapBytesWritten);
        Assert.Equal(p2c, Assert.IsType<int>(Assert.Contains(nameof(p2c), header)));

        var encodedSaltInput = Assert.IsType<string>(Assert.Contains("p2s", header));
        Assert.Equal(saltSize, encodedSaltInput.Length);

        var controlAlgorithm = new global::Jose.Pbse2HmacShaKeyManagementWithAesKeyWrap(
            keySizeBits,
            new global::Jose.AesKeyWrapManagement(keySizeBits));

        var expectedControl = controlAlgorithm.Unwrap(encryptedContentKey, password, cekSizeBits, header);
        Assert.Equal(expectedControl, contentKey.ToArray());

        var headerForUnwrap = JsonSerializer.SerializeToElement(header);
        var unwrapResult = algorithm.TryUnwrapKey(secretKey, headerForUnwrap, encryptedContentKey, decryptedContentKey, out var unwrapBytesWritten);
        Assert.True(unwrapResult);
        Assert.Equal(contentKey.Length, unwrapBytesWritten);
        Assert.Equal(contentKey, decryptedContentKey);
    }
}
