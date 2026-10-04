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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tenants.Strategies;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class StaticSingleTenantStrategyTests
{
    private static StaticSingleTenantStrategy CreateStrategy(
        IStoreManagerFactory storeManagerFactory,
        StaticSingleTenantOptions? options = null
    ) =>
        new(
            storeManagerFactory,
            Options.Create(new TenantResolutionOptions { StaticSingle = options })
        );

    [Fact]
    public void TryGetTenantId_WhenConfigured_ReturnsConfiguredIdWithoutStore()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();

        var strategy = CreateStrategy(
            storeManagerFactory.Object,
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

        var strategy = CreateStrategy(storeManagerFactory.Object);

        var result = strategy.TryGetTenantId(new DefaultHttpContext(), out var tenantId);

        Assert.True(result);
        Assert.Equal(StaticSingleTenantOptions.DefaultTenantId, tenantId);
        mocks.Verify();
    }

    [Fact]
    public async Task ResolveTenantAsync_DelegatesToProvisioner()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();
        var provisioner = mocks.Create<ITenantProvisioner>();

        var persistedTenant = new PersistedTenant
        {
            TenantId = "tenant-1",
            DomainName = null,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = "Tenant One",
            Settings = null!,
            Secrets = null!,
        };

        provisioner
            .Setup(x => x.ProvisionAsync("tenant-1", "Tenant One", It.IsAny<CancellationToken>()))
            .ReturnsAsync(persistedTenant)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddSingleton(provisioner.Object);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };

        var strategy = CreateStrategy(
            storeManagerFactory.Object,
            new StaticSingleTenantOptions { TenantId = "tenant-1", DisplayName = "Tenant One" }
        );

        var result = await strategy.ResolveTenantAsync(httpContext, CancellationToken.None);

        Assert.Same(persistedTenant, result);
        mocks.Verify();
    }
}
