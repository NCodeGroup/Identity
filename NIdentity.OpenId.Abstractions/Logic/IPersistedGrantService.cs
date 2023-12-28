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

namespace NIdentity.OpenId.Logic;

/// <summary>
/// Contains the identifiers of a persisted grant.
/// </summary>
public readonly struct PersistedGrantId
{
    /// <summary>
    /// Gets or sets the identifier of the tenant that is associated with the grant.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the type of the grant.
    /// </summary>
    public required string GrantType { get; init; }

    /// <summary>
    /// Gets or sets the unique identifier of the grant.
    /// </summary>
    public required string GrantKey { get; init; }
}

/// <summary>
/// Contains the payload of a persisted grant.
/// </summary>
/// <typeparam name="TPayload">The type of the payload for the grant.</typeparam>
public readonly struct PersistedGrant<TPayload>
{
    /// <summary>
    /// Gets or sets the identifier of the client that is associated with the grant.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets or sets the identifier of the subject that is associated with the grant.
    /// </summary>
    public string? SubjectId { get; init; }

    /// <summary>
    /// Gets or sets the payload of the grant.
    /// </summary>
    public required TPayload Payload { get; init; }
}

/// <summary>
/// Provides an abstraction for persisting any type of grant and their payloads to storage.
/// </summary>
public interface IPersistedGrantService
{
    /// <summary>
    /// Persists a grant with the specified details to storage.
    /// </summary>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="grant">Contains the payload of the persisted grant.</param>
    /// <param name="lifetime">The lifetime of the grant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TPayload">The type of the payload for the grant.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddAsync<TPayload>(
        PersistedGrantId grantId,
        PersistedGrant<TPayload> grant,
        TimeSpan lifetime,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to retrieve a grant from storage with the specified identifiers.
    /// </summary>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="singleUse">Indicates whether the grant should be checked whether it has already been consumed.</param>
    /// <param name="setConsumed">Indicates whether the grant should be marked as consumed if it has not already been consumed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TPayload">The type of the payload for the grant.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the payload from the grant.</returns>
    ValueTask<PersistedGrant<TPayload>?> TryGetAsync<TPayload>(
        PersistedGrantId grantId,
        bool singleUse,
        bool setConsumed,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates a grant as being consumed if not already.
    /// </summary>
    /// <param name="grantId">Contains the identifiers of the persisted grant.</param>
    /// <param name="consumedWhen">The <see cref="DateTimeOffset"/> to set the consumed time to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SetConsumedAsync(
        PersistedGrantId grantId,
        DateTimeOffset consumedWhen,
        CancellationToken cancellationToken);
}
