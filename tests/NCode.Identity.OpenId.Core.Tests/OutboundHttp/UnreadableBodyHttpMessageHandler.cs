// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

/// <summary>
/// Answers every request with <c>200</c> and an <see cref="UnreadableStream"/> body, so a test proves that a delivery
/// never reads the response body.
/// </summary>
internal sealed class UnreadableBodyHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new UnreadableStream()),
            }
        );
}
