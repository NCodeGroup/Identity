// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Facades;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests.Facades;

public class DefaultAzureKeyVaultClientFactoryTests
{
    private const string KeyId = "https://contoso.vault.azure.net/keys/ncode-identity-root";

    private const string KeyVersionId = KeyId + "/0123456789abcdef0123456789abcdef";

    #region GetClient Tests

    [Fact]
    public void GetClient_KeyVersionId_ReturnsSameClient()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();

        var first = factory.GetClient(KeyVersionId);
        var second = factory.GetClient(KeyVersionId);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetClient_VersionlessKeyId_ReturnsNewClient()
    {
        var factory = new DefaultAzureKeyVaultClientFactory();

        var first = factory.GetClient(KeyId);
        var second = factory.GetClient(KeyId);

        Assert.NotSame(first, second);
    }

    #endregion
}
