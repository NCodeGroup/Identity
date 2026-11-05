// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Facades;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests.Facades;

public class DefaultAzureKeyVaultClientFactoryTests
{
    private const string KeyId = "https://contoso.vault.azure.net/keys/ncode-identity-root";

    private const string KeyVersionId = KeyId + "/0123456789abcdef0123456789abcdef";

    private const string DirectoryId = "11111111-2222-3333-4444-555555555555";

    private const string OtherDirectoryId = "66666666-7777-8888-9999-aaaaaaaaaaaa";

    #region GetClient Tests

    [Fact]
    public void GetClient_KeyVersionId_ReturnsSameClient()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();

        var first = factory.GetClient(KeyVersionId, access: null);
        var second = factory.GetClient(KeyVersionId, access: null);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetClient_VersionlessKeyId_ReturnsNewClient()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();

        var first = factory.GetClient(KeyId, access: null);
        var second = factory.GetClient(KeyId, access: null);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GetClient_SameDirectoryAndKeyVersionId_ReturnsSameClient()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();

        var first = factory.GetClient(KeyVersionId, new KeyAccess(DirectoryId, "tenant:acme"));
        var second = factory.GetClient(KeyVersionId, new KeyAccess(DirectoryId, "tenant:other"));

        Assert.Same(first, second);
    }

    [Fact]
    public void GetClient_KeyVersionIdUnderDifferentDirectories_ReturnsDifferentClients()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();

        var first = factory.GetClient(KeyVersionId, new KeyAccess(DirectoryId, "tenant:acme"));
        var second = factory.GetClient(
            KeyVersionId,
            new KeyAccess(OtherDirectoryId, "tenant:acme")
        );
        var own = factory.GetClient(KeyVersionId, access: null);

        Assert.NotSame(first, second);
        Assert.NotSame(first, own);
        Assert.NotSame(second, own);
    }

    [Fact]
    public void GetClient_DirectoryWithVersionlessKeyId_ReturnsNewClient()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();

        var first = factory.GetClient(KeyId, new KeyAccess(DirectoryId, "tenant:acme"));
        var second = factory.GetClient(KeyId, new KeyAccess(DirectoryId, "tenant:acme"));

        Assert.NotSame(first, second);
    }

    #endregion

    #region Confirm Tests

    [Fact]
    public void Confirm_ThenRelease_KeepsClientCached()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();
        var access = new KeyAccess(DirectoryId, "tenant:acme");
        var before = factory.GetClient(KeyVersionId, access);

        factory.Confirm(access);
        factory.Release(access);
        var after = factory.GetClient(KeyVersionId, access);

        Assert.Same(before, after);
    }

    [Fact]
    public void Confirm_AnotherDirectory_StillReleases()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();
        var access = new KeyAccess(DirectoryId, "tenant:acme");
        var other = new KeyAccess(OtherDirectoryId, "tenant:acme");
        var before = factory.GetClient(KeyVersionId, access);
        factory.GetClient(KeyVersionId, other);

        factory.Confirm(other);
        factory.Release(access);
        var after = factory.GetClient(KeyVersionId, access);

        Assert.NotSame(before, after);
    }

    [Fact]
    public void Confirm_DirectoryWithoutClient_Succeeds()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();

        factory.Confirm(new KeyAccess(DirectoryId, "tenant:acme"));
    }

    [Fact]
    public void Confirm_NullAccess_Throws()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();

        Assert.Throws<ArgumentNullException>(() => factory.Confirm(null!));
    }

    #endregion

    #region Release Tests

    [Fact]
    public void Release_WithoutConfirm_ReturnsDifferentClientForSameDirectoryAndKeyId()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();
        var access = new KeyAccess(DirectoryId, "tenant:acme");
        var before = factory.GetClient(KeyVersionId, access);

        factory.Release(access);
        var after = factory.GetClient(KeyVersionId, access);

        Assert.NotSame(before, after);
    }

    [Fact]
    public void Release_AfterConfirm_KeepsClientCached()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();
        var access = new KeyAccess(DirectoryId, "tenant:acme");
        var before = factory.GetClient(KeyVersionId, access);
        factory.Confirm(access);

        factory.Release(access);
        var after = factory.GetClient(KeyVersionId, access);

        Assert.Same(before, after);
    }

    [Fact]
    public void Release_AnotherDirectory_KeepsClientCached()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();
        var other = new KeyAccess(OtherDirectoryId, "tenant:acme");
        var before = factory.GetClient(KeyVersionId, other);

        factory.Release(new KeyAccess(DirectoryId, "tenant:acme"));
        var after = factory.GetClient(KeyVersionId, other);

        Assert.Same(before, after);
    }

    [Fact]
    public void Release_DirectoryWithoutClient_Succeeds()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();

        factory.Release(new KeyAccess(DirectoryId, "tenant:acme"));
    }

    [Fact]
    public void Release_EmptyIdentity_KeepsClientOfTheDeploymentsOwnIdentity()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();
        var before = factory.GetClient(KeyVersionId, access: null);

        factory.Release(new KeyAccess(string.Empty, "tenant:acme"));
        var after = factory.GetClient(KeyVersionId, access: null);

        Assert.Same(before, after);
    }

    [Fact]
    public void Release_NullAccess_Throws()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();

        Assert.Throws<ArgumentNullException>(() => factory.Release(null!));
    }

    #endregion
}
