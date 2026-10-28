#region Copyright Preamble
//
//    Copyright @ 2026 NCode Group
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

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants for the known values of the <c>token_type_hint</c> parameter
    /// (<see href="https://datatracker.ietf.org/doc/html/rfc7009#section-2.1">RFC 7009 §2.1</see>). These are the
    /// known values, not all of them: the parameter is an open vocabulary.
    /// </summary>
    public static class TokenTypeHints
    {
        /// <summary>
        /// Contains the <c>token_type_hint</c> parameter value for an access token.
        /// </summary>
        public const string AccessToken = "access_token";

        /// <summary>
        /// Contains the <c>token_type_hint</c> parameter value for a refresh token.
        /// </summary>
        public const string RefreshToken = "refresh_token";
    }
}
