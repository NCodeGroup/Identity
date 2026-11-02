// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Sends requests to a provider's token endpoint (RFC 6749 §3.2).
/// </summary>
/// <remarks>
/// An OAuth error response is returned as data in <see cref="TokenResult.Error"/>. Any other failure, such as an
/// unreachable provider, a timeout, an oversized or invalid body, or a success without <c>access_token</c>, throws
/// <see cref="OpenIdProviderException"/>.
/// </remarks>
[PublicAPI]
public interface ITokenClient
{
    /// <summary>
    /// Requests a token for the specified grant.
    /// </summary>
    /// <param name="registration">The client making the request.</param>
    /// <param name="request">The grant to request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    ValueTask<TokenResult> RequestTokenAsync(
        ClientRegistration registration,
        TokenRequest request,
        CancellationToken cancellationToken
    );
}
