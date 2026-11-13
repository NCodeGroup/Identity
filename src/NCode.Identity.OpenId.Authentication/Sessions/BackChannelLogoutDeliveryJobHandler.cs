// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.OutboundHttp;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Executes a <see cref="BackChannelLogoutDeliveryJob"/> by <c>POST</c>ing the signed <c>logout_token</c> as
/// <c>application/x-www-form-urlencoded</c> to the relying party's <c>backchannel_logout_uri</c>. A delivery failure
/// throws so the <see cref="IJobDispatcher"/> retries the job (up to its attempt limit), giving back-channel logout
/// at-least-once delivery. The response body is never read: the delivery waits for the status line and headers only, so
/// a hostile endpoint cannot make the server buffer a body. A failed delivery logs its cause before it throws.
/// </summary>
internal sealed class BackChannelLogoutDeliveryJobHandler(
    IHttpClientFactory httpClientFactory,
    ILogger<BackChannelLogoutDeliveryJobHandler> logger
) : IDispatchJobHandler<BackChannelLogoutDeliveryJob>
{
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    private ILogger Logger { get; } = logger;

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

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            // A non-success status throws, signaling the dispatcher to retry with back-off.
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception)
            when (OutboundFetchFailureReasons.IsFetchFailure(exception, cancellationToken))
        {
            // The delivery has no timeout source of its own: the guarded client's resilience timeout
            // surfaces as a TimeoutException in the chain, which Classify names.
            var uriForLog = OutboundUriRedaction.ForLog(job.LogoutUri);
            var reason = OutboundFetchFailureReasons.Classify(exception, timedOut: false);
            Logger.BackChannelLogoutDeliveryFailed(job.ClientId, uriForLog, reason, exception);

            throw;
        }
    }
}
