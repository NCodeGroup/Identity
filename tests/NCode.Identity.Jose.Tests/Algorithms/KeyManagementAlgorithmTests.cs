// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Jose.Tests.Algorithms;

// See ADR-1471.
public class KeyManagementAlgorithmTests : BaseTests
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

    private ServiceProvider ServiceProvider { get; }

    private IAlgorithmCollection AlgorithmCollection { get; }

    private Mock<IRemoteKeyProvider> Provider { get; }

    public KeyManagementAlgorithmTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        AlgorithmCollection = ServiceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;

        Provider = CreateStrictMock<IRemoteKeyProvider>();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await ServiceProvider.DisposeAsync();
        await base.DisposeAsyncCore();
    }

    #region UnwrapKeyAsync Tests

    [Fact]
    public async Task UnwrapKeyAsync_LocalKey_UnwrapsSynchronously()
    {
        Assert.True(AlgorithmCollection.TryGetKeyManagementAlgorithm("A128KW", out var algorithm));

        var kek = RandomNumberGenerator.GetBytes(16);
        var secretKey = SecretKeyFactory.CreateSymmetric(new KeyMetadata { KeyId = "local" }, kek);

        var contentKey = RandomNumberGenerator.GetBytes(16);
        var encryptedContentKeyWriter = new ArrayBufferWriter<byte>(
            algorithm.GetEncryptedContentKeySizeBytes(128, contentKey.Length)
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

    [Fact]
    public async Task UnwrapKeyAsync_RemoteKey_Throws()
    {
        Assert.True(AlgorithmCollection.TryGetKeyManagementAlgorithm("A128KW", out var algorithm));

        using var heldRsa = RSA.Create(2048);
        var binding = new RemoteKeyBinding
        {
            Provider = Provider.Object,
            Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, "remote-key-1"),
            Algorithm = RsaOaep256Row,
        };
        var secretKey = SecretKeyFactory.CreateRsaRemote(
            new KeyMetadata { KeyId = "remote" },
            heldRsa.ExportSubjectPublicKeyInfo(),
            binding
        );

        var header = JsonSerializer.SerializeToElement(new Dictionary<string, object>());

        await Assert.ThrowsAsync<JoseInvalidAlgorithmException>(async () =>
            await algorithm.UnwrapKeyAsync(
                secretKey,
                header,
                new byte[24],
                new byte[16],
                CancellationToken.None
            )
        );
    }

    #endregion
}
