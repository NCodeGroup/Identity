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
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Models;

namespace NCode.Identity.OpenId.Logic;

/// <summary>
/// Provides an abstraction for persisting any type of grant and their payloads to storage.
/// </summary>
[PublicAPI]
public interface IPersistedGrantService
{
    /// <summary>
    /// Persists a persisted grant with the specified details to storage.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="grant">Contains the payload of the persisted grant.</param>
    /// <param name="createdWhen">Contains the <see cref="DateTimeOffset"/> when the grant was created.</param>
    /// <param name="lifetime">The lifetime of the persisted grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TPayload">The type of the payload for the persisted grant.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddAsync<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        PersistedGrant<TPayload> grant,
        DateTimeOffset createdWhen,
        TimeSpan? lifetime,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to retrieve a persisted grant from storage with the specified identifiers.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TPayload">The type of the payload for the persisted grant.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the payload from the persisted grant.</returns>
    ValueTask<PersistedGrant<TPayload>?> TryGetAsync<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to retrieve and consume a persisted grant from storage with the specified identifiers, if not already consumed.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TPayload">The type of the payload for the persisted grant.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the payload from the persisted grant.</returns>
    ValueTask<PersistedGrant<TPayload>?> TryConsumeOnce<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates a grant as being consumed if not already.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="consumedWhen">The <see cref="DateTimeOffset"/> to set the consumed time to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SetConsumedAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset consumedWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates a grant as being revoked if it is still active.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="revokedWhen">The <see cref="DateTimeOffset"/> to set the revoked time to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SetRevokedAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the expiration of a grant if it is still active.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="expiresWhen">The <see cref="DateTimeOffset"/> to set the expiration time to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateExpirationAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    );
}
