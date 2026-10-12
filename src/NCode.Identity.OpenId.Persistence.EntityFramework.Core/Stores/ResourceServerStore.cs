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
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Provides a default implementation of <see cref="IResourceServerStore"/> that uses Entity Framework Core for
/// persistence.
/// </summary>
internal class ResourceServerStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : CoreBaseStoreWithResourceId<PersistedResourceServer, ResourceServerEntity>, IResourceServerStore
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

    /// <summary>
    /// Maps a <see cref="ResourceServerEntity"/> to a <see cref="PersistedResourceServerSettings"/> instance.
    /// </summary>
    protected internal virtual PersistedResourceServerSettings MapSettings(
        ResourceServerEntity entity
    ) =>
        new()
        {
            TenantId = entity.Tenant.TenantId,
            ResourceServerId = entity.ResourceServerId,
            ConcurrencyToken = entity.SettingsConcurrencyToken,
            Value = entity.SettingsJson,
        };

    /// <summary>
    /// Maps a <see cref="ResourceServerEntity"/> to a <see cref="PersistedResourceServerSecrets"/> instance.
    /// </summary>
    protected internal virtual PersistedResourceServerSecrets MapSecrets(
        ResourceServerEntity entity
    ) =>
        new()
        {
            TenantId = entity.Tenant.TenantId,
            ResourceServerId = entity.ResourceServerId,
            ConcurrencyToken = entity.SecretsConcurrencyToken,
            Value = MapToPersistedSecrets(entity.Secrets),
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
                Settings = MapSettings(entity),
                Scopes = entity.Scopes.Select(MapScope).ToList(),
                Secrets = MapSecrets(entity),
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
                .Include(entity => entity.Secrets)
                    .ThenInclude(resourceServerSecret => resourceServerSecret.Secret)
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
            .Include(entity => entity.Secrets)
                .ThenInclude(resourceServerSecret => resourceServerSecret.Secret)
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
    public async ValueTask<IReadOnlyCollection<PersistedScope>> GetEnabledScopesAsync(
        IReadOnlyCollection<string>? scopeValues,
        CancellationToken cancellationToken
    )
    {
        var query = DbContext
            .ResourceServers.Where(resourceServer => !resourceServer.IsDisabled)
            .SelectMany(resourceServer => resourceServer.Scopes);

        if (scopeValues is { Count: > 0 })
        {
            var normalizedValues = scopeValues.Select(Normalize).ToList();
            query = query.Where(scope => normalizedValues.Contains(scope.NormalizedValue));
        }

        var scopes = await query
            .Select(scope => new
            {
                scope.Value,
                scope.Description,
                scope.IsSystem,
            })
            .ToListAsync(cancellationToken);

        return scopes
            .Select(scope => new PersistedScope
            {
                Value = scope.Value,
                Description = scope.Description,
                IsSystem = scope.IsSystem,
            })
            .ToList();
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<string>> GetEnabledIdentifiersAsync(
        CancellationToken cancellationToken
    )
    {
        return await DbContext
            .ResourceServers.Where(resourceServer => !resourceServer.IsDisabled)
            .Select(resourceServer => resourceServer.Identifier)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<
        IReadOnlyCollection<ResourceServerScopeProjection>
    > GetEnabledScopeProjectionsByScopesAsync(
        IReadOnlyCollection<string> scopeValues,
        CancellationToken cancellationToken
    )
    {
        var normalizedValues = scopeValues.Select(Normalize).ToList();

        return await DbContext
            .ResourceServers.Where(resourceServer =>
                !resourceServer.IsDisabled
                && resourceServer.Scopes.Any(scope =>
                    normalizedValues.Contains(scope.NormalizedValue)
                )
            )
            .Select(resourceServer => new ResourceServerScopeProjection
            {
                Identifier = resourceServer.Identifier,
                ResourceServerId = resourceServer.ResourceServerId,
                IsSystem = resourceServer.IsSystem,
                ScopeValues = resourceServer.Scopes.Select(scope => scope.Value).ToList(),
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<
        IReadOnlyCollection<ResourceServerScopeProjection>
    > GetEnabledScopeProjectionsBySystemOrIdsAsync(
        IReadOnlyCollection<string> resourceServerIds,
        CancellationToken cancellationToken
    )
    {
        var normalizedIds = resourceServerIds.Select(Normalize).ToList();

        return await DbContext
            .ResourceServers.Where(resourceServer =>
                !resourceServer.IsDisabled
                && (
                    resourceServer.IsSystem
                    || normalizedIds.Contains(resourceServer.NormalizedResourceServerId)
                )
            )
            .Select(resourceServer => new ResourceServerScopeProjection
            {
                Identifier = resourceServer.Identifier,
                ResourceServerId = resourceServer.ResourceServerId,
                IsSystem = resourceServer.IsSystem,
                ScopeValues = resourceServer.Scopes.Select(scope => scope.Value).ToList(),
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<PersistedResourceServer>> FindEnabledByScopesAsync(
        IReadOnlyCollection<string> scopeValues,
        IReadOnlyCollection<string>? identifiers,
        CancellationToken cancellationToken
    )
    {
        var normalizedValues = scopeValues.Select(Normalize).ToList();

        var query = DbContext
            .ResourceServers.Include(entity => entity.Tenant)
            .Include(entity => entity.Scopes)
            .Include(entity => entity.Secrets)
                .ThenInclude(resourceServerSecret => resourceServerSecret.Secret)
            .Where(resourceServer =>
                !resourceServer.IsDisabled
                && resourceServer.Scopes.Any(scope =>
                    normalizedValues.Contains(scope.NormalizedValue)
                )
            );

        if (identifiers is { Count: > 0 })
        {
            var normalizedIdentifiers = identifiers.Select(Normalize).ToList();
            query = query.Where(resourceServer =>
                normalizedIdentifiers.Contains(resourceServer.NormalizedIdentifier)
            );
        }

        var entities = await query
            .OrderBy(resourceServer => resourceServer.Identifier)
            .ToListAsync(cancellationToken);

        var items = new List<PersistedResourceServer>(entities.Count);
        foreach (var entity in entities)
        {
            items.Add(await MapFromEntityAsync(entity, cancellationToken));
        }

        return items;
    }

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

        // The interceptor leaves a pre-seeded insert token intact (ADR-0012); sub-resource version columns are manual.
        persistedResourceServer.ConcurrencyToken = NextConcurrencyToken();
        persistedResourceServer.Settings.ConcurrencyToken = NextConcurrencyToken();
        persistedResourceServer.Secrets.ConcurrencyToken = NextConcurrencyToken();

        var scopes = new List<ScopeEntity>(persistedResourceServer.Scopes.Count);
        var secrets = new List<ResourceServerSecretEntity>(
            persistedResourceServer.Secrets.Value.Count
        );

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
            SettingsConcurrencyToken = persistedResourceServer.Settings.ConcurrencyToken,
            SecretsConcurrencyToken = persistedResourceServer.Secrets.ConcurrencyToken,
            Name = persistedResourceServer.Name,
            IsSystem = persistedResourceServer.IsSystem,
            IsDisabled = persistedResourceServer.IsDisabled,
            SettingsJson = persistedResourceServer.Settings.Value,
            Tenant = tenantEntity,
            Scopes = scopes,
            Secrets = secrets,
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

        foreach (var persistedSecret in persistedResourceServer.Secrets.Value)
        {
            var secretEntity = MapToSecretEntity(persistedSecret);

            await DbContext.Secrets.AddAsync(secretEntity, cancellationToken);

            var resourceServerSecretEntity = new ResourceServerSecretEntity
            {
                Id = NextId(),
                Tenant = tenantEntity,
                TenantId = tenantEntity.Id,
                NormalizedTenantId = tenantEntity.NormalizedTenantId,
                ResourceServer = resourceServerEntity,
                ResourceServerId = resourceServerEntity.Id,
                Secret = secretEntity,
                SecretId = secretEntity.Id,
            };

            secrets.Add(resourceServerSecretEntity);

            await DbContext.ResourceServerSecrets.AddAsync(
                resourceServerSecretEntity,
                cancellationToken
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
        resourceServerEntity.SettingsJson = persistedResourceServer.Settings.Value;
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
        DbContext.ResourceServerSecrets.RemoveRange(resourceServerEntity.Secrets);
        DbContext.Secrets.RemoveRange(
            resourceServerEntity.Secrets.Select(resourceServerSecret => resourceServerSecret.Secret)
        );
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

    /// <inheritdoc />
    public async ValueTask UpdateSettingsAsync(
        PersistedResourceServerSettings persistedResourceServerSettings,
        CancellationToken cancellationToken
    )
    {
        var resourceServerId = persistedResourceServerSettings.ResourceServerId;
        var resourceServerEntity = await GetEntityAsync(resourceServerId, cancellationToken);

        if (
            !string.Equals(
                persistedResourceServerSettings.ConcurrencyToken,
                resourceServerEntity.SettingsConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new DbUpdateConcurrencyException(
                $"The settings for the resource server with ResourceServerId='{resourceServerId}' have been modified by another process. Please reload and try again."
            );
        }

        var nextConcurrencyToken = NextConcurrencyToken();

        resourceServerEntity.SettingsConcurrencyToken = nextConcurrencyToken;
        resourceServerEntity.SettingsJson = persistedResourceServerSettings.Value;

        DbContext.ResourceServers.Update(resourceServerEntity);

        persistedResourceServerSettings.ConcurrencyToken = nextConcurrencyToken;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedResourceServerSecrets?> GetSecretsOrDefaultAsync(
        string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var resourceServerEntity = await GetEntityOrDefaultAsync(
            resourceServerId,
            cancellationToken
        );
        if (resourceServerEntity is null)
        {
            return null;
        }

        return MapSecrets(resourceServerEntity);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedResourceServerSecret?> GetSecretOrDefaultAsync(
        string resourceServerId,
        string secretId,
        CancellationToken cancellationToken
    )
    {
        var normalizedResourceServerId = Normalize(resourceServerId);
        var normalizedSecretId = Normalize(secretId);

        var row = await DbContext
            .ResourceServerSecrets.Where(resourceServerSecret =>
                resourceServerSecret.ResourceServer.NormalizedResourceServerId
                    == normalizedResourceServerId
                && resourceServerSecret.Secret.NormalizedSecretId == normalizedSecretId
            )
            .Select(resourceServerSecret => new
            {
                resourceServerSecret.Tenant.TenantId,
                resourceServerSecret.ResourceServer.ResourceServerId,
                resourceServerSecret.Secret,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new PersistedResourceServerSecret
        {
            TenantId = row.TenantId,
            ResourceServerId = row.ResourceServerId,
            Value = MapToPersistedSecret(row.Secret),
        };
    }

    /// <inheritdoc />
    public async ValueTask AddSecretAsync(
        string resourceServerId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    )
    {
        var resourceServerEntity = await GetEntityAsync(resourceServerId, cancellationToken);

        var normalizedSecretId = Normalize(persistedSecret.SecretId);
        var alreadyExists = resourceServerEntity.Secrets.Any(resourceServerSecret =>
            resourceServerSecret.Secret.NormalizedSecretId == normalizedSecretId
        );
        if (alreadyExists)
        {
            throw new InvalidOperationException(
                $"A secret with SecretId='{persistedSecret.SecretId}' already exists for the resource server with ResourceServerId='{resourceServerId}'."
            );
        }

        // A created secret cannot have a concurrency conflict, so assign its token up front and return it on the DTO;
        // the interceptor leaves a pre-seeded insert token intact (ADR-0012).
        persistedSecret.ConcurrencyToken = NextConcurrencyToken();
        var secretEntity = MapToSecretEntity(persistedSecret);
        await DbContext.Secrets.AddAsync(secretEntity, cancellationToken);

        var resourceServerSecretEntity = new ResourceServerSecretEntity
        {
            Id = NextId(),
            Tenant = resourceServerEntity.Tenant,
            TenantId = resourceServerEntity.TenantId,
            NormalizedTenantId = resourceServerEntity.NormalizedTenantId,
            ResourceServer = resourceServerEntity,
            ResourceServerId = resourceServerEntity.Id,
            Secret = secretEntity,
            SecretId = secretEntity.Id,
        };
        await DbContext.ResourceServerSecrets.AddAsync(
            resourceServerSecretEntity,
            cancellationToken
        );

        // The resource server is tracked; mutating the token marks it Modified via change detection.
        resourceServerEntity.SecretsConcurrencyToken = NextConcurrencyToken();
    }

    /// <inheritdoc />
    public async ValueTask UpdateSecretAsync(
        string resourceServerId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    )
    {
        var resourceServerEntity = await GetEntityAsync(resourceServerId, cancellationToken);

        var normalizedSecretId = Normalize(persistedSecret.SecretId);
        var resourceServerSecretEntity = resourceServerEntity.Secrets.SingleOrDefault(
            resourceServerSecret =>
                resourceServerSecret.Secret.NormalizedSecretId == normalizedSecretId
        );
        if (resourceServerSecretEntity is null)
        {
            throw new InvalidOperationException(
                $"A secret with SecretId='{persistedSecret.SecretId}' was not found for the resource server with ResourceServerId='{resourceServerId}'."
            );
        }

        var secretEntity = resourceServerSecretEntity.Secret;
        if (
            !string.Equals(
                persistedSecret.ConcurrencyToken,
                secretEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new DbUpdateConcurrencyException(
                $"The secret with SecretId='{persistedSecret.SecretId}' has been modified by another process. Please reload and try again."
            );
        }

        // Only metadata is mutable; key material is immutable once generated (see ADR-0011). The secret's row-level
        // ConcurrencyToken is regenerated by the interceptor on save (ADR-0012).
        secretEntity.Use = persistedSecret.Use;
        secretEntity.Algorithm = persistedSecret.Algorithm;
        secretEntity.ExpiresWhen = persistedSecret.ExpiresWhen.ToUniversalTime();

        resourceServerEntity.SecretsConcurrencyToken = NextConcurrencyToken();
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveSecretAsync(
        string resourceServerId,
        string secretId,
        CancellationToken cancellationToken
    )
    {
        var resourceServerEntity = await GetEntityAsync(resourceServerId, cancellationToken);

        var normalizedSecretId = Normalize(secretId);
        var resourceServerSecretEntity = resourceServerEntity.Secrets.SingleOrDefault(
            resourceServerSecret =>
                resourceServerSecret.Secret.NormalizedSecretId == normalizedSecretId
        );
        if (resourceServerSecretEntity is null)
        {
            return false;
        }

        DbContext.ResourceServerSecrets.Remove(resourceServerSecretEntity);
        DbContext.Secrets.Remove(resourceServerSecretEntity.Secret);

        // The resource server is tracked; mutating the token marks it Modified via change detection.
        resourceServerEntity.SecretsConcurrencyToken = NextConcurrencyToken();

        return true;
    }
}
