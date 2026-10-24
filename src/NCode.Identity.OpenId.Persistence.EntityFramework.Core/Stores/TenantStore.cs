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
using System.Reflection;
using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
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
    /// <summary>
    /// Gets the generic method that removes the rows of one entity type that a tenant owns.
    /// </summary>
    private static MethodInfo RemoveOwnedRowsMethod { get; } =
        typeof(TenantStore).GetMethod(
            nameof(RemoveOwnedRowsAsync),
            BindingFlags.Instance | BindingFlags.NonPublic
        )
        ?? throw new InvalidOperationException(
            $"The '{nameof(RemoveOwnedRowsAsync)}' method could not be found."
        );

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
            Issuer = tenantEntity.Issuer,
            MtlsEndpointAliasAuthority = tenantEntity.MtlsEndpointAliasAuthority,
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
    public async ValueTask<PagedResult<PersistedTenantSettings>> GetSettingsPageAsync(
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        var query = DbContext.Tenants.AsNoTracking();

        if (DecodeCursor(page.Cursor) is { } afterId)
        {
            query = query.Where(tenant => tenant.Id > afterId);
        }

        // Fetch one extra row so a full page signals that another page exists.
        var rows = await query
            .OrderBy(tenant => tenant.Id)
            .Take(page.Limit + 1)
            .Select(tenant => new
            {
                tenant.Id,
                tenant.TenantId,
                tenant.SettingsConcurrencyToken,
                tenant.SettingsJson,
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > page.Limit;
        var count = hasMore ? page.Limit : rows.Count;

        var items = new List<PersistedTenantSettings>(count);
        for (var index = 0; index < count; index++)
        {
            var row = rows[index];
            items.Add(
                new PersistedTenantSettings
                {
                    TenantId = row.TenantId,
                    ConcurrencyToken = row.SettingsConcurrencyToken,
                    Value = row.SettingsJson,
                }
            );
        }

        var nextCursor = hasMore ? EncodeCursor(rows[count - 1].Id) : null;

        return new PagedResult<PersistedTenantSettings> { Items = items, NextCursor = nextCursor };
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTenant?> GetOrDefaultByIssuerAsync(
        string issuer,
        CancellationToken cancellationToken
    )
    {
        var normalizedIssuer = Normalize(issuer);
        return await GetOrDefaultAsync(
            entity => entity.NormalizedIssuer == normalizedIssuer,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTenant?> GetOrDefaultByHostAsync(
        string host,
        CancellationToken cancellationToken
    )
    {
        var normalizedHost = Normalize(host);
        var holders = await DbContext
            .Tenants.Where(entity =>
                entity.NormalizedIssuerHost == normalizedHost
                || entity.NormalizedMtlsEndpointAliasHost == normalizedHost
            )
            .Select(entity => entity.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        // An ambiguous host routes nowhere rather than to whichever tenant the database returns first.
        if (holders.Count != 1)
            return null;

        var id = holders[0];
        return await GetOrDefaultAsync(entity => entity.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> ListTenantIdsByHostAsync(
        string host,
        CancellationToken cancellationToken
    )
    {
        var normalizedHost = Normalize(host);
        return await DbContext
            .Tenants.Where(entity =>
                entity.NormalizedIssuerHost == normalizedHost
                || entity.NormalizedMtlsEndpointAliasHost == normalizedHost
            )
            .Select(entity => entity.TenantId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Extracts the host component of an issuer and normalizes it for a sargable lookup. Returns <c>null</c> when the
    /// tenant has no issuer, or when the issuer is not an absolute URI — in which case the tenant simply is not
    /// routable by host, rather than the write failing.
    /// </summary>
    private static string? NormalizeIssuerHost(string? issuer) =>
        Uri.TryCreate(issuer, UriKind.Absolute, out var uri) ? Normalize(uri.Host) : null;

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
            Issuer = persistedTenant.Issuer,
            NormalizedIssuer = Normalize(persistedTenant.Issuer),
            NormalizedIssuerHost = NormalizeIssuerHost(persistedTenant.Issuer),
            MtlsEndpointAliasAuthority = persistedTenant.MtlsEndpointAliasAuthority,
            NormalizedMtlsEndpointAliasHost = NormalizeIssuerHost(
                persistedTenant.MtlsEndpointAliasAuthority
            ),
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
            throw new StoreConcurrencyException(
                $"The OpenId Tenant with TenantId='{tenantId}' has been modified by another process. Please reload and try again."
            );
        }

        var previousIssuer = tenantEntity.Issuer;
        var previousNormalizedIssuer = tenantEntity.NormalizedIssuer;

        // Touch the row so the interceptor regenerates the ConcurrencyToken on save (ADR-0012).
        tenantEntity.IsDisabled = persistedTenant.IsDisabled;
        tenantEntity.DisplayName = persistedTenant.DisplayName;

        tenantEntity.Issuer = persistedTenant.Issuer;
        tenantEntity.NormalizedIssuer = Normalize(persistedTenant.Issuer);
        tenantEntity.NormalizedIssuerHost = NormalizeIssuerHost(persistedTenant.Issuer);
        tenantEntity.MtlsEndpointAliasAuthority = persistedTenant.MtlsEndpointAliasAuthority;
        tenantEntity.NormalizedMtlsEndpointAliasHost = NormalizeIssuerHost(
            persistedTenant.MtlsEndpointAliasAuthority
        );

        DbContext.Tenants.Update(tenantEntity);

        if (
            previousIssuer is not null
            && previousNormalizedIssuer is not null
            && !string.Equals(
                previousNormalizedIssuer,
                tenantEntity.NormalizedIssuer,
                StringComparison.Ordinal
            )
        )
        {
            await RetireIssuerAsync(
                tenantEntity.TenantId,
                previousIssuer,
                previousNormalizedIssuer,
                cancellationToken
            );
        }
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
            throw new StoreConcurrencyException(
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
            throw new StoreConcurrencyException(
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

        if (await HasDependentsAsync(tenantEntity, cancellationToken))
        {
            throw new InvalidOperationException(
                $"The OpenId Tenant with TenantId='{tenantId}' cannot be removed because it still has one or more clients or resource servers."
            );
        }

        // The role assignments that the tenant's principals hold at the nodes of other tenants are removed with them.
        // See ADR-0212.
        var normalizedTenantId = tenantEntity.NormalizedTenantId;
        var principals = DbContext.FederatedPrincipals.IgnoreQueryFilters();
        DbContext.RoleAssignments.RemoveRange(
            await DbContext
                .RoleAssignments.IgnoreQueryFilters()
                .Where(assignment =>
                    assignment.NormalizedTenantId != normalizedTenantId
                    && principals.Any(principal =>
                        principal.NormalizedTenantId == normalizedTenantId
                        && principal.NormalizedPrincipalId == assignment.NormalizedPrincipalId
                    )
                )
                .ToListAsync(cancellationToken)
        );

        await RemoveTenantOwnedRowsAsync(tenantEntity.NormalizedTenantId, cancellationToken);

        // A tenant's master keys belong to it through their owner, not a tenant column, so the generic removal above
        // does not reach them (ADR-0235).
        var masterKeys = await DbContext
            .MasterKeys.Where(entity =>
                entity.OwnerType == SecretOwnerTypes.Tenant
                && entity.NormalizedOwnerId == normalizedTenantId
            )
            .ToListAsync(cancellationToken);
        DbContext.MasterKeys.RemoveRange(masterKeys);

        DbContext.Tenants.Remove(tenantEntity);

        // A deleted tenant's identifier and issuer are never reissued (ADR-0189).
        await DbContext.RetiredTenants.AddAsync(
            new RetiredTenantEntity
            {
                Id = NextId(),
                TenantId = tenantEntity.TenantId,
                NormalizedTenantId = tenantEntity.NormalizedTenantId,
            },
            cancellationToken
        );

        if (tenantEntity is { Issuer: { } issuer, NormalizedIssuer: { } normalizedIssuer })
        {
            await RetireIssuerAsync(
                tenantEntity.TenantId,
                issuer,
                normalizedIssuer,
                cancellationToken
            );
        }

        return true;
    }

    /// <summary>
    /// Removes every row that a tenant owns, together with the secrets that those rows reference. Every entity that a
    /// tenant owns implements <see cref="ISupportTenantEntity"/>, including an entity that another package contributes
    /// to the model, such as a local account, so it is erased with its tenant exactly as the tenant query filter scopes it.
    /// </summary>
    /// <param name="normalizedTenantId">The normalized identifier of the tenant whose rows are removed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    // See ADR-0212.
    private async ValueTask RemoveTenantOwnedRowsAsync(
        string normalizedTenantId,
        CancellationToken cancellationToken
    )
    {
        var clrTypes = DbContext
            .Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(clrType => typeof(ISupportTenantEntity).IsAssignableFrom(clrType))
            .Distinct()
            .ToList();

        var secretIds = new List<long>();
        foreach (var clrType in clrTypes)
        {
            if (
                RemoveOwnedRowsMethod
                    .MakeGenericMethod(clrType)
                    .Invoke(this, [normalizedTenantId, cancellationToken])
                is not ValueTask<IReadOnlyList<ISupportTenantEntity>> removal
            )
            {
                throw new InvalidOperationException(
                    $"The '{nameof(RemoveOwnedRowsAsync)}' method did not return the removed rows of '{clrType}'."
                );
            }

            var rows = await removal;
            secretIds.AddRange(rows.OfType<ISupportSecretEntity>().Select(row => row.SecretId));
        }

        if (secretIds.Count == 0)
        {
            return;
        }

        var secrets = await DbContext
            .Secrets.Where(secret => secretIds.Contains(secret.Id))
            .ToListAsync(cancellationToken);
        DbContext.Secrets.RemoveRange(secrets);
    }

    /// <summary>
    /// Removes the rows of one entity type that a tenant owns, and returns the rows that were removed.
    /// </summary>
    /// <typeparam name="TEntity">The type of the tenant-owned entity.</typeparam>
    /// <param name="normalizedTenantId">The normalized identifier of the tenant whose rows are removed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the rows that were removed.</returns>
    private async ValueTask<IReadOnlyList<ISupportTenantEntity>> RemoveOwnedRowsAsync<TEntity>(
        string normalizedTenantId,
        CancellationToken cancellationToken
    )
        where TEntity : class, ISupportTenantEntity
    {
        var rows = await DbContext
            .Set<TEntity>()
            .IgnoreQueryFilters()
            .Where(entity => entity.NormalizedTenantId == normalizedTenantId)
            .ToListAsync(cancellationToken);
        DbContext.Set<TEntity>().RemoveRange(rows);
        return rows;
    }

    /// <summary>
    /// Records that a tenant gave up an issuer, unless the issuer is already retired. Only the tenant that held an issuer
    /// can take it back, so an issuer that is already retired was retired by the same tenant.
    /// </summary>
    private async ValueTask RetireIssuerAsync(
        string tenantId,
        string issuer,
        string normalizedIssuer,
        CancellationToken cancellationToken
    )
    {
        var isRetired =
            DbContext.RetiredIssuers.Local.Any(entity =>
                entity.NormalizedIssuer == normalizedIssuer
            )
            || await DbContext.RetiredIssuers.AnyAsync(
                entity => entity.NormalizedIssuer == normalizedIssuer,
                cancellationToken
            );
        if (isRetired)
            return;

        await DbContext.RetiredIssuers.AddAsync(
            new RetiredIssuerEntity
            {
                Id = NextId(),
                Issuer = issuer,
                NormalizedIssuer = normalizedIssuer,
                TenantId = tenantId,
            },
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsTenantIdRetiredAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(tenantId);
        return await DbContext.RetiredTenants.AnyAsync(
            entity => entity.NormalizedTenantId == normalizedTenantId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetRetiredIssuerHolderOrDefaultAsync(
        string issuer,
        CancellationToken cancellationToken
    )
    {
        var normalizedIssuer = Normalize(issuer);
        return await DbContext
            .RetiredIssuers.Where(entity => entity.NormalizedIssuer == normalizedIssuer)
            .Select(entity => entity.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
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

        return await HasDependentsAsync(tenantEntity, cancellationToken);
    }

    /// <summary>
    /// Determines whether a tenant has clients or resource servers that block its deletion. Everything else the tenant
    /// owns, its end users' data included, is removed with it.
    /// </summary>
    private async ValueTask<bool> HasDependentsAsync(
        TenantEntity tenantEntity,
        CancellationToken cancellationToken
    )
    {
        var hasClients = await DbContext
            .Clients.IgnoreQueryFilters()
            .AnyAsync(client => client.TenantId == tenantEntity.Id, cancellationToken);
        if (hasClients)
        {
            return true;
        }

        return await DbContext
            .ResourceServers.IgnoreQueryFilters()
            .AnyAsync(
                resourceServer =>
                    resourceServer.TenantId == tenantEntity.Id && !resourceServer.IsSystem,
                cancellationToken
            );
    }
}
