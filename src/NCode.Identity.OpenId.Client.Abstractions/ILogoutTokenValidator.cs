// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Validates the logout tokens that a provider posts to a client's back-channel logout endpoint.
/// </summary>
/// <remarks>
/// An invalid token is returned as data, where <see cref="LogoutTokenValidationResult.FailureCode"/> names why it is
/// invalid and <see cref="LogoutTokenValidationResult.FailureReason"/> describes it. A failure of the provider, such as
/// an unreachable discovery or JWKS endpoint, throws <see cref="OpenIdProviderException"/>. <c>ValidateAsync</c> refuses a
/// token whose <c>jti</c> is recorded in the shared <c>IExpiringKeySet</c> but does not record it; the caller records it
/// with <see cref="TryRecordHandledAsync"/> once it has acted on the token.
/// </remarks>
[PublicAPI]
public interface ILogoutTokenValidator
{
    /// <summary>
    /// Validates a logout token that was sent to the specified client.
    /// </summary>
    /// <param name="registration">The client the logout token was sent to.</param>
    /// <param name="request">The logout token to validate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    ValueTask<LogoutTokenValidationResult> ValidateAsync(
        ClientRegistration registration,
        LogoutTokenValidationRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that the client has handled a valid logout token, so the same token no longer validates.
    /// </summary>
    /// <remarks>
    /// Call it only after the client has ended the sessions the token names. A token that is not recorded validates
    /// again, so the provider's retry of a delivery the client failed to handle is accepted.
    /// </remarks>
    /// <param name="registration">The client the logout token was sent to.</param>
    /// <param name="result">The valid result that <see cref="ValidateAsync"/> returned for the token.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    /// <returns>
    /// <see langword="true"/> when the token was recorded; <see langword="false"/> when it was already recorded (for
    /// example by a concurrent delivery of the same token) or its replay window has passed.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="result"/> is not valid.</exception>
    ValueTask<bool> TryRecordHandledAsync(
        ClientRegistration registration,
        LogoutTokenValidationResult result,
        CancellationToken cancellationToken
    );
}
