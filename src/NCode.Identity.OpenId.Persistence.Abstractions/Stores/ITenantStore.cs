#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedTenant"/> instances.
/// </summary>
[PublicAPI]
public interface ITenantStore : IStore<PersistedTenant>
{
    /// <summary>
    /// Gets a single page of <see cref="PersistedTenant"/> instances ordered by their identifier, using keyset
    /// (cursor) pagination.
    /// </summary>
    /// <param name="cursor">The opaque cursor returned by a previous call that fetches the next page, or <c>null</c>
    /// to fetch the first page.</param>
    /// <param name="limit">The maximum number of items to return on the page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page of
    /// <see cref="PersistedTenant"/> instances and the cursor for the next page.</returns>
    ValueTask<PagedResult<PersistedTenant>> GetPageAsync(
        string? cursor,
        int limit,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a <see cref="PersistedTenant"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="tenantId">The identifier of the <see cref="PersistedTenant"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedTenant"/> instance matching the specified entity if it exists.</returns>
    ValueTask<PersistedTenant?> GetOrDefaultAsync(
        string tenantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a <see cref="PersistedTenant"/> instance from the store with the specified domain name.
    /// </summary>
    /// <param name="domainName">The domain name of the <see cref="PersistedTenant"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedTenant"/> instance matching the specified entity if it exists.</returns>
    ValueTask<PersistedTenant?> GetOrDefaultByDomainNameAsync(
        string domainName,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the <see cref="PersistedTenantSettings"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenId Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation. </param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="PersistedTenantSettings"/> for the specified identifier.</returns>
    ValueTask<PersistedTenantSettings> GetSettingsAsync(
        string tenantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the <see cref="PersistedTenantSecrets"/> instance from store with the specified identifier.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenId Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="PersistedTenantSecrets"/> for the specified identifier.</returns>
    ValueTask<PersistedTenantSecrets> GetSecretsAsync(
        string tenantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the JSON settings for an OpenId Tenant in the store.
    /// </summary>
    /// <param name="persistedTenantSettings">The <see cref="PersistedTenantSettings"/> instance to update.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateSettingsAsync(
        PersistedTenantSettings persistedTenantSettings,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get the <see cref="PersistedTenantSettings"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenId Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedTenantSettings"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedTenantSettings?> GetSettingsOrDefaultAsync(
        string tenantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get the <see cref="PersistedTenantSecrets"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenId Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedTenantSecrets"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedTenantSecrets?> GetSecretsOrDefaultAsync(
        string tenantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a single <see cref="PersistedSecret"/> for an OpenId Tenant by its identifier.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenId Tenant.</param>
    /// <param name="secretId">The identifier of the secret to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedSecret"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedSecret?> GetSecretOrDefaultAsync(
        string tenantId,
        string secretId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Adds a new secret to an OpenId Tenant and bumps the tenant's secrets concurrency token so that any
    /// running tenant instance refreshes its secret collection.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenId Tenant.</param>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> to add.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddSecretAsync(
        string tenantId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the metadata of an existing secret for an OpenId Tenant (key material is immutable) and bumps
    /// the tenant's secrets concurrency token so that any running tenant instance refreshes its secret collection.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenId Tenant.</param>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> whose metadata is updated. Its
    /// <see cref="PersistedSecret.ConcurrencyToken"/> is checked for optimistic concurrency.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateSecretAsync(
        string tenantId,
        PersistedSecret persistedSecret,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes a secret from an OpenId Tenant and bumps the tenant's secrets concurrency token so that any
    /// running tenant instance refreshes its secret collection.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenId Tenant.</param>
    /// <param name="secretId">The identifier of the secret to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// if a secret was removed; otherwise <c>false</c> when no matching secret existed.</returns>
    ValueTask<bool> RemoveSecretAsync(
        string tenantId,
        string secretId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes an OpenId Tenant from the store. The removal is rejected while the tenant still has dependent
    /// child resources (any clients or secrets); those dependents must be removed first.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenId Tenant to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// if the tenant was removed; otherwise <c>false</c> when no matching tenant existed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the tenant still has dependent child resources.</exception>
    ValueTask<bool> RemoveAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether an OpenId Tenant still has dependent child resources (any clients or secrets) that
    /// would block its removal.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenId Tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// when the tenant has dependent child resources; otherwise <c>false</c> (including when no such tenant exists).</returns>
    ValueTask<bool> HasDependentsAsync(string tenantId, CancellationToken cancellationToken);
}
