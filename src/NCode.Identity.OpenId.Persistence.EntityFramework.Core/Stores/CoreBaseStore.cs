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

using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;
using NCode.Identity.OpenId.Persistence.EntityFramework.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Extends the reusable framework <see cref="BaseStore{TItem,TEntity}"/> with the core OpenID schema helpers that
/// depend on concrete core entities: resolving the owning <see cref="TenantEntity"/> and mapping secrets. These live in
/// the core slice (not the framework) so the framework stays free of any domain entity.
/// </summary>
/// <typeparam name="TItem">The type of the persisted item, also known as a <c>Data Transfer Object</c> (DTO),
/// which represents the data contract used outside the persistence layer.</typeparam>
/// <typeparam name="TEntity">The type of the corresponding Entity Framework entity used for database operations.</typeparam>
internal abstract class CoreBaseStore<TItem, TEntity> : BaseStore<TItem, TEntity>
    where TItem : class
    where TEntity : class
{
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
        return await DbContext
            .Set<TenantEntity>()
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
        return tenantEntity
            ?? throw new InvalidOperationException(
                $"An OpenId Tenant with '{tenantId}' was not found."
            );
    }

    #endregion

    #region Secret

    /// <summary>
    /// Maps a <see cref="PersistedSecret"/> DTO to its corresponding <see cref="SecretEntity"/>.
    /// </summary>
    /// <param name="secret">The <see cref="PersistedSecret"/> DTO to map.</param>
    /// <param name="id">The optional identifier for the entity. If <c>null</c> or zero, a new identifier will be generated.</param>
    /// <returns>The newly mapped <see cref="SecretEntity"/> entity.</returns>
    protected SecretEntity MapToSecretEntity(PersistedSecret secret, long? id = null) =>
        new()
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
            EncodedValue = secret.EncodedValue,
        };

    /// <summary>
    /// Maps a <see cref="SecretEntity"/> to its corresponding <see cref="PersistedSecret"/> DTO.
    /// </summary>
    /// <param name="secret">The <see cref="SecretEntity"/> entity to map.</param>
    /// <returns>The newly mapped <see cref="PersistedSecret"/> DTO.</returns>
    protected static PersistedSecret MapToPersistedSecret(SecretEntity secret) =>
        new()
        {
            SecretId = secret.SecretId,
            ConcurrencyToken = secret.ConcurrencyToken,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ExpiresWhen = secret.ExpiresWhen,
            SecretType = secret.SecretType,
            KeySizeBits = secret.KeySizeBits,
            EncodedValue = secret.EncodedValue,
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
    protected static IReadOnlyCollection<PersistedSecret> MapToPersistedSecrets(
        IEnumerable<ISupportSecretEntity> collection
    ) => collection.Select(MapToPersistedSecret).ToList();

    #endregion
}
