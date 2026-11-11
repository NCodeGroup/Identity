// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TenantStoreHostLookupTests : IDisposable
{
    private ServiceProvider Provider { get; }
    private OpenIdDbContext DbContext { get; }
    private TenantStore Store { get; }

    public TenantStoreHostLookupTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        Provider = serviceCollection.BuildServiceProvider();
        DbContext = Provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = Provider.GetRequiredService<IIdGenerator<long>>();
        Store = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, DbContext);
        DbContext.SeedTeam(Provider.GetRequiredService<IIdGenerator<long>>(), "platform");
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Provider.Dispose();
    }

    private static PersistedTenant CreateTenant(
        string tenantId,
        string? issuer,
        string? mtlsEndpointAliasAuthority
    ) =>
        new PersistedTenantBuilder
        {
            TenantId = tenantId,
            Issuer = issuer,
            MtlsEndpointAliasAuthority = mtlsEndpointAliasAuthority,
        }.Build();

    private async ValueTask SeedTenantAsync(
        string tenantId,
        string? issuer,
        string? mtlsEndpointAliasAuthority,
        CancellationToken cancellationToken
    )
    {
        await Store.AddAsync(
            CreateTenant(tenantId, issuer, mtlsEndpointAliasAuthority),
            cancellationToken
        );
        await DbContext.SaveChangesAsync(cancellationToken);
        DbContext.ChangeTracker.Clear();
    }

    #region GetOrDefaultByHostAsync Tests

    [Fact]
    public async Task GetOrDefaultByHostAsync_WhenTheIssuerHostMatches_ReturnsTheTenant()
    {
        await SeedTenantAsync(
            "acme",
            "https://acme.auth.example",
            "https://acme.mtls.example",
            CancellationToken.None
        );

        var tenant = await Store.GetOrDefaultByHostAsync(
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
        await SeedTenantAsync(
            "acme",
            "https://acme.auth.example",
            "https://acme.mtls.example",
            CancellationToken.None
        );

        var tenant = await Store.GetOrDefaultByHostAsync(
            "acme.mtls.example",
            CancellationToken.None
        );

        Assert.NotNull(tenant);
        Assert.Equal("acme", tenant.TenantId);
    }

    [Fact]
    public async Task GetOrDefaultByHostAsync_WhenNoTenantHoldsTheHost_ReturnsNull()
    {
        await SeedTenantAsync("acme", "https://acme.auth.example", null, CancellationToken.None);

        var tenant = await Store.GetOrDefaultByHostAsync(
            "acme.mtls.example",
            CancellationToken.None
        );

        Assert.Null(tenant);
    }

    [Fact]
    public async Task GetOrDefaultByHostAsync_WhenTwoTenantsHoldTheHost_ReturnsNull()
    {
        await SeedTenantAsync(
            "acme",
            "https://acme.auth.example",
            "https://shared.example",
            CancellationToken.None
        );
        await SeedTenantAsync("globex", "https://shared.example", null, CancellationToken.None);

        var tenant = await Store.GetOrDefaultByHostAsync("shared.example", CancellationToken.None);

        Assert.Null(tenant);
    }

    [Fact]
    public async Task ListTenantIdsByHostAsync_WhenTwoTenantsHoldTheHost_ReturnsBoth()
    {
        await SeedTenantAsync(
            "acme",
            "https://acme.auth.example",
            "https://shared.example",
            CancellationToken.None
        );
        await SeedTenantAsync("globex", "https://shared.example", null, CancellationToken.None);
        await SeedTenantAsync(
            "initech",
            "https://initech.auth.example",
            null,
            CancellationToken.None
        );

        var tenantIds = await Store.ListTenantIdsByHostAsync(
            "SHARED.example",
            CancellationToken.None
        );

        Assert.Equal(["acme", "globex"], tenantIds.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task UpdateAsync_WhenTheAliasIsCleared_TheAliasHostNoLongerRoutes()
    {
        await SeedTenantAsync(
            "acme",
            "https://acme.auth.example",
            "https://acme.mtls.example",
            CancellationToken.None
        );
        var tenant = await Store.GetOrDefaultAsync("acme", CancellationToken.None);
        Assert.NotNull(tenant);

        tenant.MtlsEndpointAliasAuthority = null;
        await Store.UpdateAsync(tenant, CancellationToken.None);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        Assert.Null(
            await Store.GetOrDefaultByHostAsync("acme.mtls.example", CancellationToken.None)
        );
    }

    #endregion
}
