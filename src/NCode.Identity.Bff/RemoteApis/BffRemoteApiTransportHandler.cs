// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Sends each request through the handler that <see cref="IHttpMessageHandlerFactory"/> holds for the
/// <see cref="BffDefaults.RemoteApiHttpClientName"/> client, so the handlers a host configures for that client and the
/// factory's handler rotation apply to every forwarded request.
/// </summary>
internal sealed class BffRemoteApiTransportHandler(IHttpMessageHandlerFactory handlerFactory)
    : HttpMessageHandler
{
    private IHttpMessageHandlerFactory HandlerFactory { get; } = handlerFactory;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var handler = HandlerFactory.CreateHandler(BffDefaults.RemoteApiHttpClientName);

        // The factory owns and rotates the handler it returns, so the invoker never disposes it.
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);

        return await invoker.SendAsync(request, cancellationToken);
    }
}
