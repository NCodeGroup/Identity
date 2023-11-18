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
}
