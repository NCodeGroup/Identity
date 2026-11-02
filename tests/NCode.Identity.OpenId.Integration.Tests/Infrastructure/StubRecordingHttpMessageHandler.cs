// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A stub <see cref="HttpMessageHandler"/> that answers <c>200 OK</c> to every request and records it, standing in for
/// a relying party's <c>backchannel_logout_uri</c> without a network connection (which the SSRF guard would refuse).
/// </summary>
/// <param name="exchanges">The queue the exchanges are added to, in the order the requests arrive.</param>
internal sealed class StubRecordingHttpMessageHandler(
    ConcurrentQueue<RecordedHttpExchange> exchanges
) : HttpMessageHandler
{
    private ConcurrentQueue<RecordedHttpExchange> Exchanges { get; } = exchanges;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Exchanges.Enqueue(
            new RecordedHttpExchange(
                request.RequestUri?.AbsolutePath ?? string.Empty,
                request.Headers.Authorization?.Scheme,
                body,
                HttpStatusCode.OK
            )
        );

        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
