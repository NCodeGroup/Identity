// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId;
using Yarp.ReverseProxy.Forwarder;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Shapes a request that the BFF forwards to a remote API: it targets the destination with the rest of the local
/// path, strips the browser's credentials and forwarding headers, and hands the resolved token to the token handler;
/// it also strips <c>Set-Cookie</c> from the upstream response.
/// </summary>
/// <param name="remainder">The part of the local path after the remote API's prefix.</param>
/// <param name="token">The token that the forwarded request carries.</param>
/// <param name="antiforgeryHeaderName">The name of the BFF's anti-forgery header.</param>
internal sealed class BffRemoteApiTransformer(
    PathString remainder,
    BffTokenResult token,
    string antiforgeryHeaderName
) : HttpTransformer
{
    // A browser's credentials never reach a remote API, and a forwarding header from the browser is not trusted.
    private static string[] StrippedRequestHeaders { get; } =
    [
        HeaderNames.Cookie,
        HeaderNames.Authorization,
        OpenIdProtocol.DPoP.HeaderName,
        "Forwarded",
        "X-Forwarded-For",
        "X-Forwarded-Host",
        "X-Forwarded-Proto",
        "X-Forwarded-Prefix",
    ];

    /// <inheritdoc />
    public override async ValueTask TransformRequestAsync(
        HttpContext httpContext,
        HttpRequestMessage proxyRequest,
        string destinationPrefix,
        CancellationToken cancellationToken
    )
    {
        await base.TransformRequestAsync(
            httpContext,
            proxyRequest,
            destinationPrefix,
            cancellationToken
        );

        proxyRequest.RequestUri = RequestUtilities.MakeDestinationAddress(
            destinationPrefix,
            remainder,
            httpContext.Request.QueryString
        );

        // The remote API is addressed by its own host name, never the BFF's.
        proxyRequest.Headers.Host = null;

        foreach (var name in StrippedRequestHeaders)
            proxyRequest.Headers.Remove(name);

        proxyRequest.Headers.Remove(antiforgeryHeaderName);
        proxyRequest.Options.Set(BffAccessTokenHandler.ResolvedTokenKey, token);
    }

    /// <inheritdoc />
    public override ValueTask<bool> TransformResponseAsync(
        HttpContext httpContext,
        HttpResponseMessage? proxyResponse,
        CancellationToken cancellationToken
    )
    {
        // Removed before the base copies the headers, so a cookie that the BFF itself sets on this response survives.
        proxyResponse?.Headers.Remove(HeaderNames.SetCookie);

        return base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);
    }
}
