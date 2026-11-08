// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Secrets.AwsKms.Facades;
using NCode.Identity.Secrets.AwsKms.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Persistence.Stores;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Secrets.AwsKms.Tests;

public class DefaultRegistrationTests
{
    private const string KeyArn =
        "arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab";

    private static IConfiguration CreateConfiguration(
        params IEnumerable<(string Key, string? Value)> values
    ) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => KeyValuePair.Create(x.Key, x.Value)))
            .Build();

    private static ServiceCollection AddAwsKms(
        IConfiguration configuration,
        IAwsKmsClientFactory clientFactory
    )
    {
        var serviceCollection = new ServiceCollection();

        // A strict mock without setups fails the test when anything asks for a client, which is how the tests see
        // that registering and resolving the provider never creates one.
        serviceCollection.AddSingleton(clientFactory);

        serviceCollection
            .AddSecretsLibrary()
            .AddPersistenceLibrary()
            .AddAwsKmsKeyWrapper(configuration);

        return serviceCollection;
    }

    #region AddAwsKmsKeyWrapper Tests

    [Fact]
    public async Task AddAwsKmsKeyWrapper_RegistersProviderInCatalogWithoutCreatingClient()
    {
        var clientFactory = new Mock<IAwsKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAwsKms(
            CreateConfiguration(("SecretProtection:RootKeyProviders:AwsKms:KeyArn", KeyArn)),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var rootKeyProviders = provider.GetServices<IKeyWrapper>().ToList();

        Assert.Contains(rootKeyProviders, x => x.Name == KeyWrapperNames.AwsKms);
        Assert.Contains(rootKeyProviders, x => x is AwsKmsKeyWrapper);
        clientFactory.Verify();
    }

    [Fact]
    public async Task AddAwsKmsKeyWrapper_OwnerRootKeyProviderIsAwsKms_BuildsWithValidateOnBuildAndResolvesWrappersAndOptions()
    {
        var clientFactory = new Mock<IAwsKmsClientFactory>(MockBehavior.Strict);
        var configuration = CreateConfiguration(
            ("SecretProtection:RootKeyProvider", KeyWrapperNames.AwsKms),
            ("SecretProtection:OwnerRootKeys:Providers:0", KeyWrapperNames.AwsKms),
            ("SecretProtection:RootKeyProviders:AwsKms:KeyArn", KeyArn)
        );
        var serviceCollection = AddAwsKms(configuration, clientFactory.Object);

        // The persistence library's services need a store factory to be constructed; this test never reads a store.
        serviceCollection.AddSingleton(new Mock<IStoreManagerFactory>(MockBehavior.Strict).Object);
        serviceCollection
            .AddOptions<SecretProtectionOptions>()
            .Configure(options =>
                configuration.GetSection(SecretProtectionOptions.DefaultSectionName).Bind(options)
            );

        await using var provider = serviceCollection.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );

        var wrappers = provider.GetRequiredService<IEnumerable<IKeyWrapper>>().ToList();
        var options = provider.GetRequiredService<IOptions<SecretProtectionOptions>>().Value;

        Assert.Contains(wrappers, x => x is AwsKmsKeyWrapper);
        Assert.Equal(KeyWrapperNames.AwsKms, options.RootKeyProvider);
        Assert.Equal([KeyWrapperNames.AwsKms], options.OwnerRootKeys.Providers);
        clientFactory.Verify();
    }

    [Fact]
    public async Task AddAwsKmsKeyWrapper_UnknownSetting_Throws()
    {
        var clientFactory = new Mock<IAwsKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAwsKms(
            CreateConfiguration(("SecretProtection:RootKeyProviders:AwsKms:KeyArnn", KeyArn)),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AwsKmsKeyWrapperOptions>>();

        var exception = Assert.Throws<InvalidOperationException>(() => options.Value);
        Assert.Contains(AwsKmsKeyWrapperOptions.DefaultSectionName, exception.Message);
    }

    #endregion

    #region AddAwsKmsRemoteKeyProvider Tests

    private static ServiceCollection AddAwsKmsRemote(
        IConfiguration configuration,
        IAwsKmsClientFactory clientFactory,
        int times = 1
    )
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSingleton(clientFactory);

        var builder = serviceCollection.AddSecretsLibrary().AddPersistenceLibrary();
        for (var i = 0; i < times; ++i)
        {
            builder.AddAwsKmsRemoteKeyProvider(configuration);
        }

        return serviceCollection;
    }

    [Fact]
    public async Task AddAwsKmsRemoteKeyProvider_RegistersProviderAndAlgorithmsWithoutCreatingClient()
    {
        var clientFactory = new Mock<IAwsKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAwsKmsRemote(
            CreateConfiguration(("SecretProtection:RemoteKeyProviders:AwsKms:Region", "us-west-2")),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var providers = provider.GetServices<IRemoteKeyProvider>().ToList();
        var sources = provider.GetServices<IRemoteSigningAlgorithmSource>().ToList();
        var options = provider.GetRequiredService<IOptions<AwsKmsRemoteKeyProviderOptions>>().Value;

        Assert.Contains(providers, x => x.Name == RemoteKeyProviderNames.AwsKms);
        Assert.Contains(providers, x => x is AwsKmsRemoteKeyProvider);
        Assert.Contains(sources, x => x is AwsKmsSigningAlgorithms);
        Assert.Equal("us-west-2", options.Region);
        Assert.Equal(30, options.PendingWindowInDays);
        clientFactory.Verify();
    }

    [Fact]
    public async Task AddAwsKmsRemoteKeyProvider_ResolvesThroughTheCatalog()
    {
        var clientFactory = new Mock<IAwsKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAwsKmsRemote(
            CreateConfiguration(("SecretProtection:RemoteKeyProviders:AwsKms:Region", "us-west-2")),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IRemoteKeyProviderCatalog>();

        Assert.True(catalog.TryGetProvider(RemoteKeyProviderNames.AwsKms, out var remoteProvider));
        Assert.IsType<AwsKmsRemoteKeyProvider>(remoteProvider);
        Assert.True(
            catalog.TryGetAlgorithm(
                new RemoteSigningAlgorithmSelector
                {
                    Provider = RemoteKeyProviderNames.AwsKms,
                    Algorithm = "RS256",
                    SecretType = SecretKeyTypes.Rsa,
                    KeySizeBits = 3072,
                },
                out var algorithm
            )
        );
        Assert.Equal("RSA_3072", algorithm.KeySpec);
        clientFactory.Verify();
    }

    [Fact]
    public void AddAwsKmsRemoteKeyProvider_CalledTwice_AddsEachServiceOnce()
    {
        var clientFactory = new Mock<IAwsKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAwsKmsRemote(
            CreateConfiguration(("SecretProtection:RemoteKeyProviders:AwsKms:Region", "us-west-2")),
            clientFactory.Object,
            times: 2
        );

        Assert.Single(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IRemoteKeyProvider)
                && x.ImplementationType == typeof(AwsKmsRemoteKeyProvider)
        );
        Assert.Single(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IRemoteSigningAlgorithmSource)
                && x.ImplementationType == typeof(AwsKmsSigningAlgorithms)
        );
        Assert.Single(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IValidateOptions<AwsKmsRemoteKeyProviderOptions>)
                && x.ImplementationType == typeof(AwsKmsRemoteKeyProviderOptionsValidator)
        );
        Assert.Single(serviceCollection, x => x.ServiceType == typeof(IAwsKmsClientFactory));
    }

    [Fact]
    public async Task AddAwsKmsRemoteKeyProvider_NoRegion_FailsValidation()
    {
        var clientFactory = new Mock<IAwsKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAwsKmsRemote(CreateConfiguration(), clientFactory.Object);

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AwsKmsRemoteKeyProviderOptions>>();

        var exception = Assert.Throws<OptionsValidationException>(() => options.Value);
        Assert.Contains("Region", exception.Message);
    }

    [Fact]
    public async Task AddAwsKmsRemoteKeyProvider_UnknownSetting_Throws()
    {
        var clientFactory = new Mock<IAwsKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = AddAwsKmsRemote(
            CreateConfiguration(
                ("SecretProtection:RemoteKeyProviders:AwsKms:Regionn", "us-west-2")
            ),
            clientFactory.Object
        );

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AwsKmsRemoteKeyProviderOptions>>();

        var exception = Assert.Throws<InvalidOperationException>(() => options.Value);
        Assert.Contains(AwsKmsRemoteKeyProviderOptions.DefaultSectionName, exception.Message);
    }

    [Fact]
    public async Task AddAwsKmsRemoteKeyProviderIfConfigured_SectionExists_RegistersProviderAndAlgorithms()
    {
        var clientFactory = new Mock<IAwsKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(clientFactory.Object);

        serviceCollection
            .AddSecretsLibrary()
            .AddPersistenceLibrary()
            .AddAwsKmsRemoteKeyProviderIfConfigured(
                CreateConfiguration(
                    ("SecretProtection:RemoteKeyProviders:AwsKms:Region", "us-west-2")
                )
            );

        await using var provider = serviceCollection.BuildServiceProvider();
        var providers = provider.GetServices<IRemoteKeyProvider>().ToList();
        var sources = provider.GetServices<IRemoteSigningAlgorithmSource>().ToList();

        Assert.Contains(providers, x => x is AwsKmsRemoteKeyProvider);
        Assert.Contains(sources, x => x is AwsKmsSigningAlgorithms);
        clientFactory.Verify();
    }

    [Fact]
    public async Task AddAwsKmsRemoteKeyProviderIfConfigured_NoSection_RegistersNeitherProviderNorAlgorithms()
    {
        var clientFactory = new Mock<IAwsKmsClientFactory>(MockBehavior.Strict);
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(clientFactory.Object);

        serviceCollection
            .AddSecretsLibrary()
            .AddPersistenceLibrary()
            .AddAwsKmsRemoteKeyProviderIfConfigured(CreateConfiguration());

        await using var provider = serviceCollection.BuildServiceProvider();

        Assert.DoesNotContain(
            provider.GetServices<IRemoteKeyProvider>(),
            x => x is AwsKmsRemoteKeyProvider
        );
        Assert.DoesNotContain(
            provider.GetServices<IRemoteSigningAlgorithmSource>(),
            x => x is AwsKmsSigningAlgorithms
        );
        clientFactory.Verify();
    }

    [Fact]
    public void AddAwsKmsRemoteKeyProviderIfConfigured_NullBuilder_Throws()
    {
        IServiceBuilder<SecretPersistenceLibrary> builder = null!;

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddAwsKmsRemoteKeyProviderIfConfigured(CreateConfiguration())
        );
    }

    [Fact]
    public void AddAwsKmsRemoteKeyProviderIfConfigured_NullConfiguration_Throws()
    {
        var builder = new ServiceCollection().AddSecretsLibrary().AddPersistenceLibrary();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddAwsKmsRemoteKeyProviderIfConfigured(null!)
        );
    }

    #endregion
}
