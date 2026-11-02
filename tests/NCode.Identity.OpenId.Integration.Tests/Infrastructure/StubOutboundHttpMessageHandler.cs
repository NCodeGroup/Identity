// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A stub <see cref="HttpMessageHandler"/> that stands in for the one SSRF-guarded outbound client without a real
/// network connection (which the guard would refuse). It forwards a <c>POST</c> to one inner handler and every other
/// request to another, and fails a request whose inner handler is not set.
/// </summary>
/// <param name="getHandler">The handler for every request that is not a <c>POST</c>, or <see langword="null"/>.</param>
/// <param name="postHandler">The handler for a <c>POST</c> request, or <see langword="null"/>.</param>
internal sealed class StubOutboundHttpMessageHandler(
    HttpMessageHandler? getHandler,
    HttpMessageHandler? postHandler
) : HttpMessageHandler
{
    private HttpMessageHandler? GetHandler { get; } = getHandler;

    private HttpMessageHandler? PostHandler { get; } = postHandler;

    // SendAsync is protected on the inner handlers, so each is invoked through an invoker that does not own it.
    private HttpMessageInvoker? GetInvoker { get; } =
        getHandler is null ? null : new HttpMessageInvoker(getHandler, disposeHandler: false);

    private HttpMessageInvoker? PostInvoker { get; } =
        postHandler is null ? null : new HttpMessageInvoker(postHandler, disposeHandler: false);

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var invoker = request.Method == HttpMethod.Post ? PostInvoker : GetInvoker;
        if (invoker is null)
        {
            throw new InvalidOperationException(
                $"The integration host has no stub for {request.Method} {request.RequestUri}."
            );
        }

        return invoker.SendAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GetInvoker?.Dispose();
            PostInvoker?.Dispose();
            GetHandler?.Dispose();
            PostHandler?.Dispose();
        }

        base.Dispose(disposing);
    }
}
