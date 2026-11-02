// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Grants;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration;

/// <summary>
/// Provides the ability to decide whether a presented initial access token authorizes a caller to register a new
/// client at the dynamic client registration endpoint (RFC 7591). This is the deployment-specific seam for protected
/// registration: the default implementation treats the token as a persisted initial-access-token grant, and a host
/// may replace it to validate the token any way it chooses (for example an access token carrying a required scope).
/// </summary>
[PublicAPI]
public interface IInitialAccessTokenValidator
{
    /// <summary>
    /// Determines whether the presented initial access token authorizes registering a new client for the current
    /// request's tenant. A token that has already been spent does not authorize.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="initialAccessToken">The bearer initial access token presented by the caller.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="InitialAccessTokenGrant"/> the token carries when it authorizes registration; otherwise,
    /// <c>null</c>. A grant naming a resource server binds the registered client to it (ADR-1192).</returns>
    ValueTask<InitialAccessTokenGrant?> ValidateAsync(
        OpenIdContext openIdContext,
        string initialAccessToken,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Spends the presented initial access token once the registration it authorized has passed every check, so the
    /// token authorizes no other registration.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="initialAccessToken">The bearer initial access token presented by the caller.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when
    /// the token was spent; otherwise, <c>false</c> when the token no longer authorizes registration (already spent,
    /// expired or revoked), and the registration is then refused.</returns>
    ValueTask<bool> TryConsumeAsync(
        OpenIdContext openIdContext,
        string initialAccessToken,
        CancellationToken cancellationToken
    );
}
