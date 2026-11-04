// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Platform;

public class DefaultOpenIdPlatformProviderTests : BaseTests
{
    private CancellationTokenSource SettingsChanged { get; set; } = new();

    private (
        Mock<OpenIdEnvironment> Environment,
        Mock<OpenIdPlatform> Platform,
        Mock<IOpenIdTenantCache> TenantCache,
        Mock<IOpenIdTeamCache> TeamCache,
        DefaultOpenIdPlatformProvider Provider
    ) CreateProvider()
    {
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        var mockPlatform = CreateStrictMock<OpenIdPlatform>();

        var mockSettingsProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        mockSettingsProvider
            .Setup(x => x.GetChangeToken())
            .Returns(() => new CancellationChangeToken(SettingsChanged.Token))
            .Verifiable();
        mockPlatform
            .Setup(x => x.SettingsProvider)
            .Returns(mockSettingsProvider.Object)
            .Verifiable();

        var mockFactory = CreateStrictMock<IOpenIdPlatformFactory>();
        mockFactory
            .Setup(x => x.CreateAsync(mockEnvironment.Object, CancellationToken.None))
            .ReturnsAsync(mockPlatform.Object)
            .Verifiable(Times.Once());

        var mockRegistry = CreateStrictMock<IMaterializedPlatformRegistry>();
        mockRegistry.Setup(x => x.Register(mockPlatform.Object)).Verifiable(Times.Once());

        var mockTenantCache = CreateStrictMock<IOpenIdTenantCache>();

        var mockTeamCache = CreateStrictMock<IOpenIdTeamCache>();

        var provider = new DefaultOpenIdPlatformProvider(
            mockFactory.Object,
            mockRegistry.Object,
            mockTenantCache.Object,
            mockTeamCache.Object,
            CreateLooseMock<ILogger<DefaultOpenIdPlatformProvider>>().Object
        );

        return (mockEnvironment, mockPlatform, mockTenantCache, mockTeamCache, provider);
    }

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WhenItCreatesThePlatform_RegistersItOnce()
    {
        var (mockEnvironment, mockPlatform, _, _, provider) = CreateProvider();
        await using var _ = provider;

        var first = await provider.GetAsync(mockEnvironment.Object, CancellationToken.None);
        var second = await provider.GetAsync(mockEnvironment.Object, CancellationToken.None);

        Assert.Same(mockPlatform.Object, first);
        Assert.Same(mockPlatform.Object, second);
    }

    [Fact]
    public async Task GetAsync_WhenThePlatformSettingsChange_EvictsEveryCachedTenant()
    {
        var (mockEnvironment, _, mockTenantCache, mockTeamCache, provider) = CreateProvider();
        await using var _ = provider;
        mockTenantCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));
        mockTeamCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), CancellationToken.None))
            .Returns(ValueTask.CompletedTask);
        await provider.GetAsync(mockEnvironment.Object, CancellationToken.None);

        var first = SettingsChanged;
        SettingsChanged = new CancellationTokenSource();
        await first.CancelAsync();
        var second = SettingsChanged;
        SettingsChanged = new CancellationTokenSource();
        await second.CancelAsync();

        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public async Task GetAsync_WhenThePlatformSettingsChange_EvictsEveryCachedTeam()
    {
        var (mockEnvironment, _, mockTenantCache, mockTeamCache, provider) = CreateProvider();
        await using var _ = provider;
        mockTenantCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), CancellationToken.None))
            .Returns(ValueTask.CompletedTask);
        mockTeamCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        await provider.GetAsync(mockEnvironment.Object, CancellationToken.None);

        var changed = SettingsChanged;
        SettingsChanged = new CancellationTokenSource();
        await changed.CancelAsync();
        changed.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_AfterThePlatformIsCreated_StopsEvictingTenants()
    {
        var (mockEnvironment, _, mockTenantCache, mockTeamCache, provider) = CreateProvider();
        mockTenantCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Never());
        mockTeamCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Never());
        await provider.GetAsync(mockEnvironment.Object, CancellationToken.None);

        await provider.DisposeAsync();

        // A fresh source stands in before the old one fires, so a registration that outlived the dispose fails the
        // Times.Never() check instead of re-firing on the already cancelled token.
        var changed = SettingsChanged;
        SettingsChanged = new CancellationTokenSource();
        await changed.CancelAsync();
        changed.Dispose();
    }

    #endregion
}
