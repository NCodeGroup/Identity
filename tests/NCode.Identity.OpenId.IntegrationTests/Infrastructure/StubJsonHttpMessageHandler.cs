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

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

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
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        return Task.FromResult(response);
    }
}
