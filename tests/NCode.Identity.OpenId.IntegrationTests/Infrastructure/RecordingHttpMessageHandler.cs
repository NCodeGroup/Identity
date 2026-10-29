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

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

/// <summary>
/// A <see cref="DelegatingHandler"/> that forwards every request and adds a <see cref="RecordedHttpExchange"/> for it
/// to a shared queue, so a test can assert what a client sent over the wire (body, <c>DPoP</c> proof) and what the server answered (status,
/// <c>DPoP-Nonce</c>, body). Register it
/// with <c>AddHttpMessageHandler</c>: the client factory builds a new instance for each handler pipeline, and every
/// instance records into the same queue.
/// </summary>
/// <param name="exchanges">The queue the exchanges are added to, in the order their responses arrive.</param>
internal sealed class RecordingHttpMessageHandler(ConcurrentQueue<RecordedHttpExchange> exchanges)
    : DelegatingHandler
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

        var response = await base.SendAsync(request, cancellationToken);

        var responseBody = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync(cancellationToken);

        var proof = request.Headers.TryGetValues(OpenIdProtocol.DPoP.HeaderName, out var proofs)
            ? proofs.FirstOrDefault()
            : null;

        var nonce = response.Headers.TryGetValues(
            OpenIdProtocol.DPoP.NonceHeaderName,
            out var nonces
        )
            ? nonces.FirstOrDefault()
            : null;

        Exchanges.Enqueue(
            new RecordedHttpExchange(
                request.RequestUri?.AbsolutePath ?? string.Empty,
                request.Headers.Authorization?.Scheme,
                body,
                response.StatusCode,
                DPoPProof: proof,
                DPoPNonce: nonce,
                ResponseBody: responseBody
            )
        );

        return response;
    }
}
