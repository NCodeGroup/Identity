// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A stub <see cref="HttpMessageHandler"/> that serves a caller-supplied binary body for every request, used to stand
/// in for a certificate's CRL distribution point without a real network connection (which the SSRF guard would
/// refuse).
/// </summary>
internal sealed class StubBinaryHttpMessageHandler(Func<HttpRequestMessage, byte[]> responder)
    : HttpMessageHandler
{
    private Func<HttpRequestMessage, byte[]> Responder { get; } = responder;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var bytes = Responder(request);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pkix-crl");
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        return Task.FromResult(response);
    }
}
