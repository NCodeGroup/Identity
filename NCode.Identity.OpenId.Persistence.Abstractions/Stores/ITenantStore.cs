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

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedTenant"/> instances.
/// </summary>
[PublicAPI]
public interface ITenantStore : IStore<PersistedTenant>
{
    /// <summary>
    /// Attempts to get a <see cref="PersistedTenant"/> instance by using its natural key.
    /// </summary>
    /// <param name="tenantId">The natural key of the <see cref="PersistedTenant"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedTenant"/> instance matching the specified <paramref name="tenantId"/> if it exists.</returns>
    ValueTask<PersistedTenant?> TryGetByTenantIdAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to get a <see cref="PersistedTenant"/> instance by using its domain name.
    /// </summary>
    /// <param name="domainName">The domain name of the <see cref="PersistedTenant"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedTenant"/> instance matching the specified <paramref name="domainName"/> if it exists.</returns>
    ValueTask<PersistedTenant?> TryGetByDomainNameAsync(
        string domainName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing persisted tenant in the store.
    /// The secrets collection is not updated by this method.
    /// </summary>
    /// <param name="persistedTenant">The persisted tenant to update in the store.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateAsync(
        PersistedTenant persistedTenant,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the <see cref="JsonElement"/> settings for the specified <paramref name="tenantId"/>.
    /// The concurrency token from <paramref name="lastKnownState"/> is used to check if the current value has changed.
    /// If the value hasn't changed, this method returns the same <paramref name="lastKnownState"/> instance;
    /// otherwise, it returns the most recent value from the store.
    /// </summary>
    /// <param name="tenantId">The natural key of the tenant.</param>
    /// <param name="lastKnownState">The last known state of the <see cref="JsonElement"/> settings for the tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation. </param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="JsonElement"/> settings for the specified <paramref name="tenantId"/>.</returns>
    ValueTask<ConcurrentState<JsonElement>> GetSettingsAsync(
        string tenantId,
        ConcurrentState<JsonElement> lastKnownState,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the <see cref="PersistedSecret"/> collection for the specified <paramref name="tenantId"/>.
    /// The concurrency token from <paramref name="lastKnownState"/> is used to check if the current value has changed.
    /// If the value hasn't changed, this method returns the same <paramref name="lastKnownState"/> instance;
    /// otherwise, it returns the most recent value from the store.
    /// </summary>
    /// <param name="tenantId">The natural key of the tenant.</param>
    /// <param name="lastKnownState">The last known state of the <see cref="PersistedSecret"/> collection for the tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the collection of  <see cref="PersistedSecret"/> instances for the specified <paramref name="tenantId"/>.</returns>
    ValueTask<ConcurrentState<IReadOnlyCollection<PersistedSecret>>> GetSecretsAsync(
        string tenantId,
        ConcurrentState<IReadOnlyCollection<PersistedSecret>> lastKnownState,
        CancellationToken cancellationToken);
}
