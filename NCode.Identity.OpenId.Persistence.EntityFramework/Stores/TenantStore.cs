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
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a default implementation of <see cref="ITenantStore"/> that uses Entity Framework Core for persistence.
/// </summary>
[PublicAPI]
public class TenantStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStoreWithResourceId<PersistedTenant, TenantEntity>, ITenantStore
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
        return ValueTask.FromResult(new PersistedTenantSettings
        {
            TenantId = tenantEntity.TenantId,
            ConcurrencyToken = tenantEntity.SettingsConcurrencyToken,
            Value = tenantEntity.SettingsJson
        });
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
        return ValueTask.FromResult(new PersistedTenantSecrets
        {
            TenantId = tenantEntity.TenantId,
            ConcurrencyToken = tenantEntity.SecretsConcurrencyToken,
            Value = MapToPersistedSecrets(tenantEntity.Secrets)
        });
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
            Secrets = await MapSecretsAsync(tenantEntity, cancellationToken)
        };
    }

    /// <inheritdoc />
    protected override async ValueTask<TenantEntity?> GetEntityOrDefaultAsync(
        Expression<Func<TenantEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return await DbContext.Tenants
            .Include(tenant => tenant.Secrets)
            .ThenInclude(tenantSecret => tenantSecret.Secret)
            .SingleOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask<TenantEntity?> GetEntityOrDefaultAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(tenantId);
        return await GetEntityOrDefaultAsync(
            entity => entity.NormalizedTenantId == normalizedTenantId,
            cancellationToken
        );
    }

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
            Secrets = secrets
        };

        foreach (var persistedSecret in persistedTenant.Secrets.Value)
        {
            persistedSecret.ConcurrencyToken = NextConcurrencyToken();

            var secretEntity = MapToSecretEntity(persistedSecret);

            await DbContext.Secrets.AddAsync(secretEntity, cancellationToken);

            var tenantSecretEntity = new TenantSecretEntity
            {
                Id = NextId(),

                Tenant = tenantEntity,
                TenantId = tenantEntity.Id,

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

        if (!string.Equals(persistedTenant.ConcurrencyToken, tenantEntity.ConcurrencyToken, StringComparison.Ordinal))
        {
            throw new DbUpdateConcurrencyException(
                $"The OpenId Tenant with TenantId='{tenantId}' has been modified by another process. Please reload and try again."
            );
        }

        var nextConcurrencyToken = NextConcurrencyToken();

        tenantEntity.ConcurrencyToken = nextConcurrencyToken;
        tenantEntity.IsDisabled = persistedTenant.IsDisabled;
        tenantEntity.DisplayName = persistedTenant.DisplayName;
        // TODO: DomainName

        DbContext.Tenants.Update(tenantEntity);

        persistedTenant.ConcurrencyToken = nextConcurrencyToken;
    }

    /// <inheritdoc />
    public async ValueTask UpdateSettingsAsync(
        PersistedTenantSettings persistedTenantSettings,
        CancellationToken cancellationToken
    )
    {
        var tenantId = persistedTenantSettings.TenantId;
        var tenantEntity = await GetEntityAsync(tenantId, cancellationToken);

        if (!string.Equals(persistedTenantSettings.ConcurrencyToken, tenantEntity.SettingsConcurrencyToken, StringComparison.Ordinal))
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
}
