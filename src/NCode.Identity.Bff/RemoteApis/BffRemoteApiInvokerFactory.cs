// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId.Client;
using Yarp.ReverseProxy.Forwarder;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Creates the invokers that send forwarded requests to remote APIs. Every invoker shares one connection pool that
/// never follows a redirect, never sends or stores a cookie, never uses a proxy and never decompresses a response.
/// </summary>
internal sealed class BffRemoteApiInvokerFactory(
    IHttpContextAccessor accessor,
    IBffTokenAccessor tokens,
    IDPoPProofFactory proofs,
    BffFrontendCatalog frontends
) : IDisposable
{
    private SocketsHttpHandler Handler { get; } =
        new()
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            EnableMultipleHttp2Connections = true,
            // Replaces the trace headers that the browser sent with the BFF's own, rather than adding a second set.
            ActivityHeadersPropagator = new ReverseProxyPropagator(
                DistributedContextPropagator.Current
            ),
        };

    /// <summary>
    /// Creates an invoker that attaches the token of the specified request to each request it sends.
    /// </summary>
    /// <param name="tokenRequest">The token request of the remote API.</param>
    /// <returns>An invoker that shares the factory's connection pool and does not dispose it.</returns>
    public HttpMessageInvoker CreateInvoker(BffTokenRequest tokenRequest)
    {
        ArgumentNullException.ThrowIfNull(tokenRequest);

        var handler = new BffAccessTokenHandler(accessor, tokens, proofs, frontends, tokenRequest)
        {
            InnerHandler = Handler,
        };

        return new HttpMessageInvoker(handler, disposeHandler: false);
    }

    /// <inheritdoc />
    public void Dispose() => Handler.Dispose();
}
