// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.OutboundHttp;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Executes a <see cref="BackChannelLogoutDeliveryJob"/> by <c>POST</c>ing the signed <c>logout_token</c> as
/// <c>application/x-www-form-urlencoded</c> to the relying party's <c>backchannel_logout_uri</c>. A delivery failure
/// throws so the <see cref="IJobDispatcher"/> retries the job (up to its attempt limit), giving back-channel logout
/// at-least-once delivery. The response body is never read: the
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
