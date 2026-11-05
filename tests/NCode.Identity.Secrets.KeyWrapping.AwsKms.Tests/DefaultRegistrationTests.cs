// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Secrets.KeyWrapping.AwsKms.Facades;
using NCode.Identity.Secrets.KeyWrapping.AwsKms.Options;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Tests;

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
}
