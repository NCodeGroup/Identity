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
            storeManager,
            CancellationToken.None
        );
        var flat = await strategy.ValidateTenantAsync(
            "acme",
            new Uri("https://op.example/acme"),
            storeManager,
            CancellationToken.None
        );

        Assert.Empty(nested);
        Assert.Single(flat);
        mocks.Verify();
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenIssuerIsAbsent_Succeeds()
    {
        // Without an issuer the tenant's base address is derived from the request it is served under.
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks.Create<IStoreManagerFactory>().Object);

        var failures = await strategy.ValidateTenantAsync(
            "acme",
            issuer: null,
            mocks.Create<IStoreManager>().Object,
            CancellationToken.None
        );

        Assert.Empty(failures);
        mocks.Verify();
    }

    #endregion
}
