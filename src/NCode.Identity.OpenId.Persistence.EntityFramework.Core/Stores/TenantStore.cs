#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
/// Provides a default implementation of <see cref="ITenantStore"/> that uses Entity Framework Core for persistence.
/// </summary>
internal class TenantStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : CoreBaseStoreWithResourceId<PersistedTenant, TenantEntity>, ITenantStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <summary>
    /// Maps a <see cref="TenantEntity"/> to a <see cref="PersistedTenantSettings"/> instance.
    /// </summary>
    /// <param name="tenantEntity">The <see cref="TenantEntity"/> instance to map.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly mapped <see cref="PersistedTenantSettings"/> instance.</returns>
    protected internal virtual ValueTask<PersistedTenantSettings> MapSettingsAsync(
        TenantEntity tenantEntity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(
            new PersistedTenantSettings
            {
                TenantId = tenantEntity.TenantId,
                ConcurrencyToken = tenantEntity.SettingsConcurrencyToken,
                Value = tenantEntity.SettingsJson,
            }
        );
    }

    /// <summary>
    /// Maps a <see cref="TenantEntity"/> to a <see cref="PersistedTenantSecrets"/> instance.
    /// </summary>
    /// <param name="tenantEntity">The <see cref="TenantEntity"/> instance to map.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly mapped <see cref="PersistedTenantSecrets"/> instance.</returns>
    protected internal virtual ValueTask<PersistedTenantSecrets> MapSecretsAsync(
        TenantEntity tenantEntity,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(
            new PersistedTenantSecrets
            {
                TenantId = tenantEntity.TenantId,
                ConcurrencyToken = tenantEntity.SecretsConcurrencyToken,
                Value = MapToPersistedSecrets(tenantEntity.Secrets),
            }
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<PersistedTenant> MapFromEntityAsync(
        TenantEntity tenantEntity,
        CancellationToken cancellationToken
    )
    {
        return new PersistedTenant
        {
            TenantId = tenantEntity.TenantId,
            DomainName = tenantEntity.DomainName,
            ConcurrencyToken = tenantEntity.ConcurrencyToken,
            IsDisabled = tenantEntity.IsDisabled,
            DisplayName = tenantEntity.DisplayName,
            Settings = await MapSettingsAsync(tenantEntity, cancellationToken),
            Secrets = await MapSecretsAsync(tenantEntity, cancellationToken),
        };
    }

    /// <inheritdoc />
    protected override async ValueTask<TenantEntity?> GetEntityOrDefaultAsync(
        Expression<Func<TenantEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return GetLocalOrDefault(predicate)
            ?? await DbContext
                .Tenants.Include(tenant => tenant.Secrets)
                    .ThenInclude(tenantSecret => tenantSecret.Secret)
                .SingleOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<TenantEntity?> GetEntityOrDefaultAsync(
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(resourceId);
        return await GetEntityOrDefaultAsync(
            entity => entity.NormalizedTenantId == normalizedTenantId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    protected override async ValueTask<IReadOnlyList<TenantEntity>> GetEntityPageAsync(
        long? afterId,
        int take,
        CancellationToken cancellationToken
    )
    {
        var query = DbContext
            .Tenants.Include(tenant => tenant.Secrets)
                .ThenInclude(tenantSecret => tenantSecret.Secret)
            .AsQueryable();

        if (afterId is { } id)
        {
            query = query.Where(tenant => tenant.Id > id);
        }

        return await query.OrderBy(tenant => tenant.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override long GetSortKey(TenantEntity entity) => entity.Id;

    /// <inheritdoc />
    public async ValueTask<PersistedTenant?> GetOrDefaultByDomainNameAsync(
        string domainName,
        CancellationToken cancellationToken
    )
    {
        var normalizedDomainName = Normalize(domainName);
        return await GetOrDefaultAsync(
            entity => entity.NormalizedDomainName == normalizedDomainName,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTenantSettings> GetSettingsAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetTenantEntityAsync(tenantId, cancellationToken);
        return await MapSettingsAsync(tenantEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTenantSecrets> GetSecretsAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetTenantEntityAsync(tenantId, cancellationToken);
        return await MapSecretsAsync(tenantEntity, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedTenant persistedTenant,
        CancellationToken cancellationToken
    )
    {
        // Assign the row token up front so create can return it without a re-read; the interceptor leaves a
        // pre-seeded insert token intact (ADR-0012). The sub-resource version columns stay manual.
        persistedTenant.ConcurrencyToken = NextConcurrencyToken();
        persistedTenant.Settings.ConcurrencyToken = NextConcurrencyToken();
        persistedTenant.Secrets.ConcurrencyToken = NextConcurrencyToken();

        var secrets = new List<TenantSecretEntity>(persistedTenant.Secrets.Value.Count);

        var tenantEntity = new TenantEntity
        {
            Id = NextId(),
            TenantId = persistedTenant.TenantId,
            NormalizedTenantId = Normalize(persistedTenant.TenantId),
            DomainName = persistedTenant.DomainName,
            NormalizedDomainName = Normalize(persistedTenant.DomainName),
            ConcurrencyToken = persistedTenant.ConcurrencyToken,
            SettingsConcurrencyToken = persistedTenant.Settings.ConcurrencyToken,
            SecretsConcurrencyToken = persistedTenant.Secrets.ConcurrencyToken,
            IsDisabled = persistedTenant.IsDisabled,
            DisplayName = persistedTenant.DisplayName,
            SettingsJson = persistedTenant.Settings.Value,
            Secrets = secrets,
        };

        foreach (var persistedSecret in persistedTenant.Secrets.Value)
        {
            var secretEntity = MapToSecretEntity(persistedSecret);

            await DbContext.Secrets.AddAsync(secretEntity, cancellationToken);

            var tenantSecretEntity = new TenantSecretEntity
            {
                Id = NextId(),

                Tenant = tenantEntity,
                TenantId = tenantEntity.Id,
                NormalizedTenantId = tenantEntity.NormalizedTenantId,

                Secret = secretEntity,
                SecretId = secretEntity.Id,
            };

            secrets.Add(tenantSecretEntity);

            await DbContext.TenantSecrets.AddAsync(tenantSecretEntity, cancellationToken);
        }

        await DbContext.Tenants.AddAsync(tenantEntity, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask UpdateAsync(
        PersistedTenant persistedTenant,
        CancellationToken cancellationToken
    )
    {
        var tenantId = persistedTenant.TenantId;
        var tenantEntity = await GetEntityAsync(tenantId, cancellationToken);

        if (
            !string.Equals(
                persistedTenant.ConcurrencyToken,
                tenantEntity.ConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new DbUpdateConcurrencyException(
                $"The OpenId Tenant with TenantId='{tenantId}' has been modified by another process. Please reload and try again."
            );
        }

        // Touch the row so the interceptor regenerates the ConcurrencyToken on save (ADR-0012).
        tenantEntity.IsDisabled = persistedTenant.IsDisabled;
        tenantEntity.DisplayName = persistedTenant.DisplayName;

        tenantEntity.DomainName = persistedTenant.DomainName;
        tenantEntity.NormalizedDomainName = Normalize(persistedTenant.DomainName);

        DbContext.Tenants.Update(tenantEntity);
    }

    /// <inheritdoc />
    public async ValueTask UpdateSettingsAsync(
        PersistedTenantSettings persistedTenantSettings,
        CancellationToken cancellationToken
    )
    {
        var tenantId = persistedTenantSettings.TenantId;
        var tenantEntity = await GetEntityAsync(tenantId, cancellationToken);

        if (
            !string.Equals(
                persistedTenantSettings.ConcurrencyToken,
                tenantEntity.SettingsConcurrencyToken,
                StringComparison.Ordinal
            )
        )
        {
            throw new DbUpdateConcurrencyException(
                $"The settings for OpenId Tenant with TenantId='{tenantId}' have been modified by another process. Please reload and try again."
            );
        }

        var nextConcurrencyToken = NextConcurrencyToken();

        tenantEntity.SettingsConcurrencyToken = nextConcurrencyToken;
        tenantEntity.SettingsJson = persistedTenantSettings.Value;

        DbContext.Tenants.Update(tenantEntity);

        persistedTenantSettings.ConcurrencyToken = nextConcurrencyToken;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTenantSettings?> GetSettingsOrDefaultAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetEntityOrDefaultAsync(tenantId, cancellationToken);
        if (tenantEntity is null)
        {
            return null;
        }

        return await MapSettingsAsync(tenantEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTenantSecrets?> GetSecretsOrDefaultAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetEntityOrDefaultAsync(tenantId, cancellationToken);
        if (tenantEntity is null)
        {
            return null;
        }

        return await MapSecretsAsync(tenantEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedSecret?> GetSecretOrDefaultAsync(
        string tenantId,
        string secretId,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(tenantId);
        var normalizedSecretId = Normalize(secretId);

        var secretEntity = await DbContext
            .TenantSecrets.Where(tenantSecret =>
                tenantSecret.Tenant.NormalizedTenantId == normalizedTenantId
                && tenantSecret.Secret.NormalizedSecretId == normalizedSecretId
            )
            .Select(tenantSecret => tenantSecret.Secret)
            .SingleOrDefaultAsync(cancellationToken);

        return secretEntity is null ? null : MapToPersistedSecret(secretEntity);
    }

    /// <inheritdoc />
    public async ValueTask AddSecretAsync(
        string tenantId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetEntityAsync(tenantId, cancellationToken);

        var normalizedSecretId = Normalize(persistedSecret.SecretId);
        var alreadyExists = tenantEntity.Secrets.Any(tenantSecret =>
            tenantSecret.Secret.NormalizedSecretId == normalizedSecretId
        );
        if (alreadyExists)
        {
            throw new InvalidOperationException(
                $"A secret with SecretId='{persistedSecret.SecretId}' already exists for OpenId Tenant with TenantId='{tenantId}'."
            );
        }

        // A created secret cannot have a concurrency conflict, so assign its token up front and return it on
        // the DTO; the interceptor leaves a pre-seeded insert token intact (ADR-0012).
        persistedSecret.ConcurrencyToken = NextConcurrencyToken();
        var secretEntity = MapToSecretEntity(persistedSecret);
        await DbContext.Secrets.AddAsync(secretEntity, cancellationToken);

        var tenantSecretEntity = new TenantSecretEntity
        {
            Id = NextId(),
            Tenant = tenantEntity,
            TenantId = tenantEntity.Id,
            NormalizedTenantId = tenantEntity.NormalizedTenantId,
            Secret = secretEntity,
            SecretId = secretEntity.Id,
        };
        await DbContext.TenantSecrets.AddAsync(tenantSecretEntity, cancellationToken);

        // The tenant is tracked; mutating the token marks it Modified via change detection.
        tenantEntity.SecretsConcurrencyToken = NextConcurrencyToken();
    }

    /// <inheritdoc />
    public async ValueTask UpdateSecretAsync(
        string tenantId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetEntityAsync(tenantId, cancellationToken);

        var normalizedSecretId = Normalize(persistedSecret.SecretId);
        var tenantSecretEntity = tenantEntity.Secrets.SingleOrDefault(tenantSecret =>
            tenantSecret.Secret.NormalizedSecretId == normalizedSecretId
        );
        if (tenantSecretEntity is null)
        {
            throw new InvalidOperationException(
                $"A secret with SecretId='{persistedSecret.SecretId}' was not found for OpenId Tenant with TenantId='{tenantId}'."
            );
        }

        var secretEntity = tenantSecretEntity.Secret;
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

        // Only metadata is mutable; key material is immutable once generated (see ADR-0011). The secret's
        // row-level ConcurrencyToken is regenerated by the interceptor on save (ADR-0012).
        secretEntity.Use = persistedSecret.Use;
        secretEntity.Algorithm = persistedSecret.Algorithm;
        secretEntity.ExpiresWhen = persistedSecret.ExpiresWhen.ToUniversalTime();

        tenantEntity.SecretsConcurrencyToken = NextConcurrencyToken();
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveSecretAsync(
        string tenantId,
        string secretId,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetEntityAsync(tenantId, cancellationToken);

        var normalizedSecretId = Normalize(secretId);
        var tenantSecretEntity = tenantEntity.Secrets.SingleOrDefault(tenantSecret =>
            tenantSecret.Secret.NormalizedSecretId == normalizedSecretId
        );
        if (tenantSecretEntity is null)
        {
            return false;
        }

        DbContext.TenantSecrets.Remove(tenantSecretEntity);
        DbContext.Secrets.Remove(tenantSecretEntity.Secret);

        // The tenant is tracked; mutating the token marks it Modified via change detection.
        tenantEntity.SecretsConcurrencyToken = NextConcurrencyToken();

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<bool> RemoveAsync(string tenantId, CancellationToken cancellationToken)
    {
        var tenantEntity = await GetEntityOrDefaultAsync(tenantId, cancellationToken);
        if (tenantEntity is null)
        {
            return false;
        }

        var hasClients = await DbContext.Clients.AnyAsync(
            client => client.TenantId == tenantEntity.Id,
            cancellationToken
        );
        var hasSecrets = await DbContext.TenantSecrets.AnyAsync(
            tenantSecret => tenantSecret.TenantId == tenantEntity.Id,
            cancellationToken
        );
        if (hasClients || hasSecrets)
        {
            throw new InvalidOperationException(
                $"The OpenId Tenant with TenantId='{tenantId}' cannot be removed because it still has one or more clients or secrets."
            );
        }

        DbContext.Tenants.Remove(tenantEntity);

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<bool> HasDependentsAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetEntityOrDefaultAsync(tenantId, cancellationToken);
        if (tenantEntity is null)
        {
            return false;
        }

        var hasClients = await DbContext.Clients.AnyAsync(
            client => client.TenantId == tenantEntity.Id,
            cancellationToken
        );
        if (hasClients)
        {
            return true;
        }

        return await DbContext.TenantSecrets.AnyAsync(
            tenantSecret => tenantSecret.TenantId == tenantEntity.Id,
            cancellationToken
        );
    }
}
