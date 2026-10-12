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
/// Provides an abstraction for the durable end-user consent subsystem: resolving whether a subject has already decided
/// on a client's requested scopes (and which of them were granted, for OIDC down-scoping), persisting a granular
/// consent decision, revoking it, enumerating the subject's authorized applications, and resolving the pending
/// authorization request an interactive consent surface must present.
/// </summary>
[PublicAPI]
public interface IConsentService
{
    /// <summary>
    /// Resolves the subject's recorded consent against the requested scopes: whether every requested scope has already
    /// been decided (so the consent surface can be skipped) and the granted subset that is actually issued.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="clientId">The natural identifier of the client.</param>
    /// <param name="subjectId">The identifier of the subject.</param>
    /// <param name="requestedScopes">The scopes the current authorization request is asking for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="ConsentResolution"/> describing whether consent is satisfied and the granted subset.</returns>
    ValueTask<ConsentResolution> ResolveConsentAsync(
        OpenIdContext openIdContext,
        string clientId,
        string subjectId,
        IReadOnlyCollection<string> requestedScopes,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Persists the subject's granular consent decision for the client: <paramref name="requestedScopes"/> records the
    /// universe the subject decided on (grown monotonically) and <paramref name="grantedScopes"/> (a subset) the scopes
    /// the subject allowed, replacing any earlier decision for those same requested scopes in place.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="clientId">The natural identifier of the client.</param>
    /// <param name="subjectId">The identifier of the subject.</param>
    /// <param name="requestedScopes">The scopes the subject was presented with and decided on.</param>
    /// <param name="grantedScopes">The subset of <paramref name="requestedScopes"/> the subject allowed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask GrantConsentAsync(
        OpenIdContext openIdContext,
        string clientId,
        string subjectId,
        IReadOnlyCollection<string> requestedScopes,
        IReadOnlyCollection<string> grantedScopes,
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
    /// Enumerates the applications the subject has granted standing consent to, each with its friendly display name and
    /// the granted scopes (with friendly descriptions when available), for the self-service "authorized applications"
    /// surface.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="subjectId">The identifier of the subject.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the subject's
    /// authorized applications.</returns>
    ValueTask<IReadOnlyList<AuthorizedApplication>> GetAuthorizedApplicationsAsync(
        OpenIdContext openIdContext,
        string subjectId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Resolves (without consuming) the pending authorization request an interactive consent surface must present, from
    /// the local return URL the consent page was redirected to, enriched with the client's friendly display name and
    /// the requested scopes' friendly descriptions and already-granted state.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="subjectId">The identifier of the signed-in subject (so already-granted scopes can be pre-selected).</param>
    /// <param name="returnUrl">The local return URL carrying the parked continuation (its <c>state</c> grant key).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the pending consent
    /// details when the continuation resolves to an active authorization request; otherwise <see langword="null"/>.</returns>
    ValueTask<PendingConsent?> ResolvePendingConsentAsync(
        OpenIdContext openIdContext,
        string subjectId,
        string returnUrl,
        CancellationToken cancellationToken
    );
}
