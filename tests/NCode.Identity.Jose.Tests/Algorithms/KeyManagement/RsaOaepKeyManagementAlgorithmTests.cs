// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Jose;
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Jose.Tests.Algorithms.KeyManagement;

public class RsaOaepKeyManagementAlgorithmTests : BaseTests
{
    private static RemoteDecryptionAlgorithm RsaOaep256Row { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RSA-OAEP-256",
            SecretType = "rsa",
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
        };

    [Fact]
    public void Code_ReturnsCode()
    {
        const string code = nameof(code);

        var algorithm = new RsaOaepKeyManagementAlgorithm(code, HashAlgorithmName.SHA256);
        Assert.Equal(code, algorithm.Code);
    }

    [Fact]
    public void KeyType_ReturnsRsaSecretKey()
    {
        const string code = nameof(code);

        var algorithm = new RsaOaepKeyManagementAlgorithm(code, HashAlgorithmName.SHA256);
        Assert.Equal(typeof(RsaSecretKey), Assert.Single(algorithm.KeyTypes).KeyClrType);
    }

    [Fact]
    public void KeyBitSizes_ReturnsExpected()
    {
        const string code = nameof(code);

        var algorithm = new RsaOaepKeyManagementAlgorithm(code, HashAlgorithmName.SHA256);
        var result = Assert.Single(Assert.Single(algorithm.KeyTypes).KeyBitSizes);
        Assert.Equal(2048, result.MinSize);
        Assert.Equal(16384, result.MaxSize);
        Assert.Equal(64, result.SkipSize);
    }

    public static IEnumerable<object[]> GetCommonRsaKeyParameterTestData()
    {
        yield return new object[] { 2048, HashAlgorithmName.SHA1 };
        yield return new object[] { 2048, HashAlgorithmName.SHA256 };
        yield return new object[] { 2048, HashAlgorithmName.SHA384 };
        yield return new object[] { 2048, HashAlgorithmName.SHA512 };

        yield return new object[] { 3072, HashAlgorithmName.SHA1 };
        yield return new object[] { 3072, HashAlgorithmName.SHA256 };
        yield return new object[] { 3072, HashAlgorithmName.SHA384 };
        yield return new object[] { 3072, HashAlgorithmName.SHA512 };
    }

    public static IEnumerable<object[]> GetLegalCekByteSizesTestData()
    {
        // https://crypto.stackexchange.com/a/42100

        yield return new object[]
        {
            1024,
            HashAlgorithmName.SHA256,
            new[] { new KeySizes(1, 62, 1) },
        };
        yield return new object[]
        {
            1024,
            HashAlgorithmName.SHA384,
            new[] { new KeySizes(1, 30, 1) },
        };

        yield return new object[]
        {
            2048,
            HashAlgorithmName.SHA1,
            new[] { new KeySizes(1, 214, 1) },
        };
        yield return new object[]
        {
            2048,
            HashAlgorithmName.SHA256,
            new[] { new KeySizes(1, 190, 1) },
        };
        yield return new object[]
        {
            2048,
            HashAlgorithmName.SHA384,
            new[] { new KeySizes(1, 158, 1) },
        };
        yield return new object[]
        {
            2048,
            HashAlgorithmName.SHA512,
            new[] { new KeySizes(1, 126, 1) },
        };

        yield return new object[]
        {
            3072,
            HashAlgorithmName.SHA256,
            new[] { new KeySizes(1, 318, 1) },
        };
        yield return new object[]
        {
            3072,
            HashAlgorithmName.SHA384,
            new[] { new KeySizes(1, 286, 1) },
        };
        yield return new object[]
        {
            3072,
            HashAlgorithmName.SHA512,
            new[] { new KeySizes(1, 254, 1) },
        };
    }

    [Theory]
    [MemberData(nameof(GetLegalCekByteSizesTestData))]
    public void GetLegalCekByteSizes_WhenGivenKeySize_ReturnsExpected(
        int kekSizeBits,
        HashAlgorithmName hashAlgorithmName,
        IEnumerable<KeySizes> expected
    )
    {
        const string code = nameof(code);
        var algorithm = new RsaOaepKeyManagementAlgorithm(code, hashAlgorithmName);
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
    public void GetEncryptedContentKeySizeBytes_WhenGivenKeySize_ReturnsExpected(
        int kekSizeBits,
        int expected
    )
    {
        const string code = nameof(code);
        var anyHashAlgorithmName = HashAlgorithmName.SHA256;
        var algorithm = new RsaOaepKeyManagementAlgorithm(code, anyHashAlgorithmName);
        var anyCekSizeBytes = RandomNumberGenerator.GetInt32(int.MaxValue);
        var result = algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, anyCekSizeBytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetCommonRsaKeyParameterTestData))]
    public void WrapKey_WhenGivenValidKey_EncryptsKey(
        int kekSizeBits,
        HashAlgorithmName hashAlgorithmName
    )
    {
        const string code = nameof(code);

        using var key = RSA.Create(kekSizeBits);
        var secretKey = SecretKeyFactory.CreateRsa(default, key);

        var algorithm = new RsaOaepKeyManagementAlgorithm(code, hashAlgorithmName);
        var header = new Dictionary<string, object>();

        var cekSizeBytes = algorithm.GetLegalCekByteSizes(kekSizeBits).Single().MaxSize;
        var encryptedCekSizeBytes = algorithm.GetEncryptedContentKeySizeBytes(
            secretKey.KeySizeBits,
            cekSizeBytes
        );

        Span<byte> cek = new byte[cekSizeBytes];
        var encryptedCekWriter = new ArrayBufferWriter<byte>(encryptedCekSizeBytes);

        RandomNumberGenerator.Fill(cek);

        algorithm.WrapKey(secretKey, header, cek, encryptedCekWriter);
        Assert.Equal(encryptedCekSizeBytes, encryptedCekWriter.WrittenCount);

        var decrypted = key.Decrypt(
            encryptedCekWriter.WrittenSpan.ToArray(),
            RSAEncryptionPadding.CreateOaep(hashAlgorithmName)
        );
        Assert.Equal(Convert.ToBase64String(cek), Convert.ToBase64String(decrypted));
    }

    [Theory]
    [MemberData(nameof(GetCommonRsaKeyParameterTestData))]
    public void RoundTrip_EncryptsAndDecrypts(int kekSizeBits, HashAlgorithmName hashAlgorithmName)
    {
        const string code = nameof(code);
        const string keyId = nameof(keyId);

        var metadata = new KeyMetadata { KeyId = keyId };
        using var key = RSA.Create(kekSizeBits);
        var secretKey = SecretKeyFactory.CreateRsa(metadata, key);

        var algorithm = new RsaOaepKeyManagementAlgorithm(code, hashAlgorithmName);
        var headerForWrap = new Dictionary<string, object>();

        var cekSizeBytes = algorithm.GetLegalCekByteSizes(kekSizeBits).Single().MaxSize;
        var encryptedCekSizeBytes = algorithm.GetEncryptedContentKeySizeBytes(
            secretKey.KeySizeBits,
            cekSizeBytes
        );

        Span<byte> cek = new byte[cekSizeBytes];
        Span<byte> decryptedCek = new byte[cekSizeBytes];
        var encryptedCekWriter = new ArrayBufferWriter<byte>(encryptedCekSizeBytes);

        RandomNumberGenerator.Fill(cek);

        algorithm.WrapKey(secretKey, headerForWrap, cek, encryptedCekWriter);
        Assert.Equal(encryptedCekSizeBytes, encryptedCekWriter.WrittenCount);

        var cekSizeBits = cekSizeBytes << 3;
        var controlAlgorithm = GetControlAlgorithm(hashAlgorithmName);
        if (controlAlgorithm != null)
        {
            var controlResult = controlAlgorithm.Unwrap(
                encryptedCekWriter.WrittenSpan.ToArray(),
                key,
                cekSizeBits,
                headerForWrap
            );
            Assert.Equal(cek.ToArray(), controlResult);
        }

        var headerForUnwrap = JsonSerializer.SerializeToElement(headerForWrap);
        var unwrapResult = algorithm.TryUnwrapKey(
            secretKey,
            headerForUnwrap,
            encryptedCekWriter.WrittenSpan,
            decryptedCek,
            out var unwrapBytesWritten
        );
        Assert.True(unwrapResult);
        Assert.Equal(cekSizeBytes, unwrapBytesWritten);
        Assert.Equal(cek.ToArray(), decryptedCek.ToArray());
    }

    #region UnwrapKeyAsync Tests

    [Fact]
    public async Task UnwrapKeyAsync_RemoteKey_DecryptsThroughTheService()
    {
        const string code = "RSA-OAEP-256";

        using var heldRsa = RSA.Create(2048);
        var provider = CreateStrictMock<IRemoteKeyProvider>();
        RemoteDecryptionRequest? capturedRequest = null;

        provider
            .Setup(x =>
                x.DecryptAsync(
                    It.IsAny<RemoteDecryptionRequest>(),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(
                (RemoteDecryptionRequest request, Memory<byte> plaintext, CancellationToken _) =>
                {
                    capturedRequest = request;
                    var bytes = heldRsa.Decrypt(
                        request.Ciphertext.ToArray(),
                        RSAEncryptionPadding.OaepSHA256
                    );
                    bytes.CopyTo(plaintext);
                    return ValueTask.FromResult(bytes.Length);
                }
            )
            .Verifiable();

        var secretKey = SecretKeyFactory.CreateRsaRemote(
            new KeyMetadata { KeyId = "remote" },
            heldRsa.ExportSubjectPublicKeyInfo(),
            new RemoteKeyBinding
            {
                Provider = provider.Object,
                Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, "remote-key-1"),
                Algorithm = RsaOaep256Row,
            }
        );

        var algorithm = new RsaOaepKeyManagementAlgorithm(code, HashAlgorithmName.SHA256);
        var contentKey = RandomNumberGenerator.GetBytes(32);
        var encryptedContentKeyWriter = new ArrayBufferWriter<byte>(
            algorithm.GetEncryptedContentKeySizeBytes(2048, contentKey.Length)
        );

        var headerForWrap = new Dictionary<string, object>();
        algorithm.WrapKey(secretKey, headerForWrap, contentKey, encryptedContentKeyWriter);

        var header = JsonSerializer.SerializeToElement(headerForWrap);
        var decrypted = new byte[contentKey.Length];

        var bytesWritten = await algorithm.UnwrapKeyAsync(
            secretKey,
            header,
            encryptedContentKeyWriter.WrittenMemory,
            decrypted,
            CancellationToken.None
        );

        Assert.Equal(contentKey.Length, bytesWritten);
        Assert.Equal(contentKey, decrypted);
        Assert.NotNull(capturedRequest);
        Assert.Equal("RSA-OAEP-256", capturedRequest.Algorithm.Algorithm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    [InlineData(257)]
    public async Task UnwrapKeyAsync_RemoteKeyAndAnEncryptedKeyNotTheModulusSize_ThrowsWithoutCallingTheService(
        int length
    )
    {
        const string code = "RSA-OAEP-256";

        using var heldRsa = RSA.Create(2048);
        var provider = CreateStrictMock<IRemoteKeyProvider>();

        var secretKey = SecretKeyFactory.CreateRsaRemote(
            new KeyMetadata { KeyId = "remote" },
            heldRsa.ExportSubjectPublicKeyInfo(),
            new RemoteKeyBinding
            {
                Provider = provider.Object,
                Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, "remote-key-1"),
                Algorithm = RsaOaep256Row,
            }
        );

        var algorithm = new RsaOaepKeyManagementAlgorithm(code, HashAlgorithmName.SHA256);
        var header = JsonSerializer.SerializeToElement(new Dictionary<string, object>());

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await algorithm.UnwrapKeyAsync(
                secretKey,
                header,
                new byte[length],
                new byte[32],
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task UnwrapKeyAsync_LocalKey_UnwrapsSynchronously()
    {
        const string code = nameof(code);

        using var key = RSA.Create(2048);
        var secretKey = SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "local" }, key);

        var algorithm = new RsaOaepKeyManagementAlgorithm(code, HashAlgorithmName.SHA256);
        var contentKey = RandomNumberGenerator.GetBytes(32);
        var encryptedContentKeyWriter = new ArrayBufferWriter<byte>(
            algorithm.GetEncryptedContentKeySizeBytes(2048, contentKey.Length)
        );

        var headerForWrap = new Dictionary<string, object>();
        algorithm.WrapKey(secretKey, headerForWrap, contentKey, encryptedContentKeyWriter);

        var header = JsonSerializer.SerializeToElement(headerForWrap);
        var decrypted = new byte[contentKey.Length];

        var task = algorithm.UnwrapKeyAsync(
            secretKey,
            header,
            encryptedContentKeyWriter.WrittenMemory,
            decrypted,
            CancellationToken.None
        );

        Assert.True(task.IsCompletedSuccessfully);
        var bytesWritten = await task;
        Assert.Equal(contentKey.Length, bytesWritten);
        Assert.Equal(contentKey, decrypted);
    }

    #endregion

    #region TryUnwrapKey Tests

    [Fact]
    public void TryUnwrapKey_RemoteKey_Throws()
    {
        const string code = nameof(code);

        using var heldRsa = RSA.Create(2048);
        var provider = CreateStrictMock<IRemoteKeyProvider>();
        var secretKey = SecretKeyFactory.CreateRsaRemote(
            new KeyMetadata { KeyId = "remote" },
            heldRsa.ExportSubjectPublicKeyInfo(),
            new RemoteKeyBinding
            {
                Provider = provider.Object,
                Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, "remote-key-1"),
                Algorithm = RsaOaep256Row,
            }
        );

        var algorithm = new RsaOaepKeyManagementAlgorithm(code, HashAlgorithmName.SHA256);
        var header = JsonSerializer.SerializeToElement(new Dictionary<string, object>());

        Assert.Throws<JoseInvalidAlgorithmException>(() =>
            algorithm.TryUnwrapKey(secretKey, header, new byte[256], new byte[32], out _)
        );
    }

    #endregion

    private static IKeyManagement? GetControlAlgorithm(HashAlgorithmName hashAlgorithmName)
    {
        if (hashAlgorithmName == HashAlgorithmName.SHA1)
            return new RsaKeyManagement(true);
        if (hashAlgorithmName == HashAlgorithmName.SHA256)
            return new RsaOaepKeyManagement(256);
        if (hashAlgorithmName == HashAlgorithmName.SHA384)
            return new RsaOaepKeyManagement(384);
        if (hashAlgorithmName == HashAlgorithmName.SHA512)
            return new RsaOaepKeyManagement(512);
        return null;
    }
}
