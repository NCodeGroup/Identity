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
using System.Linq.Expressions;
using IdGen;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides a base implementation for <see cref="IStore"/> that uses Entity Framework Core for data persistence.
/// This abstract class provides common functionality for CRUD operations, entity mapping, and tenant management.
/// </summary>
/// <typeparam name="TItem">The type of the persisted item, also known as a <c>Data Transfer Object</c> (DTO),
/// which represents the data contract used outside of the persistence layer.</typeparam>
/// <typeparam name="TEntity">The type of the corresponding Entity Framework entity used for database operations.</typeparam>
[PublicAPI]
public abstract class BaseStore<TItem, TEntity> : IStore
    where TItem : class
    where TEntity : class
{
    /// <summary>
    /// Gets the <see cref="IStoreProvider"/> instance that provides access to other stores
    /// and dependency injection services.
    /// </summary>
    protected abstract IStoreProvider StoreProvider { get; }

    /// <summary>
    /// Gets the <see cref="IIdGenerator{T}"/> instance used to generate unique identifiers for entities.
    /// </summary>
    protected abstract IIdGenerator<long> IdGenerator { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdDbContext"/> instance used for database operations in this store.
    /// </summary>
    protected abstract OpenIdDbContext DbContext { get; }

    /// <summary>
    /// Gets the next unique identifier for an entity. If a value is already provided and non-zero,
    /// returns that value; otherwise generates a new unique identifier using the <see cref="IdGenerator"/>.
    /// </summary>
    /// <param name="value">The current value of the identifier, if any. When <c>null</c> or zero,
    /// a new identifier will be generated.</param>
    /// <returns>The provided identifier if non-zero; otherwise a newly generated unique identifier.</returns>
    protected long NextId(long? value = null)
    {
        var valueOrDefault = value.GetValueOrDefault(0);
        return valueOrDefault == 0 ? IdGenerator.CreateId() : valueOrDefault;
    }

    /// <summary>
    /// Generates a new unique concurrency token for optimistic concurrency control.
    /// The token is a GUID formatted as a 32-character hexadecimal string without hyphens.
    /// </summary>
    /// <returns>A new unique concurrency token string.</returns>
    protected static string NextConcurrencyToken()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Normalizes a string value to uppercase for case-insensitive database lookups.
    /// This enables sargable (Search ARGument ABLE) queries for DBMS engines that
    /// don't support case-insensitive indices natively.
    /// </summary>
    /// <param name="value">The string value to normalize, or <c>null</c>.</param>
    /// <returns>The uppercase version of the input string, or <c>null</c> if the input was <c>null</c>.</returns>
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
    protected abstract ValueTask<TItem> MapFromEntityAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to retrieve an entity from the store using the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate to use to find the entity.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the entity if found; otherwise <c>null</c>.</returns>
    protected abstract ValueTask<TEntity?> GetEntityOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to retrieve a DTO from the store using the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate to use to find the entity.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the DTO if found; otherwise <c>null</c>.</returns>
    protected virtual async ValueTask<TItem?> GetOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken
    )
    {
        var entity = await GetEntityOrDefaultAsync(predicate, cancellationToken);
        return entity is null ? null : await MapFromEntityAsync(entity, cancellationToken);
    }

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
    /// Attempts to retrieve a <see cref="TenantEntity"/> instance from the store using the specified identifier.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenId Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="TenantEntity"/> if found; otherwise <c>null</c>.</returns>
    protected async ValueTask<TenantEntity?> GetTenantEntityOrDefaultAsync(
        string? tenantId,
        CancellationToken cancellationToken
    )
    {
        var normalizedTenantId = Normalize(tenantId);
        return await DbContext.Tenants
            .Where(tenant => tenant.NormalizedTenantId == normalizedTenantId)
            .Include(tenant => tenant.Secrets)
            .ThenInclude(tenantSecret => tenantSecret.Secret)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Attempts to retrieve a <see cref="TenantEntity"/> instance from the store using any object that supports the <see cref="ISupportTenantId"/> abstraction.
    /// </summary>
    /// <param name="supportTenantId">An object that supports a tenant identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="TenantEntity"/> if found; otherwise <c>null</c>.</returns>
    protected async ValueTask<TenantEntity?> GetTenantEntityOrDefaultAsync(
        ISupportTenantId supportTenantId,
        CancellationToken cancellationToken
    )
    {
        return await GetTenantEntityOrDefaultAsync(supportTenantId.TenantId, cancellationToken);
    }

    /// <summary>
    /// Gets a <see cref="TenantEntity"/> instance from the store using any object that supports the <see cref="ISupportTenantId"/> abstraction.
    /// </summary>
    /// <param name="supportTenantId">An object that supports a tenant identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="TenantEntity"/> if found; otherwise throws an <see cref="InvalidOperationException"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the <see cref="TenantEntity"/> is not found.</exception>
    protected async ValueTask<TenantEntity> GetTenantEntityAsync(
        ISupportTenantId supportTenantId,
        CancellationToken cancellationToken
    )
    {
        return await GetTenantEntityAsync(supportTenantId.TenantId, cancellationToken);
    }

    /// <summary>
    /// Gets a <see cref="TenantEntity"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenId Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="TenantEntity"/> if found; otherwise throws an <see cref="InvalidOperationException"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the <see cref="TenantEntity"/> is not found.</exception>
    protected async ValueTask<TenantEntity> GetTenantEntityAsync(
        string? tenantId,
        CancellationToken cancellationToken
    )
    {
        var tenantEntity = await GetTenantEntityOrDefaultAsync(tenantId, cancellationToken);
        return tenantEntity ?? throw new InvalidOperationException($"An OpenId Tenant with '{tenantId}' was not found.");
    }

    #endregion

    #region Secret

    /// <summary>
    /// Maps a <see cref="PersistedSecret"/> DTO to its corresponding <see cref="SecretEntity"/>.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> DTO to map.</param>
    /// <param name="id">The optional identifier for the entity. If <c>null</c> or zero, a new identifier will be generated.</param>
    /// <returns>The newly mapped <see cref="SecretEntity"/> entity.</returns>
    protected SecretEntity MapToSecretEntity(PersistedSecret secret, long? id = null) => new()
    {
        Id = NextId(id),
        SecretId = secret.SecretId,
        NormalizedSecretId = Normalize(secret.SecretId),
        ConcurrencyToken = secret.ConcurrencyToken,
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
    protected static PersistedSecret MapToPersistedSecret(SecretEntity secret) => new()
    {
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
    protected static PersistedSecret MapToPersistedSecret(ISupportSecretEntity parent) =>
        MapToPersistedSecret(parent.Secret);

    /// <summary>
    /// Maps a collection of <see cref="SecretEntity"/> instances to their corresponding collection of <see cref="PersistedSecret"/> DTOs.
    /// </summary>
    /// <param name="collection">The collection of <see cref="SecretEntity"/> instances to map.</param>
    /// <returns>The newly mapped collection of <see cref="PersistedSecret"/> DTOs.</returns>
    protected static IReadOnlyCollection<PersistedSecret> MapToPersistedSecrets(IEnumerable<ISupportSecretEntity> collection) =>
        collection.Select(MapToPersistedSecret).ToList();

    #endregion
}
