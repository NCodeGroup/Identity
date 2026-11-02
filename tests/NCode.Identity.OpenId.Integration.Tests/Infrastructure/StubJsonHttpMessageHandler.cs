// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Buffers;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A stub <see cref="HttpMessageHandler"/> that serves a caller-supplied JSON body for every request, used to stand in
/// for a client's <c>jwks_uri</c> endpoint without a real network connection (which the SSRF guard would refuse).
/// </summary>
internal sealed class StubJsonHttpMessageHandler(Func<HttpRequestMessage, string> responder)
    : HttpMessageHandler
{
    private Func<HttpRequestMessage, string> Responder { get; } = responder;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var json = Responder(request);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, SecureEncoding.UTF8, "application/json"),
        };
        return Task.FromResult(response);
    }
}
