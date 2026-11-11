// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.Registration;
using NCode.Identity.Secrets.GoogleCloudKms.Facades;
using NCode.Identity.Secrets.GoogleCloudKms.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.Secrets.GoogleCloudKms.Tests;

public class DefaultRegistrationTests : BaseTests
{
    private const string KeyName =
        "projects/my-project/locations/us-central1/keyRings/ncode-identity/cryptoKeys/root";

    private const string KeyRing =
        "projects/my-project/locations/us-central1/keyRings/ncode-identity";

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
            .AddGoogleCloudKmsKeyWrapper(configuration);

        return serviceCollection;
    }

    #region AddGoogleCloudKmsKeyWrapper Tests

    [Fact]
    public async Task AddGoogleCloudKmsKeyWrapper_RegistersProviderInCatalogWithoutCreatingClient()
    {
        var clientFactory = CreateStrictMock<IGoogleCloudKmsClientFactory>();
        var serviceCollection = AddGoogleCloudKms(
            CreateConfiguration(
                ("SecretProtection:RootKeyProviders:GoogleCloudKms:KeyName", KeyName)
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var rootKeyProviders = provider.GetServices<IKeyWrapper>().ToList();

        Assert.Contains(rootKeyProviders, x => x.Name == KeyWrapperNames.GoogleCloudKms);
        Assert.Contains(rootKeyProviders, x => x is GoogleCloudKmsKeyWrapper);
    }

    [Fact]
    public async Task AddGoogleCloudKmsKeyWrapper_UnknownSetting_Throws()
    {
        var clientFactory = CreateStrictMock<IGoogleCloudKmsClientFactory>();
        var serviceCollection = AddGoogleCloudKms(
            CreateConfiguration(
                ("SecretProtection:RootKeyProviders:GoogleCloudKms:KeyNamee", KeyName)
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GoogleCloudKmsKeyWrapperOptions>>();

        var exception = Assert.Throws<InvalidOperationException>(() => options.Value);
        Assert.Contains(GoogleCloudKmsKeyWrapperOptions.DefaultSectionName, exception.Message);
    }

    #endregion

    #region AddGoogleCloudKmsRemoteKeyProvider Tests

    private static ServiceCollection AddGoogleCloudKmsRemote(
        IConfiguration configuration,
        IGoogleCloudKmsClientFactory clientFactory,
        int times = 1
    )
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSingleton(clientFactory);

        var builder = serviceCollection.AddSecretsLibrary();
        for (var i = 0; i < times; ++i)
        {
            builder.AddGoogleCloudKmsRemoteKeyProvider(configuration);
        }

        return serviceCollection;
    }

    [Fact]
    public async Task AddGoogleCloudKmsRemoteKeyProvider_RegistersProviderAndAlgorithmsWithoutCreatingClient()
    {
        var clientFactory = CreateStrictMock<IGoogleCloudKmsClientFactory>();
        var serviceCollection = AddGoogleCloudKmsRemote(
            CreateConfiguration(
                ("SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing", KeyRing)
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var providers = provider.GetServices<IRemoteKeyProvider>().ToList();
        var sources = provider.GetServices<IRemoteKeyDescriptorSource>().ToList();
        var options = provider
            .GetRequiredService<IOptions<GoogleCloudKmsRemoteKeyProviderOptions>>()
            .Value;

        Assert.Contains(providers, x => x.Name == RemoteKeyProviderNames.GoogleCloudKms);
        Assert.Contains(providers, x => x is GoogleCloudKmsRemoteKeyProvider);
        Assert.Contains(sources, x => x is GoogleCloudKmsRemoteKeyDescriptors);
        Assert.Equal(KeyRing, options.KeyRing);
        Assert.True(options.HardwareProtected);
    }

    [Fact]
    public async Task AddGoogleCloudKmsRemoteKeyProvider_ResolvesThroughTheCatalog()
    {
        var clientFactory = CreateStrictMock<IGoogleCloudKmsClientFactory>();
        var serviceCollection = AddGoogleCloudKmsRemote(
            CreateConfiguration(
                ("SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing", KeyRing)
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IRemoteKeyProviderCatalog>();
        var descriptorCatalog = provider.GetRequiredService<IRemoteKeyDescriptorCatalog>();

        Assert.True(
            catalog.TryGetProvider(RemoteKeyProviderNames.GoogleCloudKms, out var remoteProvider)
        );
        Assert.IsType<GoogleCloudKmsRemoteKeyProvider>(remoteProvider);
        Assert.True(
            descriptorCatalog.TryGetAlgorithm(
                new RemoteKeyAlgorithmSelector
                {
                    Provider = RemoteKeyProviderNames.GoogleCloudKms,
                    Algorithm = "PS256",
                    SecretType = SecretKeyTypes.Rsa,
                    KeySizeBits = 3072,
                },
                out var algorithm
            )
        );
        Assert.Equal("RSA_SIGN_PSS_3072_SHA256", algorithm.KeySpec);
        var descriptor = Assert.Single(descriptorCatalog.Providers);
        Assert.True(descriptor.CreatesKeys);
    }

    [Fact]
    public void AddGoogleCloudKmsRemoteKeyProvider_CalledTwice_AddsEachServiceOnce()
    {
        var clientFactory = CreateStrictMock<IGoogleCloudKmsClientFactory>();
        var serviceCollection = AddGoogleCloudKmsRemote(
            CreateConfiguration(
                ("SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing", KeyRing)
            ),
            clientFactory.Object,
            times: 2
        );

        Assert.Single(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IRemoteKeyProvider)
                && x.ImplementationType == typeof(GoogleCloudKmsRemoteKeyProvider)
        );
        Assert.Single(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IRemoteKeyDescriptorSource)
                && x.ImplementationType == typeof(GoogleCloudKmsRemoteKeyDescriptors)
        );
        Assert.Single(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IValidateOptions<GoogleCloudKmsRemoteKeyProviderOptions>)
                && x.ImplementationType == typeof(GoogleCloudKmsRemoteKeyProviderOptionsValidator)
        );
        Assert.Single(
            serviceCollection,
            x => x.ServiceType == typeof(IGoogleCloudKmsClientFactory)
        );
    }

    [Fact]
    public async Task AddGoogleCloudKmsRemoteKeyProvider_NoKeyRing_DescribesAProviderThatCreatesNoKeys()
    {
        var clientFactory = CreateStrictMock<IGoogleCloudKmsClientFactory>();
        var serviceCollection = AddGoogleCloudKmsRemote(
            CreateConfiguration(),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<
            IOptions<GoogleCloudKmsRemoteKeyProviderOptions>
        >();

        Assert.Null(options.Value.KeyRing);
        var descriptor = Assert.Single(
            provider.GetRequiredService<IRemoteKeyDescriptorCatalog>().Providers
        );
        Assert.False(descriptor.CreatesKeys);
    }

    [Fact]
    public async Task AddGoogleCloudKmsRemoteKeyProvider_UnknownSetting_Throws()
    {
        var clientFactory = CreateStrictMock<IGoogleCloudKmsClientFactory>();
        var serviceCollection = AddGoogleCloudKmsRemote(
            CreateConfiguration(
                ("SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRingg", KeyRing)
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<
            IOptions<GoogleCloudKmsRemoteKeyProviderOptions>
        >();

        var exception = Assert.Throws<InvalidOperationException>(() => options.Value);
        Assert.Contains(
            GoogleCloudKmsRemoteKeyProviderOptions.DefaultSectionName,
            exception.Message
        );
    }

    [Fact]
    public async Task AddGoogleCloudKmsRemoteKeyProviderIfConfigured_SectionExists_RegistersProviderAndAlgorithms()
    {
        var clientFactory = CreateStrictMock<IGoogleCloudKmsClientFactory>();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(clientFactory.Object);

        serviceCollection
            .AddSecretsLibrary()
            .AddGoogleCloudKmsRemoteKeyProviderIfConfigured(
                CreateConfiguration(
                    ("SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing", KeyRing)
                )
            );

        await using var provider = serviceCollection.BuildServiceProvider();
        var providers = provider.GetServices<IRemoteKeyProvider>().ToList();
        var sources = provider.GetServices<IRemoteKeyDescriptorSource>().ToList();

        Assert.Contains(providers, x => x is GoogleCloudKmsRemoteKeyProvider);
        Assert.Contains(sources, x => x is GoogleCloudKmsRemoteKeyDescriptors);
    }

    [Fact]
    public async Task AddGoogleCloudKmsRemoteKeyProviderIfConfigured_NoSection_RegistersNeitherProviderNorAlgorithms()
    {
        var clientFactory = CreateStrictMock<IGoogleCloudKmsClientFactory>();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(clientFactory.Object);

        serviceCollection
            .AddSecretsLibrary()
            .AddGoogleCloudKmsRemoteKeyProviderIfConfigured(CreateConfiguration());

        await using var provider = serviceCollection.BuildServiceProvider();

        Assert.DoesNotContain(
            provider.GetServices<IRemoteKeyProvider>(),
            x => x is GoogleCloudKmsRemoteKeyProvider
        );
        Assert.DoesNotContain(
            provider.GetServices<IRemoteKeyDescriptorSource>(),
            x => x is GoogleCloudKmsRemoteKeyDescriptors
        );
    }

    [Fact]
    public void AddGoogleCloudKmsRemoteKeyProviderIfConfigured_NullBuilder_Throws()
    {
        IServiceBuilder<SecretsLibrary> builder = null!;

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddGoogleCloudKmsRemoteKeyProviderIfConfigured(CreateConfiguration())
        );
    }

    [Fact]
    public void AddGoogleCloudKmsRemoteKeyProviderIfConfigured_NullConfiguration_Throws()
    {
        var builder = new ServiceCollection().AddSecretsLibrary();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddGoogleCloudKmsRemoteKeyProviderIfConfigured(null!)
        );
    }

    #endregion
}
