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

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
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
    /// requested scopes call for them, the id token and refresh token.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that initiated the request.</param>
    /// <param name="payload">The approved CIBA grant payload.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The composed <see cref="ITokenResponse"/>.</returns>
    ValueTask<ITokenResponse> CreateTokenResponseAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        CibaGrant payload,
        CancellationToken cancellationToken
    );
}
