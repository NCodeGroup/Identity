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

using System.Buffers.Binary;
using System.Buffers.Text;
using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IAuditRecordStore"/> that uses Entity Framework Core for persistence.
/// An addition reads which of its event identifiers the store already holds in one query and adds the rest, so a batch
/// retried after a save that committed adds nothing twice. A removal stages a bounded batch, with its resources, through
/// the change tracker, which every provider supports.
/// </summary>
// See ADR-0303.
internal sealed class AuditRecordStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : IAuditRecordStore
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
    public async ValueTask<int> AddAsync(
        IReadOnlyCollection<PersistedAuditRecord> records,
        CancellationToken cancellationToken
    )
    {
        ArgumentOutOfRangeException.ThrowIfZero(records.Count, nameof(records));

        var eventIds = records.Select(record => record.EventId).Distinct().ToList();
        var held = await DbContext
            .AuditRecords.Where(entity => eventIds.Contains(entity.EventId))
            .Select(entity => entity.EventId)
            .ToListAsync(cancellationToken);
        var skipped = held.ToHashSet();

        var added = 0;
        foreach (var record in records)
        {
            // A batch can hold one event twice when it was delivered twice before it was drained.
            if (!skipped.Add(record.EventId))
                continue;

            await DbContext.AuditRecords.AddAsync(CreateEntity(record), cancellationToken);
            added++;
        }

        return added;
    }

    private AuditRecordEntity CreateEntity(PersistedAuditRecord record)
    {
        var entity = new AuditRecordEntity
        {
            Id = IdGenerator.CreateId(),
            EventId = record.EventId,
            EventTime = record.EventTime.ToUniversalTime(),
            EventName = record.EventName,
            EventCategory = record.EventCategory,
            Outcome = record.Outcome,
            Severity = record.Severity,
            TenantId = record.TenantId,
            SubjectId = record.SubjectId,
            ClientId = record.ClientId,
            ActorType = record.ActorType,
            ActorId = record.ActorId,
            TraceId = record.TraceId,
            SourceIp = record.SourceIp,
            Record = record.Record,
        };

        var ordinal = 0;
        foreach (var resource in record.Resources)
        {
            entity.Resources.Add(
                new AuditRecordResourceEntity
                {
                    Id = IdGenerator.CreateId(),
                    AuditRecord = entity,
                    Ordinal = ordinal++,
                    Type = resource.Type,
                    ResourceId = resource.Id,
                }
            );
        }

        return entity;
    }

    /// <inheritdoc />
    public async ValueTask<PagedResult<PersistedAuditRecord>> GetPageAsync(
        AuditRecordFilter filter,
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(filter.TenantId, nameof(filter));

        var tenantId = filter.TenantId;
        var query = DbContext
            .AuditRecords.Include(entity => entity.Resources)
            .Where(entity => entity.TenantId == tenantId);

        // Newest first: the surrogate identifiers are time-ordered, so a page continues below the last one returned.
        if (DecodeCursor(page.Cursor) is { } beforeId)
            query = query.Where(entity => entity.Id < beforeId);

        query = ApplyFilter(query, filter);

        // Fetch one extra row so a full page signals that another page exists.
        var entities = await query
            .OrderByDescending(entity => entity.Id)
            .Take(page.Limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = entities.Count > page.Limit;
        var count = hasMore ? page.Limit : entities.Count;
        var items = entities.Take(count).Select(MapFromEntity).ToList();
        var nextCursor = hasMore ? EncodeCursor(entities[count - 1].Id) : null;

        return new PagedResult<PersistedAuditRecord> { Items = items, NextCursor = nextCursor };
    }

    private static IQueryable<AuditRecordEntity> ApplyFilter(
        IQueryable<AuditRecordEntity> query,
        AuditRecordFilter filter
    )
    {
        if (filter.StartTime is { } startTime)
        {
            var start = startTime.ToUniversalTime();
            query = query.Where(entity => entity.EventTime >= start);
        }

        if (filter.EndTime is { } endTime)
        {
            var end = endTime.ToUniversalTime();
            query = query.Where(entity => entity.EventTime < end);
        }

        if (filter.EventName is { } eventName)
            query = query.Where(entity => entity.EventName == eventName);

        if (filter.EventCategory is { } eventCategory)
            query = query.Where(entity => entity.EventCategory == eventCategory);

        if (filter.Outcome is { } outcome)
            query = query.Where(entity => entity.Outcome == outcome);

        if (filter.SubjectId is { } subjectId)
            query = query.Where(entity => entity.SubjectId == subjectId);

        if (filter.ClientId is { } clientId)
            query = query.Where(entity => entity.ClientId == clientId);

        if (filter.ActorId is { } actorId)
            query = query.Where(entity => entity.ActorId == actorId);

        if (filter.TraceId is { } traceId)
            query = query.Where(entity => entity.TraceId == traceId);

        var resourceType = filter.ResourceType;
        var resourceId = filter.ResourceId;
        if (resourceType is not null || resourceId is not null)
        {
            query = query.Where(entity =>
                entity.Resources.Any(resource =>
                    (resourceType == null || resource.Type == resourceType)
                    && (resourceId == null || resource.ResourceId == resourceId)
                )
            );
        }

        return query;
    }

    private static PersistedAuditRecord MapFromEntity(AuditRecordEntity entity) =>
        new()
        {
            EventId = entity.EventId,
            EventTime = entity.EventTime,
            EventName = entity.EventName,
            EventCategory = entity.EventCategory,
            Outcome = entity.Outcome,
            Severity = entity.Severity,
            TenantId = entity.TenantId,
            SubjectId = entity.SubjectId,
            ClientId = entity.ClientId,
            ActorType = entity.ActorType,
            ActorId = entity.ActorId,
            TraceId = entity.TraceId,
            SourceIp = entity.SourceIp,
            Resources =
            [
                .. entity
                    .Resources.OrderBy(resource => resource.Ordinal)
                    .Select(resource => new PersistedAuditResource
                    {
                        Type = resource.Type,
                        Id = resource.ResourceId,
                    }),
            ],
            Record = entity.Record,
        };

    // The cursor encoding of the keyset-paginated stores: the base64url of the big-endian surrogate identifier.
    private static string EncodeCursor(long key)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, key);
        return Base64Url.EncodeToString(bytes);
    }

    private static long? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
            return null;

        // A malformed cursor is treated as the start of the collection rather than a hard error.
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        if (
            !Base64Url.TryDecodeFromChars(cursor, bytes, out var written)
            || written != sizeof(long)
        )
            return null;

        return BinaryPrimitives.ReadInt64BigEndian(bytes);
    }

    /// <inheritdoc />
    public async ValueTask<int> RemoveBeforeAsync(
        DateTimeOffset time,
        int limit,
        CancellationToken cancellationToken
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        var cutoff = time.ToUniversalTime();
        var records = await DbContext
            .AuditRecords.Include(entity => entity.Resources)
            .Where(entity => entity.EventTime < cutoff)
            .OrderBy(entity => entity.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        // Every foreign key restricts deletion, so a record's resources are removed with it.
        foreach (var record in records)
            DbContext.AuditRecordResources.RemoveRange(record.Resources);

        DbContext.AuditRecords.RemoveRange(records);

        return records.Count;
    }
}
