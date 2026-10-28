#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Collections.Concurrent;
using System.Net;

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

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
