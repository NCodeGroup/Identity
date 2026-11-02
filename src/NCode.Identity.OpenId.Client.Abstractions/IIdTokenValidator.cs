// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Validates the ID tokens that a provider issues to a client.
/// </summary>
/// <remarks>
/// An invalid token is returned as data, where <see cref="IdTokenValidationResult.FailureCode"/> names why it is
/// invalid and <see cref="IdTokenValidationResult.FailureReason"/> describes it. A failure of the
/// provider, such as an unreachable discovery or JWKS endpoint, throws <see cref="OpenIdProviderException"/>.
/// </remarks>
[PublicAPI]
public interface IIdTokenValidator
{
    /// <summary>
    /// Validates an ID token that was issued to the specified client.
    /// </summary>
    /// <param name="registration">The client the ID token was issued to.</param>
    /// <param name="request">The ID token and the values to check it against.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    ValueTask<IdTokenValidationResult> ValidateAsync(
        ClientRegistration registration,
        IdTokenValidationRequest request,
        CancellationToken cancellationToken
    );
}
