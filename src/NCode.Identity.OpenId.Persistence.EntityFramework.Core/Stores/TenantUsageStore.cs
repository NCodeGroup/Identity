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
/// persistence. An addition reads the batch's entries for each day in one query and updates them under their
/// concurrency tokens, which every provider supports, so two replicas adding to the same entry cannot lose either count:
/// the second commit fails and is retried. Each addition records the replica's last added batch in the same unit of
/// work, so a batch the replica retries after a save that committed is skipped.
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
        Guid replicaId,
        long batchNumber,
        IReadOnlyCollection<PersistedTenantUsage> usage,
        CancellationToken cancellationToken
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchNumber, 1);
        ArgumentOutOfRangeException.ThrowIfZero(usage.Count, nameof(usage));

        // A checkpoint added earlier in this unit of work is not yet in the database.
        var checkpoint =
            DbContext.TenantUsageCheckpoints.Local.FirstOrDefault(entity =>
                entity.ReplicaId == replicaId
            )
            ?? await DbContext.TenantUsageCheckpoints.SingleOrDefaultAsync(
                entity => entity.ReplicaId == replicaId,
                cancellationToken
            );

        // The replica added this batch before: a save that committed but reported a failure is retried under its number.
        if (checkpoint is not null && checkpoint.LastBatchNumber >= batchNumber)
            return;

        await AddUsageAsync(usage, cancellationToken);

        var lastDay = usage.Max(entry => entry.Day);
        if (checkpoint is null)
        {
            await DbContext.TenantUsageCheckpoints.AddAsync(
                new TenantUsageCheckpointEntity
                {
                    Id = IdGenerator.CreateId(),
                    ReplicaId = replicaId,
                    LastBatchNumber = batchNumber,
                    LastDay = lastDay,
                    ConcurrencyToken = Guid.NewGuid().ToString("N"),
                },
                cancellationToken
            );
            return;
        }

        checkpoint.LastBatchNumber = batchNumber;
        if (lastDay > checkpoint.LastDay)
            checkpoint.LastDay = lastDay;
    }

    /// <summary>
    /// Adds each entry's count to its tenant's usage of the meter on the day, creating the day's entry when it does not
    /// exist.
    /// </summary>
    /// <param name="usage">The usage to add.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    private async ValueTask AddUsageAsync(
        IReadOnlyCollection<PersistedTenantUsage> usage,
        CancellationToken cancellationToken
    )
    {
        foreach (var dayUsage in usage.GroupBy(entry => entry.Day))
        {
            var day = dayUsage.Key;
            var tenantIds = dayUsage
                .Select(entry => entry.TenantId)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var entities = await DbContext
                .TenantUsage.Where(entity =>
                    entity.Day == day && tenantIds.Contains(entity.TenantId)
                )
                .ToDictionaryAsync(entity => (entity.TenantId, entity.Meter), cancellationToken);

            // An entry added earlier in this unit of work is not yet in the database.
            foreach (var entity in DbContext.TenantUsage.Local)
            {
                if (entity.Day == day && tenantIds.Contains(entity.TenantId))
                    entities.TryAdd((entity.TenantId, entity.Meter), entity);
            }

            foreach (var entry in dayUsage)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(entry.Count, nameof(usage));

                if (entities.TryGetValue((entry.TenantId, entry.Meter), out var entity))
                {
                    entity.Count += entry.Count;
                    continue;
                }

                entity = new TenantUsageEntity
                {
                    Id = IdGenerator.CreateId(),
                    TenantId = entry.TenantId,
                    Day = day,
                    Meter = entry.Meter,
                    Count = entry.Count,
                    ConcurrencyToken = Guid.NewGuid().ToString("N"),
                };
                entities.Add((entry.TenantId, entry.Meter), entity);
                await DbContext.TenantUsage.AddAsync(entity, cancellationToken);
            }
        }
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
        var removed = await DbContext
            .TenantUsage.Where(usage => usage.Day < day)
            .ExecuteDeleteAsync(cancellationToken);

        // A checkpoint guards only usage its replica can still retry; past the retention it guards nothing.
        await DbContext
            .TenantUsageCheckpoints.Where(checkpoint => checkpoint.LastDay < day)
            .ExecuteDeleteAsync(cancellationToken);

        return removed;
    }
}
