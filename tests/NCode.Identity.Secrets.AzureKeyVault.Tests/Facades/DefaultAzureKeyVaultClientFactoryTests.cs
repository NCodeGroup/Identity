// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using NCode.Identity.Secrets.AzureKeyVault.Facades;
using NCode.Identity.Secrets.KeyWrapping;
using Xunit;

namespace NCode.Identity.Secrets.AzureKeyVault.Tests.Facades;

public class DefaultAzureKeyVaultClientFactoryTests
{
    private const string KeyId = "https://contoso.vault.azure.net/keys/ncode-identity-root";

    private const string KeyVersionId = KeyId + "/0123456789abcdef0123456789abcdef";

    private const string DirectoryId = "11111111-2222-3333-4444-555555555555";

    private const string OtherDirectoryId = "66666666-7777-8888-9999-aaaaaaaaaaaa";

    private static Uri VaultUri { get; } = new("https://contoso.vault.azure.net");

    private static Uri OtherVaultUri { get; } = new("https://fabrikam.vault.azure.net");

    private static DefaultAzureKeyVaultClientFactory CreateFactory() => new(TimeProvider.System);

    // The client that the operation receives, which the factory hands out for the key and access.
    private static async Task<CryptographyClient> GetClientAsync(
        DefaultAzureKeyVaultClientFactory factory,
        string keyId,
        KeyAccess? access
    ) =>
        await factory.ExecuteAsync(
            keyId,
            access,
            (client, _) => ValueTask.FromResult(client),
            CancellationToken.None
        );

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_KeyVersionId_RunsOperationsWithTheSameClient()
    {
        using var factory = CreateFactory();

        var first = await GetClientAsync(factory, KeyVersionId, access: null);
        var second = await GetClientAsync(factory, KeyVersionId, access: null);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task ExecuteAsync_VersionlessKeyId_RunsOperationsWithNewClients()
    {
        using var factory = CreateFactory();

        var first = await GetClientAsync(factory, KeyId, access: null);
        var second = await GetClientAsync(factory, KeyId, access: null);

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task ExecuteAsync_SameDirectoryAndKeyVersionId_RunsOperationsWithTheSameClient()
    {
        using var factory = CreateFactory();

        var first = await GetClientAsync(
            factory,
            KeyVersionId,
            new KeyAccess(DirectoryId, "tenant:acme")
        );
        var second = await GetClientAsync(
            factory,
            KeyVersionId,
            new KeyAccess(DirectoryId, "tenant:other")
        );

        Assert.Same(first, second);
    }

    [Fact]
    public async Task ExecuteAsync_KeyVersionIdUnderDifferentDirectories_RunsOperationsWithDifferentClients()
    {
        using var factory = CreateFactory();

        var first = await GetClientAsync(
            factory,
            KeyVersionId,
            new KeyAccess(DirectoryId, "tenant:acme")
        );
        var second = await GetClientAsync(
            factory,
            KeyVersionId,
            new KeyAccess(OtherDirectoryId, "tenant:acme")
        );
        var own = await GetClientAsync(factory, KeyVersionId, access: null);

        Assert.NotSame(first, second);
        Assert.NotSame(first, own);
        Assert.NotSame(second, own);
    }

    [Fact]
    public async Task ExecuteAsync_DirectoryWithVersionlessKeyId_RunsOperationsWithNewClients()
    {
        using var factory = CreateFactory();

        var first = await GetClientAsync(factory, KeyId, new KeyAccess(DirectoryId, "tenant:acme"));
        var second = await GetClientAsync(
            factory,
            KeyId,
            new KeyAccess(DirectoryId, "tenant:acme")
        );

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task ExecuteAsync_OperationFailsForAnotherDirectory_KeepsTheClientOfTheDirectory()
    {
        using var factory = CreateFactory();
        var access = new KeyAccess(DirectoryId, "tenant:acme");
        var failure = new InvalidOperationException("The directory has not consented.");
        var before = await GetClientAsync(factory, KeyVersionId, access);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            factory
                .ExecuteAsync<CryptographyClient>(
                    KeyVersionId,
                    new KeyAccess(OtherDirectoryId, "tenant:acme"),
                    (_, _) => throw failure,
                    CancellationToken.None
                )
                .AsTask()
        );
        var after = await GetClientAsync(factory, KeyVersionId, access);

        Assert.Same(failure, exception);
        Assert.Same(before, after);
    }

    [Fact]
    public async Task ExecuteAsync_NullOperation_Throws()
    {
        using var factory = CreateFactory();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory
                .ExecuteAsync<CryptographyClient>(
                    KeyVersionId,
                    access: null,
                    null!,
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    #endregion

    #region ExecuteKeyClientAsync Tests

    private static async Task<KeyClient> GetKeyClientAsync(
        DefaultAzureKeyVaultClientFactory factory,
        Uri vaultUri
    ) =>
        await factory.ExecuteKeyClientAsync(
            vaultUri,
            (client, _) => ValueTask.FromResult(client),
            CancellationToken.None
        );

    [Fact]
    public async Task ExecuteKeyClientAsync_SameVault_RunsOperationsWithTheSameClient()
    {
        using var factory = CreateFactory();

        var first = await GetKeyClientAsync(factory, VaultUri);
        var second = await GetKeyClientAsync(factory, VaultUri);

        Assert.Same(first, second);
        Assert.Equal(VaultUri, first.VaultUri);
    }

    [Fact]
    public async Task ExecuteKeyClientAsync_DifferentVaults_RunsOperationsWithDifferentClients()
    {
        using var factory = CreateFactory();

        var first = await GetKeyClientAsync(factory, VaultUri);
        var second = await GetKeyClientAsync(factory, OtherVaultUri);

        Assert.NotSame(first, second);
        Assert.Equal(OtherVaultUri, second.VaultUri);
    }

    [Fact]
    public async Task ExecuteKeyClientAsync_Operation_ReturnsItsResult()
    {
        using var factory = CreateFactory();

        var result = await factory.ExecuteKeyClientAsync(
            VaultUri,
            (_, _) => ValueTask.FromResult(42),
            CancellationToken.None
        );

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteKeyClientAsync_NullVaultUri_Throws()
    {
        using var factory = CreateFactory();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory
                .ExecuteKeyClientAsync(
                    null!,
                    (_, _) => ValueTask.FromResult(0),
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task ExecuteKeyClientAsync_NullOperation_Throws()
    {
        using var factory = CreateFactory();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory.ExecuteKeyClientAsync<int>(VaultUri, null!, CancellationToken.None).AsTask()
        );
    }

    #endregion

    #region ExecuteKeyClientWithoutRetryAsync Tests

    private static async Task<KeyClient> GetKeyClientWithoutRetryAsync(
        DefaultAzureKeyVaultClientFactory factory,
        Uri vaultUri
    ) =>
        await factory.ExecuteKeyClientWithoutRetryAsync(
            vaultUri,
            (client, _) => ValueTask.FromResult(client),
            CancellationToken.None
        );

    [Fact]
    public async Task ExecuteKeyClientWithoutRetryAsync_SameVault_RunsOperationsWithTheSameClient()
    {
        using var factory = CreateFactory();

        var first = await GetKeyClientWithoutRetryAsync(factory, VaultUri);
        var second = await GetKeyClientWithoutRetryAsync(factory, VaultUri);

        Assert.Same(first, second);
        Assert.Equal(VaultUri, first.VaultUri);
    }

    [Fact]
    public async Task ExecuteKeyClientWithoutRetryAsync_DifferentVaults_RunsOperationsWithDifferentClients()
    {
        using var factory = CreateFactory();

        var first = await GetKeyClientWithoutRetryAsync(factory, VaultUri);
        var second = await GetKeyClientWithoutRetryAsync(factory, OtherVaultUri);

        Assert.NotSame(first, second);
        Assert.Equal(OtherVaultUri, second.VaultUri);
    }

    [Fact]
    public async Task ExecuteKeyClientWithoutRetryAsync_SameVault_RunsOperationsWithAnotherClientThanTheRetryingOne()
    {
        using var factory = CreateFactory();

        var withRetry = await GetKeyClientAsync(factory, VaultUri);
        var withoutRetry = await GetKeyClientWithoutRetryAsync(factory, VaultUri);

        Assert.NotSame(withRetry, withoutRetry);
    }

    [Fact]
    public async Task ExecuteKeyClientWithoutRetryAsync_Operation_ReturnsItsResult()
    {
        using var factory = CreateFactory();

        var result = await factory.ExecuteKeyClientWithoutRetryAsync(
            VaultUri,
            (_, _) => ValueTask.FromResult(42),
            CancellationToken.None
        );

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteKeyClientWithoutRetryAsync_NullVaultUri_Throws()
    {
        using var factory = CreateFactory();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory
                .ExecuteKeyClientWithoutRetryAsync(
                    null!,
                    (_, _) => ValueTask.FromResult(0),
                    CancellationToken.None
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task ExecuteKeyClientWithoutRetryAsync_NullOperation_Throws()
    {
        using var factory = CreateFactory();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            factory
                .ExecuteKeyClientWithoutRetryAsync<int>(VaultUri, null!, CancellationToken.None)
                .AsTask()
        );
    }

    #endregion
}
