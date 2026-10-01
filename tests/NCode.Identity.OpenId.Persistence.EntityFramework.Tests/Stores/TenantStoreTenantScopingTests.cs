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

using System.Text.Json;
using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// A tenant's secrets are tenant-scoped like every other tenant-owned row: the tenant entity itself stays unscoped
// (so it can be discovered by id during resolution), but its secrets are isolated by the ambient-tenant filter. In
// practice tenant resolution and tenant management read secrets with no ambient scope; these tests assert the filter.
public sealed class TenantStoreTenantScopingTests : IDisposable
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private const string SecretB = "secret-b";

    private readonly ServiceProvider _provider;
    private readonly TestAmbientTenantAccessor _ambientTenantAccessor = new();
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _store;

    public TenantStoreTenantScopingTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
        services.AddSingleton<IdValueGenerator>();
        services.AddSingleton<UseIdGeneratorConvention>();

        _provider = services.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<OpenIdDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .UseApplicationServiceProvider(_provider)
            .Options;

        _dbContext = new OpenIdDbContext(options, _ambientTenantAccessor);
        _store = new TenantStore(
            Mock.Of<IStoreProvider>(),
            _provider.GetRequiredService<IIdGenerator<long>>(),
            _dbContext
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    private static PersistedSecret CreateSecret(string secretId) =>
        new()
        {
            SecretId = secretId,
            ConcurrencyToken = string.Empty,
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretTypes.Symmetric,
            KeySizeBits = 256,
            EncodedValue = "encoded-value",
        };

    private async Task SeedTenantAsync(string tenantId, params PersistedSecret[] secrets)
    {
        var tenant = new PersistedTenant
        {
            TenantId = tenantId,
            ConcurrencyToken = string.Empty,
            DomainName = null,
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
                Value = secrets,
            },
        };

        await _store.AddAsync(tenant, CancellationToken.None);
    }

    private async Task SeedAsync()
    {
        await SeedTenantAsync(TenantA);
        await SeedTenantAsync(TenantB, CreateSecret(SecretB));
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenScopedToOtherTenant_ReturnsNull()
    {
        await SeedAsync();

        _ambientTenantAccessor.TenantId = TenantA;

        var crossTenant = await _store.GetSecretOrDefaultAsync(
            TenantB,
            SecretB,
            CancellationToken.None
        );

        Assert.Null(crossTenant);
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenUnscoped_ReturnsSecret()
    {
        await SeedAsync();

        var secret = await _store.GetSecretOrDefaultAsync(TenantB, SecretB, CancellationToken.None);

        Assert.NotNull(secret);
        Assert.Equal(SecretB, secret.SecretId);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenScopedToOtherTenant_LoadsTenantButFiltersItsSecrets()
    {
        await SeedAsync();

        _ambientTenantAccessor.TenantId = TenantA;

        // The tenant entity is unscoped (discoverable by id), but its secrets are tenant-scoped and filtered out.
        var tenantB = await _store.GetOrDefaultAsync(TenantB, CancellationToken.None);

        Assert.NotNull(tenantB);
        Assert.Empty(tenantB.Secrets.Value);
    }

    private sealed class TestAmbientTenantAccessor : IAmbientTenantAccessor
    {
        public string? TenantId { get; set; }

        public bool IsScoped => TenantId is not null;

        public IDisposable BeginScope(string tenantId)
        {
            TenantId = tenantId;
            return new NoopScope();
        }

        private sealed class NoopScope : IDisposable
        {
            public void Dispose() { }
        }
    }
}
