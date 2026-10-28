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

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Delivery;

/// <summary>
/// A self-contained, serializable description of a single CIBA client notification to deliver to a client's
/// <c>client_notification_endpoint</c>: the completion ping (ping delivery) or the issued tokens (push delivery). It is
/// handed to the <see cref="NCode.Identity.Dispatch.IJobDispatcher"/> so delivery is retried out of band and can be
/// routed through a durable or distributed dispatcher adapter. Because a push job carries issued tokens, a durable
/// adapter that persists it must protect the payload at rest.
/// </summary>
internal sealed record CibaDeliveryJob : IDispatchJob
{
    /// <summary>
    /// Gets the delivery mode this notification serves, one of
    /// <see cref="OpenIdProtocol.BackchannelTokenDeliveryModes"/> (<c>ping</c> or <c>push</c>). It is informational,
    /// used for diagnostics.
    /// </summary>
    public required string DeliveryMode { get; init; }

    /// <summary>
    /// Gets the identifier of the client the notification is delivered to, used for diagnostics.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the client's <c>client_notification_endpoint</c> the notification is <c>POST</c>ed to.
    /// </summary>
    public required string EndpointUrl { get; init; }

    /// <summary>
    /// Gets the <c>client_notification_token</c> presented as a bearer credential on the notification, or <c>null</c>
    /// when the client registered none.
    /// </summary>
    public string? ClientNotificationToken { get; init; }

    /// <summary>
    /// Gets the <c>auth_req_id</c> of the completed backchannel authentication request.
    /// </summary>
    public required string AuthReqId { get; init; }

    /// <summary>
    /// Gets the issued access token for push delivery, or <c>null</c> for ping delivery.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Gets the token type of the issued access token for push delivery, or <c>null</c> for ping delivery.
    /// </summary>
    public string? TokenType { get; init; }

    /// <summary>
    /// Gets the lifetime in seconds of the issued access token for push delivery, or <c>null</c> when not applicable.
    /// </summary>
    public long? ExpiresInSeconds { get; init; }

    /// <summary>
    /// Gets the issued refresh token for push delivery, or <c>null</c> when none was issued.
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Gets the issued ID token for push delivery, or <c>null</c> when none was issued.
    /// </summary>
    public string? IdToken { get; init; }

    /// <summary>
    /// Gets the space-delimited granted scopes for push delivery, or <c>null</c> when not applicable.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Gets the error code of the push error payload delivered for a request that was not approved, such as
    /// <c>access_denied</c>, or <c>null</c> for a ping or a token delivery.
    /// </summary>
    public string? Error { get; init; }
}
