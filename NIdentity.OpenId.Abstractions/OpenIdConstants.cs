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

using NCode.Jose;

namespace NIdentity.OpenId;

/// <summary>
/// Contains constants for various <c>OAuth</c> and <c>OpenID Connect</c> implementations.
/// </summary>
public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains the space ' ' character which is used as the separator in string lists.
    /// </summary>
    public const char ParameterSeparatorChar = ' ';

    /// <summary>
    /// Contains the space ' ' character which is used as the separator in string lists.
    /// </summary>
    public const string ParameterSeparatorString = " ";

    public static class ClaimTypes
    {
        public const string Normal = "normal";

        public const string Aggregated = "aggregated";

        public const string Distributed = "distributed";
    }

    public static class GrantTypes
    {
        public const string AuthorizationCode = "authorization_code";
        public const string Implicit = "implicit";
        public const string Hybrid = "hybrid";
        public const string Password = "password";
        public const string ClientCredentials = "client_credentials";
        public const string RefreshToken = "refresh_token";
        public const string DeviceCode = "urn:ietf:params:oauth:grant-type:device_code";
    }

    /// <summary>
    /// Contains the names for various <c>OAuth</c> and <c>OpenID Connect</c> endpoints and routes.
    /// </summary>
    public static class EndpointNames
    {
        /// <summary>
        /// Contains the name for the discovery endpoint.
        /// </summary>
        public const string Discovery = "discovery_endpoint";

        /// <summary>
        /// Contains the name for the authorization endpoint.
        /// </summary>
        public const string Authorization = "authorization_endpoint";
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
        /// Contains the relative path for the discovery endpoint.
        /// </summary>
        public const string Discovery = "/.well-known/openid-configuration";

        /// <summary>
        /// Contains the relative path for the authorization endpoint.
        /// </summary>
        public const string Authorization = $"{Prefix}/authorize";
    }

    /// <summary>
    /// Contains constants for various type of the <c>OAuth</c> and <c>OpenID Connect</c> tokens.
    /// </summary>
    public static class TokenTypes
    {
        /// <summary>
        /// Contains the <c>Bearer</c> constant value.
        /// </summary>
        public const string Bearer = "Bearer";

        /// <summary>
        /// Contains the <c>id_token</c> constant value.
        /// </summary>
        public const string IdToken = "id_token";

        /// <summary>
        /// Contains the <c>access_token</c> constant value.
        /// </summary>
        public const string AccessToken = "access_token";

        /// <summary>
        /// Contains the <c>refresh_token</c> constant value.
        /// </summary>
        public const string RefreshToken = "refresh_token";
    }

    /// <summary>
    /// Contains the standard claims that are included in various <c>OpenID Connect</c> scopes.
    /// </summary>
    public static class ClaimsByScope
    {
        /// <summary>
        /// Contains the standard claims that are included in the <c>profile</c> scope.
        /// </summary>
        public static IReadOnlyCollection<string> Profile { get; } = new HashSet<string>
        {
            JoseClaimNames.Payload.Name,
            JoseClaimNames.Payload.FamilyName,
            JoseClaimNames.Payload.GivenName,
            JoseClaimNames.Payload.MiddleName,
            JoseClaimNames.Payload.Nickname,
            JoseClaimNames.Payload.PreferredUsername,
            JoseClaimNames.Payload.Profile,
            JoseClaimNames.Payload.Picture,
            JoseClaimNames.Payload.Website,
            JoseClaimNames.Payload.Gender,
            JoseClaimNames.Payload.Birthdate,
            JoseClaimNames.Payload.Zoneinfo,
            JoseClaimNames.Payload.Locale,
            JoseClaimNames.Payload.UpdatedAt
        };

        /// <summary>
        /// Contains the standard claims that are included in the <c>email</c> scope.
        /// </summary>
        public static IReadOnlyCollection<string> Email { get; } = new HashSet<string>
        {
            JoseClaimNames.Payload.Email,
            JoseClaimNames.Payload.EmailVerified
        };

        /// <summary>
        /// Contains the standard claims that are included in the <c>address</c> scope.
        /// </summary>
        public static IReadOnlyCollection<string> Address { get; } = new HashSet<string>
        {
            JoseClaimNames.Payload.Address
        };

        /// <summary>
        /// Contains the standard claims that are included in the <c>phone</c> scope.
        /// </summary>
        public static IReadOnlyCollection<string> Phone { get; } = new HashSet<string>
        {
            JoseClaimNames.Payload.PhoneNumber,
            JoseClaimNames.Payload.PhoneNumberVerified
        };
    }
}
