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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tenants.Strategies;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class DynamicByPathTenantStrategyTests : IDisposable
{
    private ServiceProvider ServiceProvider { get; } =
        new ServiceCollection().AddRouting().BuildServiceProvider();

    public void Dispose()
    {
        ServiceProvider.Dispose();
    }

    private DynamicByPathTenantStrategy CreateStrategy(
        IStoreManagerFactory storeManagerFactory,
        DynamicByPathTenantOptions? options = null
    ) =>
        new(
            storeManagerFactory,
            Options.Create(new TenantResolutionOptions { DynamicByPath = options }),
            ServiceProvider.GetRequiredService<TemplateBinderFactory>()
        );

    #region TryGetTenantId Tests

    [Fact]
    public void TryGetTenantId_WhenRouteValuePresent_ReturnsTrueWithoutStore()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();

        var strategy = CreateStrategy(storeManagerFactory.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["tenantId"] = "tenant-1";

        var result = strategy.TryGetTenantId(httpContext, out var tenantId);

        Assert.True(result);
        Assert.Equal("tenant-1", tenantId);
        mocks.Verify();
    }

    [Fact]
    public void TryGetTenantId_WhenRouteValueMissing_ReturnsFalse()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();

        var strategy = CreateStrategy(storeManagerFactory.Object);

        var httpContext = new DefaultHttpContext();

        var result = strategy.TryGetTenantId(httpContext, out var tenantId);

        Assert.False(result);
        Assert.Null(tenantId);
        mocks.Verify();
    }

    [Fact]
    public void TryGetTenantId_WhenRouteValueEmpty_ReturnsFalse()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();

        var strategy = CreateStrategy(storeManagerFactory.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["tenantId"] = string.Empty;

        var result = strategy.TryGetTenantId(httpContext, out var tenantId);

        Assert.False(result);
        Assert.Null(tenantId);
        mocks.Verify();
    }

    [Fact]
    public void TryGetTenantId_HonorsConfiguredRouteParameterName()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();

        var strategy = CreateStrategy(
            storeManagerFactory.Object,
            new DynamicByPathTenantOptions { TenantIdRouteParameterName = "tid" }
        );

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["tid"] = "tenant-2";

        var result = strategy.TryGetTenantId(httpContext, out var tenantId);

        Assert.True(result);
        Assert.Equal("tenant-2", tenantId);
        mocks.Verify();
    }

    #endregion

    #region ValidateTenantAsync Tests

    [Theory]
    [InlineData("https://op.example/acme")]
    [InlineData("https://op.example/acme/")]
    [InlineData("https://op.example/base/acme")]
    public async Task ValidateTenantAsync_WhenIssuerEndsWithTheTenantsRoute_Succeeds(string issuer)
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks.Create<IStoreManagerFactory>().Object);

        var failures = await strategy.ValidateTenantAsync(
            "acme",
            new Uri(issuer),
            null,
            mocks.Create<IStoreManager>().Object,
            CancellationToken.None
        );

        Assert.Empty(failures);
        mocks.Verify();
    }

    [Theory]
    [InlineData("https://op.example")]
    [InlineData("https://op.example/globex")]
    [InlineData("https://op.example/xacme")]
    [InlineData("https://op.example/acme/v2")]
    public async Task ValidateTenantAsync_WhenIssuerDoesNotEndWithTheTenantsRoute_Fails(
        string issuer
    )
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks.Create<IStoreManagerFactory>().Object);

        var failures = await strategy.ValidateTenantAsync(
            "acme",
            new Uri(issuer),
            null,
            mocks.Create<IStoreManager>().Object,
            CancellationToken.None
        );

        var failure = Assert.Single(failures);
        Assert.False(failure.IsConflict);
        Assert.Contains("must end with '/acme'", failure.Detail, StringComparison.Ordinal);
        mocks.Verify();
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenRouteIsNested_BindsTheWholeRoute()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            new DynamicByPathTenantOptions { TenantPath = "/tenants/{tenantId}" }
        );
        var storeManager = mocks.Create<IStoreManager>().Object;

        var nested = await strategy.ValidateTenantAsync(
            "acme",
            new Uri("https://op.example/tenants/acme"),
            null,
            storeManager,
            CancellationToken.None
        );
        var flat = await strategy.ValidateTenantAsync(
            "acme",
            new Uri("https://op.example/acme"),
            null,
            storeManager,
            CancellationToken.None
        );

        Assert.Empty(nested);
        Assert.Single(flat);
        mocks.Verify();
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenIssuerIsAbsent_FailsBecauseTheTenantIsServedOnlyBeneathOne()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks.Create<IStoreManagerFactory>().Object);

        var failures = await strategy.ValidateTenantAsync(
            "acme",
            issuer: null,
            null,
            mocks.Create<IStoreManager>().Object,
            CancellationToken.None
        );

        var failure = Assert.Single(failures);
        Assert.Contains("requires an issuer", failure.Detail, StringComparison.Ordinal);
        Assert.Contains("issuer template", failure.Detail, StringComparison.Ordinal);
        mocks.Verify();
    }

    #endregion

    #region GetDefaultIssuerAsync Tests

    [Fact]
    public async Task GetDefaultIssuerAsync_WhenTemplateIsConfigured_ExpandsItForTheTenant()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            new DynamicByPathTenantOptions { IssuerTemplate = "https://op.example/{tenantId}" }
        );

        var issuer = await strategy.GetDefaultIssuerAsync("acme", CancellationToken.None);

        Assert.Equal("https://op.example/acme", issuer);
        mocks.Verify();
    }

    [Fact]
    public async Task GetDefaultIssuerAsync_WhenNoTemplate_ReturnsNone()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks.Create<IStoreManagerFactory>().Object);

        var issuer = await strategy.GetDefaultIssuerAsync("acme", CancellationToken.None);

        Assert.Null(issuer);
        mocks.Verify();
    }

    [Fact]
    public async Task GetDefaultIssuerAsync_WhenRootTenantHasAConfiguredIssuer_PrefersItOverTheTemplate()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = new DynamicByPathTenantStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            Options.Create(
                new TenantResolutionOptions
                {
                    Root = { Issuer = "https://control.example/root" },
                    DynamicByPath = new DynamicByPathTenantOptions
                    {
                        IssuerTemplate = "https://op.example/{tenantId}",
                    },
                }
            ),
            ServiceProvider.GetRequiredService<TemplateBinderFactory>()
        );

        var rootIssuer = await strategy.GetDefaultIssuerAsync(
            RootTenantOptions.DefaultTenantId,
            CancellationToken.None
        );
        var workloadIssuer = await strategy.GetDefaultIssuerAsync("acme", CancellationToken.None);

        Assert.Equal("https://control.example/root", rootIssuer);
        Assert.Equal("https://op.example/acme", workloadIssuer);
        mocks.Verify();
    }

    #endregion

    #region ResolveTenantAsync Tests

    [Fact]
    public async Task ResolveTenantAsync_WhenTenantExists_ReturnsIt()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var tenant = CreateTenant("acme", isDisabled: false);
        var storeManagerFactory = SetupStore(mocks, "acme", tenant);
        var strategy = CreateStrategy(storeManagerFactory.Object);

        var result = await strategy.ResolveTenantAsync(
            CreateHttpContext("acme", provisioner: null),
            CancellationToken.None
        );

        Assert.Same(tenant, result);
        mocks.Verify();
    }

    [Fact]
    public async Task ResolveTenantAsync_WhenTenantIsMissing_ThrowsNotFound()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = SetupStore(mocks, "acme", tenant: null);
        var strategy = CreateStrategy(storeManagerFactory.Object);

        // A missing workload tenant is never created on demand: only the management API creates one.
        await Assert.ThrowsAsync<HttpResultException>(async () =>
            await strategy.ResolveTenantAsync(
                CreateHttpContext("acme", provisioner: null),
                CancellationToken.None
            )
        );

        mocks.Verify();
    }

    [Fact]
    public async Task ResolveTenantAsync_WhenTenantIsDisabled_ThrowsNotFound()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = SetupStore(mocks, "acme", CreateTenant("acme", isDisabled: true));
        var strategy = CreateStrategy(storeManagerFactory.Object);

        await Assert.ThrowsAsync<HttpResultException>(async () =>
            await strategy.ResolveTenantAsync(
                CreateHttpContext("acme", provisioner: null),
                CancellationToken.None
            )
        );

        mocks.Verify();
    }

    [Fact]
    public async Task ResolveTenantAsync_WhenRootTenantIsMissing_CreatesItOnDemand()
    {
        const string rootTenantId = RootTenantOptions.DefaultTenantId;

        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = SetupStore(mocks, rootTenantId, tenant: null);
        var provisioner = mocks.Create<ITenantProvisioner>();
        var root = CreateTenant(rootTenantId, isDisabled: false);
        provisioner
            .Setup(x =>
                x.EnsureCreatedAsync(
                    rootTenantId,
                    RootTenantOptions.DefaultDisplayName,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(root)
            .Verifiable();
        var strategy = CreateStrategy(storeManagerFactory.Object);

        var result = await strategy.ResolveTenantAsync(
            CreateHttpContext(rootTenantId, provisioner.Object),
            CancellationToken.None
        );

        Assert.Same(root, result);
        mocks.Verify();
    }

    #endregion

    #region GetControlPlaneTenantId Tests

    [Fact]
    public void GetControlPlaneTenantId_Always_ReturnsTheRootTenantId()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks.Create<IStoreManagerFactory>().Object);

        Assert.Equal(RootTenantOptions.DefaultTenantId, strategy.GetControlPlaneTenantId());
    }

    #endregion

    #region ResolveControlPlaneTenantAsync Tests

    [Fact]
    public async Task ResolveControlPlaneTenantAsync_WhenTheRequestAddressesAnotherTenant_ReturnsTheRootTenant()
    {
        const string rootTenantId = RootTenantOptions.DefaultTenantId;

        var mocks = new MockRepository(MockBehavior.Strict);
        var root = CreateTenant(rootTenantId, isDisabled: false);
        var storeManagerFactory = SetupStore(mocks, rootTenantId, root);
        var strategy = CreateStrategy(storeManagerFactory.Object);

        var result = await strategy.ResolveControlPlaneTenantAsync(
            CreateHttpContext("acme", provisioner: null),
            CancellationToken.None
        );

        Assert.Same(root, result);
        mocks.Verify();
    }

    [Fact]
    public async Task ResolveControlPlaneTenantAsync_WhenRootTenantIsMissing_CreatesItOnDemand()
    {
        const string rootTenantId = RootTenantOptions.DefaultTenantId;

        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = SetupStore(mocks, rootTenantId, tenant: null);
        var provisioner = mocks.Create<ITenantProvisioner>();
        var root = CreateTenant(rootTenantId, isDisabled: false);
        provisioner
            .Setup(x =>
                x.EnsureCreatedAsync(
                    rootTenantId,
                    RootTenantOptions.DefaultDisplayName,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(root)
            .Verifiable();
        var strategy = CreateStrategy(storeManagerFactory.Object);

        // A collection route such as POST /api/tenants carries no tenant route value at all.
        var httpContext = CreateHttpContext("acme", provisioner.Object);
        httpContext.Request.RouteValues.Clear();

        var result = await strategy.ResolveControlPlaneTenantAsync(
            httpContext,
            CancellationToken.None
        );

        Assert.Same(root, result);
        mocks.Verify();
    }

    #endregion

    private static Mock<IStoreManagerFactory> SetupStore(
        MockRepository mocks,
        string tenantId,
        PersistedTenant? tenant
    )
    {
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();
        var storeManager = mocks.Create<IStoreManager>();
        var store = mocks.Create<ITenantStore>();
        storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(storeManager.Object)
            .Verifiable();
        storeManager.Setup(x => x.GetStore<ITenantStore>()).Returns(store.Object).Verifiable();
        storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        store
            .Setup(x => x.GetOrDefaultAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant)
            .Verifiable();
        return storeManagerFactory;
    }

    private static DefaultHttpContext CreateHttpContext(
        string tenantId,
        ITenantProvisioner? provisioner
    )
    {
        var services = new ServiceCollection();
        if (provisioner is not null)
            services.AddSingleton(provisioner);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
        httpContext.Request.RouteValues["tenantId"] = tenantId;
        return httpContext;
    }

    private static PersistedTenant CreateTenant(string tenantId, bool isDisabled) =>
        new()
        {
            TenantId = tenantId,
            Issuer = $"https://op.example/{tenantId}",
            ConcurrencyToken = "token",
            IsDisabled = isDisabled,
            DisplayName = tenantId,
            Settings = null!,
            Secrets = null!,
        };
}
