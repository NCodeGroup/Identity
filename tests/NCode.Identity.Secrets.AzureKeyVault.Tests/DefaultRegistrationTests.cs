// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.Registration;
using NCode.Identity.Secrets.AzureKeyVault.Facades;
using NCode.Identity.Secrets.AzureKeyVault.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.Secrets.AzureKeyVault.Tests;

public class DefaultRegistrationTests : BaseTests
{
    private const string KeyId = "https://contoso.vault.azure.net/keys/ncode-identity-root";

    private const string VaultUri = "https://contoso.vault.azure.net";

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
        var clientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
        var serviceCollection = AddAzureKeyVault(
            CreateConfiguration(("SecretProtection:RootKeyProviders:AzureKeyVault:KeyId", KeyId)),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var rootKeyProviders = provider.GetServices<IKeyWrapper>().ToList();

        Assert.Contains(rootKeyProviders, x => x.Name == KeyWrapperNames.AzureKeyVault);
        Assert.Contains(rootKeyProviders, x => x is AzureKeyVaultKeyWrapper);
    }

    [Fact]
    public async Task AddAzureKeyVaultKeyWrapper_UnknownSetting_Throws()
    {
        var clientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
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

    #region AddAzureKeyVaultRemoteKeyProvider Tests

    private static ServiceCollection AddAzureKeyVaultRemote(
        IConfiguration configuration,
        IAzureKeyVaultClientFactory clientFactory,
        int times = 1
    )
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSingleton(clientFactory);

        var builder = serviceCollection.AddSecretsLibrary();
        for (var i = 0; i < times; ++i)
        {
            builder.AddAzureKeyVaultRemoteKeyProvider(configuration);
        }

        return serviceCollection;
    }

    [Fact]
    public async Task AddAzureKeyVaultRemoteKeyProvider_RegistersProviderAndAlgorithmsWithoutCreatingClient()
    {
        var clientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
        var serviceCollection = AddAzureKeyVaultRemote(
            CreateConfiguration(
                ("SecretProtection:RemoteKeyProviders:AzureKeyVault:VaultUri", VaultUri)
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var providers = provider.GetServices<IRemoteKeyProvider>().ToList();
        var sources = provider.GetServices<IRemoteKeyDescriptorSource>().ToList();
        var options = provider
            .GetRequiredService<IOptions<AzureKeyVaultRemoteKeyProviderOptions>>()
            .Value;

        Assert.Contains(providers, x => x.Name == RemoteKeyProviderNames.AzureKeyVault);
        Assert.Contains(providers, x => x is AzureKeyVaultRemoteKeyProvider);
        Assert.Contains(sources, x => x is AzureKeyVaultRemoteKeyDescriptors);
        Assert.Equal(VaultUri, options.VaultUri);
        Assert.True(options.HardwareProtected);
    }

    [Fact]
    public async Task AddAzureKeyVaultRemoteKeyProvider_ResolvesThroughTheCatalog()
    {
        var clientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
        var serviceCollection = AddAzureKeyVaultRemote(
            CreateConfiguration(
                ("SecretProtection:RemoteKeyProviders:AzureKeyVault:VaultUri", VaultUri)
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IRemoteKeyProviderCatalog>();
        var descriptorCatalog = provider.GetRequiredService<IRemoteKeyDescriptorCatalog>();

        Assert.True(
            catalog.TryGetProvider(RemoteKeyProviderNames.AzureKeyVault, out var remoteProvider)
        );
        Assert.IsType<AzureKeyVaultRemoteKeyProvider>(remoteProvider);
        Assert.True(
            descriptorCatalog.TryGetAlgorithm(
                new RemoteKeyAlgorithmSelector
                {
                    Provider = RemoteKeyProviderNames.AzureKeyVault,
                    Algorithm = "RS256",
                    SecretType = SecretKeyTypes.Rsa,
                    KeySizeBits = 3072,
                },
                out var algorithm
            )
        );
        Assert.Equal("RSA", algorithm.KeySpec);
        var descriptor = Assert.Single(descriptorCatalog.Providers);
        Assert.True(descriptor.CreatesKeys);
    }

    [Fact]
    public void AddAzureKeyVaultRemoteKeyProvider_CalledTwice_AddsEachServiceOnce()
    {
        var clientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
        var serviceCollection = AddAzureKeyVaultRemote(
            CreateConfiguration(
                ("SecretProtection:RemoteKeyProviders:AzureKeyVault:VaultUri", VaultUri)
            ),
            clientFactory.Object,
            times: 2
        );

        Assert.Single(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IRemoteKeyProvider)
                && x.ImplementationType == typeof(AzureKeyVaultRemoteKeyProvider)
        );
        Assert.Single(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IRemoteKeyDescriptorSource)
                && x.ImplementationType == typeof(AzureKeyVaultRemoteKeyDescriptors)
        );
        Assert.Single(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IValidateOptions<AzureKeyVaultRemoteKeyProviderOptions>)
                && x.ImplementationType == typeof(AzureKeyVaultRemoteKeyProviderOptionsValidator)
        );
        Assert.Single(serviceCollection, x => x.ServiceType == typeof(IAzureKeyVaultClientFactory));
    }

    [Fact]
    public async Task AddAzureKeyVaultRemoteKeyProvider_NoVaultUri_DescribesAProviderThatCreatesNoKeys()
    {
        var clientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
        var serviceCollection = AddAzureKeyVaultRemote(CreateConfiguration(), clientFactory.Object);

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<
            IOptions<AzureKeyVaultRemoteKeyProviderOptions>
        >();

        Assert.Null(options.Value.VaultUri);
        var descriptor = Assert.Single(
            provider.GetRequiredService<IRemoteKeyDescriptorCatalog>().Providers
        );
        Assert.False(descriptor.CreatesKeys);
    }

    [Fact]
    public async Task AddAzureKeyVaultRemoteKeyProvider_UnknownSetting_Throws()
    {
        var clientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
        var serviceCollection = AddAzureKeyVaultRemote(
            CreateConfiguration(
                ("SecretProtection:RemoteKeyProviders:AzureKeyVault:VaultUrii", VaultUri)
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<
            IOptions<AzureKeyVaultRemoteKeyProviderOptions>
        >();

        var exception = Assert.Throws<InvalidOperationException>(() => options.Value);
        Assert.Contains(
            AzureKeyVaultRemoteKeyProviderOptions.DefaultSectionName,
            exception.Message
        );
    }

    [Fact]
    public async Task AddAzureKeyVaultRemoteKeyProviderIfConfigured_SectionExists_RegistersProviderAndAlgorithms()
    {
        var clientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(clientFactory.Object);

        serviceCollection
            .AddSecretsLibrary()
            .AddAzureKeyVaultRemoteKeyProviderIfConfigured(
                CreateConfiguration(
                    ("SecretProtection:RemoteKeyProviders:AzureKeyVault:VaultUri", VaultUri)
                )
            );

        await using var provider = serviceCollection.BuildServiceProvider();
        var providers = provider.GetServices<IRemoteKeyProvider>().ToList();
        var sources = provider.GetServices<IRemoteKeyDescriptorSource>().ToList();

        Assert.Contains(providers, x => x is AzureKeyVaultRemoteKeyProvider);
        Assert.Contains(sources, x => x is AzureKeyVaultRemoteKeyDescriptors);
    }

    [Fact]
    public async Task AddAzureKeyVaultRemoteKeyProviderIfConfigured_NoSection_RegistersNeitherProviderNorAlgorithms()
    {
        var clientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(clientFactory.Object);

        serviceCollection
            .AddSecretsLibrary()
            .AddAzureKeyVaultRemoteKeyProviderIfConfigured(CreateConfiguration());

        await using var provider = serviceCollection.BuildServiceProvider();

        Assert.DoesNotContain(
            provider.GetServices<IRemoteKeyProvider>(),
            x => x is AzureKeyVaultRemoteKeyProvider
        );
        Assert.DoesNotContain(
            provider.GetServices<IRemoteKeyDescriptorSource>(),
            x => x is AzureKeyVaultRemoteKeyDescriptors
        );
    }

    [Fact]
    public void AddAzureKeyVaultRemoteKeyProviderIfConfigured_NullBuilder_Throws()
    {
        IServiceBuilder<SecretsLibrary> builder = null!;

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddAzureKeyVaultRemoteKeyProviderIfConfigured(CreateConfiguration())
        );
    }

    [Fact]
    public void AddAzureKeyVaultRemoteKeyProviderIfConfigured_NullConfiguration_Throws()
    {
        var builder = new ServiceCollection().AddSecretsLibrary();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddAzureKeyVaultRemoteKeyProviderIfConfigured(null!)
        );
    }

    #endregion

    #region ReferencedKeyHostSuffixes Tests

    [Fact]
    public async Task AddAzureKeyVaultKeyWrapper_NoSuffixesConfigured_UsesTheDefaultSuffixes()
    {
        var clientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
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
        var clientFactory = CreateStrictMock<IAzureKeyVaultClientFactory>();
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
