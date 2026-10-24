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

public sealed class TenantStoreRetirementTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string Issuer = "https://op.example/tenant-1";
    private const string NewIssuer = "https://op.example/tenant-1-renamed";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _store;

    public TenantStoreRetirementTests()
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

    private static PersistedTenant CreateTenant(string? issuer) =>
        new()
        {
            TenantId = TenantId,
            ConcurrencyToken = string.Empty,
            Issuer = issuer,
            IsDisabled = false,
            DisplayName = "Tenant One",
            Settings = new PersistedTenantSettings
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    private async Task SeedTenantAsync(string? issuer)
    {
        await _store.AddAsync(CreateTenant(issuer), CancellationToken.None);
        await SaveAndClearAsync();
    }

    private async Task SaveAndClearAsync()
    {
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task ChangeIssuerAsync(string? issuer)
    {
        var tenant = await _store.GetOrDefaultAsync(TenantId, CancellationToken.None);
        Assert.NotNull(tenant);
        tenant.Issuer = issuer;
        await _store.UpdateAsync(tenant, CancellationToken.None);
        await SaveAndClearAsync();
    }

    #region IsTenantIdRetiredAsync Tests

    [Fact]
    public async Task IsTenantIdRetiredAsync_WhenNothingIsRetired_ReturnsFalse()
    {
        await SeedTenantAsync(Issuer);

        var isRetired = await _store.IsTenantIdRetiredAsync(TenantId, CancellationToken.None);

        Assert.False(isRetired);
    }

    #endregion

    #region GetRetiredIssuerHolderOrDefaultAsync Tests

    [Fact]
    public async Task GetRetiredIssuerHolderOrDefaultAsync_WhenNothingIsRetired_ReturnsNull()
    {
        await SeedTenantAsync(Issuer);

        var holder = await _store.GetRetiredIssuerHolderOrDefaultAsync(
            Issuer,
            CancellationToken.None
        );

        Assert.Null(holder);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WhenTenantHasAnIssuer_RetiresTheIdentifierAndTheIssuer()
    {
        await SeedTenantAsync(Issuer);

        var removed = await _store.RemoveAsync(TenantId, CancellationToken.None);
        await SaveAndClearAsync();

        Assert.True(removed);
        Assert.True(
            await _store.IsTenantIdRetiredAsync(TenantId.ToUpperInvariant(), CancellationToken.None)
        );
        Assert.Equal(
            TenantId,
            await _store.GetRetiredIssuerHolderOrDefaultAsync(
                Issuer.ToUpperInvariant(),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task RemoveAsync_WhenTenantHasNoIssuer_RetiresOnlyTheIdentifier()
    {
        await SeedTenantAsync(issuer: null);

        await _store.RemoveAsync(TenantId, CancellationToken.None);
        await SaveAndClearAsync();

        Assert.True(await _store.IsTenantIdRetiredAsync(TenantId, CancellationToken.None));
        Assert.Empty(await _dbContext.RetiredIssuers.ToListAsync());
    }

    [Fact]
    public async Task RemoveAsync_WhenTheIssuerIsAlreadyRetired_KeepsASingleRetiredIssuer()
    {
        await SeedTenantAsync(Issuer);
        await ChangeIssuerAsync(NewIssuer);
        await ChangeIssuerAsync(Issuer);

        await _store.RemoveAsync(TenantId, CancellationToken.None);
        await SaveAndClearAsync();

        var retiredIssuers = await _dbContext
            .RetiredIssuers.OrderBy(entity => entity.Issuer)
            .Select(entity => entity.Issuer)
            .ToListAsync();
        Assert.Equal([Issuer, NewIssuer], retiredIssuers);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WhenTheIssuerChanges_RetiresThePreviousIssuer()
    {
        await SeedTenantAsync(Issuer);

        await ChangeIssuerAsync(NewIssuer);

        Assert.Equal(
            TenantId,
            await _store.GetRetiredIssuerHolderOrDefaultAsync(Issuer, CancellationToken.None)
        );
        Assert.Null(
            await _store.GetRetiredIssuerHolderOrDefaultAsync(NewIssuer, CancellationToken.None)
        );
        Assert.False(await _store.IsTenantIdRetiredAsync(TenantId, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WhenTheIssuerIsCleared_RetiresThePreviousIssuer()
    {
        await SeedTenantAsync(Issuer);

        await ChangeIssuerAsync(issuer: null);

        Assert.Equal(
            TenantId,
            await _store.GetRetiredIssuerHolderOrDefaultAsync(Issuer, CancellationToken.None)
        );
    }

    [Fact]
    public async Task UpdateAsync_WhenOnlyTheIssuerCaseChanges_RetiresNothing()
    {
        await SeedTenantAsync(Issuer);

        await ChangeIssuerAsync(Issuer.ToUpperInvariant());

        Assert.Empty(await _dbContext.RetiredIssuers.ToListAsync());
    }

    #endregion
}
