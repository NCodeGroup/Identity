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
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Provides an abstraction for managing persisted end-user sessions, including creating a session at sign-in, resolving
/// a session by its <c>sid</c>, and tracking the relying parties a session has issued tokens to. A session is revoked
/// at logout by the session-scoped grant revocation (<see cref="NCode.Identity.OpenId.Persistence.Stores"/>), which
/// revokes the session grant alongside the grants issued under it.
/// </summary>
[PublicAPI]
public interface ISessionService
{
    /// <summary>
    /// Creates and persists a new end-user session and returns its opaque session identifier (<c>sid</c>).
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="request">The details used to create the session.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the opaque session
    /// identifier (<c>sid</c>).</returns>
    ValueTask<string> CreateAsync(
        OpenIdContext openIdContext,
        CreateSessionRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to retrieve a persisted session by its opaque session identifier (<c>sid</c>).
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="sessionId">The opaque session identifier (<c>sid</c>).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the persisted
    /// session if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedGrant<SessionGrant>?> GetOrDefaultAsync(
        OpenIdContext openIdContext,
        string sessionId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that the specified client (relying party) has participated in the session, so it is notified on
    /// back-channel logout. This is idempotent.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="sessionId">The opaque session identifier (<c>sid</c>).</param>
    /// <param name="clientId">The identifier of the participating client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> if an
    /// active session was found and updated; otherwise <c>false</c>.</returns>
    ValueTask<bool> AddClientAsync(
        OpenIdContext openIdContext,
        string sessionId,
        string clientId,
        CancellationToken cancellationToken
    );
}
