// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Ciba;

/// <summary>
/// Composes the token response for an approved CIBA request. Shared by the token endpoint (poll/ping delivery) and the
/// approval transition (push delivery) so all delivery modes issue identical tokens from the pending grant payload.
/// </summary>
internal interface ICibaTokenResponseService
{
    /// <summary>
    /// Creates the <see cref="ITokenResponse"/> for an approved CIBA request, issuing the access token and, when the
    /// requested scopes call for them, the ID token and refresh token.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that initiated the request.</param>
    /// <param name="payload">The approved CIBA grant payload.</param>
    /// <param name="proofOfPossessionBinding">The <see cref="ProofOfPossessionBinding"/> for the request (unbound for push delivery).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The composed <see cref="ITokenResponse"/>.</returns>
    ValueTask<ITokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CibaGrant payload,
        ProofOfPossessionBinding proofOfPossessionBinding,
        CancellationToken cancellationToken
    );
}
