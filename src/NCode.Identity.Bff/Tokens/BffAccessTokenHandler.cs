// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Attaches the access token of the current session, and its DPoP proof when the token is DPoP-bound, to outbound requests.
/// </summary>
/// <param name="accessor">The accessor of the current request.</param>
/// <param name="tokens">The accessor of the BFF's tokens.</param>
/// <param name="proofs">The factory of DPoP proofs.</param>
/// <param name="frontends">The catalog of the frontends, whose registration signs the DPoP proofs of the request's frontend.</param>
/// <param name="tokenRequest">
/// The token request to resolve when a request carries no <see cref="ResolvedTokenKey"/>, or <see langword="null"/> to
/// send such a request unchanged, as a YARP cluster does for a route that is not a BFF remote API.
/// </param>
internal class BffAccessTokenHandler(
    IHttpContextAccessor accessor,
    IBffTokenAccessor tokens,
    IDPoPProofFactory proofs,
    BffFrontendCatalog frontends,
    BffTokenRequest? tokenRequest
) : DelegatingHandler
{
    /// <summary>
    /// The request option that carries a token the caller already resolved, so a forwarder that must answer a token
    /// failure itself, before it starts the upstream request, does not resolve the token twice.
    /// </summary>
    internal static HttpRequestOptionsKey<BffTokenResult> ResolvedTokenKey { get; } =
        new("NCode.Identity.Bff.ResolvedToken");

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        BffTokenResult result;
        if (request.Options.TryGetValue(ResolvedTokenKey, out var resolved))
            result = resolved;
        else if (tokenRequest is not null)
            result = await ResolveTokenAsync(tokenRequest, cancellationToken);
        else
            return await base.SendAsync(request, cancellationToken);

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
        if (!isDPoP)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                OpenIdProtocol.TokenTypes.Bearer,
                token.Value
            );
            return await SendRecordingNonceAsync(request, cancellationToken);
        }

        var jkt =
            token.DPoPJwkThumbprint
            ?? throw new InvalidOperationException(
                "A DPoP access token must carry the thumbprint of its key."
            );
        var requestUri =
            request.RequestUri ?? throw new InvalidOperationException("The request has no URI.");
        var registration = frontends.GetRequired(GetHttpContext()).Registration;

        // The content must be replayable for the one retry of a nonce challenge.
        if (request.Content is { } content)
            await content.LoadIntoBufferAsync(cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue(
            OpenIdProtocol.TokenTypes.DPoP,
            token.Value
        );

        var proof = await proofs.CreateResourceProofAsync(
            registration,
            jkt,
            request.Method,
            requestUri,
            token.Value,
            cancellationToken
        );
        request.Headers.Add(OpenIdProtocol.DPoP.HeaderName, proof);

        var response = await SendRecordingNonceAsync(request, cancellationToken);
        if (!IsNonceChallenge(response))
            return response;

        response.Dispose();

        var retryProof = await proofs.CreateResourceProofAsync(
            registration,
            jkt,
            request.Method,
            requestUri,
            token.Value,
            cancellationToken
        );
        request.Headers.Remove(OpenIdProtocol.DPoP.HeaderName);
        request.Headers.Add(OpenIdProtocol.DPoP.HeaderName, retryProof);

        return await SendRecordingNonceAsync(request, cancellationToken);
    }

    private async ValueTask<BffTokenResult> ResolveTokenAsync(
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        return await tokens.GetAccessTokenAsync(GetHttpContext(), request, cancellationToken);
    }

    private HttpContext GetHttpContext() =>
        accessor.HttpContext
        ?? throw new InvalidOperationException(
            "The BFF access token handler requires a current HttpContext."
        );

    private async Task<HttpResponseMessage> SendRecordingNonceAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
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

    private static bool IsNonceChallenge(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return false;

        if (!response.Headers.Contains(OpenIdProtocol.DPoP.NonceHeaderName))
            return false;

        const string expected = $"error=\"{OpenIdProtocol.ErrorCodes.UseDPoPNonce}\"";
        foreach (var challenge in response.Headers.WwwAuthenticate)
        {
            if (
                string.Equals(
                    challenge.Scheme,
                    OpenIdProtocol.TokenTypes.DPoP,
                    StringComparison.OrdinalIgnoreCase
                )
                && challenge.Parameter is { } parameter
                && parameter.Contains(expected, StringComparison.Ordinal)
            )
            {
                return true;
            }
        }

        return false;
    }
}
