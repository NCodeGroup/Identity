// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace NCode.Identity.Bff.Blazor.Client.Tests;

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond
) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHttpMessageHandler Respond(HttpStatusCode statusCode) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        Requests.Add(request);

        return await respond(request, cancellationToken);
    }
}
