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

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class RootTenantSeedingTests(PlaygroundApplicationFactory factory)
    : IClassFixture<PlaygroundApplicationFactory>
{
    private PlaygroundApplicationFactory Factory { get; } = factory;

    [Fact]
    public async Task ResolvingWorkloadTenant_AlsoProvisionsTheRootTenant()
    {
        // A first request forces the static single-tenant strategy to resolve (and lazily provision) the tenant,
        // which must also ensure the root (control-plane) tenant exists (ADR-0024).
        using (var warmupClient = Factory.CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        using var scope = Factory.Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<ITenantStore>();

        var rootTenant = await store.GetOrDefaultAsync(
            TenantResolutionOptions.DefaultRootTenantId,
            CancellationToken.None
        );

        Assert.NotNull(rootTenant);
        Assert.Equal(TenantResolutionOptions.DefaultRootTenantId, rootTenant.TenantId);
    }
}
