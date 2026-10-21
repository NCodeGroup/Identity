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

using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Provides a default implementation of <see cref="ITenantUsageStore"/> that uses Entity Framework Core for
/// persistence. An addition reads the day's entry and updates it under its concurrency token, which every provider
/// supports, so two replicas adding to the same entry cannot lose either count: the second commit fails and is retried.
/// </summary>
internal sealed class TenantUsageStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : ITenantUsageStore
{
    private IStoreProvider StoreProvider { get; } = storeProvider;

    private IIdGenerator<long> IdGenerator { get; } = idGenerator;

    private OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    public object? GetService(Type serviceType) => StoreProvider.GetService(serviceType);

    /// <inheritdoc />
    public TStore GetStore<TStore>()
        where TStore : IStore => StoreProvider.GetStore<TStore>();

    /// <inheritdoc />
    public async ValueTask AddAsync(
        string tenantId,
        DateOnly day,
        string meter,
        long count,
        CancellationToken cancellationToken
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var entity =
            DbContext.TenantUsage.Local.FirstOrDefault(usage =>
                usage.TenantId == tenantId && usage.Day == day && usage.Meter == meter
            )
            ?? await DbContext.TenantUsage.SingleOrDefaultAsync(
                usage => usage.TenantId == tenantId && usage.Day == day && usage.Meter == meter,
                cancellationToken
            );

        if (entity is not null)
        {
            entity.Count += count;
            return;
        }

        await DbContext.TenantUsage.AddAsync(
            new TenantUsageEntity
            {
                Id = IdGenerator.CreateId(),
                TenantId = tenantId,
                Day = day,
                Meter = meter,
                Count = count,
                ConcurrencyToken = Guid.NewGuid().ToString("N"),
            },
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PersistedTenantUsage>> ListAsync(
        string tenantId,
        DateOnly firstDay,
        DateOnly lastDay,
        CancellationToken cancellationToken
    )
    {
        var entities = await DbContext
            .TenantUsage.AsNoTracking()
            .Where(usage =>
                usage.TenantId == tenantId && usage.Day >= firstDay && usage.Day <= lastDay
            )
            .OrderBy(usage => usage.Day)
            .ThenBy(usage => usage.Meter)
            .ToListAsync(cancellationToken);

        return entities
            .Select(entity => new PersistedTenantUsage
            {
                TenantId = entity.TenantId,
                Day = entity.Day,
                Meter = entity.Meter,
                Count = entity.Count,
            })
            .ToList();
    }

    /// <inheritdoc />
    public async ValueTask<int> RemoveBeforeAsync(DateOnly day, CancellationToken cancellationToken)
    {
        var entities = await DbContext
            .TenantUsage.Where(usage => usage.Day < day)
            .ToListAsync(cancellationToken);

        DbContext.TenantUsage.RemoveRange(entities);
        return entities.Count;
    }
}
