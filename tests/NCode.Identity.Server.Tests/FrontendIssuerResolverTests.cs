// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Server.Frontend;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class FrontendIssuerResolverTests : BaseTests
{
    private const string RootTenantId = "root";

    private Mock<ITenantResolver> MockTenantResolver { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private DefaultHttpContext HttpContext { get; } = new();
    private FrontendIssuerResolver Resolver { get; }

    public FrontendIssuerResolverTests()
    {
        MockTenantResolver = CreateStrictMock<ITenantResolver>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTenantStore = CreateStrictMock<ITenantStore>();

        var resolutionOptions = new TenantResolutionOptions
        {
            Root = new RootTenantOptions { TenantId = RootTenantId },
        };

        Resolver = new FrontendIssuerResolver(
            MockTenantResolver.Object,
            Options.Create(resolutionOptions),
            MockStoreManagerFactory.Object
        );
    }

    private void SetupTenantId(string? tenantId)
    {
        var requestTenantId = tenantId;
        MockTenantResolver
            .Setup(x => x.TryGetTenantId(HttpContext, out requestTenantId))
            .Returns(tenantId is not null)
            .Verifiable();
    }

    private void SetupStoredTenant(string tenantId, PersistedTenant? persistedTenant)
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(persistedTenant)
            .Verifiable();
    }

    private void SetupDefaultIssuer(string tenantId, string? issuer) =>
        MockTenantResolver
            .Setup(x => x.GetDefaultIssuerAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issuer)
            .Verifiable();

    private static PersistedTenant CreateTenant(string tenantId, string? issuer) =>
        new PersistedTenantBuilder
        {
            TenantId = tenantId,
            Issuer = issuer,
            DisplayName = "Test Tenant",
            Settings = default,
        }.Build();

    #region ResolveIssuerAsync Tests

    [Fact]
    public async Task ResolveIssuerAsync_WhenTheRequestIdentifiesNoTenant_ReturnsTheRootTenantsStoredIssuer()
    {
        // Under a multi-tenant strategy the Management console is served for the root tenant, whatever the host.
        HttpContext.Request.Host = new HostString("manage.example");
        SetupTenantId(null);
        SetupStoredTenant(RootTenantId, CreateTenant(RootTenantId, "https://auth.example"));

        var issuer = await Resolver.ResolveIssuerAsync(HttpContext, CancellationToken.None);

        Assert.Equal("https://auth.example", issuer);
    }

    [Fact]
    public async Task ResolveIssuerAsync_WhenTheRequestIdentifiesATenant_ReturnsItsStoredIssuer()
    {
        // The static single-tenant strategy identifies its only tenant for every request.
        SetupTenantId("single");
        SetupStoredTenant("single", CreateTenant("single", "https://id.example"));

        var issuer = await Resolver.ResolveIssuerAsync(HttpContext, CancellationToken.None);

        Assert.Equal("https://id.example", issuer);
    }

    [Fact]
    public async Task ResolveIssuerAsync_WhenTheTenantIsNotProvisioned_ReturnsTheIssuerItsStrategyDerives()
    {
        SetupTenantId(null);
        SetupStoredTenant(RootTenantId, null);
        SetupDefaultIssuer(RootTenantId, "https://auth.example");

        var issuer = await Resolver.ResolveIssuerAsync(HttpContext, CancellationToken.None);

        Assert.Equal("https://auth.example", issuer);
    }

    [Fact]
    public async Task ResolveIssuerAsync_WhenNoIssuerIsStoredOrDerived_ReturnsNullRatherThanTheRequestOrigin()
    {
        HttpContext.Request.Scheme = "https";
        HttpContext.Request.Host = new HostString("attacker.example");
        SetupTenantId(null);
        SetupStoredTenant(RootTenantId, CreateTenant(RootTenantId, issuer: null));
        SetupDefaultIssuer(RootTenantId, null);

        var issuer = await Resolver.ResolveIssuerAsync(HttpContext, CancellationToken.None);

        Assert.Null(issuer);
    }

    #endregion
}
