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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Subject;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;

/// <summary>
/// Represents the payload of a pending CIBA (OpenID Connect Client-Initiated Backchannel Authentication) request. The
/// request is persisted keyed by its <c>auth_req_id</c>, transitions its <see cref="Status"/> out of band when the end
/// user approves or denies it on their authentication device, and is polled by the client at the token endpoint. The
/// <see cref="Status"/> values are shared with the device flow (<see cref="DeviceAuthorizationStatus"/>).
/// </summary>
[PublicAPI]
public readonly record struct CibaGrant
{
    /// <summary>
    /// Gets the identifier of the client that initiated the backchannel authentication request.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the scopes requested by the client.
    /// </summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>
    /// Gets the resource indicators (RFC 8707) requested by the client, or <c>null</c> when none were requested.
    /// </summary>
    public IReadOnlyList<string>? Resources { get; init; }

    /// <summary>
    /// Gets the approval status of the request. See <see cref="DeviceAuthorizationStatus"/>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the token delivery mode for the request. See <see cref="OpenIdConstants.BackchannelTokenDeliveryModes"/>.
    /// </summary>
    public required string DeliveryMode { get; init; }

    /// <summary>
    /// Gets the authenticated subject that approved the request, or <c>null</c> while the request is pending or denied.
    /// </summary>
    public SubjectAuthentication? SubjectAuthentication { get; init; }

    /// <summary>
    /// Gets when the client most recently polled the token endpoint for this request, used to enforce the polling
    /// interval (the <c>slow_down</c> response).
    /// </summary>
    public DateTimeOffset? LastPolledWhen { get; init; }

    /// <summary>
    /// Gets the client notification token supplied for ping/push delivery, or <c>null</c> for poll delivery.
    /// </summary>
    public string? ClientNotificationToken { get; init; }

    /// <summary>
    /// Gets the optional human-readable binding message the client and authentication device display.
    /// </summary>
    public string? BindingMessage { get; init; }
}
