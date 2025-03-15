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
using System.Text.Json;
using IdGen;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a default implementation of <see cref="ITenantStore"/> that uses Entity Framework Core for persistence.
/// </summary>
[PublicAPI]
public class TenantStore(
    IStoreProvider storeProvider,
    IIdGenerator<long> idGenerator,
    OpenIdDbContext openIdDbContext
) : BaseStoreWithEntityId<PersistedTenant, TenantEntity>, ITenantStore
{
    /// <inheritdoc />
    protected override IStoreProvider StoreProvider { get; } = storeProvider;

    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = openIdDbContext;

    /// <inheritdoc />
    protected override ValueTask<PersistedTenant> MapAsync(
        TenantEntity tenant,
        CancellationToken cancellationToken
    )
    {
        return ValueTask.FromResult(new PersistedTenant
        {
            Id = tenant.Id,
            TenantId = tenant.TenantId,
            DomainName = tenant.DomainName,
            ConcurrencyToken = tenant.ConcurrencyToken,
            IsDisabled = tenant.IsDisabled,
            DisplayName = tenant.DisplayName,
            SettingsState = ConcurrentStateFactory.Create(tenant.SettingsJson, tenant.SettingsConcurrencyToken),
            SecretsState = ConcurrentStateFactory.Create(MapExisting(tenant.Secrets), tenant.SecretsConcurrencyToken)
        });
    }

    /// <inheritdoc />
    protected override async ValueTask<TenantEntity?> TryGetEntityAsync(
        Expression<Func<TenantEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        return await DbContext.Tenants
            .Include(tenant => tenant.Secrets)
            .ThenInclude(tenantSecret => tenantSecret.Secret)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedTenant persistedTenant,
        CancellationToken cancellationToken
    )
    {
        persistedTenant.Id = NextId(persistedTenant.Id);

        var secrets = new List<TenantSecretEntity>(persistedTenant.SecretsState.Value.Count);

        var tenantEntity = new TenantEntity
        {
            Id = persistedTenant.Id,
            TenantId = persistedTenant.TenantId,
            NormalizedTenantId = Normalize(persistedTenant.TenantId),
            DomainName = persistedTenant.DomainName,
            NormalizedDomainName = Normalize(persistedTenant.DomainName),
            ConcurrencyToken = NextConcurrencyToken(),
            SettingsConcurrencyToken = persistedTenant.SettingsState.ConcurrencyToken,
            SecretsConcurrencyToken = persistedTenant.SecretsState.ConcurrencyToken,
            IsDisabled = persistedTenant.IsDisabled,
            DisplayName = persistedTenant.DisplayName,
            SettingsJson = persistedTenant.SettingsState.Value,
            Secrets = secrets
        };

        foreach (var persistedSecret in persistedTenant.SecretsState.Value)
        {
            var secretEntity = MapNew(persistedSecret);

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
    public async ValueTask UpdateAsync(
        PersistedTenant persistedTenant,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetEntityByIdAsync(persistedTenant.Id, cancellationToken);

        tenantEntity.ConcurrencyToken = NextConcurrencyToken();
        tenantEntity.SettingsConcurrencyToken = persistedTenant.SettingsState.ConcurrencyToken;
        // we don't update the secrets concurrency token here because that is a disconnected entity collection
        tenantEntity.IsDisabled = persistedTenant.IsDisabled;
        tenantEntity.DisplayName = persistedTenant.DisplayName;
        tenantEntity.SettingsJson = persistedTenant.SettingsState.Value;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTenant?> TryGetByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(tenantId);
        return await TryGetAsync(
            tenant => tenant.NormalizedTenantId == normalizedTenantId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTenant?> TryGetByDomainNameAsync(
        string domainName,
        CancellationToken cancellationToken
    )
    {
        var normalizedDomainName = Normalize(domainName);
        return await TryGetAsync(
            tenant => tenant.NormalizedDomainName == normalizedDomainName,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<ConcurrentState<JsonElement>> GetSettingsAsync(
        string tenantId,
        ConcurrentState<JsonElement> lastKnownState,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(tenantId);

        var entity = await DbContext.Tenants
            .Where(tenant => tenant.NormalizedTenantId == normalizedTenantId)
            .SingleAsync(cancellationToken);

        var concurrencyToken = entity.SettingsConcurrencyToken;
        if (string.Equals(concurrencyToken, lastKnownState.ConcurrencyToken, StringComparison.Ordinal))
        {
            return lastKnownState;
        }

        var settingsJson = entity.SettingsJson;
        return ConcurrentStateFactory.Create(settingsJson, concurrencyToken);
    }

    /// <inheritdoc />
    public async ValueTask<ConcurrentState<IReadOnlyCollection<PersistedSecret>>> GetSecretsAsync(
        string tenantId,
        ConcurrentState<IReadOnlyCollection<PersistedSecret>> lastKnownState,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(tenantId);

        var concurrencyToken = await DbContext.Tenants
            .Where(tenant => tenant.NormalizedTenantId == normalizedTenantId)
            .Select(tenant => tenant.SecretsConcurrencyToken)
            .SingleAsync(cancellationToken);

        if (string.Equals(concurrencyToken, lastKnownState.ConcurrencyToken, StringComparison.Ordinal))
        {
            return lastKnownState;
        }

        IReadOnlyCollection<PersistedSecret> secrets = await DbContext.TenantSecrets
            .Include(tenantSecret => tenantSecret.Tenant)
            .Include(tenantSecret => tenantSecret.Secret)
            .Where(tenantSecret => tenantSecret.Tenant.NormalizedTenantId == normalizedTenantId)
            .Select(tenantSecret => tenantSecret.Secret)
            .Select(secret => MapExisting(secret))
            .ToListAsync(cancellationToken);

        return ConcurrentStateFactory.Create(secrets, concurrencyToken);
    }
}
