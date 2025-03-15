#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using IdGen;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a base implementation for <see cref="IStore{TItem}"/> that uses entity framework.
/// </summary>
/// <typeparam name="TItem">The type of the persisted item, also known as a <c>Data Transfer Object</c> or <c>DTO</c>.</typeparam>
/// <typeparam name="TEntity">The type of the corresponding entity.</typeparam>
[PublicAPI]
public abstract class BaseStore<TItem, TEntity> : IStore
    where TItem : class
    where TEntity : class
{
    /// <summary>
    /// Gets the <see cref="IStoreProvider"/> for this store.
    /// </summary>
    protected abstract IStoreProvider StoreProvider { get; }

    /// <summary>
    /// Gets the <see cref="IdGenerator"/> for this store.
    /// </summary>
    protected abstract IIdGenerator<long> IdGenerator { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdDbContext"/> for this store.
    /// </summary>
    protected abstract OpenIdDbContext DbContext { get; }

    /// <summary>
    /// Gets the next unique identifier for an entity if not already provided.
    /// </summary>
    /// <param name="value">The current value of the identifier, if any.</param>
    protected long NextId(long? value = null)
    {
        var valueOrDefault = value.GetValueOrDefault(0);
        return valueOrDefault == 0 ? IdGenerator.CreateId() : valueOrDefault;
    }

    /// <summary>
    /// Gets the next unique concurrency token for an entity.
    /// </summary>
    protected static string NextConcurrencyToken()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Returns a string value in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [return: NotNullIfNotNull("value")]
    protected static string? Normalize(string? value) => value?.ToUpperInvariant();

    /// <summary>
    /// Maps an entity to its corresponding DTO.
    /// </summary>
    /// <param name="entity">The entity to map.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// newly mapped DTO instance.</returns>
    protected abstract ValueTask<TItem> MapAsync(TEntity entity, CancellationToken cancellationToken);

    #region IStoreProvider

    /// <inheritdoc />
    public object? GetService(Type serviceType)
        => StoreProvider.GetService(serviceType);

    /// <inheritdoc />
    public virtual TStore GetStore<TStore>()
        where TStore : IStore
        => StoreProvider.GetStore<TStore>();

    #endregion

    #region Tenant

    /// <summary>
    /// Attempts to retrieve a tenant from the store based on the provided tenant identifier.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to use to find the tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="TenantEntity"/> if found; otherwise <c>null</c>.</returns>
    protected async ValueTask<TenantEntity?> TryGetTenantAsync(
        string? tenantId,
        CancellationToken cancellationToken)
    {
        var normalizedTenantId = Normalize(tenantId);
        return await DbContext.Tenants
            .Where(tenant => tenant.NormalizedTenantId == normalizedTenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Attempts to retrieve a tenant from the store based on the provided tenant identifier.
    /// </summary>
    /// <param name="supportTenantId">An object that supports a tenant identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="TenantEntity"/> if found; otherwise <c>null</c>.</returns>
    protected async ValueTask<TenantEntity?> TryGetTenantAsync(
        ISupportTenantId supportTenantId,
        CancellationToken cancellationToken)
    {
        return await TryGetTenantAsync(supportTenantId.TenantId, cancellationToken);
    }

    /// <summary>
    /// Gets a tenant from the store based on the provided tenant identifier.
    /// </summary>
    /// <param name="supportTenantId">An object that supports a tenant identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="TenantEntity"/> if found; otherwise throws an <see cref="InvalidOperationException"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the tenant is not found.</exception>
    protected async ValueTask<TenantEntity> GetTenantAsync(
        ISupportTenantId supportTenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await TryGetTenantAsync(supportTenantId.TenantId, cancellationToken);
        return tenant ?? throw new InvalidOperationException("Tenant not found.");
    }

    #endregion

    #region Secret

    /// <summary>
    /// Maps a <see cref="PersistedSecret"/> DTO to its corresponding <see cref="SecretEntity"/>.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> DTO to map.</param>
    /// <returns>The newly mapped <see cref="SecretEntity"/> entity.</returns>
    protected SecretEntity MapNew(PersistedSecret secret) => new()
    {
        Id = NextId(secret.Id),
        SecretId = secret.SecretId,
        NormalizedSecretId = Normalize(secret.SecretId),
        ConcurrencyToken = NextConcurrencyToken(),
        Use = secret.Use,
        Algorithm = secret.Algorithm,
        CreatedWhen = secret.CreatedWhen.ToUniversalTime(),
        ExpiresWhen = secret.ExpiresWhen.ToUniversalTime(),
        SecretType = secret.SecretType,
        KeySizeBits = secret.KeySizeBits,
        EncodingType = secret.EncodingType,
        EncodedValue = secret.EncodedValue,
    };

    /// <summary>
    /// Maps a <see cref="SecretEntity"/> to its corresponding <see cref="PersistedSecret"/> DTO.
    /// </summary>
    /// <param name="secret">The <see cref="SecretEntity"/> entity to map.</param>
    /// <returns>The newly mapped <see cref="PersistedSecret"/> DTO.</returns>
    protected static PersistedSecret MapExisting(SecretEntity secret) => new()
    {
        Id = secret.Id,
        SecretId = secret.SecretId,
        ConcurrencyToken = secret.ConcurrencyToken,
        Use = secret.Use,
        Algorithm = secret.Algorithm,
        CreatedWhen = secret.CreatedWhen,
        ExpiresWhen = secret.ExpiresWhen,
        SecretType = secret.SecretType,
        KeySizeBits = secret.KeySizeBits,
        EncodingType = secret.EncodingType,
        EncodedValue = secret.EncodedValue
    };

    /// <summary>
    /// Maps a <see cref="SecretEntity"/> to its corresponding <see cref="PersistedSecret"/> DTO.
    /// </summary>
    /// <param name="parent">An object that contains a secret.</param>
    /// <returns>The newly mapped <see cref="PersistedSecret"/> DTO.</returns>
    protected static PersistedSecret MapExisting(ISupportSecret parent) =>
        MapExisting(parent.Secret);

    /// <summary>
    /// Maps a collection of <see cref="SecretEntity"/> instances to their corresponding collection of <see cref="PersistedSecret"/> DTOs.
    /// </summary>
    /// <param name="collection">The collection of <see cref="SecretEntity"/> instances to map.</param>
    /// <returns>The newly mapped collection of <see cref="PersistedSecret"/> DTOs.</returns>
    protected static IReadOnlyCollection<PersistedSecret> MapExisting(IEnumerable<ISupportSecret> collection) =>
        collection.Select(MapExisting).ToList();

    #endregion
}
