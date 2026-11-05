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
}
