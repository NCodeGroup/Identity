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

using System.Net.Http.Headers;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.OutboundHttp;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Delivery;

/// <summary>
/// Executes a <see cref="CibaDeliveryJob"/> by <c>POST</c>ing the CIBA client notification (the completion ping, the
/// issued tokens, or the error payload of a denied push request per CIBA Core §12) to the client's
/// <c>client_notification_endpoint</c>, authenticated with the client notification token. A delivery failure throws so
/// the <see cref="IJobDispatcher"/> retries the job (up to its attempt limit), giving CIBA push and ping delivery
/// at-least-once semantics rather than the former best-effort single attempt. The response body is never read: the
/// delivery waits for the status line and headers only, so a hostile endpoint cannot make the server buffer a body.
/// </summary>
internal sealed class CibaDeliveryJobHandler(IHttpClientFactory httpClientFactory)
    : IDispatchJobHandler<CibaDeliveryJob>
{
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;

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
        using var response = await httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        // A non-success status throws, signaling the dispatcher to retry with back-off.
        response.EnsureSuccessStatusCode();
    }
}
