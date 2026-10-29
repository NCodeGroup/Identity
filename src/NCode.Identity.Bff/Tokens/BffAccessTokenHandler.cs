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

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Client;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Attaches the access token of the current session, and its DPoP proof when the token is DPoP-bound, to outbound requests.
/// </summary>
internal class BffAccessTokenHandler(
    IHttpContextAccessor accessor,
    IBffTokenAccessor tokens,
    IDPoPProofFactory proofs,
    BffClientRegistrationProvider registrations,
    BffTokenRequest tokenRequest
) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var httpContext =
            accessor.HttpContext
            ?? throw new InvalidOperationException(
                "The BFF access token handler requires a current HttpContext."
            );

        var result = await tokens.GetAccessTokenAsync(httpContext, tokenRequest, cancellationToken);
        if (!result.IsSuccess)
            throw new BffTokenUnavailableException(result.StatusCode, result.Reason);

        if (result.Token is not { } token)
            return await base.SendAsync(request, cancellationToken);

        request.Headers.Authorization = null;
        request.Headers.Remove(OpenIdProtocol.DPoP.HeaderName);

        var isDPoP = string.Equals(
            token.TokenType,
            OpenIdProtocol.TokenTypes.DPoP,
            StringComparison.OrdinalIgnoreCase
        );
        if (isDPoP)
        {
            var jkt =
                token.DPoPJwkThumbprint
                ?? throw new InvalidOperationException(
                    "A DPoP access token must carry the thumbprint of its key."
                );
            var requestUri =
                request.RequestUri
                ?? throw new InvalidOperationException("The request has no URI.");
            var registration = registrations.Registration;
            var proof = await proofs.CreateResourceProofAsync(
                registration,
                jkt,
                request.Method,
                requestUri,
                token.Value,
                cancellationToken
            );
            request.Headers.Authorization = new AuthenticationHeaderValue(
                OpenIdProtocol.TokenTypes.DPoP,
                token.Value
            );
            request.Headers.Add(OpenIdProtocol.DPoP.HeaderName, proof);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                OpenIdProtocol.TokenTypes.Bearer,
                token.Value
            );
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (
            request.RequestUri is { } responseUri
            && response.Headers.TryGetValues(OpenIdProtocol.DPoP.NonceHeaderName, out var nonces)
            && nonces.FirstOrDefault() is { Length: > 0 } nonce
        )
        {
            proofs.RecordNonce(responseUri, nonce);
        }

        return response;
    }
}
