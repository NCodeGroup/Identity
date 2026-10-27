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
                body
            )
        );

        return await respond(request, cancellationToken);
    }
}
