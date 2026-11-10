// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

// EF InMemory supports no set-based delete, and the hermetic host runs on it (ADR-1213).
public sealed class TenantUsageStoreInMemoryTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string Requests = "requests";

    private static DateOnly Day { get; } = new(2026, 10, 21);

    private ServiceProvider Provider { get; }

    public TenantUsageStoreInMemoryTests()
    {
        // Each scope builds its own options, so the database name is drawn once for all of them.
        var databaseName = Guid.NewGuid().ToString("N");

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestOpenIdDbContext(options =>
            options.UseInMemoryDatabase(databaseName)
        );
        Provider = serviceCollection.BuildServiceProvider();
    }

    public void Dispose()
    {
        Provider.Dispose();
    }

    private TenantUsageStore CreateStore(OpenIdDbContext context) =>
        new(Mock.Of<IStoreProvider>(), Provider.GetRequiredService<IIdGenerator<long>>(), context);

    private async Task AddAsync(DateOnly day)
    {
        using var scope = Provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await CreateStore(context)
            .AddAsync(
                Guid.NewGuid(),
                1,
                [
                    new DataContracts.PersistedTenantUsage
                    {
                        TenantId = TenantId,
                        TeamPath = "/platform/",
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

        using (var scope = Provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
            await CreateStore(context).RemoveBeforeAsync(Day, 10, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        using var readScope = Provider.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        var days = await readContext
            .Set<TenantUsageEntity>()
            .Select(entity => entity.Day)
            .ToListAsync();

        Assert.Equal([Day], days);
    }

    #endregion
}
