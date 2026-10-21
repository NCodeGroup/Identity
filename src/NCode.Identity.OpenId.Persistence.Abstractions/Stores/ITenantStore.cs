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
    /// <param name="page">The <see cref="PageRequest"/> describing the pagination window (cursor and limit).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page of
    /// <see cref="PersistedTenant"/> instances and the cursor for the next page.</returns>
    ValueTask<PagedResult<PersistedTenant>> GetPageAsync(
        PageRequest page,
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
    /// Attempts to get a <see cref="PersistedTenant"/> instance from the store with the specified issuer.
    /// </summary>
    /// <remarks>
    /// An issuer identifies a tenant, so at most one tenant can hold a given value; this is the lookup that enforces
    /// it on write.
    /// </remarks>
    /// <param name="issuer">The issuer of the <see cref="PersistedTenant"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedTenant"/> instance matching the specified entity if it exists.</returns>
    ValueTask<PersistedTenant?> GetOrDefaultByIssuerAsync(
        string issuer,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a <see cref="PersistedTenant"/> instance from the store whose issuer or mutual-TLS endpoint-alias
    /// authority has the specified host.
    /// </summary>
    /// <remarks>
    /// Both hosts are derived from the tenant's addresses rather than stored independently, so the value routed on cannot
    /// drift from the identity and aliases published to clients. More than one tenant may share a host (path-based
    /// tenants do), so a host-based deployment is responsible for keeping its hosts distinct; when more than one tenant
    /// holds the host, none is returned, so an ambiguous host never routes to an arbitrary tenant.
    /// </remarks>
    /// <param name="host">The issuer or alias host of the <see cref="PersistedTenant"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedTenant"/> instance that alone holds the host, if any.</returns>
    ValueTask<PersistedTenant?> GetOrDefaultByHostAsync(
        string host,
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
    /// <remarks>
    /// Removing a tenant retires its identifier and its issuer in the same unit of work, so neither is reissued
    /// (ADR-0189). Changing a tenant's issuer through <c>UpdateAsync</c> likewise retires the issuer it gave up.
    /// </remarks>
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

    /// <summary>
    /// Determines whether the specified tenant identifier belonged to a deleted tenant. A tenant identifier is never
    /// reissued (ADR-0189).
    /// </summary>
    /// <param name="tenantId">The tenant identifier to look up.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when a
    /// deleted tenant had the identifier; otherwise <c>false</c>.</returns>
    ValueTask<bool> IsTenantIdRetiredAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the identifier of the tenant that gave up the specified issuer, by changing its issuer or by being deleted.
    /// Relying parties that trusted the issuer would trust any tenant that took it, so only that tenant can take it back
    /// (ADR-0189).
    /// </summary>
    /// <param name="issuer">The issuer to look up.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the identifier of
    /// the tenant that held the issuer, or <c>null</c> when no tenant has given it up.</returns>
    ValueTask<string?> GetRetiredIssuerHolderOrDefaultAsync(
        string issuer,
        CancellationToken cancellationToken
    );
}
