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
using NCode.Identity.OpenId.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Provides an abstraction for persisting any type of grant and their payloads to storage.
/// </summary>
[PublicAPI]
public interface IPersistedGrantService
{
    /// <summary>
    /// Factory method to create a persisted grant identifier with the specified details.
    /// </summary>
    /// <param name="tenantId">The tenant identifier that scopes the grant.</param>
    /// <param name="grantType">The type of the grant.</param>
    /// <param name="grantKey">The identifier of the resource associated with the grant.</param>
    /// <returns>The <see cref="PersistedGrantId"/> that represents the persisted grant identifier.</returns>
    PersistedGrantId CreateGrantId(string tenantId, string grantType, string grantKey);

    /// <summary>
    /// Persists a grant with the specified details to storage.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="grant">Contains the payload of the persisted grant.</param>
    /// <param name="createdWhen">The <see cref="DateTimeOffset"/> when the grant was created.</param>
    /// <param name="lifetime">The optional lifetime of the persisted grant. If <c>null</c>, the grant does not expire.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TPayload">The type of the payload for the persisted grant.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the opaque,
    /// server-generated public identifier of the persisted grant.</returns>
    ValueTask<string> AddAsync<TPayload>(
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
    ValueTask<PersistedGrant<TPayload>?> GetOrDefaultAsync<TPayload>(
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
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the payload from the
    /// persisted grant that this call consumed. The result is <c>null</c> when the grant does not exist, is not active
    /// or was already consumed, and also when a concurrent request consumed the grant first.</returns>
    ValueTask<PersistedGrant<TPayload>?> ConsumeOnceOrDefault<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Revokes a grant unless it is already revoked, whether or not it expired, and reports whether this call revoked
    /// it. A revocation that loses a concurrency race reads the grant again and retries a few times; it fails when
    /// every attempt loses.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="revokedWhen">The <see cref="DateTimeOffset"/> to set the revoked time to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when
    /// this call revoked the grant. The result is <c>false</c> when the grant does not exist or is already revoked,
    /// including by a concurrent request.</returns>
    ValueTask<bool> SetRevokedOnceAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Revokes a session grant, together with every other grant issued under its session identifier, in one unit of
    /// work unless the session grant is already revoked, whether or not it expired, and reports whether this call
    /// revoked it. A revocation that loses a concurrency race reads the grants again and retries a few times; it fails
    /// when every attempt loses.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="grantId">Contains the identifiers of the persisted session grant.</param>
    /// <param name="revokedWhen">The <see cref="DateTimeOffset"/> to set the revoked time to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when
    /// this call revoked the session grant and the grants issued under it. The result is <c>false</c> when the grant
    /// does not exist, belongs to another tenant, or is already revoked, including by a concurrent request.</returns>
    ValueTask<bool> RevokeSessionOnceAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Revokes a grant if it is still active and reports whether this call revoked it.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="revokedWhen">The <see cref="DateTimeOffset"/> to set the revoked time to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> if this
    /// call revoked the grant. The result is <c>false</c> when the grant does not exist or is not active, including
    /// when a concurrent request spent or revoked it first. A concurrent write that leaves the grant active is read
    /// again; the call fails when every attempt loses.</returns>
    ValueTask<bool> TryRevokeOnceAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Revokes the replaced grant if it is still active, marks it consumed, and stores the specified grant in the same
    /// unit of work, so the replacement exists only if this call revoked the grant it replaces.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="replacedGrantId">Contains the identifiers of the persisted grant to revoke.</param>
    /// <param name="grantId">Contains the identifiers of the persisted grant to store.</param>
    /// <param name="grant">Contains the payload of the persisted grant to store.</param>
    /// <param name="createdWhen">The <see cref="DateTimeOffset"/> when the grant to store was created, which is also
    /// when the replaced grant is revoked.</param>
    /// <param name="lifetime">The optional lifetime of the persisted grant to store. If <c>null</c>, the grant does not expire.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TPayload">The type of the payload for the persisted grant.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the opaque,
    /// server-generated public identifier of the stored grant. The result is <c>null</c>, and nothing is stored, when
    /// the replaced grant does not exist or is not active, including when a concurrent request spent or revoked it
    /// first. A concurrent write that leaves the replaced grant active is read again; the call fails when every
    /// attempt loses.</returns>
    /// <exception cref="InvalidOperationException">The specified grant is not active.</exception>
    ValueTask<string?> ReplaceOnceOrDefault<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId replacedGrantId,
        PersistedGrantId grantId,
        PersistedGrant<TPayload> grant,
        DateTimeOffset createdWhen,
        TimeSpan? lifetime,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Stores the grant in the refresh-token rotation family it names if a grant of that family is still active, in
    /// one unit of work that also updates every active grant of the family, so a concurrent revocation of the family
    /// either prevents the store or, when it reads the family again, revokes the stored grant too.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="grantId">Contains the identifiers of the persisted grant to store.</param>
    /// <param name="grant">Contains the payload of the persisted grant to store, and the family it joins.</param>
    /// <param name="createdWhen">The <see cref="DateTimeOffset"/> when the grant to store was created.</param>
    /// <param name="lifetime">The optional lifetime of the persisted grant to store. If <c>null</c>, the grant does not expire.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TPayload">The type of the payload for the persisted grant.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the opaque,
    /// server-generated public identifier of the stored grant. The result is <c>null</c>, and nothing is stored, when
    /// no grant of the family is active. A concurrent write to a grant of the family is read again; the call fails
    /// when every attempt loses.</returns>
    /// <exception cref="InvalidOperationException">The specified grant is not active.</exception>
    /// <exception cref="ArgumentException">The specified grant names no refresh-token rotation family.</exception>
    ValueTask<string?> AddToRefreshTokenLineageOrDefault<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        PersistedGrant<TPayload> grant,
        DateTimeOffset createdWhen,
        TimeSpan? lifetime,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Revokes every active grant belonging to the specified refresh-token rotation family. Used by refresh-token
    /// reuse detection to revoke the whole family when a replayed token is presented, and by the revocation endpoint
    /// to revoke the family of the refresh token it is given.
    /// </summary>
    /// <remarks>
    /// A revocation that loses a concurrency race with another write to a grant of the family, such as a rotation that
    /// commits in between, reads the family again and retries a few times, so a grant added concurrently is revoked too.
    /// It fails when every attempt loses.
    /// </remarks>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="refreshTokenLineageId">The opaque identifier of the refresh-token rotation family to revoke.</param>
    /// <param name="revokedWhen">The <see cref="DateTimeOffset"/> to set the revoked time to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the number of
    /// grants that were revoked.</returns>
    ValueTask<long> RevokeByRefreshTokenLineageAsync(
        OpenIdContext openIdContext,
        string refreshTokenLineageId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Revokes every active grant that the filter selects, such as the grants of an end-user session, a subject, or a
    /// client.
    /// </summary>
    /// <remarks>
    /// A revocation that loses a concurrency race with another write to one of the selected grants, such as a rotation
    /// that commits in between, reads the selected grants again and retries a few times, so a grant added concurrently
    /// is revoked too. It fails when every attempt loses.
    /// </remarks>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="filter">The <see cref="GrantFilter"/> that selects the grants to revoke.</param>
    /// <param name="revokedWhen">The <see cref="DateTimeOffset"/> to set the revoked time to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the number of
    /// grants that were revoked.</returns>
    /// <exception cref="ArgumentException">The filter sets no criterion.</exception>
    ValueTask<long> RevokeWhereAsync(
        OpenIdContext openIdContext,
        GrantFilter filter,
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

    /// <summary>
    /// Changes the payload, and optionally the expiration, of a grant that is still active by applying a function to
    /// the grant as it is stored.
    /// </summary>
    /// <remarks>
    /// The change is applied to the grant as stored and is written under the concurrency token of that read. A write
    /// that loses a concurrency race reads the grant again and applies the function again, a few times; the call fails
    /// when every attempt loses. The function may therefore run more than once, so it decides from its argument only.
    /// </remarks>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="update">The function that receives the grant as stored and returns the change to apply, or
    /// <c>null</c> to leave the grant unchanged.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TPayload">The type of the payload for the persisted grant.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> if this
    /// call changed the grant. The result is <c>false</c> when the grant does not exist or is not active, in which case
    /// the function is not called, and when the function returns <c>null</c>.</returns>
    ValueTask<bool> UpdatePayloadAsync<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        Func<PersistedGrant<TPayload>, PersistedGrantUpdate<TPayload>?> update,
        CancellationToken cancellationToken
    );
}
