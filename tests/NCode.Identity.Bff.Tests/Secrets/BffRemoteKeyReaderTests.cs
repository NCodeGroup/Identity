// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Secrets;
using NCode.Identity.Jose;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.Bff.Tests.Secrets;

public sealed class BffRemoteKeyReaderTests : BaseTests, IAsyncLifetime
{
    private const string RingName = "ring";

    private FakeRemoteKeyProvider RemoteKeyProvider { get; } = new();

    private ServiceProvider Provider { get; }

    private BffRemoteKeyReader Reader => Provider.GetRequiredService<BffRemoteKeyReader>();

    public BffRemoteKeyReaderTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        serviceCollection.AddSingleton<IRemoteKeyProvider>(RemoteKeyProvider);
        serviceCollection.AddSingleton<IRemoteKeyDescriptorSource>(RemoteKeyProvider);
        serviceCollection.AddSingleton<BffRemoteKeyReader>();
        Provider = serviceCollection.BuildServiceProvider();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await Provider.DisposeAsync();
        RemoteKeyProvider.Dispose();
    }

    private static BffRemoteKeyOptions CreateSigningOptions() =>
        new()
        {
            Provider = FakeRemoteKeyProvider.ProviderName,
            KeyId = FakeRemoteKeyProvider.SigningKeyId,
            Algorithm = "ES256",
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = 256,
            Curve = "P-256",
        };

    private static BffRemoteKeyOptions CreateDecryptionOptions() =>
        new()
        {
            Provider = FakeRemoteKeyProvider.ProviderName,
            KeyId = FakeRemoteKeyProvider.DecryptionKeyId,
            Algorithm = "RSA-OAEP-256",
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 2048,
        };

    #region ReadAsync Tests

    [Fact]
    public async Task ReadAsync_WithASigningKey_ReturnsItsPublicHalfAndSignsAChallenge()
    {
        var key = await Reader.ReadAsync(
            RingName,
            "client-sig",
            CreateSigningOptions(),
            CancellationToken.None
        );
        using var keyDisposal = key as IDisposable;

        Assert.IsAssignableFrom<IRemoteSecretKey>(key);
        Assert.Equal("client-sig", key.KeyId);
        Assert.Equal(SecretKeyUses.Signature, key.Metadata.Use);
        Assert.Equal("ES256", key.Metadata.Algorithm);
        Assert.Equal(1, RemoteKeyProvider.SignatureCount);
        Assert.Equal(0, RemoteKeyProvider.DecryptionCount);
    }

    [Fact]
    public async Task ReadAsync_WithADecryptionKey_ReturnsAnEncryptionKeyAndDecryptsAChallenge()
    {
        var key = await Reader.ReadAsync(
            RingName,
            "client-enc",
            CreateDecryptionOptions(),
            CancellationToken.None
        );
        using var keyDisposal = key as IDisposable;

        Assert.IsAssignableFrom<IRemoteSecretKey>(key);
        Assert.Equal("client-enc", key.KeyId);
        Assert.Equal(SecretKeyUses.Encryption, key.Metadata.Use);
        Assert.Equal("RSA-OAEP-256", key.Metadata.Algorithm);
        Assert.Equal(0, RemoteKeyProvider.SignatureCount);
        Assert.Equal(1, RemoteKeyProvider.DecryptionCount);
    }

    [Fact]
    public async Task ReadAsync_WithAnUnregisteredProvider_ThrowsNamingTheKeyRingAndKey()
    {
        var options = CreateSigningOptions();
        options.Provider = "other-kms";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Reader.ReadAsync(RingName, "client-sig", options, CancellationToken.None)
        );

        Assert.Contains("'client-sig'", exception.Message);
        Assert.Contains($"'{RingName}'", exception.Message);
        Assert.Contains("'other-kms'", exception.Message);
        Assert.IsType<RemoteKeyRefusedException>(exception.InnerException);
    }

    [Fact]
    public async Task ReadAsync_WithAKeyTheKmsLacks_ThrowsNamingTheKeyRingAndKey()
    {
        var options = CreateSigningOptions();
        options.KeyId = "kms-missing";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Reader.ReadAsync(RingName, "client-sig", options, CancellationToken.None)
        );

        Assert.Contains("'client-sig'", exception.Message);
        Assert.Contains($"'{RingName}'", exception.Message);
        Assert.IsType<CryptographicException>(exception.InnerException);
    }

    [Fact]
    public async Task ReadAsync_WithAKeyThatSignsWithAnotherKey_Throws()
    {
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        RemoteKeyProvider.ActualSigningKey = otherKey;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Reader.ReadAsync(
                RingName,
                "client-sig",
                CreateSigningOptions(),
                CancellationToken.None
            )
        );

        Assert.Contains("'client-sig'", exception.Message);
        Assert.Contains($"'{RingName}'", exception.Message);
    }

    [Fact]
    public async Task ReadAsync_WithAKeyThatDecryptsWithAnotherKey_Throws()
    {
        using var otherKey = RSA.Create(2048);
        RemoteKeyProvider.ActualDecryptionKey = otherKey;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Reader.ReadAsync(
                RingName,
                "client-enc",
                CreateDecryptionOptions(),
                CancellationToken.None
            )
        );

        Assert.Contains("'client-enc'", exception.Message);
        Assert.Contains($"'{RingName}'", exception.Message);
    }

    #endregion
}
