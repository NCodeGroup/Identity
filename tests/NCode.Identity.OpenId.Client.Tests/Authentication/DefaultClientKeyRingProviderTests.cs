// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Client.Authentication;
using NCode.Identity.Secrets.Collections;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Authentication;

public class DefaultClientKeyRingProviderTests : BaseTests
{
    private const string KeyRing = "ring";

    private static ServiceProvider CreateServiceProvider(ISecretKeyCollectionProvider provider)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddKeyedSingleton(KeyRing, provider);
        return serviceCollection.BuildServiceProvider();
    }

    private static DefaultClientKeyRingProvider CreateProvider(ServiceProvider serviceProvider) =>
        new(serviceProvider, serviceProvider.GetRequiredService<IServiceProviderIsKeyedService>());

    #region HasKeyRing Tests

    [Fact]
    public async Task HasKeyRing_Registered_ReturnsTrue()
    {
        var mockProvider = CreateStrictMock<ISecretKeyCollectionProvider>();
        await using var serviceProvider = CreateServiceProvider(mockProvider.Object);
        var provider = CreateProvider(serviceProvider);

        Assert.True(provider.HasKeyRing(KeyRing));
    }

    [Fact]
    public async Task HasKeyRing_Unregistered_ReturnsFalse()
    {
        var mockProvider = CreateStrictMock<ISecretKeyCollectionProvider>();
        await using var serviceProvider = CreateServiceProvider(mockProvider.Object);
        var provider = CreateProvider(serviceProvider);

        Assert.False(provider.HasKeyRing("missing"));
    }

    #endregion

    #region GetKeyRing Tests

    [Fact]
    public async Task GetKeyRing_Registered_ReturnsItsCollection()
    {
        var mockCollection = CreateStrictMock<ISecretKeyCollection>();
        var mockProvider = CreateStrictMock<ISecretKeyCollectionProvider>();
        mockProvider.Setup(x => x.Collection).Returns(mockCollection.Object).Verifiable();
        await using var serviceProvider = CreateServiceProvider(mockProvider.Object);
        var provider = CreateProvider(serviceProvider);

        var result = provider.GetKeyRing(KeyRing);

        Assert.Same(mockCollection.Object, result);
    }

    [Fact]
    public async Task GetKeyRing_Unregistered_Throws()
    {
        var mockProvider = CreateStrictMock<ISecretKeyCollectionProvider>();
        await using var serviceProvider = CreateServiceProvider(mockProvider.Object);
        var provider = CreateProvider(serviceProvider);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetKeyRing("missing")
        );

        Assert.Contains("'missing'", exception.Message);
    }

    #endregion
}
