// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets;

public sealed class DefaultRegistrationTests
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
    [Fact]
    public async Task AddSecretsLibrary_ResolvesTheRemoteKeyProviderCatalog()
    {
        var providerMock = new Mock<IRemoteKeyProvider>(MockBehavior.Strict);
        providerMock.Setup(x => x.Name).Returns(RemoteKeyProviderNames.AwsKms).Verifiable();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(providerMock.Object);
        serviceCollection.AddSecretsLibrary();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var catalog = serviceProvider.GetRequiredService<IRemoteKeyProviderCatalog>();

        Assert.IsType<DefaultRemoteKeyProviderCatalog>(catalog);
        Assert.True(catalog.TryGetProvider(RemoteKeyProviderNames.AwsKms, out var provider));
        Assert.Same(providerMock.Object, provider);
        providerMock.Verify();
    }

    #endregion
}
