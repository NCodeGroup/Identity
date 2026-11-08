// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Facades;
using NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Options;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Tests;

public class DefaultRegistrationTests
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
        var clientFactory = new Mock<IGoogleCloudKmsClientFactory>(MockBehavior.Strict);
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
        clientFactory.Verify();
    }

    [Fact]
    public async Task AddGoogleCloudKmsKeyWrapper_UnknownSetting_Throws()
    {
        var clientFactory = new Mock<IGoogleCloudKmsClientFactory>(MockBehavior.Strict);
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

        var builder = serviceCollection.AddSecretsLibrary().AddPersistenceLibrary();
        for (var i = 0; i < times; ++i)
        {
            builder.AddGoogleCloudKmsRemoteKeyProvider(configuration);
        }

        return serviceCollection;
    }

    [Fact]
    public async Task AddGoogleCloudKmsRemoteKeyProvider_RegistersProviderAndAlgorithmsWithoutCreatingClient()
    {
        var clientFactory = new Mock<IGoogleCloudKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddGoogleCloudKmsRemote(
            CreateConfiguration(
                ("SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing", KeyRing)
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var providers = provider.GetServices<IRemoteKeyProvider>().ToList();
        var sources = provider.GetServices<IRemoteSigningAlgorithmSource>().ToList();
        var options = provider
            .GetRequiredService<IOptions<GoogleCloudKmsRemoteKeyProviderOptions>>()
            .Value;

        Assert.Contains(providers, x => x.Name == RemoteKeyProviderNames.GoogleCloudKms);
        Assert.Contains(providers, x => x is GoogleCloudKmsRemoteKeyProvider);
        Assert.Contains(sources, x => x is GoogleCloudKmsSigningAlgorithms);
        Assert.Equal(KeyRing, options.KeyRing);
        Assert.True(options.HardwareProtected);
        clientFactory.Verify();
    }

    [Fact]
    public async Task AddGoogleCloudKmsRemoteKeyProvider_ResolvesThroughTheCatalog()
    {
        var clientFactory = new Mock<IGoogleCloudKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddGoogleCloudKmsRemote(
            CreateConfiguration(
                ("SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing", KeyRing)
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IRemoteKeyProviderCatalog>();

        Assert.True(
            catalog.TryGetProvider(RemoteKeyProviderNames.GoogleCloudKms, out var remoteProvider)
        );
        Assert.IsType<GoogleCloudKmsRemoteKeyProvider>(remoteProvider);
        Assert.True(
            catalog.TryGetAlgorithm(
                new RemoteSigningAlgorithmSelector
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
        clientFactory.Verify();
    }

    [Fact]
    public void AddGoogleCloudKmsRemoteKeyProvider_CalledTwice_AddsEachServiceOnce()
    {
        var clientFactory = new Mock<IGoogleCloudKmsClientFactory>(MockBehavior.Strict);
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
                x.ServiceType == typeof(IRemoteSigningAlgorithmSource)
                && x.ImplementationType == typeof(GoogleCloudKmsSigningAlgorithms)
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
    public async Task AddGoogleCloudKmsRemoteKeyProvider_NoKeyRing_FailsValidation()
    {
        var clientFactory = new Mock<IGoogleCloudKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddGoogleCloudKmsRemote(
            CreateConfiguration(),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<
            IOptions<GoogleCloudKmsRemoteKeyProviderOptions>
        >();

        var exception = Assert.Throws<OptionsValidationException>(() => options.Value);
        Assert.Contains("KeyRing", exception.Message);
    }

    [Fact]
    public async Task AddGoogleCloudKmsRemoteKeyProvider_UnknownSetting_Throws()
    {
        var clientFactory = new Mock<IGoogleCloudKmsClientFactory>(MockBehavior.Strict);
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
        var clientFactory = new Mock<IGoogleCloudKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(clientFactory.Object);

        serviceCollection
            .AddSecretsLibrary()
            .AddPersistenceLibrary()
            .AddGoogleCloudKmsRemoteKeyProviderIfConfigured(
                CreateConfiguration(
                    ("SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing", KeyRing)
                )
            );

        await using var provider = serviceCollection.BuildServiceProvider();
        var providers = provider.GetServices<IRemoteKeyProvider>().ToList();
        var sources = provider.GetServices<IRemoteSigningAlgorithmSource>().ToList();

        Assert.Contains(providers, x => x is GoogleCloudKmsRemoteKeyProvider);
        Assert.Contains(sources, x => x is GoogleCloudKmsSigningAlgorithms);
        clientFactory.Verify();
    }

    [Fact]
    public async Task AddGoogleCloudKmsRemoteKeyProviderIfConfigured_NoSection_RegistersNeitherProviderNorAlgorithms()
    {
        var clientFactory = new Mock<IGoogleCloudKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(clientFactory.Object);

        serviceCollection
            .AddSecretsLibrary()
            .AddPersistenceLibrary()
            .AddGoogleCloudKmsRemoteKeyProviderIfConfigured(CreateConfiguration());

        await using var provider = serviceCollection.BuildServiceProvider();

        Assert.DoesNotContain(
            provider.GetServices<IRemoteKeyProvider>(),
            x => x is GoogleCloudKmsRemoteKeyProvider
        );
        Assert.DoesNotContain(
            provider.GetServices<IRemoteSigningAlgorithmSource>(),
            x => x is GoogleCloudKmsSigningAlgorithms
        );
        clientFactory.Verify();
    }

    [Fact]
    public void AddGoogleCloudKmsRemoteKeyProviderIfConfigured_NullBuilder_Throws()
    {
        IServiceBuilder<SecretPersistenceLibrary> builder = null!;

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddGoogleCloudKmsRemoteKeyProviderIfConfigured(CreateConfiguration())
        );
    }

    [Fact]
    public void AddGoogleCloudKmsRemoteKeyProviderIfConfigured_NullConfiguration_Throws()
    {
        var builder = new ServiceCollection().AddSecretsLibrary().AddPersistenceLibrary();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddGoogleCloudKmsRemoteKeyProviderIfConfigured(null!)
        );
    }

    #endregion
}
