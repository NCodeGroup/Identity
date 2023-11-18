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

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains constant values for various <c>OAuth</c> and <c>OpenID Connect</c> grant types.
    /// </summary>
    public static class GrantTypes
    {
        /// <summary>
        /// Contains the constant value 'authorization_code'.
        /// </summary>
        public const string AuthorizationCode = "authorization_code";

        /// <summary>
        /// Contains the constant value 'implicit'.
        /// </summary>
        public const string Implicit = "implicit";

        /// <summary>
        /// Contains the constant value 'hybrid'.
        /// </summary>
        public const string Hybrid = "hybrid";

        /// <summary>
        /// Contains the constant value 'password'.
        /// </summary>
        public const string Password = "password";

        /// <summary>
        /// Contains the constant value 'client_credentials'.
        /// </summary>
        public const string ClientCredentials = "client_credentials";

        /// <summary>
        /// Contains the constant value 'refresh_token'.
        /// </summary>
        public const string RefreshToken = "refresh_token";

        /// <summary>
        /// Contains the constant value 'urn:ietf:params:oauth:grant-type:device_code'.
        /// </summary>
        public const string DeviceCode = "urn:ietf:params:oauth:grant-type:device_code";
    }
}
