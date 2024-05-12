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
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

internal class TenantStore(
    IIdGenerator<long> idGenerator,
    OpenIdDbContext dbContext
) : BaseStore<PersistedTenant, TenantEntity>, ITenantStore
{
    /// <inheritdoc />
    protected override IIdGenerator<long> IdGenerator { get; } = idGenerator;

    /// <inheritdoc />
    protected override OpenIdDbContext DbContext { get; } = dbContext;

    /// <inheritdoc />
    public override async ValueTask AddAsync(
        PersistedTenant persistedTenant,
        CancellationToken cancellationToken)
    {
        var tenantId = NextId(persistedTenant.Id);
        var secrets = new List<TenantSecretEntity>(persistedTenant.Secrets.Count);

        var tenantEntity = new TenantEntity
        {
            Id = tenantId,
            TenantId = persistedTenant.TenantId,
            NormalizedTenantId = Normalize(persistedTenant.TenantId),
            DomainName = persistedTenant.DomainName,
            NormalizedDomainName = Normalize(persistedTenant.DomainName),
            ConcurrencyToken = NextConcurrencyToken(persistedTenant.ConcurrencyToken),
            IsDisabled = persistedTenant.IsDisabled,
            DisplayName = persistedTenant.DisplayName,
            Settings = persistedTenant.Settings,
            Secrets = secrets
        };

        foreach (var persistedSecret in persistedTenant.Secrets)
        {
            var secretEntity = Map(tenantEntity, persistedSecret);

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
        CancellationToken cancellationToken)
    {
        var tenantEntity = await GetEntityByIdAsync(persistedTenant.Id, cancellationToken);

        tenantEntity.ConcurrencyToken = NextConcurrencyToken();
        tenantEntity.IsDisabled = persistedTenant.IsDisabled;
        tenantEntity.DisplayName = persistedTenant.DisplayName;
        tenantEntity.Settings = persistedTenant.Settings;
    }

    /// <inheritdoc />
    public override ValueTask RemoveByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        // TODO: do we really want to support this?
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTenant?> TryGetByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var normalizedTenantId = Normalize(tenantId);
        return await TryGetAsync(
            tenant => tenant.NormalizedTenantId == normalizedTenantId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedTenant?> TryGetByDomainNameAsync(
        string domainName,
        CancellationToken cancellationToken)
    {
        var normalizedDomainName = Normalize(domainName);
        return await TryGetAsync(
            tenant => tenant.NormalizedDomainName == normalizedDomainName,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<PersistedSecret>> GetSecretsAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var normalizedTenantId = Normalize(tenantId);
        return await DbContext.TenantSecrets
            .Include(tenantSecret => tenantSecret.Tenant)
            .Include(tenantSecret => tenantSecret.Secret)
            .Where(tenantSecret => tenantSecret.Tenant.NormalizedTenantId == normalizedTenantId)
            .Select(tenantSecret => tenantSecret.Secret)
            .Select(secret => Map(secret))
            .ToListAsync(cancellationToken: cancellationToken);
    }

    //

    /// <inheritdoc />
    protected override ValueTask<PersistedTenant> MapAsync(
        TenantEntity tenant,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new PersistedTenant
        {
            Id = tenant.Id,
            TenantId = tenant.TenantId,
            DomainName = tenant.DomainName,
            ConcurrencyToken = tenant.ConcurrencyToken,
            IsDisabled = tenant.IsDisabled,
            DisplayName = tenant.DisplayName,
            Settings = tenant.Settings,
            Secrets = Map(tenant.Secrets).ToList(),
        });
    }

    /// <inheritdoc />
    protected override async ValueTask<TenantEntity?> TryGetEntityAsync(
        Expression<Func<TenantEntity, bool>> predicate,
        CancellationToken cancellationToken)
    {
        return await DbContext.Tenants
            .Include(tenant => tenant.Secrets)
            .ThenInclude(tenantSecret => tenantSecret.Secret)
            .FirstOrDefaultAsync(predicate, cancellationToken);
    }
}
