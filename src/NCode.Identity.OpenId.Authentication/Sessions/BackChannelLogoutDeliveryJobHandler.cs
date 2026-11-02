// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Dispatch;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Executes a <see cref="BackChannelLogoutDeliveryJob"/> by <c>POST</c>ing the signed <c>logout_token</c> as
/// <c>application/x-www-form-urlencoded</c> to the relying party's <c>backchannel_logout_uri</c>. A delivery failure
/// throws so the <see cref="IJobDispatcher"/> retries the job (up to its attempt limit), giving back-channel logout
/// at-least-once delivery rather than the former best-effort single attempt.
/// </summary>
internal sealed class BackChannelLogoutDeliveryJobHandler(IHttpClientFactory httpClientFactory)
    : IDispatchJobHandler<BackChannelLogoutDeliveryJob>
{
    /// <summary>
    /// The name of the SSRF-guarded HTTP client used to POST the <c>logout_token</c> to the relying party's
    /// client-registered <c>backchannel_logout_uri</c>.
    /// </summary>
    internal const string HttpClientName = "NCode.Identity.OpenId.BackChannelLogout";

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

        var httpClient = HttpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.PostAsync(job.LogoutUri, content, cancellationToken);

        // A non-success status throws, signaling the dispatcher to retry with back-off.
        response.EnsureSuccessStatusCode();
    }
}
