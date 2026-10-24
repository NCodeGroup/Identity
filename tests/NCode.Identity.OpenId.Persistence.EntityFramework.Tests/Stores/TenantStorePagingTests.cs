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
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

public sealed class TenantStorePagingTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly OpenIdDbContext _dbContext;
    private readonly TenantStore _store;

    public TenantStorePagingTests()
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

    private static PersistedTenant CreateTenant(string tenantId, int index) =>
        new()
        {
            TenantId = tenantId,
            ConcurrencyToken = string.Empty,
            Issuer = null,
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(
                    new Dictionary<string, object> { ["index"] = index }
                ),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    private async Task SeedAsync(int count)
    {
        for (var index = 1; index <= count; index++)
        {
            await _store.AddAsync(CreateTenant($"tenant-{index}", index), CancellationToken.None);
        }
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #region GetSettingsPageAsync Tests

    [Fact]
    public async Task GetSettingsPageAsync_TraversesEveryTenantExactlyOnce()
    {
        await SeedAsync(5);

        var seen = new Dictionary<string, int>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var page = await _store.GetSettingsPageAsync(
                new PageRequest { Cursor = cursor, Limit = 2 },
                CancellationToken.None
            );
            Assert.True(page.Items.Count <= 2);
            foreach (var settings in page.Items)
            {
                Assert.False(string.IsNullOrEmpty(settings.ConcurrencyToken));
                seen.Add(settings.TenantId, settings.Value.GetProperty("index").GetInt32());
            }
            cursor = page.NextCursor;
            pageCount++;
        } while (cursor is not null);

        Assert.Equal(3, pageCount);
        Assert.Equal(5, seen.Count);
        for (var index = 1; index <= 5; index++)
        {
            Assert.Equal(index, seen[$"tenant-{index}"]);
        }
    }

    [Fact]
    public async Task GetSettingsPageAsync_ReadsWithoutTracking()
    {
        await SeedAsync(3);

        var page = await _store.GetSettingsPageAsync(
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );

        Assert.Equal(3, page.Items.Count);
        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    #endregion
}
