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
using NCode.Identity.Secrets.Persistence.GoogleCloudKms.Facades;
using NCode.Identity.Secrets.Persistence.GoogleCloudKms.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.Secrets.Persistence.GoogleCloudKms.Tests;

public class DefaultRegistrationTests
{
    private const string KeyName =
        "projects/my-project/locations/us-central1/keyRings/ncode-identity/cryptoKeys/root";

    private static IConfiguration CreateConfiguration(
        params IEnumerable<(string Key, string? Value)> values
    ) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => KeyValuePair.Create(x.Key, x.Value)))
            .Build();

    private static ServiceCollection AddGoogleCloudKms(
        IConfiguration configuration,
        IGoogleCloudKmsClientFactory clientFactory
    )
    {
        var serviceCollection = new ServiceCollection();

        // A strict mock without setups fails the test when anything asks for a client, which is how the tests see
        // that registering and resolving the provider never creates one.
        serviceCollection.AddSingleton(clientFactory);

        serviceCollection
            .AddSecretsLibrary()
            .AddPersistenceLibrary()
            .AddGoogleCloudKmsRootKeyProvider(configuration);

        return serviceCollection;
    }

    #region AddGoogleCloudKmsRootKeyProvider Tests

    [Fact]
    public async Task AddGoogleCloudKmsRootKeyProvider_RegistersProviderInCatalogWithoutCreatingClient()
    {
        var clientFactory = new Mock<IGoogleCloudKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddGoogleCloudKms(
            CreateConfiguration(
                ("SecretProtection:RootKeyProviders:GoogleCloudKms:KeyName", KeyName)
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var rootKeyProviders = provider.GetServices<IRootKeyProvider>().ToList();

        Assert.Contains(
            rootKeyProviders,
            x => x.ProviderName == RootKeyProviderNames.GoogleCloudKms
        );
        Assert.Contains(rootKeyProviders, x => x is GoogleCloudKmsRootKeyProvider);
        clientFactory.Verify();
    }

    [Fact]
    public async Task AddGoogleCloudKmsRootKeyProvider_UnknownSetting_Throws()
    {
        var clientFactory = new Mock<IGoogleCloudKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddGoogleCloudKms(
            CreateConfiguration(
                ("SecretProtection:RootKeyProviders:GoogleCloudKms:KeyNamee", KeyName)
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GoogleCloudKmsRootKeyOptions>>();

        var exception = Assert.Throws<InvalidOperationException>(() => options.Value);
        Assert.Contains(GoogleCloudKmsRootKeyOptions.DefaultSectionName, exception.Message);
    }

    #endregion
}
