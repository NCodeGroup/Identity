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

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// EF InMemory supports no set-based delete, and the hermetic host runs on it (ADR-0213).
public sealed class TenantUsageStoreInMemoryTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string Requests = "requests";

    private static readonly DateOnly Day = new(2026, 10, 21);

    private readonly ServiceProvider _provider;

    public TenantUsageStoreInMemoryTests()
    {
        // Each scope builds its own options, so the database name is drawn once for all of them.
        var databaseName = Guid.NewGuid().ToString("N");

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(databaseName)
        );
        _provider = serviceCollection.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    private TenantUsageStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), _provider.GetRequiredService<IIdGenerator<long>>(), context);

    private async Task AddAsync(DateOnly day)
    {
        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await CreateStore(context)
            .AddAsync(
                Guid.NewGuid(),
                1,
                [
                    new DataContracts.PersistedTenantUsage
                    {
                        TenantId = TenantId,
                        Day = day,
                        Meter = Requests,
                        Count = 1,
                    },
                ],
                CancellationToken.None
            );
        await context.SaveChangesAsync();
    }

    #region RemoveBeforeAsync Tests

    [Fact]
    public async Task RemoveBeforeAsync_OnAProviderWithoutSetBasedDeletes_RemovesTheEarlierDays()
    {
        await AddAsync(Day.AddDays(-1));
        await AddAsync(Day);

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            await CreateStore(context).RemoveBeforeAsync(Day, 10, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        using var readScope = _provider.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var days = await readContext
            .Set<TenantUsageEntity>()
            .Select(entity => entity.Day)
            .ToListAsync();

        Assert.Equal([Day], days);
    }

    #endregion
}
