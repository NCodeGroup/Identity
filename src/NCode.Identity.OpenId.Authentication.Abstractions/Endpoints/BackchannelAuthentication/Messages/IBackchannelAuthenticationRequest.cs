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
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;

/// <summary>
/// Represents the message for an OpenID Connect CIBA backchannel authentication request (§7.1).
/// </summary>
[PublicAPI]
public interface IBackchannelAuthenticationRequest : IOpenIdRequest
{
    /// <summary>
    /// Gets or sets the scopes requested by the client.
    /// </summary>
    List<string>? Scopes { get; set; }

    /// <summary>
    /// Gets or sets the resource indicators (RFC 8707) requested by the client.
    /// </summary>
    List<string>? Resources { get; set; }

    /// <summary>
    /// Gets or sets the <c>login_hint</c> identifying the user to authenticate.
    /// </summary>
    string? LoginHint { get; set; }

    /// <summary>
    /// Gets or sets the <c>login_hint_token</c> identifying the user to authenticate.
    /// </summary>
    string? LoginHintToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>id_token_hint</c> identifying the user to authenticate.
    /// </summary>
    string? IdTokenHint { get; set; }

    /// <summary>
    /// Gets or sets the <c>binding_message</c> the client and authentication device display.
    /// </summary>
    string? BindingMessage { get; set; }

    /// <summary>
    /// Gets or sets the <c>client_notification_token</c> for ping/push delivery.
    /// </summary>
    string? ClientNotificationToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>requested_expiry</c> (seconds) the client requests for the <c>auth_req_id</c>.
    /// </summary>
    string? RequestedExpiry { get; set; }

    /// <summary>
    /// Gets or sets the <c>request</c> parameter: a signed (and optionally encrypted) JWT carrying the request
    /// parameters as claims (OpenID Connect CIBA §7.1.1, a FAPI-CIBA requirement).
    /// </summary>
    string? RequestJwt { get; set; }
}
