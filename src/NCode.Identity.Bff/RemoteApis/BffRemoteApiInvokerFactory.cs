// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Creates the invokers that send forwarded requests to remote APIs. Every invoker attaches its remote API's token and
/// sends through the <see cref="BffDefaults.RemoteApiHttpClientName"/> client, whose primary handler the BFF registers
/// to follow no redirect, send or store no cookie, use no proxy and decompress no response.
/// </summary>
// See ADR-1381 and ADR-1487.
internal sealed class BffRemoteApiInvokerFactory(
    IHttpContextAccessor accessor,
    IBffConnectionSessionAccessor connections,
    BffTokenContexts contexts,
    IBffTokenAccessor tokens,
    IDPoPProofFactory proofs,
    IHttpMessageHandlerFactory handlerFactory
) : IDisposable
{
    private BffRemoteApiTransportHandler Transport { get; } = new(handlerFactory);

    /// <summary>
    /// Creates an invoker that attaches the token of the specified request to each request it sends.
    /// </summary>
    /// <param name="tokenRequest">The token request of the remote API.</param>
    /// <returns>An invoker that shares the factory's connection pool and does not dispose it.</returns>
    public HttpMessageInvoker CreateInvoker(BffTokenRequest tokenRequest)
    {
        ArgumentNullException.ThrowIfNull(tokenRequest);

        var handler = new BffAccessTokenHandler(
            accessor,
            connections,
            contexts,
            tokens,
            proofs,
            tokenRequest
        )
        {
            InnerHandler = Transport,
        };

        return new HttpMessageInvoker(handler, disposeHandler: false);
    }

    /// <inheritdoc />
    public void Dispose() => Transport.Dispose();
}
