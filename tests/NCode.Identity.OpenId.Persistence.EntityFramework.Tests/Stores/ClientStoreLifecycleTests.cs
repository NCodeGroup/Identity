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
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class ClientStoreLifecycleTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ClientStore _store;

    public ClientStoreLifecycleTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
        services.AddSingleton<IdValueGenerator>();
        services.AddSingleton<UseIdGeneratorConvention>();
        services.AddDbContext<OpenIdDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = services.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<OpenIdDbContext>();
        var idGenerator = _provider.GetRequiredService<IIdGenerator<long>>();
        _tenantStore = new TenantStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
        _store = new ClientStore(Mock.Of<IStoreProvider>(), idGenerator, _dbContext);
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

    private static PersistedClient CreateClient(params PersistedSecret[] secrets) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = string.Empty,
                Value = secrets,
            },
        };

    private async Task SeedTenantAsync()
    {
        var tenant = new PersistedTenant
        {
            TenantId = TenantId,
            ConcurrencyToken = string.Empty,
            DomainName = null,
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

        await _tenantStore.AddAsync(tenant, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedClientAsync(params PersistedSecret[] secrets)
    {
        await SeedTenantAsync();
        await _store.AddAsync(CreateClient(secrets), CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AddAsync_AssignsRowConcurrencyToken()
    {
        await SeedTenantAsync();
        var client = CreateClient();

        await _store.AddAsync(client, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(client.ConcurrencyToken));
    }

    [Fact]
    public async Task RemoveAsync_WhenNoSecrets_RemovesAndReturnsTrue()
    {
        await SeedClientAsync();

        var removed = await _store.RemoveAsync(ClientId, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.True(removed);
        var reloaded = await _store.GetOrDefaultAsync(ClientId, CancellationToken.None);
        Assert.Null(reloaded);
    }

    [Fact]
    public async Task RemoveAsync_WhenHasSecrets_Throws()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _store.RemoveAsync(ClientId, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RemoveAsync_WhenMissing_ReturnsFalse()
    {
        var removed = await _store.RemoveAsync("missing", CancellationToken.None);

        Assert.False(removed);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenNoSecrets_ReturnsFalse()
    {
        await SeedClientAsync();

        var result = await _store.HasDependentsAsync(ClientId, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task HasDependentsAsync_WhenHasSecrets_ReturnsTrue()
    {
        await SeedClientAsync(CreateSecret("secret-1"));

        var result = await _store.HasDependentsAsync(ClientId, CancellationToken.None);

        Assert.True(result);
    }
}
