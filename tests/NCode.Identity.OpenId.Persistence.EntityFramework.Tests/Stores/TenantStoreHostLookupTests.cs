#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TenantStoreHostLookupTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _store;

    public TenantStoreHostLookupTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static PersistedTenant CreateTenant(
        string tenantId,
        string? issuer,
        string? mtlsEndpointAliasAuthority
    ) =>
        new()
        {
            TenantId = tenantId,
            ConcurrencyToken = string.Empty,
            Issuer = issuer,
            MtlsEndpointAliasAuthority = mtlsEndpointAliasAuthority,
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    private async Task SeedTenantAsync(
        string tenantId,
        string? issuer,
        string? mtlsEndpointAliasAuthority
    )
    {
        await _store.AddAsync(
            CreateTenant(tenantId, issuer, mtlsEndpointAliasAuthority),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #region GetOrDefaultByHostAsync Tests

    [Fact]
    public async Task GetOrDefaultByHostAsync_WhenTheIssuerHostMatches_ReturnsTheTenant()
    {
        await SeedTenantAsync("acme", "https://acme.auth.example", "https://acme.mtls.example");

        var tenant = await _store.GetOrDefaultByHostAsync(
            "ACME.auth.example",
            CancellationToken.None
        );

        Assert.NotNull(tenant);
        Assert.Equal("acme", tenant.TenantId);
        Assert.Equal("https://acme.mtls.example", tenant.MtlsEndpointAliasAuthority);
    }

    [Fact]
    public async Task GetOrDefaultByHostAsync_WhenTheAliasHostMatches_ReturnsTheTenant()
    {
        await SeedTenantAsync("acme", "https://acme.auth.example", "https://acme.mtls.example");

        var tenant = await _store.GetOrDefaultByHostAsync(
            "acme.mtls.example",
            CancellationToken.None
        );

        Assert.NotNull(tenant);
        Assert.Equal("acme", tenant.TenantId);
    }

    [Fact]
    public async Task GetOrDefaultByHostAsync_WhenNoTenantHoldsTheHost_ReturnsNull()
    {
        await SeedTenantAsync("acme", "https://acme.auth.example", null);

        var tenant = await _store.GetOrDefaultByHostAsync(
            "acme.mtls.example",
            CancellationToken.None
        );

        Assert.Null(tenant);
    }

    [Fact]
    public async Task GetOrDefaultByHostAsync_WhenTwoTenantsHoldTheHost_ReturnsNull()
    {
        await SeedTenantAsync("acme", "https://acme.auth.example", "https://shared.example");
        await SeedTenantAsync("globex", "https://shared.example", null);

        var tenant = await _store.GetOrDefaultByHostAsync("shared.example", CancellationToken.None);

        Assert.Null(tenant);
    }

    [Fact]
    public async Task UpdateAsync_WhenTheAliasIsCleared_TheAliasHostNoLongerRoutes()
    {
        await SeedTenantAsync("acme", "https://acme.auth.example", "https://acme.mtls.example");
        var tenant = await _store.GetOrDefaultAsync("acme", CancellationToken.None);
        Assert.NotNull(tenant);

        tenant.MtlsEndpointAliasAuthority = null;
        await _store.UpdateAsync(tenant, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Null(
            await _store.GetOrDefaultByHostAsync("acme.mtls.example", CancellationToken.None)
        );
    }

    #endregion
}
