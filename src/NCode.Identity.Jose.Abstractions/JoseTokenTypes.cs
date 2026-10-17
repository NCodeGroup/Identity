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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Jose;

/// <summary>
/// Constants for the registered values that can be used in the <c>typ</c> JOSE header parameter, plus helpers that
/// compare <c>typ</c> values using the media-type matching rules from <c>RFC 7515</c> §4.1.9.
/// https://datatracker.ietf.org/doc/html/rfc7515#section-4.1.9
/// </summary>
[PublicAPI]
public static class JoseTokenTypes
{
    private const string ApplicationPrefix = "application/";

    /// <summary>
    /// Specifies that the content is a <c>JWT</c>.
    /// https://datatracker.ietf.org/doc/html/rfc7519#section-5.1
    /// </summary>
    public const string Jwt = "JWT";

    /// <summary>
    /// Specifies that the content is an OAuth 2.0 <c>JWT access token</c> (<c>at+jwt</c>).
    /// https://datatracker.ietf.org/doc/html/rfc9068#section-2.1
    /// </summary>
    public const string AccessTokenJwt = "at+jwt";

    /// <summary>
    /// Specifies that the content is a <c>DPoP proof JWT</c> (<c>dpop+jwt</c>).
    /// https://datatracker.ietf.org/doc/html/rfc9449#section-4.2
    /// </summary>
    public const string DPoPJwt = "dpop+jwt";

    /// <summary>
    /// Specifies that the content is an OpenID Connect <c>back-channel logout token</c> (<c>logout+jwt</c>).
    /// https://openid.net/specs/openid-connect-backchannel-1_0.html#LogoutToken
    /// </summary>
    public const string LogoutJwt = "logout+jwt";

    /// <summary>
    /// Specifies that the content is a <c>Security Event Token</c> (SET, <c>secevent+jwt</c>).
    /// https://datatracker.ietf.org/doc/html/rfc8417#section-2.3
    /// </summary>
    public const string SecurityEventJwt = "secevent+jwt";

    /// <summary>
    /// Specifies that the content is an OAuth 2.0 <c>authorization request object</c> (JAR, <c>oauth-authz-req+jwt</c>).
    /// https://datatracker.ietf.org/doc/html/rfc9101#section-10.9
    /// </summary>
    public const string AuthorizationRequestJwt = "oauth-authz-req+jwt";

    /// <summary>
    /// Normalizes a <c>typ</c> JOSE header value for media-type comparison per <c>RFC 7515</c> §4.1.9: a value that
    /// omits a <c>'/'</c> is treated as if the <c>application/</c> prefix were prepended. The result is not lower-cased
    /// because media-type matching is performed case-insensitively by <see cref="AreSame"/>.
    /// </summary>
    /// <param name="tokenType">The <c>typ</c> value to normalize.</param>
    /// <returns>The media type with an explicit <c>type/subtype</c> form.</returns>
    public static string Normalize(string tokenType)
    {
        ArgumentException.ThrowIfNullOrEmpty(tokenType);
        return tokenType.Contains('/', StringComparison.Ordinal)
            ? tokenType
            : ApplicationPrefix + tokenType;
    }

    /// <summary>
    /// Determines whether two <c>typ</c> JOSE header values denote the same media type using the matching rules from
    /// <c>RFC 7515</c> §4.1.9: the <c>application/</c> prefix is optional and the comparison is case-insensitive.
    /// </summary>
    /// <param name="left">The first <c>typ</c> value to compare, or <c>null</c>.</param>
    /// <param name="right">The second <c>typ</c> value to compare, or <c>null</c>.</param>
    /// <returns><c>true</c> if both values denote the same media type; otherwise, <c>false</c>.</returns>
    public static bool AreSame(
        [NotNullWhen(true)] string? left,
        [NotNullWhen(true)] string? right
    ) =>
        !string.IsNullOrEmpty(left)
        && !string.IsNullOrEmpty(right)
        && string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
}
