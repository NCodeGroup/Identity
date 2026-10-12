#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedConsent"/> instances: the durable record that
/// a subject has authorized a client for a set of scopes. Consent records are upserted by their natural
/// (tenant, client, subject) key and, unlike the ephemeral grant store, are not revoked by session termination.
/// </summary>
[PublicAPI]
public interface IConsentStore : IStore
{
    /// <summary>
    /// Attempts to get the consent a subject has granted to a client.
    /// </summary>
    /// <param name="clientId">The natural identifier of the client.</param>
    /// <param name="subjectId">The identifier of the subject.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedConsent"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedConsent?> GetOrDefaultAsync(
        string clientId,
        string subjectId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets every consent record a subject has granted (used by the self-service "authorized applications" surface).
    /// </summary>
    /// <param name="subjectId">The identifier of the subject.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the subject's
    /// consent records.</returns>
    ValueTask<IReadOnlyList<PersistedConsent>> GetBySubjectAsync(
        string subjectId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Creates or updates a subject's consent for a client in place (keyed by tenant, client, and subject). When a
    /// record already exists its granted scopes and update timestamp are replaced.
    /// </summary>
    /// <param name="consent">The <see cref="PersistedConsent"/> to persist.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SaveAsync(PersistedConsent consent, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a subject's consent for a client, if present.
    /// </summary>
    /// <param name="clientId">The natural identifier of the client.</param>
    /// <param name="subjectId">The identifier of the subject.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RemoveAsync(string clientId, string subjectId, CancellationToken cancellationToken);
}
