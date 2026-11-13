// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

/// <summary>
/// Answers every request with <c>200</c> and a <see cref="FailingReadStream"/> body that throws
/// <paramref name="exception"/> when it is read.
/// </summary>
/// <param name="exception">The exception that reading the response body throws.</param>
internal sealed class FailingBodyHttpMessageHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var content = new StreamContent(new FailingReadStream(exception));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/oauth-authz-req+jwt");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
