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
using IdGen;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IResourceServerStore"/> that uses Entity Framework Core for
/// persistence.
/// </summary>
[PublicAPI]
internal class ResourceServerStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStoreWithResourceId<PersistedResourceServer, ResourceServerEntity>, IResourceServerStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    private static PersistedScope MapScope(ScopeEntity scope) =>
        new()
        {
            Value = scope.Value,
            Description = scope.Description,
            IsSystem = scope.IsSystem,
        };

    /// <inheritdoc />
    protected override ValueTask<PersistedResourceServer> MapFromEntityAsync(
        ResourceServerEntity entity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(
            new PersistedResourceServer
            {
                TenantId = entity.Tenant.TenantId,
                ResourceServerId = entity.ResourceServerId,
                Identifier = entity.Identifier,
                ConcurrencyToken = entity.ConcurrencyToken,
                Name = entity.Name,
                IsSystem = entity.IsSystem,
                IsDisabled = entity.IsDisabled,
                Settings = entity.SettingsJson,
                Scopes = entity.Scopes.Select(MapScope).ToList(),
            }
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<ResourceServerEntity?> GetEntityOrDefaultAsync(
        Expression<Func<ResourceServerEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return GetLocalOrDefault(predicate)
            ?? await DbContext
                .ResourceServers.Include(entity => entity.Tenant)
                .Include(entity => entity.Scopes)
                .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<ResourceServerEntity?> GetEntityOrDefaultAsync(
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        var normalizedResourceServerId = Normalize(resourceId);
        return await GetEntityOrDefaultAsync(
            entity => entity.NormalizedResourceServerId == normalizedResourceServerId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<PersistedResourceServer?> GetByIdentifierOrDefaultAsync(
        string identifier,
        CancellationToken cancellationToken
    )
    {
        var normalizedIdentifier = Normalize(identifier);
        var entity = await GetEntityOrDefaultAsync(
            resourceServer => resourceServer.NormalizedIdentifier == normalizedIdentifier,
            cancellationToken
        );

        return entity is null ? null : await MapFromEntityAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<ResourceServerEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = DbContext
            .ResourceServers.Include(entity => entity.Tenant)
            .Include(entity => entity.Scopes)
            .AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(entity => entity.Id > id);
        }

        return await query.OrderBy(entity => entity.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(ResourceServerEntity entity) => entity.Id;

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedResourceServer persistedResourceServer,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetTenantEntityAsync(
            persistedResourceServer.TenantId,
            cancellationToken
        );

        // The interceptor leaves a pre-seeded insert token intact (ADR-0012).
        persistedResourceServer.ConcurrencyToken = NextConcurrencyToken();

        var scopes = new List<ScopeEntity>(persistedResourceServer.Scopes.Count);

        var resourceServerEntity = new ResourceServerEntity
        {
            Id = NextId(),
            TenantId = tenantEntity.Id,
            NormalizedTenantId = tenantEntity.NormalizedTenantId,
            ResourceServerId = persistedResourceServer.ResourceServerId,
            NormalizedResourceServerId = Normalize(persistedResourceServer.ResourceServerId),
            Identifier = persistedResourceServer.Identifier,
            NormalizedIdentifier = Normalize(persistedResourceServer.Identifier),
            ConcurrencyToken = persistedResourceServer.ConcurrencyToken,
            ScopesConcurrencyToken = NextConcurrencyToken(),
            Name = persistedResourceServer.Name,
            IsSystem = persistedResourceServer.IsSystem,
            IsDisabled = persistedResourceServer.IsDisabled,
            SettingsJson = persistedResourceServer.Settings,
            Tenant = tenantEntity,
            Scopes = scopes,
        };

        foreach (var persistedScope in persistedResourceServer.Scopes)
        {
            scopes.Add(
                new ScopeEntity
                {
                    Id = NextId(),
                    TenantId = tenantEntity.Id,
                    NormalizedTenantId = tenantEntity.NormalizedTenantId,
                    ResourceServerId = resourceServerEntity.Id,
                    Value = persistedScope.Value,
                    NormalizedValue = Normalize(persistedScope.Value),
                    ConcurrencyToken = NextConcurrencyToken(),
                    Description = persistedScope.Description,
                    IsSystem = persistedScope.IsSystem,
                    Tenant = tenantEntity,
                    ResourceServer = resourceServerEntity,
                }
            );
        }

        await DbContext.ResourceServers.AddAsync(resourceServerEntity, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask UpdateAsync(
        PersistedResourceServer persistedResourceServer,
        CancellationToken cancellationToken
    )
    {
        var resourceServerEntity = await GetEntityAsync(
            persistedResourceServer.ResourceServerId,
            cancellationToken
        );

        if (
            !string.Equals(
                persistedResourceServer.ConcurrencyToken,
                resourceServerEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new DbUpdateConcurrencyException(
                "The resource server has been modified by another process. Please reload and try again."
            );
        }

        // The row-level ConcurrencyToken is regenerated by the interceptor on save (ADR-0012).
        resourceServerEntity.Name = persistedResourceServer.Name;
        resourceServerEntity.IsDisabled = persistedResourceServer.IsDisabled;
        resourceServerEntity.SettingsJson = persistedResourceServer.Settings;
    }

    /// <inheritdoc />
    public async ValueTask<bool> HasDependentsAsync(
        string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var normalizedResourceServerId = Normalize(resourceServerId);
        return await DbContext.ClientGrants.AnyAsync(
            grant => grant.ResourceServer.NormalizedResourceServerId == normalizedResourceServerId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string resourceServerId, CancellationToken cancellationToken)
    {
        var resourceServerEntity = await GetEntityOrDefaultAsync(
            resourceServerId,
            cancellationToken
        );
        if (resourceServerEntity is null)
        {
            return;
        }

        if (resourceServerEntity.IsSystem)
        {
            throw new InvalidOperationException(
                $"The resource server with ResourceServerId='{resourceServerId}' is a reserved system resource server and cannot be removed."
            );
        }

        var hasGrants = await DbContext.ClientGrants.AnyAsync(
            grant => grant.ResourceServerId == resourceServerEntity.Id,
            cancellationToken
        );
        if (hasGrants)
        {
            throw new InvalidOperationException(
                $"The resource server with ResourceServerId='{resourceServerId}' cannot be removed because one or more client grants still reference it."
            );
        }

        DbContext.Scopes.RemoveRange(resourceServerEntity.Scopes);
        DbContext.ResourceServers.Remove(resourceServerEntity);
    }

    /// <inheritdoc />
    public async ValueTask AddScopeAsync(
        string resourceServerId,
        PersistedScope persistedScope,
        CancellationToken cancellationToken
    )
    {
        var resourceServerEntity = await GetEntityAsync(resourceServerId, cancellationToken);

        var normalizedValue = Normalize(persistedScope.Value);
        var alreadyExists = resourceServerEntity.Scopes.Any(scope =>
            scope.NormalizedValue == normalizedValue
        );
        if (alreadyExists)
        {
            throw new InvalidOperationException(
                $"A scope with Value='{persistedScope.Value}' already exists for resource server with ResourceServerId='{resourceServerId}'."
            );
        }

        var scopeEntity = new ScopeEntity
        {
            Id = NextId(),
            TenantId = resourceServerEntity.TenantId,
            NormalizedTenantId = resourceServerEntity.NormalizedTenantId,
            ResourceServerId = resourceServerEntity.Id,
            Value = persistedScope.Value,
            NormalizedValue = normalizedValue,
            ConcurrencyToken = NextConcurrencyToken(),
            Description = persistedScope.Description,
            IsSystem = persistedScope.IsSystem,
            Tenant = resourceServerEntity.Tenant,
            ResourceServer = resourceServerEntity,
        };
        await DbContext.Scopes.AddAsync(scopeEntity, cancellationToken);

        resourceServerEntity.ScopesConcurrencyToken = NextConcurrencyToken();
    }

    /// <inheritdoc />
    public async ValueTask UpdateScopeAsync(
        string resourceServerId,
        PersistedScope persistedScope,
        CancellationToken cancellationToken
    )
    {
        var resourceServerEntity = await GetEntityAsync(resourceServerId, cancellationToken);

        var normalizedValue = Normalize(persistedScope.Value);
        var scopeEntity = resourceServerEntity.Scopes.SingleOrDefault(scope =>
            scope.NormalizedValue == normalizedValue
        );
        if (scopeEntity is null)
        {
            throw new InvalidOperationException(
                $"A scope with Value='{persistedScope.Value}' was not found for resource server with ResourceServerId='{resourceServerId}'."
            );
        }

        scopeEntity.Description = persistedScope.Description;

        resourceServerEntity.ScopesConcurrencyToken = NextConcurrencyToken();
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveScopeAsync(
        string resourceServerId,
        string scopeValue,
        CancellationToken cancellationToken
    )
    {
        var resourceServerEntity = await GetEntityAsync(resourceServerId, cancellationToken);

        var normalizedValue = Normalize(scopeValue);
        var scopeEntity = resourceServerEntity.Scopes.SingleOrDefault(scope =>
            scope.NormalizedValue == normalizedValue
        );
        if (scopeEntity is null)
        {
            return false;
        }

        if (scopeEntity.IsSystem)
        {
            throw new InvalidOperationException(
                $"The scope with Value='{scopeValue}' is a reserved system scope and cannot be removed."
            );
        }

        DbContext.Scopes.Remove(scopeEntity);

        resourceServerEntity.ScopesConcurrencyToken = NextConcurrencyToken();

        return true;
    }
}
