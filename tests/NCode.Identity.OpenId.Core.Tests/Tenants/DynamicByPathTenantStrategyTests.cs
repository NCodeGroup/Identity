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
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class DynamicByPathTenantStrategyTests
{
    private static DynamicByPathTenantStrategy CreateStrategy(
        IStoreManagerFactory storeManagerFactory,
        DynamicByPathTenantOptions? options = null
    ) =>
        new(
            storeManagerFactory,
            Options.Create(new TenantResolutionOptions { DynamicByPath = options })
        );

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
}
