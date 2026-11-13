// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.OutboundHttp;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Delivery;

/// <summary>
/// Executes a <see cref="CibaDeliveryJob"/> by <c>POST</c>ing the CIBA client notification (the completion ping, the
/// issued tokens, or the error payload of a denied push request per CIBA Core §12) to the client's
/// <c>client_notification_endpoint</c>, authenticated with the client notification token. A delivery failure throws so
/// the <see cref="IJobDispatcher"/> retries the job (up to its attempt limit), giving CIBA push and ping delivery
/// at-least-once semantics. The response body is never read: the delivery waits for the status line and headers only,
/// so a hostile endpoint cannot make the server buffer a body. A failed delivery logs its cause before it throws.
/// </summary>
internal sealed class CibaDeliveryJobHandler(
    IHttpClientFactory httpClientFactory,
    ILogger<CibaDeliveryJobHandler> logger
) : IDispatchJobHandler<CibaDeliveryJob>
{
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    private ILogger Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        CibaDeliveryJob job,
        DispatchContext context,
        CancellationToken cancellationToken
    )
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [OpenIdProtocol.Parameters.AuthReqId] = job.AuthReqId,
        };

        if (!string.IsNullOrEmpty(job.Error))
            body[OpenIdProtocol.Parameters.ErrorCode] = job.Error;

        if (!string.IsNullOrEmpty(job.AccessToken))
            body[OpenIdProtocol.Parameters.AccessToken] = job.AccessToken;

        if (!string.IsNullOrEmpty(job.TokenType))
            body[OpenIdProtocol.Parameters.TokenType] = job.TokenType;

        if (job.ExpiresInSeconds is { } expiresIn)
            body[OpenIdProtocol.Parameters.ExpiresIn] = expiresIn;

        if (!string.IsNullOrEmpty(job.RefreshToken))
            body[OpenIdProtocol.Parameters.RefreshToken] = job.RefreshToken;

        if (job.RefreshTokenExpiresInSeconds is { } refreshExpiresIn)
            body[OpenIdProtocol.Parameters.RefreshTokenExpiresIn] = refreshExpiresIn;

        if (!string.IsNullOrEmpty(job.IdToken))
            body[OpenIdProtocol.Parameters.IdToken] = job.IdToken;

        if (!string.IsNullOrEmpty(job.Scope))
            body[OpenIdProtocol.Parameters.Scope] = job.Scope;

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, SecureEncoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, job.EndpointUrl)
        {
            Content = content,
        };

        if (!string.IsNullOrEmpty(job.ClientNotificationToken))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                job.ClientNotificationToken
            );
        }

        var httpClient = HttpClientFactory.CreateClient(GuardedHttpClient.Name);

        try
        {
            using var response = await httpClient.SendAsync(
                requestMessage,
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
            var uriForLog = OutboundUriRedaction.ForLog(job.EndpointUrl);
            var reason = OutboundFetchFailureReasons.Classify(exception, timedOut: false);
            Logger.CibaDeliveryFailed(job.ClientId, uriForLog, reason, exception);

            throw;
        }
    }
}
