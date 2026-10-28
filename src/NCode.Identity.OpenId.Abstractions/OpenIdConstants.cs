#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using Microsoft.AspNetCore.Authentication;

namespace NCode.Identity.OpenId;

/// <summary>
/// Contains the OP's server-side constants. The wire vocabulary shared with clients is <see cref="OpenIdProtocol"/>.
/// </summary>
[PublicAPI]
public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains the names for various <c>OAuth</c> and <c>OpenID Connect</c> endpoints and routes.
    /// </summary>
    public static class EndpointNames
    {
        /// <summary>
        /// Contains the name for the <c>authorization</c> endpoint.
        /// </summary>
        public const string Authorization = OpenIdProtocol.MetadataNames.AuthorizationEndpoint;

        /// <summary>
        /// Contains the name for the <c>continue</c> (callback) endpoint.
        /// </summary>
        public const string Continue = "continue_endpoint";

        /// <summary>
        /// Contains the name for the <c>discovery</c> endpoint.
        /// </summary>
        public const string Discovery = "discovery_endpoint";

        /// <summary>
        /// Contains the name for the <c>device authorization</c> endpoint.
        /// This value is also used as the <c>device_authorization_endpoint</c> key in the discovery metadata.
        /// </summary>
        public const string DeviceAuthorization = OpenIdProtocol
            .MetadataNames
            .DeviceAuthorizationEndpoint;

        /// <summary>
        /// Contains the name for the user-facing <c>device verification</c> endpoint where the end user enters their
        /// <c>user_code</c> to approve or deny a device authorization request.
        /// </summary>
        public const string DeviceVerification = "device_verification_endpoint";

        /// <summary>
        /// Contains the name for the <c>backchannel authentication</c> endpoint (OpenID Connect CIBA).
        /// This value is also used as the <c>backchannel_authentication_endpoint</c> key in the discovery metadata.
        /// </summary>
        public const string BackchannelAuthentication = OpenIdProtocol
            .MetadataNames
            .BackchannelAuthenticationEndpoint;

        /// <summary>
        /// Contains the name for the <c>pushed authorization request</c> endpoint (RFC 9126).
        /// This value is also used as the <c>pushed_authorization_request_endpoint</c> key in the discovery metadata.
        /// </summary>
        public const string PushedAuthorizationRequest = OpenIdProtocol
            .MetadataNames
            .PushedAuthorizationRequestEndpoint;

        /// <summary>
        /// Contains the name for the <c>JSON Web Key Set (JWKS)</c> endpoint.
        /// This value is also used as the <c>jwks_uri</c> key in the discovery metadata.
        /// </summary>
        public const string Jwks = OpenIdProtocol.MetadataNames.JwksUri;

        /// <summary>
        /// Contains the name for the <c>token</c> endpoint.
        /// </summary>
        public const string Token = OpenIdProtocol.MetadataNames.TokenEndpoint;

        /// <summary>
        /// Contains the name for the <c>revocation</c> endpoint.
        /// </summary>
        public const string Revocation = OpenIdProtocol.MetadataNames.RevocationEndpoint;

        /// <summary>
        /// Contains the name for the <c>introspection</c> endpoint.
        /// </summary>
        public const string Introspection = OpenIdProtocol.MetadataNames.IntrospectionEndpoint;

        /// <summary>
        /// Contains the name for the <c>userinfo</c> endpoint.
        /// </summary>
        public const string UserInfo = OpenIdProtocol.MetadataNames.UserInfoEndpoint;

        /// <summary>
        /// Contains the name for the <c>end session</c> (RP-initiated logout) endpoint.
        /// This value is also used as the <c>end_session_endpoint</c> key in the discovery metadata.
        /// </summary>
        public const string EndSession = OpenIdProtocol.MetadataNames.EndSessionEndpoint;

        /// <summary>
        /// Contains the name for the <c>check session</c> iframe endpoint (OpenID Connect Session Management 1.0).
        /// This value is also used as the <c>check_session_iframe</c> key in the discovery metadata.
        /// </summary>
        public const string CheckSessionIframe = OpenIdProtocol.MetadataNames.CheckSessionIframe;

        /// <summary>
        /// Contains the name for the <c>dynamic client registration</c> endpoint (RFC 7591).
        /// This value is also used as the <c>registration_endpoint</c> key in the discovery metadata.
        /// </summary>
        public const string Registration = OpenIdProtocol.MetadataNames.RegistrationEndpoint;

        /// <summary>
        /// Contains the name for the per-client <c>registration management</c> endpoint (RFC 7592) that a client uses
        /// to read, update, or delete its own registration via its <c>registration_access_token</c>.
        /// </summary>
        public const string RegistrationManagement = "registration_management_endpoint";
    }

    /// <summary>
    /// Contains the relative paths for various <c>OAuth</c> and <c>OpenID Connect</c> endpoints and routes.
    /// Be aware that these paths may be relative to the base address of the current tenant.
    /// </summary>
    public static class EndpointPaths
    {
        /// <summary>
        /// Contains the common prefix for all endpoints and routes.
        /// </summary>
        private const string Prefix = "/oauth2";

        /// <summary>
        /// Contains the relative path for the <c>authorization</c> endpoint.
        /// </summary>
        public const string Authorization = $"{Prefix}/authorize";

        /// <summary>
        /// Contains the relative path for the <c>continue</c> (callback) endpoint.
        /// </summary>
        public const string Continue = $"{Prefix}/continue";

        /// <summary>
        /// Contains the relative path for the <c>discovery</c> endpoint.
        /// </summary>
        public const string Discovery = OpenIdProtocol.WellKnownPaths.OpenIdConfiguration;

        /// <summary>
        /// Contains the relative path for the <c>device authorization</c> endpoint.
        /// </summary>
        public const string DeviceAuthorization = $"{Prefix}/device_authorization";

        /// <summary>
        /// Contains the relative path for the user-facing <c>device verification</c> endpoint.
        /// </summary>
        public const string DeviceVerification = $"{Prefix}/device";

        /// <summary>
        /// Contains the relative path for the <c>backchannel authentication</c> endpoint (OpenID Connect CIBA).
        /// </summary>
        public const string BackchannelAuthentication = $"{Prefix}/bc-authorize";

        /// <summary>
        /// Contains the relative path for the <c>pushed authorization request</c> endpoint (RFC 9126).
        /// </summary>
        public const string PushedAuthorizationRequest = $"{Prefix}/par";

        /// <summary>
        /// Contains the relative path for the <c>JSON Web Key Set (JWKS)</c> endpoint.
        /// </summary>
        public const string Jwks = $"{Prefix}/jwks";

        /// <summary>
        /// Contains the relative path for the <c>token</c> endpoint.
        /// </summary>
        public const string Token = $"{Prefix}/token";

        /// <summary>
        /// Contains the relative path for the <c>revocation</c> endpoint.
        /// </summary>
        public const string Revocation = $"{Prefix}/revoke";

        /// <summary>
        /// Contains the relative path for the <c>introspection</c> endpoint.
        /// </summary>
        public const string Introspection = $"{Prefix}/introspect";

        /// <summary>
        /// Contains the relative path for the <c>userinfo</c> endpoint.
        /// </summary>
        public const string UserInfo = $"{Prefix}/userinfo";

        /// <summary>
        /// Contains the relative path for the <c>end session</c> (RP-initiated logout) endpoint.
        /// </summary>
        public const string EndSession = $"{Prefix}/end_session";

        /// <summary>
        /// Contains the relative path for the <c>check session</c> iframe endpoint (OpenID Connect Session
        /// Management 1.0).
        /// </summary>
        public const string CheckSession = $"{Prefix}/check_session";

        /// <summary>
        /// Contains the relative path for the <c>dynamic client registration</c> endpoint (RFC 7591).
        /// </summary>
        public const string Registration = $"{Prefix}/register";
    }

    /// <summary>
    /// Contains the OpenAPI tag names applied to <c>OAuth</c>, <c>OpenID Connect</c>, and management endpoints.
    /// </summary>
    public static class EndpointTags
    {
        /// <summary>
        /// Contains the OpenAPI tag applied to all <c>OAuth</c> and <c>OpenID Connect</c> endpoints.
        /// </summary>
        public const string OpenId = "oidc";

        /// <summary>
        /// Contains the OpenAPI tag applied to the <c>clients</c> management endpoints.
        /// </summary>
        public const string Clients = "Clients";

        /// <summary>
        /// Contains the OpenAPI tag applied to the <c>grants</c> management endpoints.
        /// </summary>
        public const string Grants = "Grants";

        /// <summary>
        /// Contains the OpenAPI tag applied to the <c>sessions</c> management endpoints.
        /// </summary>
        public const string Sessions = "Sessions";

        /// <summary>
        /// Contains the OpenAPI tag applied to the <c>client grants</c> management endpoints.
        /// </summary>
        public const string ClientGrants = "ClientGrants";

        /// <summary>
        /// Contains the OpenAPI tag applied to the <c>resource servers</c> management endpoints.
        /// </summary>
        public const string ResourceServers = "ResourceServers";

        /// <summary>
        /// Contains the OpenAPI tag applied to the <c>servers</c> management endpoints.
        /// </summary>
        public const string Servers = "Servers";

        /// <summary>
        /// Contains the OpenAPI tag applied to the <c>tenants</c> management endpoints.
        /// </summary>
        public const string Tenants = "Tenants";

        /// <summary>
        /// Contains the OpenAPI tag applied to the <c>local accounts</c> management endpoints.
        /// </summary>
        public const string LocalAccounts = "LocalAccounts";

        /// <summary>
        /// Contains the OpenAPI tag applied to the <c>principals</c> management endpoints.
        /// </summary>
        public const string Principals = "Principals";

        /// <summary>
        /// Contains the OpenAPI tag applied to the server <c>capabilities</c> discovery endpoint.
        /// </summary>
        public const string Capabilities = "Capabilities";
    }

    /// <summary>
    /// Contains constants for various codes that can be used to identify the tenant strategy.
    /// </summary>
    public static class TenantStrategyCodes
    {
        /// <summary>
        /// Identifies the tenant strategy that always resolves the same single, statically-configured tenant.
        /// </summary>
        public const string StaticSingle = nameof(StaticSingle);

        /// <summary>
        /// Identifies the tenant strategy that resolves the tenant dynamically from the request host.
        /// </summary>
        public const string DynamicByHost = nameof(DynamicByHost);

        /// <summary>
        /// Identifies the tenant strategy that resolves the tenant dynamically from the request path.
        /// </summary>
        public const string DynamicByPath = nameof(DynamicByPath);
    }

    /// <summary>
    /// Contains constants for various codes that can be used to identify the type of continuation operation.
    /// </summary>
    public static class ContinueCodes
    {
        /// <summary>
        /// Identifies the continuation of a previously-initiated <c>authorization</c> operation.
        /// </summary>
        public const string Authorization = "continue_authorization";
    }

    /// <summary>
    /// Contains constants for various types of OpenID grants.
    /// </summary>
    public static class PersistedGrantTypes
    {
        /// <summary>
        /// Identifies a persisted grant for a <c>continue</c> (callback) operation.
        /// </summary>
        public const string Continue = "continue";

        /// <summary>
        /// Identifies a persisted grant for an <c>authorization code</c>.
        /// </summary>
        public const string AuthorizationCode = "authorization_code";

        /// <summary>
        /// Identifies a persisted grant for a <c>refresh token</c>.
        /// </summary>
        public const string RefreshToken = "refresh_token";

        /// <summary>
        /// Identifies a persisted grant for a pending <c>device authorization</c> request (RFC 8628), keyed by the
        /// <c>device_code</c> that the device polls with.
        /// </summary>
        public const string DeviceCode = "device_code";

        /// <summary>
        /// Identifies a persisted grant that maps a human-typable <c>user_code</c> to its pending device authorization
        /// request, so the verification endpoint can resolve the request from the <c>user_code</c> alone.
        /// </summary>
        public const string DeviceUserCode = "device_user_code";

        /// <summary>
        /// Identifies a persisted grant for a pending <c>CIBA</c> backchannel authentication request (OpenID Connect
        /// CIBA), keyed by the <c>auth_req_id</c> that the client polls with.
        /// </summary>
        public const string Ciba = "ciba";

        /// <summary>
        /// Identifies a persisted grant that carries the UserInfo claims requested via the OpenID Connect
        /// <c>claims</c> request parameter, keyed by the access token's <c>jti</c> so the UserInfo endpoint can honor
        /// them.
        /// </summary>
        public const string UserInfoClaims = "userinfo_claims";

        /// <summary>
        /// Identifies a persisted grant for a <c>registration access token</c> (RFC 7592), keyed by the token value,
        /// whose payload binds the token to the client it manages.
        /// </summary>
        public const string RegistrationAccessToken = "registration_access_token";

        /// <summary>
        /// Identifies a persisted grant for an <c>initial access token</c> (RFC 7591), keyed by the token value, that
        /// authorizes a caller to register a new client at the dynamic client registration endpoint.
        /// </summary>
        public const string InitialAccessToken = "initial_access_token";

        /// <summary>
        /// Identifies a persisted grant for an end-user <c>session</c> (an OpenID Connect login at the OP), keyed by
        /// the opaque <c>sid</c>, whose payload carries the authentication context and the set of relying parties the
        /// session has issued tokens to. It backs the <c>sid</c> claim and the logout pipeline.
        /// </summary>
        public const string Session = "session";

        /// <summary>
        /// Identifies a persisted grant for a <c>pushed authorization request</c> (RFC 9126), keyed by the opaque
        /// reference embedded in the issued <c>request_uri</c>, whose payload carries the authorization request
        /// parameters the client pushed. The authorization endpoint consumes it once to replay the request.
        /// </summary>
        public const string PushedAuthorizationRequest = "pushed_authorization_request";
    }

    /// <summary>
    /// Contains constants for various types of security tokens.
    /// </summary>
    public static class SecurityTokenTypes
    {
        /// <summary>
        /// Identifies an <c>ID token</c> security token.
        /// </summary>
        public const string IdToken = "id_token";

        /// <summary>
        /// Identifies an <c>access token</c> security token.
        /// </summary>
        public const string AccessToken = "access_token";

        /// <summary>
        /// Identifies a <c>refresh token</c> security token.
        /// </summary>
        public const string RefreshToken = "refresh_token";

        /// <summary>
        /// Identifies an <c>authorization code</c> security token.
        /// </summary>
        public const string AuthorizationCode = "authorization_code";

        /// <summary>
        /// Identifies a <c>logout token</c> security token (OpenID Connect Back-Channel Logout); its value is the
        /// <c>typ</c> header of the <c>logout_token</c> JWT.
        /// </summary>
        public const string LogoutToken = "logout+jwt";
    }

    /// <summary>
    /// Contains constants for the expiration policies of refresh tokens. A refresh token always expires.
    /// </summary>
    public static class RefreshTokenExpirationPolicy
    {
        /// <summary>
        /// Indicates that refresh tokens expire at a fixed point in time regardless of use.
        /// </summary>
        public const string Absolute = "absolute";

        /// <summary>
        /// Indicates that the refresh token expiration is extended each time it is used, up to an absolute maximum.
        /// </summary>
        public const string Sliding = "sliding";
    }

    /// <summary>
    /// Contains constants for how the server responds when a revoked refresh token is replayed (reuse detection).
    /// </summary>
    public static class RefreshTokenReusePolicy
    {
        /// <summary>
        /// Indicates that a replayed refresh token is rejected and audited, but no further tokens are revoked.
        /// </summary>
        public const string None = "none";

        /// <summary>
        /// Indicates that a replayed refresh token revokes its entire rotation family (every token descended from
        /// the same authorization grant), per the OAuth 2.0 Security BCP (RFC 9700).
        /// </summary>
        public const string RevokeAll = "revoke_all";
    }

    /// <summary>
    /// Contains constants for which refresh tokens are rotated, that is replaced by a new refresh token each time they
    /// are used.
    /// </summary>
    public static class RefreshTokenRotationPolicy
    {
        /// <summary>
        /// Indicates that a refresh token is rotated when it is bound neither to a proof-of-possession key nor to
        /// client authentication, that is a public client's token without a DPoP or certificate binding, as the OAuth
        /// 2.0 Security BCP (RFC 9700) requires.
        /// </summary>
        public const string Unbound = "unbound";

        /// <summary>
        /// Indicates that every refresh token is rotated.
        /// </summary>
        public const string Always = "always";
    }

    /// <summary>
    /// Contains constants for how the revocation status of a <c>tls_client_auth</c> client certificate chain is
    /// checked. The values are the revocation policy: revocation is required, checked without being required, or not
    /// checked. How the status is obtained is the client certificate revocation checker and its registered sources.
    /// </summary>
    public static class ClientCertificateRevocationMode
    {
        /// <summary>
        /// Indicates that every certificate below the trust anchor is checked through the registered revocation
        /// sources, the OCSP responders and then the certificate revocation lists its issuer names, which the server
        /// reaches through its outbound-request guard; a chain whose revocation status cannot be determined is
        /// rejected.
        /// </summary>
        public const string Online = "online";

        /// <summary>
        /// Indicates that every certificate below the trust anchor is checked as for <see cref="Online"/>; a revoked
        /// certificate is rejected, and one whose revocation status cannot be determined is accepted with a warning,
        /// so a client keeps authenticating while the revocation source is unreachable — and so does a revoked
        /// certificate whose source an attacker blocks.
        /// </summary>
        public const string OnlineSoftFail = "online_soft_fail";

        /// <summary>
        /// Indicates that revocation is not checked, for a PKI that issues short-lived certificates instead of
        /// publishing revocation data.
        /// </summary>
        public const string None = "none";
    }

    /// <summary>
    /// Contains constants for the named <c>security_profiles</c> values a server, tenant, or client can select to
    /// tighten the OP to a standardized security baseline.
    /// </summary>
    public static class SecurityProfiles
    {
        /// <summary>
        /// The OpenID Foundation <c>FAPI 2.0</c> Security Profile: code-only responses, PAR, PKCE <c>S256</c>,
        /// sender-constrained tokens, strong (asymmetric / mutual-TLS) client authentication, and asymmetric-only
        /// signing (<c>PS256</c> / <c>ES256</c> / <c>EdDSA</c>).
        /// </summary>
        public const string Fapi2 = "fapi2";

        /// <summary>
        /// The OpenID Foundation <c>FAPI 1.0 Advanced</c> Security Profile (Final): <c>code</c> with a JWT-secured
        /// authorization response (JARM) or <c>code id_token</c>, PAR, PKCE <c>S256</c>, signed request objects,
        /// mutual-TLS sender-constrained tokens, strong (asymmetric / mutual-TLS) client authentication, and
        /// asymmetric-only signing (<c>PS256</c> / <c>ES256</c>).
        /// </summary>
        public const string Fapi1Advanced = "fapi1-advanced";
    }

    /// <summary>
    /// Contains constants for various items that can be stored within <see cref="AuthenticationProperties"/>.
    /// </summary>
    /// <remarks>
    /// Be aware that items are not the same as the parameters within <see cref="AuthenticationProperties"/>.
    /// Items are state values that are serialized for the authentication session.
    /// Parameters are only for flowing data between call sites.
    /// </remarks>
    public static class AuthenticationPropertyItems
    {
        /// <summary>
        /// Contains the item key for the current tenant identifier.
        /// </summary>
        public const string TenantId = ".tenant";
    }

    /// <summary>
    /// Contains constants for well-known <c>authentication context class reference (acr)</c> value prefixes.
    /// </summary>
    public static class AuthenticationContextClassReferencePrefixes
    {
        /// <summary>
        /// Contains the <c>acr</c> prefix that identifies the authenticating identity provider.
        /// </summary>
        public const string IdentityProvider = "idp:";
    }
}
