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
/// Provides an abstraction for persisting any type of grant and their payloads to storage.
/// </summary>
public interface IPersistedGrantService
{
    /// <summary>
    /// Persists a grant with the specified details to storage.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant that is associated with the grant.</param>
    /// <param name="grantType">The type of the grant.</param>
    /// <param name="grantKey">The unique identifier of the grant.</param>
    /// <param name="clientId">The identifier of the client that is associated with the grant.</param>
    /// <param name="subjectId">The identifier of the subject that is associated with the grant.</param>
    /// <param name="lifetime">The lifetime of the grant.</param>
    /// <param name="payload">The payload of the grant that is to be persisted.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TPayload">The type of the payload for the grant.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddAsync<TPayload>(
        string tenantId,
        string grantType,
        string grantKey,
        string? clientId,
        string? subjectId,
        TimeSpan lifetime,
        TPayload payload,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to retrieve a grant with the specified identifiers from storage.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant that is associated with the grant.</param>
    /// <param name="grantType">The type of the grant.</param>
    /// <param name="grantKey">The unique identifier of the grant.</param>
    /// <param name="singleUse">Indicates whether the grant should be checked whether it has already been consumed.</param>
    /// <param name="setConsumed">Indicates whether the grant should be marked as consumed if it has not already been consumed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TPayload">The type of the payload for the grant.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the payload from the grant.</returns>
    ValueTask<TPayload?> TryGetAsync<TPayload>(
        string tenantId,
        string grantType,
        string grantKey,
        bool singleUse,
        bool setConsumed,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates a grant as being consumed if not already.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant that is associated with the grant.</param>
    /// <param name="grantType">The type of the grant.</param>
    /// <param name="grantKey">The unique identifier of the grant.</param>
    /// <param name="consumedWhen">The <see cref="DateTimeOffset"/> to set the consumed time to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SetConsumedAsync(
        string tenantId,
        string grantType,
        string grantKey,
        DateTimeOffset consumedWhen,
        CancellationToken cancellationToken);
}
