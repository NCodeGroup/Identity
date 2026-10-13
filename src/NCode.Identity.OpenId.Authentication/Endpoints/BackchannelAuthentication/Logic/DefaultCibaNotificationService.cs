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
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Logic;

/// <summary>
/// Provides the default implementation of <see cref="ICibaNotificationService"/>. The authentication-device transport
/// is deployment-specific, so <see cref="NotifyAuthenticationDeviceAsync"/> is an inert no-op a host replaces. The
/// client ping notification and push token delivery are a standard HTTP <c>POST</c> to the client's registered
/// <c>client_notification_endpoint</c>, authenticated with the client notification token; delivery is best-effort.
/// </summary>
internal sealed class DefaultCibaNotificationService(
    IHttpClientFactory httpClientFactory,
    ILogger<DefaultCibaNotificationService> logger
) : ICibaNotificationService
{
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    private ILogger<DefaultCibaNotificationService> Logger { get; } = logger;

    /// <inheritdoc />
    public ValueTask NotifyAuthenticationDeviceAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string authReqId,
        CibaGrant request,
        CancellationToken cancellationToken
    ) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public async ValueTask NotifyClientAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string authReqId,
        CibaGrant request,
        CancellationToken cancellationToken
    )
    {
        var endpoint = openIdClient.Settings.GetValue(OpenIdSettingKeys.ClientNotificationEndpoint);
        if (string.IsNullOrEmpty(endpoint))
            return;

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [OpenIdConstants.Parameters.AuthReqId] = authReqId,
        };

        try
        {
            await PostAsync(endpoint, request.ClientNotificationToken, payload, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // Best-effort: an unreachable client endpoint does not fail the approval; the client can still poll.
            Logger.CibaClientNotificationFailed(openIdClient.ClientId, exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask DeliverTokensAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string authReqId,
        CibaGrant request,
        ITokenResponse tokenResponse,
        CancellationToken cancellationToken
    )
    {
        var endpoint = openIdClient.Settings.GetValue(OpenIdSettingKeys.ClientNotificationEndpoint);
        if (string.IsNullOrEmpty(endpoint))
            return;

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [OpenIdConstants.Parameters.AuthReqId] = authReqId,
        };

        if (!string.IsNullOrEmpty(tokenResponse.AccessToken))
            payload[OpenIdConstants.Parameters.AccessToken] = tokenResponse.AccessToken;

        if (!string.IsNullOrEmpty(tokenResponse.TokenType))
            payload[OpenIdConstants.Parameters.TokenType] = tokenResponse.TokenType;

        if (tokenResponse.ExpiresIn is { } expiresIn)
            payload[OpenIdConstants.Parameters.ExpiresIn] = (long)expiresIn.TotalSeconds;

        if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            payload[OpenIdConstants.Parameters.RefreshToken] = tokenResponse.RefreshToken;

        if (!string.IsNullOrEmpty(tokenResponse.IdToken))
            payload[OpenIdConstants.Parameters.IdToken] = tokenResponse.IdToken;

        if (tokenResponse.Scopes is { Count: > 0 } scopes)
            payload[OpenIdConstants.Parameters.Scope] = string.Join(' ', scopes);

        try
        {
            await PostAsync(endpoint, request.ClientNotificationToken, payload, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // Best-effort: push cannot fall back to polling, so a failed delivery is logged for operator follow-up.
            Logger.CibaTokenDeliveryFailed(openIdClient.ClientId, exception);
        }
    }

    private async ValueTask PostAsync(
        string endpoint,
        string? clientNotificationToken,
        IReadOnlyDictionary<string, object?> payload,
        CancellationToken cancellationToken
    )
    {
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = content,
        };

        if (!string.IsNullOrEmpty(clientNotificationToken))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                clientNotificationToken
            );
        }

        var httpClient = HttpClientFactory.CreateClient();
        using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
    }
}
