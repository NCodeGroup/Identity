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
    /// Contains the space ' ' character (as a string) which is used as the separator in string lists.
    /// </summary>
    public const string ParameterSeparatorString = " ";

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
}
