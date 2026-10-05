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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Records audit events for the authentication subsystem by deriving the common audit envelope from
/// the ambient request context and publishing a strongly-typed audit event.
/// </summary>
/// <remarks>
/// This facade centralizes audit-envelope derivation so publishing sites state only what is specific
/// to their operation. Audit events carry identifiers and metadata only, never secret material.
/// </remarks>
[PublicAPI]
public interface IAuditEventRecorder
{
    /// <summary>
    /// Records that a security token was issued.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> the token was issued to.</param>
    /// <param name="subjectId">The identifier of the subject the token was issued for, if any.</param>
    /// <param name="securityToken">The issued token; only its non-sensitive metadata is recorded.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordTokenIssuedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        SecurityToken securityToken,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that a token (a refresh-token grant) was revoked.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that revoked the token.</param>
    /// <param name="subjectId">The identifier of the subject the revoked token was issued for, if any.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordTokenRevokedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an authorization request was granted for a subject.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> the authorization was granted to.</param>
    /// <param name="subjectId">The identifier of the subject the authorization was granted for, if any.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordAuthorizationGrantedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that an authorization request was denied for a subject.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> the authorization was denied for.</param>
    /// <param name="subjectId">The identifier of the subject the authorization was denied for, if any.</param>
    /// <param name="reason">A short, non-sensitive reason the authorization was denied.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordAuthorizationDeniedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        string? reason,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that authenticating a client at the token endpoint failed.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="clientId">The identifier of the client that failed authentication.</param>
    /// <param name="reason">A short, non-sensitive reason authentication failed.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordClientAuthenticationFailedAsync(
        OpenIdContext openIdContext,
        string clientId,
        string? reason,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that a protocol endpoint returned an <c>OAuth</c> or <c>OpenID Connect</c> error response.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="error">The <see cref="IOpenIdError"/> that was returned; only its non-sensitive metadata is recorded.</param>
    /// <param name="endpointName">The name of the endpoint that produced the error, if known.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordOpenIdErrorAsync(
        OpenIdContext openIdContext,
        IOpenIdError error,
        string? endpointName,
        CancellationToken cancellationToken
    );
}
