// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets;

public sealed class DefaultRegistrationTests : BaseTests
{
    #region AddSecretsLibrary Tests

    // The key-type descriptors log, so the library registers logging and composes on a container that has none.
    [Fact]
    public async Task AddSecretsLibrary_WhenTheHostRegistersNoLogging_ResolvesTheKeyTypeCatalog()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var catalog = serviceProvider.GetRequiredService<ISecretKeyTypeCatalog>();

        Assert.True(catalog.TryGet(SecretKeyTypes.Ecc, out _));
    }

    // See ADR-1471.
    // See ADR-1474.
    [Fact]
    public async Task AddSecretsLibrary_ResolvesTheRemoteKeyCatalogs()
    {
        var providerMock = CreateStrictMock<IRemoteKeyProvider>();
        providerMock.Setup(x => x.Name).Returns(RemoteKeyProviderNames.AwsKms).Verifiable();
        var sourceMock = CreateStrictMock<IRemoteKeyDescriptorSource>();
        sourceMock
            .Setup(x => x.Providers)
            .Returns([new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AwsKms }])
            .Verifiable();
        sourceMock.Setup(x => x.Algorithms).Returns([]).Verifiable();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(providerMock.Object);
        serviceCollection.AddSingleton(sourceMock.Object);
        serviceCollection.AddSecretsLibrary();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var descriptorCatalog = serviceProvider.GetRequiredService<IRemoteKeyDescriptorCatalog>();
        var providerCatalog = serviceProvider.GetRequiredService<IRemoteKeyProviderCatalog>();
        var resolver = serviceProvider.GetRequiredService<IRemoteKeyResolver>();

        Assert.IsType<DefaultRemoteKeyDescriptorCatalog>(descriptorCatalog);
        Assert.IsType<DefaultRemoteKeyProviderCatalog>(providerCatalog);
        Assert.IsType<DefaultRemoteKeyResolver>(resolver);
        Assert.True(
            providerCatalog.TryGetProvider(RemoteKeyProviderNames.AwsKms, out var provider)
        );
        Assert.Same(providerMock.Object, provider);
    }

    #endregion
}
