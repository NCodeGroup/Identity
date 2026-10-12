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
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Consent;

/// <summary>
/// Provides an abstraction for the durable end-user consent subsystem: checking whether a subject has already consented
/// to a client for a set of scopes (so the consent screen can be skipped), persisting a consent decision, revoking it,
/// and resolving the pending authorization request an interactive consent surface must present.
/// </summary>
[PublicAPI]
public interface IConsentService
{
    /// <summary>
    /// Determines whether the subject has already consented to the client for every one of the requested scopes.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="clientId">The natural identifier of the client.</param>
    /// <param name="subjectId">The identifier of the subject.</param>
    /// <param name="requestedScopes">The scopes the current authorization request is asking for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when the
    /// subject's recorded consent already covers every requested scope; otherwise <c>false</c>.</returns>
    ValueTask<bool> HasConsentAsync(
        OpenIdContext openIdContext,
        string clientId,
        string subjectId,
        IReadOnlyCollection<string> requestedScopes,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Persists the subject's consent to the client for the specified scopes, widening any existing record in place.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="clientId">The natural identifier of the client.</param>
    /// <param name="subjectId">The identifier of the subject.</param>
    /// <param name="scopes">The scopes the subject consented to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask GrantConsentAsync(
        OpenIdContext openIdContext,
        string clientId,
        string subjectId,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes the subject's consent to the client, if present (used by the self-service "authorized applications"
    /// surface).
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="clientId">The natural identifier of the client.</param>
    /// <param name="subjectId">The identifier of the subject.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RevokeConsentAsync(
        OpenIdContext openIdContext,
        string clientId,
        string subjectId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Resolves (without consuming) the pending authorization request an interactive consent surface must present, from
    /// the local return URL the consent page was redirected to.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="returnUrl">The local return URL carrying the parked continuation (its <c>state</c> grant key).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the pending consent
    /// details when the continuation resolves to an active authorization request; otherwise <c>null</c>.</returns>
    ValueTask<PendingConsent?> ResolvePendingConsentAsync(
        OpenIdContext openIdContext,
        string returnUrl,
        CancellationToken cancellationToken
    );
}
