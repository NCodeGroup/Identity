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

using System.Linq.Expressions;
using System.Text.Json;
using IdGen;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IClientGrantStore"/> that uses Entity Framework Core for
/// persistence.
/// </summary>
[PublicAPI]
internal class ClientGrantStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStore<PersistedClientGrant, ClientGrantEntity>, IClientGrantStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    private static List<string> DeserializeScopes(JsonElement scopesJson) =>
        scopesJson.ValueKind == JsonValueKind.Array
            ? scopesJson.Deserialize<List<string>>() ?? []
            : [];

    /// <inheritdoc />
    protected override ValueTask<PersistedClientGrant> MapFromEntityAsync(
        ClientGrantEntity entity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(
            new PersistedClientGrant
            {
                TenantId = entity.Tenant.TenantId,
                ClientId = entity.Client.ClientId,
                ResourceServerId = entity.ResourceServer.ResourceServerId,
                ConcurrencyToken = entity.ConcurrencyToken,
                Scopes = DeserializeScopes(entity.ScopesJson),
            }
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<ClientGrantEntity?> GetEntityOrDefaultAsync(
        Expression<Func<ClientGrantEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return GetLocalOrDefault(predicate)
            ?? await DbContext
                .ClientGrants.Include(entity => entity.Tenant)
                .Include(entity => entity.Client)
                .Include(entity => entity.ResourceServer)
                .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<ClientGrantEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = BuildIncludedQuery();

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        return await query.OrderBy(entity => entity.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(ClientGrantEntity entity) => entity.Id;

    /// <inheritdoc />
    public async ValueTask<PagedResult<PersistedClientGrant>> GetPageAsync(
        string clientId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken
    )
    {
        var normalizedClientId = Normalize(clientId);
        var afterId = DecodeCursor(cursor);

        var query = BuildIncludedQuery()
            .Where(entity => entity.Client.NormalizedClientId == normalizedClientId);

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        var entities = await query
            .OrderBy(entity => entity.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return await BuildPageAsync(entities, limit, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedClientGrant?> GetOrDefaultAsync(
        string clientId,
        string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var normalizedClientId = Normalize(clientId);
        var normalizedResourceServerId = Normalize(resourceServerId);

        var entity = await GetEntityOrDefaultAsync(
            grant =>
                grant.Client.NormalizedClientId == normalizedClientId
                && grant.ResourceServer.NormalizedResourceServerId == normalizedResourceServerId,
            cancellationToken
        );

        return entity is null ? null : await MapFromEntityAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask AddAsync(
        PersistedClientGrant persistedClientGrant,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetTenantEntityAsync(
            persistedClientGrant.TenantId,
            cancellationToken
        );

        var normalizedClientId = Normalize(persistedClientGrant.ClientId);
        var clientEntity =
            await DbContext.Clients.FirstOrDefaultAsync(
                client =>
                    client.TenantId == tenantEntity.Id
                    && client.NormalizedClientId == normalizedClientId,
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Client '{persistedClientGrant.ClientId}' not found."
            );

        var normalizedResourceServerId = Normalize(persistedClientGrant.ResourceServerId);
        var resourceServerEntity =
            await DbContext.ResourceServers.FirstOrDefaultAsync(
                resourceServer =>
                    resourceServer.TenantId == tenantEntity.Id
                    && resourceServer.NormalizedResourceServerId == normalizedResourceServerId,
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Resource server '{persistedClientGrant.ResourceServerId}' not found."
            );

        // The interceptor leaves a pre-seeded insert token intact (ADR-0012).
        persistedClientGrant.ConcurrencyToken = NextConcurrencyToken();

        var grantEntity = new ClientGrantEntity
        {
            Id = NextId(),
            TenantId = tenantEntity.Id,
            NormalizedTenantId = tenantEntity.NormalizedTenantId,
            ClientId = clientEntity.Id,
            ResourceServerId = resourceServerEntity.Id,
            ConcurrencyToken = persistedClientGrant.ConcurrencyToken,
            ScopesJson = JsonSerializer.SerializeToElement(persistedClientGrant.Scopes),
            Tenant = tenantEntity,
            Client = clientEntity,
            ResourceServer = resourceServerEntity,
        };

        await DbContext.ClientGrants.AddAsync(grantEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(
        PersistedClientGrant persistedClientGrant,
        CancellationToken cancellationToken
    )
    {
        var normalizedClientId = Normalize(persistedClientGrant.ClientId);
        var normalizedResourceServerId = Normalize(persistedClientGrant.ResourceServerId);

        var grantEntity =
            await GetEntityOrDefaultAsync(
                grant =>
                    grant.Client.NormalizedClientId == normalizedClientId
                    && grant.ResourceServer.NormalizedResourceServerId
                        == normalizedResourceServerId,
                cancellationToken
            ) ?? throw new InvalidOperationException("The specified client grant was not found.");

        if (
            !string.Equals(
                persistedClientGrant.ConcurrencyToken,
                grantEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new DbUpdateConcurrencyException(
                "The client grant has been modified by another process. Please reload and try again."
            );
        }

        // The row-level ConcurrencyToken is regenerated by the interceptor on save (ADR-0012).
        grantEntity.ScopesJson = JsonSerializer.SerializeToElement(persistedClientGrant.Scopes);
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveAsync(
        string clientId,
        string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var normalizedClientId = Normalize(clientId);
        var normalizedResourceServerId = Normalize(resourceServerId);

        var grantEntity = await GetEntityOrDefaultAsync(
            grant =>
                grant.Client.NormalizedClientId == normalizedClientId
                && grant.ResourceServer.NormalizedResourceServerId == normalizedResourceServerId,
            cancellationToken
        );
        if (grantEntity is null)
        {
            return false;
        }

        DbContext.ClientGrants.Remove(grantEntity);

        return true;
    }

    private IQueryable<ClientGrantEntity> BuildIncludedQuery() =>
        DbContext
            .ClientGrants.Include(entity => entity.Tenant)
            .Include(entity => entity.Client)
            .Include(entity => entity.ResourceServer)
            .AsQueryable();
}
