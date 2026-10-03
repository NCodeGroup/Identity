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
using NCode.Identity.OpenId.ResourceServers;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tenants.Strategies;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class StaticSingleTenantStrategyTests
{
    private static StaticSingleTenantStrategy CreateStrategy(
        IStoreManagerFactory storeManagerFactory,
        ISystemResourceServerSeeder seeder,
        StaticSingleTenantOptions? options = null
    ) =>
        new(
            storeManagerFactory,
            Options.Create(new TenantResolutionOptions { StaticSingle = options }),
            seeder
        );

    [Fact]
    public void TryGetTenantId_WhenConfigured_ReturnsConfiguredIdWithoutStore()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();
        var seeder = mocks.Create<ISystemResourceServerSeeder>();

        var strategy = CreateStrategy(
            storeManagerFactory.Object,
            seeder.Object,
            new StaticSingleTenantOptions { TenantId = "tenant-1" }
        );

        var result = strategy.TryGetTenantId(new DefaultHttpContext(), out var tenantId);

        Assert.True(result);
        Assert.Equal("tenant-1", tenantId);
        mocks.Verify();
    }

    [Fact]
    public void TryGetTenantId_WhenNotConfigured_ReturnsDefaultId()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();
        var seeder = mocks.Create<ISystemResourceServerSeeder>();

        var strategy = CreateStrategy(storeManagerFactory.Object, seeder.Object);

        var result = strategy.TryGetTenantId(new DefaultHttpContext(), out var tenantId);

        Assert.True(result);
        Assert.Equal(StaticSingleTenantOptions.DefaultTenantId, tenantId);
        mocks.Verify();
    }
}
