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
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedTenant"/> instances.
/// </summary>
[PublicAPI]
public interface ITenantStore : IStore<PersistedTenant>
{
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
}
