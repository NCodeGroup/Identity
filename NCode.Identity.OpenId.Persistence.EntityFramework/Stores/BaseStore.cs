﻿#region Copyright Preamble

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

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

internal abstract class BaseStore<TItem, TEntity> : IStore<TItem>
    where TItem : class, ISupportId<long>
    where TEntity : class, ISupportId<long>
{
    protected abstract IIdGenerator<long> IdGenerator { get; }

    protected abstract OpenIdDbContext DbContext { get; }

    protected long NextId(long? value = null)
    {
        var valueOrDefault = value.GetValueOrDefault(0);
        return valueOrDefault == 0 ? IdGenerator.CreateId() : valueOrDefault;
    }

    protected static string NextConcurrencyToken()
    {
        return Guid.NewGuid().ToString("N");
    }

    [return: NotNullIfNotNull("value")]
    protected static string? Normalize(string? value) => value?.ToUpperInvariant();

    #region IStore

    /// <inheritdoc />
    public abstract ValueTask AddAsync(TItem item, CancellationToken cancellationToken);

    /// <inheritdoc />
    public abstract ValueTask RemoveByIdAsync(long id, CancellationToken cancellationToken);

    /// <inheritdoc />
    public virtual async ValueTask<TItem?> TryGetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return await TryGetAsync(entity => entity.Id == id, cancellationToken);
    }

    //

    protected abstract ValueTask<TItem> MapAsync(TEntity entity, CancellationToken cancellationToken);

    protected abstract ValueTask<TEntity?> TryGetEntityAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken);

    protected async ValueTask<TEntity> GetEntityByIdAsync(long id, CancellationToken cancellationToken)
    {
        var entity = await TryGetEntityAsync(entity => entity.Id == id, cancellationToken);
        return entity ?? throw new InvalidOperationException("Entity not found.");
    }

    protected virtual async ValueTask<TItem?> TryGetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var entity = await TryGetEntityAsync(predicate, cancellationToken);
        return entity is null ? null : await MapAsync(entity, cancellationToken);
    }

    #endregion

    #region Tenant

    protected async ValueTask<TenantEntity?> TryGetTenantAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var normalizedTenantId = Normalize(tenantId);
        return await DbContext.Tenants
            .Where(tenant => tenant.NormalizedTenantId == normalizedTenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    protected async ValueTask<TenantEntity?> TryGetTenantAsync(
        ISupportTenantId supportTenantId,
        CancellationToken cancellationToken)
    {
        return await TryGetTenantAsync(supportTenantId.TenantId, cancellationToken);
    }

    protected async ValueTask<TenantEntity> GetTenantAsync(
        ISupportTenantId supportTenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await TryGetTenantAsync(supportTenantId.TenantId, cancellationToken);
        return tenant ?? throw new InvalidOperationException("Tenant not found.");
    }

    #endregion

    #region Secret

    protected SecretEntity MapNew(TenantEntity tenant, PersistedSecret secret) => new()
    {
        Id = NextId(secret.Id),
        TenantId = tenant.Id,
        SecretId = secret.SecretId,
        NormalizedSecretId = Normalize(secret.SecretId),
        ConcurrencyToken = NextConcurrencyToken(),
        Use = secret.Use,
        Algorithm = secret.Algorithm,
        CreatedWhen = secret.CreatedWhen.ToUniversalTime(),
        ExpiresWhen = secret.ExpiresWhen.ToUniversalTime(),
        SecretType = secret.SecretType,
        KeySizeBits = secret.KeySizeBits,
        UnprotectedSizeBytes = secret.UnprotectedSizeBytes,
        ProtectedValue = secret.ProtectedValue,
        Tenant = tenant
    };

    protected static PersistedSecret MapExisting(SecretEntity secret) => new()
    {
        Id = secret.Id,
        TenantId = secret.Tenant.TenantId,
        SecretId = secret.SecretId,
        ConcurrencyToken = secret.ConcurrencyToken,
        Use = secret.Use,
        Algorithm = secret.Algorithm,
        CreatedWhen = secret.CreatedWhen,
        ExpiresWhen = secret.ExpiresWhen,
        SecretType = secret.SecretType,
        KeySizeBits = secret.KeySizeBits,
        UnprotectedSizeBytes = secret.UnprotectedSizeBytes,
        ProtectedValue = secret.ProtectedValue
    };

    protected static PersistedSecret MapExisting(ISupportSecret parent) =>
        MapExisting(parent.Secret);

    protected static IEnumerable<PersistedSecret> MapExisting(IEnumerable<ISupportSecret> collection) =>
        collection.Select(MapExisting);

    #endregion
}
