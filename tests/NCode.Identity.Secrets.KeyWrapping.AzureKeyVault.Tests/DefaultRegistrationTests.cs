// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Facades;
using NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Options;
using NCode.Identity.Secrets.Persistence;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests;

public class DefaultRegistrationTests
{
    private const string KeyId = "https://contoso.vault.azure.net/keys/ncode-identity-root";

    private static IConfiguration CreateConfiguration(
        params IEnumerable<(string Key, string? Value)> values
    ) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => KeyValuePair.Create(x.Key, x.Value)))
            .Build();

    private static ServiceCollection AddAzureKeyVault(
        IConfiguration configuration,
        IAzureKeyVaultClientFactory clientFactory
    )
    {
        var serviceCollection = new ServiceCollection();

        // A strict mock without setups fails the test when anything asks for a client, which is how the tests see
        // that registering and resolving the provider never creates one.
        serviceCollection.AddSingleton(clientFactory);

        serviceCollection
            .AddSecretsLibrary()
            .AddPersistenceLibrary()
            .AddAzureKeyVaultKeyWrapper(configuration);

        return serviceCollection;
    }

    #region AddAzureKeyVaultKeyWrapper Tests

    [Fact]
    public async Task AddAzureKeyVaultKeyWrapper_RegistersProviderInCatalogWithoutCreatingClient()
    {
        var clientFactory = new Mock<IAzureKeyVaultClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAzureKeyVault(
            CreateConfiguration(("SecretProtection:RootKeyProviders:AzureKeyVault:KeyId", KeyId)),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var rootKeyProviders = provider.GetServices<IKeyWrapper>().ToList();

        Assert.Contains(rootKeyProviders, x => x.Name == KeyWrapperNames.AzureKeyVault);
        Assert.Contains(rootKeyProviders, x => x is AzureKeyVaultKeyWrapper);
        clientFactory.Verify();
    }

    [Fact]
    public async Task AddAzureKeyVaultKeyWrapper_UnknownSetting_Throws()
    {
        var clientFactory = new Mock<IAzureKeyVaultClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAzureKeyVault(
            CreateConfiguration(("SecretProtection:RootKeyProviders:AzureKeyVault:KeyIdd", KeyId)),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AzureKeyVaultKeyWrapperOptions>>();

        var exception = Assert.Throws<InvalidOperationException>(() => options.Value);
        Assert.Contains(AzureKeyVaultKeyWrapperOptions.DefaultSectionName, exception.Message);
    }

    #endregion

    #region ReferencedKeyHostSuffixes Tests

    [Fact]
    public async Task AddAzureKeyVaultKeyWrapper_NoSuffixesConfigured_UsesTheDefaultSuffixes()
    {
        var clientFactory = new Mock<IAzureKeyVaultClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAzureKeyVault(
            CreateConfiguration(("SecretProtection:RootKeyProviders:AzureKeyVault:KeyId", KeyId)),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AzureKeyVaultKeyWrapperOptions>>();

        Assert.Equal(
            [
                ".vault.azure.net",
                ".managedhsm.azure.net",
                ".vault.azure.cn",
                ".managedhsm.azure.cn",
                ".vault.usgovcloudapi.net",
                ".managedhsm.usgovcloudapi.net",
            ],
            options.Value.ReferencedKeyHostSuffixes
        );
    }

    [Fact]
    public async Task AddAzureKeyVaultKeyWrapper_SuffixesConfigured_ReplacesTheDefaultSuffixes()
    {
        var clientFactory = new Mock<IAzureKeyVaultClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAzureKeyVault(
            CreateConfiguration(
                ("SecretProtection:RootKeyProviders:AzureKeyVault:KeyId", KeyId),
                (
                    "SecretProtection:RootKeyProviders:AzureKeyVault:ReferencedKeyHostSuffixes:0",
                    ".corp.example"
                ),
                (
                    "SecretProtection:RootKeyProviders:AzureKeyVault:ReferencedKeyHostSuffixes:1",
                    ".vault.azure.net"
                )
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AzureKeyVaultKeyWrapperOptions>>();

        Assert.Equal(
            [".corp.example", ".vault.azure.net"],
            options.Value.ReferencedKeyHostSuffixes
        );
    }

    #endregion
}
