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

using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.OutboundHttp;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Executes a <see cref="BackChannelLogoutDeliveryJob"/> by <c>POST</c>ing the signed <c>logout_token</c> as
/// <c>application/x-www-form-urlencoded</c> to the relying party's <c>backchannel_logout_uri</c>. A delivery failure
/// throws so the <see cref="IJobDispatcher"/> retries the job (up to its attempt limit), giving back-channel logout
/// at-least-once delivery rather than the former best-effort single attempt. The response body is never read: the
/// delivery waits for the status line and headers only, so a hostile endpoint cannot make the server buffer a body.
/// </summary>
internal sealed class BackChannelLogoutDeliveryJobHandler(IHttpClientFactory httpClientFactory)
    : IDispatchJobHandler<BackChannelLogoutDeliveryJob>
{
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        BackChannelLogoutDeliveryJob job,
        DispatchContext context,
        CancellationToken cancellationToken
    )
    {
        KeyValuePair<string, string?>[] formValues =
        [
            new(OpenIdProtocol.Parameters.LogoutToken, job.LogoutToken),
        ];
        using var content = new FormUrlEncodedContent(formValues);

        using var request = new HttpRequestMessage(HttpMethod.Post, job.LogoutUri)
        {
            Content = content,
        };

        var httpClient = HttpClientFactory.CreateClient(GuardedHttpClient.Name);
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        // A non-success status throws, signaling the dispatcher to retry with back-off.
        response.EnsureSuccessStatusCode();
    }
}
