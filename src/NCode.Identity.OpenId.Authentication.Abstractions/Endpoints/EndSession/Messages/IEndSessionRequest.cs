// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;

/// <summary>
/// Represents the message for an <c>OpenID Connect</c> RP-Initiated Logout request
/// (<see href="https://openid.net/specs/openid-connect-rpinitiated-1_0.html">RP-Initiated Logout 1.0</see>).
/// </summary>
[PublicAPI]
public interface IEndSessionRequest : IOpenIdRequest
{
    /// <summary>
    /// Gets or sets the <c>id_token_hint</c>: an ID token previously issued to the client, used to identify the
    /// end-user's session and the requesting client.
    /// </summary>
    string? IdTokenHint { get; set; }

    /// <summary>
    /// Gets or sets the <c>client_id</c> of the relying party requesting the logout.
    /// </summary>
    string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the <c>post_logout_redirect_uri</c> the OP should redirect the user agent to after logout. It is
    /// honored only when it is registered for the requesting client.
    /// </summary>
    string? PostLogoutRedirectUri { get; set; }

    /// <summary>
    /// Gets or sets the opaque <c>state</c> value echoed back on the post-logout redirect.
    /// </summary>
    string? State { get; set; }
}
