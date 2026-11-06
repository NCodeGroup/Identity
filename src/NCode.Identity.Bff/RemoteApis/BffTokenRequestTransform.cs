// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Net.Http.Headers;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Responses;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId;
using Yarp.ReverseProxy.Transforms;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Shapes a request that a YARP route forwards to a BFF remote API: it strips the browser's credentials, obtains the
/// route's token before anything is forwarded, answers a failure itself, and hands the token to the invoker's token
/// handler.
/// </summary>
/// <param name="tokenRequest">The token request that the route's metadata names.</param>
/// <param name="tokens">The accessor that resolves the token.</param>
/// <param name="frontends">The frontends that the BFF serves; the current set names the anti-forgery header.</param>
internal sealed class BffTokenRequestTransform(
    BffTokenRequest tokenRequest,
    IBffTokenAccessor tokens,
    BffFrontendCatalog frontends
) : RequestTransform
{
    // A browser's credentials never reach a remote API.
    private static string[] StrippedHeaders { get; } =
    [HeaderNames.Cookie, HeaderNames.Authorization, OpenIdProtocol.DPoP.HeaderName];

    /// <summary>
    /// Gets the token request that the route's metadata names.
    /// </summary>
    public BffTokenRequest TokenRequest { get; } = tokenRequest;

    /// <inheritdoc />
    public override async ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var name in StrippedHeaders)
            RemoveHeader(context, name);

        RemoveHeader(context, frontends.Current.Options.Antiforgery.HeaderName);

        var httpContext = context.HttpContext;
        var token = await tokens.GetAccessTokenAsync(
            httpContext,
            TokenRequest,
            context.CancellationToken
        );
        if (!token.IsSuccess)
        {
            // YARP forwards nothing once a request transform has set the response.
            await BffResponses.WriteTokenUnavailableAsync(httpContext, token.StatusCode);
            return;
        }

        context.ProxyRequest.Options.Set(BffAccessTokenHandler.ResolvedTokenKey, token);
    }
}
