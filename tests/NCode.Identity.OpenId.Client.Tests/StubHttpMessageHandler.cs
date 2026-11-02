// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Buffers;

namespace NCode.Identity.OpenId.Client.Tests;

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond
) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    public static StubHttpMessageHandler Respond(
        HttpStatusCode statusCode,
        string body,
        string mediaType = "application/json"
    ) =>
        new(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(statusCode)
                    {
                        Content = new StringContent(body, SecureEncoding.UTF8, mediaType),
                    }
                )
        );

    public static StubHttpMessageHandler Sequence(params HttpResponseMessage[] responses)
    {
        var queue = new Queue<HttpResponseMessage>(responses);
        return new((_, _) => Task.FromResult(queue.Dequeue()));
    }

    public static HttpResponseMessage CreateResponse(
        HttpStatusCode statusCode,
        string body,
        string? dpopNonce = null
    )
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, SecureEncoding.UTF8, "application/json"),
        };
        if (dpopNonce is not null)
            response.Headers.Add("DPoP-Nonce", dpopNonce);
        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(
            new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization?.ToString(),
                request.Headers.Accept.ToString(),
                body,
                request.Headers.TryGetValues("DPoP", out var dpop) ? dpop.Single() : null
            )
        );

        return await respond(request, cancellationToken);
    }
}
