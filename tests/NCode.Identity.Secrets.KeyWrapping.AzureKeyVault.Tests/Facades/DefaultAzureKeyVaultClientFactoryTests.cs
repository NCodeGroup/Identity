// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Azure.Security.KeyVault.Keys.Cryptography;
using NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Facades;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests.Facades;

public class DefaultAzureKeyVaultClientFactoryTests
{
    private const string KeyId = "https://contoso.vault.azure.net/keys/ncode-identity-root";

    private const string KeyVersionId = KeyId + "/0123456789abcdef0123456789abcdef";

    private const string DirectoryId = "11111111-2222-3333-4444-555555555555";

    private const string OtherDirectoryId = "66666666-7777-8888-9999-aaaaaaaaaaaa";

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
}
