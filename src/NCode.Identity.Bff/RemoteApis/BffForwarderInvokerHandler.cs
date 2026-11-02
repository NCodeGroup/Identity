// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Ends a handler chain in an <see cref="HttpMessageInvoker"/> that another factory created, and owns that invoker.
/// </summary>
/// <remarks>
/// It sends asynchronously only, as YARP does: a synchronous send would pass the BFF's handlers unchanged, so it fails.
/// </remarks>
/// <param name="invoker">The invoker that sends each request.</param>
internal sealed class BffForwarderInvokerHandler(HttpMessageInvoker invoker) : HttpMessageHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    ) => invoker.SendAsync(request, cancellationToken);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            invoker.Dispose();

        base.Dispose(disposing);
    }
}
