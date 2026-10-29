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

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures the backend-for-frontend (BFF) of a single frontend. Bound from the <c>Bff</c> configuration section.
/// </summary>
[PublicAPI]
public sealed class BffOptions
{
    /// <summary>
    /// Gets or sets the issuer URL of the OpenID Provider. The configuration key is <c>Bff:Authority</c>.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client identifier of the BFF at the OpenID Provider. The configuration key is
    /// <c>Bff:ClientId</c>.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how the BFF authenticates to the token endpoint. The configuration section is
    /// <c>Bff:ClientAuthentication</c>.
    /// </summary>
    public BffClientAuthenticationOptions ClientAuthentication { get; set; } = new();

    /// <summary>
    /// Gets or sets the scopes that the BFF requests at login. The configuration key is <c>Bff:Scopes</c>.
    /// </summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    /// Gets or sets the resource indicators that the BFF requests at login. The configuration key is
    /// <c>Bff:Resources</c>.
    /// </summary>
    public List<string> Resources { get; set; } = [];

    /// <summary>
    /// Gets or sets the allow-list of local paths that a login or logout may return to. The configuration key is
    /// <c>Bff:ReturnUrls</c>.
    /// </summary>
    public List<string> ReturnUrls { get; set; } = [];

    /// <summary>
    /// Gets or sets the ID token claim types that <c>/bff/user</c> exposes to the frontend. The configuration key is
    /// <c>Bff:UserClaims</c>.
    /// </summary>
    public List<string> UserClaims { get; set; } = [];

    /// <summary>
    /// Gets or sets the extra authorization request parameters that a frontend may pass to the login endpoint. The
    /// configuration key is <c>Bff:LoginParameters</c>.
    /// </summary>
    public List<string> LoginParameters { get; set; } = [];

    /// <summary>
    /// Gets or sets the origins that a same-site request may come from. The configuration key is
    /// <c>Bff:TrustedOrigins</c>.
    /// </summary>
    public List<string> TrustedOrigins { get; set; } = [];

    /// <summary>
    /// Gets or sets the local path that the browser is sent to when a login fails. The default is <c>/</c>. The
    /// configuration key is <c>Bff:ErrorPath</c>.
    /// </summary>
    public string ErrorPath { get; set; } = "/";

    /// <summary>
    /// Gets or sets the path prefix of the BFF endpoints. The default is <c>/bff</c>. The configuration key is
    /// <c>Bff:PathPrefix</c>.
    /// </summary>
    public string PathPrefix { get; set; } = BffDefaults.EndpointPrefix;

    /// <summary>
    /// Gets or sets a value indicating whether a back-channel logout token must carry a <c>sid</c> claim. The default
    /// is <c>true</c>. The configuration key is <c>Bff:BackchannelLogoutSessionRequired</c>.
    /// </summary>
    public bool BackchannelLogoutSessionRequired { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the BFF requests the provider's UserInfo endpoint at login and adds its
    /// claims to the session, for providers that keep profile claims out of the ID token. A UserInfo claim replaces the
    /// ID token's claims of its type, except the ones that anchor the session to the authentication. The default is
    /// <c>false</c>. The configuration key is <c>Bff:LoadUserInfo</c>.
    /// </summary>
    // See ADR-0299.
    public bool LoadUserInfo { get; set; }

    /// <summary>
    /// Gets or sets the anti-forgery header options. The configuration section is <c>Bff:Antiforgery</c>.
    /// </summary>
    public BffAntiforgeryOptions Antiforgery { get; set; } = new();

    /// <summary>
    /// Gets or sets the session lifetime and cookie options. The configuration section is <c>Bff:Session</c>.
    /// </summary>
    public BffSessionOptions Session { get; set; } = new();

    /// <summary>
    /// Gets or sets the DPoP options. The configuration section is <c>Bff:DPoP</c>.
    /// </summary>
    public BffDPoPOptions DPoP { get; set; } = new();

    /// <summary>
    /// Gets or sets the token refresh options. The configuration section is <c>Bff:Tokens</c>.
    /// </summary>
    public BffTokenOptions Tokens { get; set; } = new();
}
