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
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class ClientStorePagingTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _tenantStore;
    private readonly ClientStore _store;

    public ClientStorePagingTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
        serviceCollection.AddSingleton<IdValueGenerator>();
        serviceCollection.AddSingleton<UseIdGeneratorConvention>();
        serviceCollection.AddDbContext<OpenIdDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        );

        _provider = serviceCollection.BuildServiceProvider();
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

    private static PersistedClient CreateClient(string clientId) =>
        new()
        {
            TenantId = TenantId,
            ClientId = clientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    private async Task SeedAsync(int count)
    {
        var tenant = new PersistedTenant
        {
            TenantId = TenantId,
            ConcurrencyToken = string.Empty,
            DomainName = null,
            IsDisabled = false,
            DisplayName = TenantId,
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

        for (var index = 1; index <= count; index++)
        {
            await _store.AddAsync(CreateClient($"client-{index}"), CancellationToken.None);
        }
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetPageAsync_TraversesEveryItemExactlyOnce()
    {
        await SeedAsync(5);

        var seen = new List<string>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var page = await _store.GetPageAsync(cursor, 2, CancellationToken.None);
            Assert.True(page.Items.Count <= 2);
            seen.AddRange(page.Items.Select(client => client.ClientId));
            cursor = page.NextCursor;
            pageCount++;
        } while (cursor is not null);

        Assert.Equal(3, pageCount);
        Assert.Equal(5, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    [Fact]
    public async Task GetPageAsync_WhenAllFitOnOnePage_ReturnsNullCursor()
    {
        await SeedAsync(3);

        var page = await _store.GetPageAsync(cursor: null, limit: 10, CancellationToken.None);

        Assert.Equal(3, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task GetPageAsync_WhenCursorIsMalformed_StartsFromBeginning()
    {
        await SeedAsync(3);

        var page = await _store.GetPageAsync("not-a-valid-cursor", 10, CancellationToken.None);

        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public async Task GetPageAsync_WhenEmpty_ReturnsEmptyPageAndNullCursor()
    {
        var page = await _store.GetPageAsync(cursor: null, limit: 10, CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }
}
