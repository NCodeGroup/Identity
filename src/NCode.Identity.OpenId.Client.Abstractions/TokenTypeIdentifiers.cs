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

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Provides the known token type identifiers defined by RFC 8693 section 3.
/// </summary>
[PublicAPI]
public static class TokenTypeIdentifiers
{
    /// <summary>
    /// Identifies an OAuth 2.0 access token.
    /// </summary>
    public const string AccessToken = "urn:ietf:params:oauth:token-type:access_token";

    /// <summary>
    /// Identifies an OAuth 2.0 refresh token.
    /// </summary>
    public const string RefreshToken = "urn:ietf:params:oauth:token-type:refresh_token";

    /// <summary>
    /// Identifies an OpenID Connect ID token.
    /// </summary>
    public const string IdToken = "urn:ietf:params:oauth:token-type:id_token";

    /// <summary>
    /// Identifies a JSON Web Token.
    /// </summary>
    public const string Jwt = "urn:ietf:params:oauth:token-type:jwt";
}
