// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures one browser frontend that the BFF serves — its selection rule, client registration, endpoints, session
/// and remote APIs. Bound from a <c>Bff:Frontends:{name}</c> configuration section.
/// </summary>
[PublicAPI]
public sealed class BffFrontendOptions
{
    /// <summary>
    /// Gets or sets how the BFF decides that a request belongs to this frontend. The configuration section is
    /// <c>Bff:Frontends:{name}:Selection</c>.
    /// </summary>
    public BffFrontendSelectionOptions Selection { get; set; } = new();

    /// <summary>
    /// Gets or sets the issuer URL of the OpenID Provider. The configuration key is
    /// <c>Bff:Frontends:{name}:Authority</c>.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute URL the BFF is published at — scheme, host, optional port and path base, such as
    /// <c>https://app.example.com</c> or <c>https://example.com/app</c>. The BFF builds the <c>redirect_uri</c> and
    /// the <c>post_logout_redirect_uri</c> from it, never from the request. The configuration key is
    /// <c>Bff:Frontends:{name}:PublicOrigin</c>.
    /// </summary>
    public string PublicOrigin { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client identifier of the BFF at the OpenID Provider. The configuration key is
    /// <c>Bff:Frontends:{name}:ClientId</c>.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how the BFF authenticates to the token endpoint. The configuration section is
    /// <c>Bff:Frontends:{name}:ClientAuthentication</c>.
    /// </summary>
    public BffClientAuthenticationOptions ClientAuthentication { get; set; } = new();

    /// <summary>
    /// Gets or sets the scopes that the BFF requests at login, after <c>openid</c>. The configuration key is
    /// <c>Bff:Frontends:{name}:Scopes</c>. List <c>offline_access</c> only for an OpenID Provider that issues a
    /// refresh token for that scope alone: an offline grant outlives the provider's login session, so such a provider
    /// can end that session without sending the BFF a back-channel logout request.
    /// </summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    /// Gets or sets the resource indicators that the BFF requests at login. The configuration key is
    /// <c>Bff:Frontends:{name}:Resources</c>.
    /// </summary>
    public List<string> Resources { get; set; } = [];

    /// <summary>
    /// Gets or sets the allow-list of local paths that a login or logout may return to. The configuration key is
    /// <c>Bff:Frontends:{name}:ReturnUrls</c>.
    /// </summary>
    public List<string> ReturnUrls { get; set; } = [];

    /// <summary>
    /// Gets or sets the ID token claim types that <c>/bff/user</c> exposes to the frontend. The configuration key is
    /// <c>Bff:Frontends:{name}:UserClaims</c>.
    /// </summary>
    public List<string> UserClaims { get; set; } = [];

    /// <summary>
    /// Gets or sets the extra authorization request parameters that a frontend may pass to the login endpoint. The
    /// configuration key is <c>Bff:Frontends:{name}:LoginParameters</c>.
    /// </summary>
    public List<string> LoginParameters { get; set; } = [];

    /// <summary>
    /// Gets or sets the origins that a same-site request may come from. The configuration key is
    /// <c>Bff:Frontends:{name}:TrustedOrigins</c>.
    /// </summary>
    public List<string> TrustedOrigins { get; set; } = [];

    /// <summary>
    /// Gets or sets the local path that the browser is sent to when a login fails. The default is <c>/</c>. The
    /// configuration key is <c>Bff:Frontends:{name}:ErrorPath</c>.
    /// </summary>
    public string ErrorPath { get; set; } = "/";

    /// <summary>
    /// Gets or sets the path prefix of the BFF endpoints. The default is <c>/bff</c>. The configuration key is
    /// <c>Bff:Frontends:{name}:PathPrefix</c>.
    /// </summary>
    public string PathPrefix { get; set; } = BffDefaults.EndpointPrefix;

    /// <summary>
    /// Gets or sets a value indicating whether a back-channel logout token must carry a <c>sid</c> claim. The default
    /// is <c>true</c>. The configuration key is <c>Bff:Frontends:{name}:BackchannelLogoutSessionRequired</c>.
    /// </summary>
    public bool BackchannelLogoutSessionRequired { get; set; } = true;

    /// <summary>
    /// Gets or sets the protection the BFF registered for its ID tokens and logout tokens. The configuration section is
    /// <c>Bff:Frontends:{name}:IdToken</c>.
    /// </summary>
    public BffIdTokenOptions IdToken { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the BFF loads UserInfo at login and the protection it registered for the UserInfo
    /// responses. The configuration section is <c>Bff:Frontends:{name}:UserInfo</c>.
    /// </summary>
    public BffUserInfoOptions UserInfo { get; set; } = new();

    /// <summary>
    /// Gets or sets the headers that <c>UseBffSecurityHeaders()</c> writes. The configuration section is
    /// <c>Bff:Frontends:{name}:SecurityHeaders</c>.
    /// </summary>
    public BffSecurityHeadersOptions SecurityHeaders { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the BFF publishes its public client-authentication keys. The configuration section is
    /// <c>Bff:Frontends:{name}:Jwks</c>.
    /// </summary>
    public BffJwksOptions Jwks { get; set; } = new();

    /// <summary>
    /// Gets or sets the session lifetime and cookie options. The configuration section is
    /// <c>Bff:Frontends:{name}:Session</c>.
    /// </summary>
    public BffSessionOptions Session { get; set; } = new();

    /// <summary>
    /// Gets or sets the DPoP options. The configuration section is <c>Bff:Frontends:{name}:DPoP</c>.
    /// </summary>
    public BffDPoPOptions DPoP { get; set; } = new();

    /// <summary>
    /// Gets or sets the remote APIs that <c>MapBffRemoteApis()</c> forwards, by name. The configuration section is
    /// <c>Bff:Frontends:{name}:RemoteApis</c>.
    /// </summary>
    public Dictionary<string, BffRemoteApiOptions> RemoteApis { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
