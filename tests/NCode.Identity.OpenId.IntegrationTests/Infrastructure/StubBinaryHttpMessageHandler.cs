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
using System.Net.Http.Headers;

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

/// <summary>
/// A stub <see cref="HttpMessageHandler"/> that serves a caller-supplied binary body for every request, used to stand
/// in for a certificate's CRL distribution point without a real network connection (which the SSRF guard would
/// refuse).
/// </summary>
internal sealed class StubBinaryHttpMessageHandler(Func<HttpRequestMessage, byte[]> responder)
    : HttpMessageHandler
{
    private Func<HttpRequestMessage, byte[]> Responder { get; } = responder;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var bytes = Responder(request);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pkix-crl");
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        return Task.FromResult(response);
    }
}
