// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId.Client;
using Yarp.ReverseProxy.Forwarder;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Wraps the invokers of another <see cref="IForwarderHttpClientFactory"/> so that every request a YARP route forwards
/// passes the BFF's cookie boundary and carries the token that its route resolved.
/// </summary>
/// <param name="inner">The factory that creates the invokers to wrap.</param>
/// <param name="accessor">The accessor of the current request.</param>
/// <param name="connections">The accessor of the current connection.</param>
/// <param name="contexts">Creates the token context of the current request.</param>
/// <param name="tokens">The accessor of the BFF's tokens.</param>
/// <param name="proofs">The factory of DPoP proofs.</param>
internal sealed class BffForwarderHttpClientFactory(
    IForwarderHttpClientFactory inner,
    IHttpContextAccessor accessor,
    IBffConnectionSessionAccessor connections,
    BffTokenContexts contexts,
    IBffTokenAccessor tokens,
    IDPoPProofFactory proofs
) : IForwarderHttpClientFactory
{
    /// <inheritdoc />
    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The inner factory decides whether its own previous invoker can be reused, so it never sees the wrapper.
        var oldInvoker = context.OldClient as BffForwarderInvoker;
        var innerContext = new ForwarderHttpClientContext
        {
            ClusterId = context.ClusterId,
            OldConfig = context.OldConfig,
            OldMetadata = context.OldMetadata,
            OldClient = oldInvoker?.Inner ?? context.OldClient,
            NewConfig = context.NewConfig,
            NewMetadata = context.NewMetadata,
        };

        var client = inner.CreateClient(innerContext);
        if (oldInvoker is not null && ReferenceEquals(client, oldInvoker.Inner))
            return oldInvoker;

        var handler = new BffCookieBoundaryHandler
        {
            // With no token request of its own, the token handler attaches only a token that the route resolved.
            InnerHandler = new BffAccessTokenHandler(
                accessor,
                connections,
                contexts,
                tokens,
                proofs,
                tokenRequest: null
            )
            {
                InnerHandler = new BffForwarderInvokerHandler(client),
            },
        };

        return new BffForwarderInvoker(handler, client);
    }
}
