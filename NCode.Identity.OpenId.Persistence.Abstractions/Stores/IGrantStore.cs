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
/// Provides an abstraction for persisting any type of grant and their payloads to storage.
/// </summary>
[PublicAPI]
public interface IGrantStore : IStore<PersistedGrant>
{
    /// <summary>
    /// Attempts to retrieve a persisted grant from the store.
    /// </summary>
    /// <param name="tenantId">The tenant identifier for the grant.</param>
    /// <param name="grantType">The type of grant to retrieve.</param>
    /// <param name="hashedKey">The hashed key of the grant to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="PersistedGrant"/> instance if found.</returns>
    ValueTask<PersistedGrant?> TryGetAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing persisted grant as being consumed.
    /// </summary>
    /// <param name="tenantId">The tenant identifier for the grant.</param>
    /// <param name="grantType">The type of grant to update.</param>
    /// <param name="hashedKey">The hashed key of the grant to update.</param>
    /// <param name="consumedWhen">The datetime to update the grant as consumed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SetConsumedOnceAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        DateTimeOffset consumedWhen,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing persisted grant as being revoked.
    /// </summary>
    /// <param name="tenantId">The tenant identifier for the grant.</param>
    /// <param name="grantType">The type of grant to update.</param>
    /// <param name="hashedKey">The hashed key of the grant to update.</param>
    /// <param name="revokedWhen">The datetime to update the grant as revoked.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SetRevokedOnceAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing persisted grant as being expired.
    /// </summary>
    /// <param name="tenantId">The tenant identifier for the grant.</param>
    /// <param name="grantType">The type of grant to update.</param>
    /// <param name="hashedKey">The hashed key of the grant to update.</param>
    /// <param name="expiresWhen">The datetime to update the grant as expired.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateExpirationAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken);
}
