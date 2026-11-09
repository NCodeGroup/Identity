// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Jose.Tests;

/// <summary>
/// Proves that a failed key unwrap is indistinguishable from a failed authentication tag (RFC 7516 §11.5): both fail
/// with the same exception type and message, and neither carries an inner exception.
/// </summary>
public sealed class JoseSerializerJweDecryptionTests : IAsyncLifetime, IDisposable
{
    private const string Payload = "the payload";

    private static RemoteDecryptionAlgorithm RsaOaep256Row { get; } =
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = AlgorithmCodes.KeyManagement.RsaOaep256,
            SecretType = "rsa",
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
        };

    private readonly MockRepository _mockRepository = new(MockBehavior.Strict);
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly ServiceProvider _serviceProvider;

    public JoseSerializerJweDecryptionTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecretsLibrary().AddJoseLibrary();
        _serviceProvider = services.BuildServiceProvider();
    }

    private IJoseSerializer JoseSerializer =>
        _serviceProvider.GetRequiredService<IJoseSerializer>();

    private ISecretKeyFactory SecretKeyFactory =>
        _serviceProvider.GetRequiredService<ISecretKeyFactory>();

    private IAlgorithmCollection Algorithms =>
        _serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>().Collection;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _serviceProvider.DisposeAsync();

    public void Dispose()
    {
        _rsa.Dispose();
        _mockRepository.Verify();
    }

    #region Scaffolding

    private SecretKey CreateRsaKey() =>
        SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, _rsa);

    private SecretKey CreateSymmetricKey(int bitCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(bitCount / 8);
        return SecretKeyFactory.CreateSymmetric(new KeyMetadata { KeyId = "sym" }, bytes);
    }

    // The remote key holds the public half of the local key, so the provider mock stands for the service that holds the private half.
    private SecretKey CreateRemoteRsaKey(Mock<IRemoteKeyProvider> provider) =>
        SecretKeyFactory.CreateRsaRemote(
            new KeyMetadata { KeyId = "remote-rsa" },
            _rsa.ExportSubjectPublicKeyInfo(),
            new RemoteKeyBinding
            {
                Provider = provider.Object,
                Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, "remote-key-1"),
                Algorithm = RsaOaep256Row,
            }
        );

    private ValueTask<string> EncryptAsync(SecretKey secretKey, string keyManagementCode)
    {
        Assert.True(
            Algorithms.TryGetKeyManagementAlgorithm(keyManagementCode, out var keyManagement)
        );
        Assert.True(
            Algorithms.TryGetAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes128Gcm,
                out var contentEncryption
            )
        );
        var credentials = new JoseEncryptionCredentials(
            secretKey,
            keyManagement,
            contentEncryption,
            null
        );
        return JoseSerializer
            .CreateEncoder(new JoseEncryptionOptions(credentials))
            .EncodeAsync(Payload, CancellationToken.None);
    }

    // Replaces one segment of a compact JWE: 1 is the encrypted key, 4 the authentication tag.
    private static string ReplaceSegment(string token, int index, Func<byte[], byte[]> change)
    {
        var segments = token.Split('.');
        var bytes = System.Buffers.Text.Base64Url.DecodeFromChars(segments[index]);
        segments[index] = System.Buffers.Text.Base64Url.EncodeToString(change(bytes));
        return string.Join('.', segments);
    }

    private static byte[] FlipFirstBit(byte[] bytes)
    {
        var copy = bytes.ToArray();
        copy[0] ^= 0x01;
        return copy;
    }

    private async Task AssertUniformFailureAsync(string token, SecretKey secretKey)
    {
        var exception = await Assert.ThrowsAsync<JoseEncryptionException>(async () =>
            await JoseSerializer.DecodeAsync(token, secretKey, CancellationToken.None)
        );

        Assert.Equal("Failed to decrypt the JWE.", exception.Message);
        Assert.Null(exception.InnerException);
    }

    #endregion

    #region Decode Tests

    [Fact]
    public async Task Decode_WhenTheJweIsIntact_ReturnsThePayload()
    {
        var secretKey = CreateRsaKey();
        var token = await EncryptAsync(secretKey, AlgorithmCodes.KeyManagement.RsaOaep256);

        Assert.Equal(
            Payload,
            await JoseSerializer.DecodeAsync(token, secretKey, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Decode_WhenTheRsaEncryptedKeyIsTampered_FailsLikeATamperedTag()
    {
        var secretKey = CreateRsaKey();
        var token = await EncryptAsync(secretKey, AlgorithmCodes.KeyManagement.RsaOaep256);

        await AssertUniformFailureAsync(ReplaceSegment(token, 1, FlipFirstBit), secretKey);
        await AssertUniformFailureAsync(ReplaceSegment(token, 4, FlipFirstBit), secretKey);
    }

    [Fact]
    public async Task Decode_WhenTheAesWrappedKeyFailsItsIntegrityCheck_FailsLikeATamperedTag()
    {
        var secretKey = CreateSymmetricKey(128);
        var token = await EncryptAsync(secretKey, AlgorithmCodes.KeyManagement.Aes128);

        await AssertUniformFailureAsync(ReplaceSegment(token, 1, FlipFirstBit), secretKey);
        await AssertUniformFailureAsync(ReplaceSegment(token, 4, FlipFirstBit), secretKey);
    }

    [Fact]
    public async Task Decode_WhenTheEncryptedKeyHasTheWrongLength_FailsLikeATamperedTag()
    {
        var secretKey = CreateSymmetricKey(128);
        var token = await EncryptAsync(secretKey, AlgorithmCodes.KeyManagement.Aes128);

        await AssertUniformFailureAsync(ReplaceSegment(token, 1, bytes => bytes[..^8]), secretKey);
    }

    [Fact]
    public async Task Decode_WhenTheKeyDoesNotFitTheAlgorithm_ReportsTheConfigurationError()
    {
        var secretKey = CreateRsaKey();
        var token = await EncryptAsync(secretKey, AlgorithmCodes.KeyManagement.RsaOaep256);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await JoseSerializer.DecodeAsync(token, CreateSymmetricKey(128), CancellationToken.None)
        );
    }

    [Fact]
    public async Task DecodeAsync_UnwrapThrowsIOException_Propagates()
    {
        var provider = _mockRepository.Create<IRemoteKeyProvider>();
        provider
            .Setup(x =>
                x.DecryptAsync(
                    It.IsAny<RemoteDecryptionRequest>(),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(() => ValueTask.FromException<int>(new IOException("x")))
            .Verifiable();
        var token = await EncryptAsync(CreateRsaKey(), AlgorithmCodes.KeyManagement.RsaOaep256);

        await Assert.ThrowsAsync<IOException>(async () =>
            await JoseSerializer.DecodeAsync(
                token,
                CreateRemoteRsaKey(provider),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task DecodeAsync_UnwrapThrowsCryptographicException_FailsAtTheTag()
    {
        var provider = _mockRepository.Create<IRemoteKeyProvider>();
        provider
            .Setup(x =>
                x.DecryptAsync(
                    It.IsAny<RemoteDecryptionRequest>(),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(() => ValueTask.FromException<int>(new CryptographicException("x")))
            .Verifiable();
        var token = await EncryptAsync(CreateRsaKey(), AlgorithmCodes.KeyManagement.RsaOaep256);

        var exception = await Assert.ThrowsAsync<JoseEncryptionException>(async () =>
            await JoseSerializer.DecodeAsync(
                token,
                CreateRemoteRsaKey(provider),
                CancellationToken.None
            )
        );

        Assert.Equal("Failed to decrypt the JWE.", exception.Message);
    }

    #endregion
}
