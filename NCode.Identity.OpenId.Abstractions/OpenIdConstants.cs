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
/// Contains constants for various <c>OAuth</c> and <c>OpenID Connect</c> implementations.
/// </summary>
[PublicAPI]
public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains the space ' ' character which is used as the separator in string lists.
    /// </summary>
    public const char ParameterSeparatorChar = ' ';

    /// <summary>
    /// Contains the space ' ' character (as a string) which is used as the separator in string lists.
    /// </summary>
    public const string ParameterSeparatorString = " ";

    /// <summary>
    /// Contains the names for various <c>OAuth</c> and <c>OpenID Connect</c> endpoints and routes.
    /// </summary>
    public static class EndpointNames
    {
        /// <summary>
        /// Contains the name for the <c>authorization</c> endpoint.
        /// </summary>
        public const string Authorization = "authorization_endpoint";

        /// <summary>
        /// Contains the name for the <c>continue</c> (aka callback) endpoint.
        /// </summary>
        public const string Continue = "continue_endpoint";

        /// <summary>
        /// Contains the name for the <c>discovery</c> endpoint.
        /// </summary>
        public const string Discovery = "discovery_endpoint";

        /// <summary>
        /// Contains the name for the <c>token</c> endpoint.
        /// </summary>
        public const string Token = "token_endpoint";
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
        /// Contains the relative path for the <c>continue</c> (aka callback) endpoint.
        /// </summary>
        public const string Continue = $"{Prefix}/continue";

        /// <summary>
        /// Contains the relative path for the <c>discovery</c> endpoint.
        /// </summary>
        public const string Discovery = "/.well-known/openid-configuration";

        /// <summary>
        /// Contains the relative path for the <c>token</c> endpoint.
        /// </summary>
        public const string Token = $"{Prefix}/token";
    }

    /// <summary>
    /// Contains constants for various codes that can be used to identify the tenant provider.
    /// </summary>
    public static class TenantProviderCodes
    {
        public const string StaticSingle = nameof(StaticSingle);
        public const string DynamicByHost = nameof(DynamicByHost);
        public const string DynamicByPath = nameof(DynamicByPath);
    }

    /// <summary>
    /// Contains constants for various codes that can be used to identify the type of continuation operation.
    /// </summary>
    public static class ContinueCodes
    {
        public const string Authorization = "continue_authorization";
    }

    /// <summary>
    /// Contains constants for various types of OpenID grants.
    /// </summary>
    public static class PersistedGrantTypes
    {
        public const string Continue = "continue";
        public const string AuthorizationCode = "authorization_code";
        public const string RefreshToken = "refresh_token";
    }

    /// <summary>
    /// Contains constants for various types of security tokens.
    /// </summary>
    public static class SecurityTokenTypes
    {
        public const string IdToken = "id_token";
        public const string AccessToken = "access_token";
        public const string RefreshToken = "refresh_token";
        public const string AuthorizationCode = "authorization_code";
    }

    /// <summary>
    /// Contains constants for various types of expiration policies for refresh tokens.
    /// </summary>
    public static class RefreshTokenExpirationPolicy
    {
        public const string None = "none";
        public const string Absolute = "absolute";
        public const string Sliding = "sliding";
    }

    /// <summary>
    /// Contains constants for various types of client authentication methods.
    /// These values are used in the <c>token_endpoint_auth_methods_supported</c> metadata.
    /// </summary>
    public static class ClientAuthenticationMethods
    {
        /// <summary>
        /// Indicates that the client does not authenticate itself, either because it uses only the Implicit Flow (and so does not use the Token Endpoint) or because it is a Public Client with no Client Secret or other authentication mechanism.
        /// </summary>
        public const string None = "none";

        /// <summary>
        /// Indicates that the authorization server uses the Client Credentials from the POST request body to authenticate the client.
        /// </summary>
        public const string ClientSecretPost = "client_secret_post";

        /// <summary>
        /// Indicates that the authorization server uses the HTTP Basic authentication scheme to authenticate the client.
        /// </summary>
        public const string ClientSecretBasic = "client_secret_basic";

        public const string ClientSecretJwt = "client_secret_jwt";
        public const string PrivateKeyJwt = "private_key_jwt";
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
        public const string TenantId = ".tenant";
    }

    public static class SubjectTypes
    {
        public const string Public = "public";

        // https://docs.safewhere.com/identify/concepts/connections/oauth/advanced-topics/oauth-ppid.html
        // example: base64urlencode(HS256Signature(sectorIdentifier + client_id + salt, key))
        public const string Pairwise = "pairwise";
    }

    // authentication context class reference (acr)
    public static class AuthenticationContextClassReferencePrefixes
    {
        public const string IdentityProvider = "idp:";
    }
}
