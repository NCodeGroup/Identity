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

public sealed class ClientStoreTenantScopingTests : IDisposable
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private const string ClientA = "client-a";
    private const string ClientB = "client-b";
    private const string SecretB = "secret-b";

    private readonly ServiceProvider _provider;
    private readonly TestAmbientTenantAccessor _ambientTenantAccessor = new();
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ClientStore _store;

    public ClientStoreTenantScopingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
        serviceCollection.AddSingleton<IdValueGenerator>();
        serviceCollection.AddSingleton<UseIdGeneratorConvention>();

        _provider = serviceCollection.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<OpenIdDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .UseApplicationServiceProvider(_provider)
            .Options;

        _dbContext = new OpenIdDbContext(options, _ambientTenantAccessor);
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _tenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _store = new ClientStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _provider.Dispose();
    }

    #region Helpers

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

    private static PersistedClient CreateClient(
        string tenantId,
        string clientId,
        params PersistedSecret[] secrets
    ) =>
        new()
        {
            TenantId = tenantId,
            ClientId = clientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = tenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = tenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = secrets,
            },
        };

    private async Task SeedTenantAsync(string tenantId)
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
                Value = [],
            },
        };

        await _tenantStore.AddAsync(tenant, CancellationToken.None);
    }

    // Seeds two tenants, each owning one client (tenant-b's client also has a secret), then detaches everything so
    // subsequent reads hit the database query where the tenant-scoping global filter applies.
    private async Task SeedAsync()
    {
        await SeedTenantAsync(TenantA);
        await SeedTenantAsync(TenantB);
        await _dbContext.SaveChangesAsync();

        await _store.AddAsync(CreateClient(TenantA, ClientA), CancellationToken.None);
        await _store.AddAsync(
            CreateClient(TenantB, ClientB, CreateSecret(SecretB)),
            CancellationToken.None
        );
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #endregion

    [Fact]
    public async Task GetOrDefaultAsync_WhenScopedToOtherTenant_ReturnsNull()
    {
        await SeedAsync();

        _ambientTenantAccessor.TenantId = TenantA;

        var crossTenant = await _store.GetOrDefaultAsync(ClientB, CancellationToken.None);
        var sameTenant = await _store.GetOrDefaultAsync(ClientA, CancellationToken.None);

        Assert.Null(crossTenant);
        Assert.NotNull(sameTenant);
    }

    [Fact]
    public async Task GetSecretOrDefaultAsync_WhenScopedToOtherTenant_ReturnsNull()
    {
        await SeedAsync();

        _ambientTenantAccessor.TenantId = TenantA;

        var crossTenant = await _store.GetSecretOrDefaultAsync(
            ClientB,
            SecretB,
            CancellationToken.None
        );

        Assert.Null(crossTenant);
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenUnscoped_ReturnsAllTenants()
    {
        await SeedAsync();

        var tenantA = await _store.GetOrDefaultAsync(ClientA, CancellationToken.None);
        var tenantB = await _store.GetOrDefaultAsync(ClientB, CancellationToken.None);

        Assert.NotNull(tenantA);
        Assert.NotNull(tenantB);
    }

    [Fact]
    public async Task GetPageAsync_WhenScopedToTenant_ReturnsOnlyThatTenant()
    {
        await SeedAsync();

        _ambientTenantAccessor.TenantId = TenantA;

        var page = await _store.GetPageAsync(cursor: null, limit: 10, CancellationToken.None);

        Assert.All(page.Items, client => Assert.Equal(TenantA, client.TenantId));
        Assert.Contains(page.Items, client => client.ClientId == ClientA);
        Assert.DoesNotContain(page.Items, client => client.ClientId == ClientB);
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
