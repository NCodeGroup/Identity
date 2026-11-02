// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Sends requests to a provider's revocation endpoint (RFC 7009).
/// </summary>
/// <remarks>
/// An OAuth error response is returned as data. Any other failure throws <see cref="OpenIdProviderException"/>, and a
/// provider without a <c>revocation_endpoint</c> throws <see cref="InvalidOperationException"/>.
/// </remarks>
[PublicAPI]
public interface ITokenRevocationClient
{
    /// <summary>
    /// Revokes a token.
    /// </summary>
    /// <param name="registration">The client making the request.</param>
    /// <param name="request">The token to revoke.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    /// <returns><c>null</c> on success, or the error the provider returned.</returns>
    ValueTask<ProtocolError?> RevokeTokenAsync(
        ClientRegistration registration,
        TokenRevocationRequest request,
        CancellationToken cancellationToken
    );
}
