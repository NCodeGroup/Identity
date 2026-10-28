#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
}
