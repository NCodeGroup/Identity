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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Servers;

public class DefaultOpenIdServerProviderTests : BaseTests
{
    private CancellationTokenSource SettingsChanged { get; set; } = new();

    private (
        Mock<OpenIdEnvironment> Environment,
        Mock<OpenIdServer> Server,
        Mock<IOpenIdTenantCache> TenantCache,
        DefaultOpenIdServerProvider Provider
    ) CreateProvider()
    {
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        var mockServer = CreateStrictMock<OpenIdServer>();

        var mockSettingsProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        mockSettingsProvider
            .Setup(x => x.GetChangeToken())
            .Returns(() => new CancellationChangeToken(SettingsChanged.Token))
            .Verifiable();
        mockServer.Setup(x => x.SettingsProvider).Returns(mockSettingsProvider.Object).Verifiable();

        var mockFactory = CreateStrictMock<IOpenIdServerFactory>();
        mockFactory
            .Setup(x => x.CreateAsync(mockEnvironment.Object, CancellationToken.None))
            .Returns(ValueTask.FromResult(mockServer.Object))
            .Verifiable(Times.Once());

        var mockRegistry = CreateStrictMock<IMaterializedServerRegistry>();
        mockRegistry.Setup(x => x.Register(mockServer.Object)).Verifiable(Times.Once());

        var mockTenantCache = CreateStrictMock<IOpenIdTenantCache>();

        var provider = new DefaultOpenIdServerProvider(
            mockFactory.Object,
            mockRegistry.Object,
            mockTenantCache.Object,
            CreateLooseMock<ILogger<DefaultOpenIdServerProvider>>().Object
        );

        return (mockEnvironment, mockServer, mockTenantCache, provider);
    }

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WhenItCreatesTheServer_RegistersItOnce()
    {
        var (mockEnvironment, mockServer, _, provider) = CreateProvider();
        await using var _ = provider;

        var first = await provider.GetAsync(mockEnvironment.Object, CancellationToken.None);
        var second = await provider.GetAsync(mockEnvironment.Object, CancellationToken.None);

        Assert.Same(mockServer.Object, first);
        Assert.Same(mockServer.Object, second);
    }

    [Fact]
    public async Task GetAsync_WhenTheServerSettingsChange_EvictsEveryCachedTenant()
    {
        var (mockEnvironment, _, mockTenantCache, provider) = CreateProvider();
        await using var _ = provider;
        mockTenantCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));
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
    public async Task DisposeAsync_AfterTheServerIsCreated_StopsEvictingTenants()
    {
        var (mockEnvironment, _, mockTenantCache, provider) = CreateProvider();
        mockTenantCache
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
