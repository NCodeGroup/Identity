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

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization;

/// <summary>
/// Provides the operations on an OAuth 2.0 <c>response_type</c> value: its canonical form, whether it issues an
/// authorization code, whether it returns tokens from the authorization endpoint, its default <c>response_mode</c>, and
/// the grant types it uses.
/// </summary>
/// <remarks>
/// A <c>response_type</c> value is a space-delimited list of names whose order does not matter (RFC 6749 §3.1.1), so
/// the server compares response types in one canonical form — the names in ordinal order, separated by a single space —
/// which is the form in which <c>response_types_supported</c> lists them.
/// </remarks>
// See ADR-0141.
internal static class ResponseTypeValues
{
    /// <summary>
    /// Gets the canonical form of the response type made of <paramref name="names"/>.
    /// </summary>
    /// <param name="names">The names of the response type, in any order.</param>
    /// <returns>The names in ordinal order, separated by a single space.</returns>
    public static string Canonicalize(IEnumerable<string> names)
    {
        var orderedNames = names.Order(StringComparer.Ordinal);
        return string.Join(OpenIdConstants.ParameterSeparatorChar, orderedNames);
    }

    /// <summary>
    /// Gets the canonical form of a written <c>response_type</c> value. A value that is not a list of names separated
    /// by single spaces (it has an empty name) is returned as written, so it matches no response type.
    /// </summary>
    /// <param name="value">The <c>response_type</c> value, its names in any order.</param>
    /// <returns>The canonical form of <paramref name="value"/>, or <paramref name="value"/> when it is malformed.</returns>
    public static string Canonicalize(string value)
    {
        var names = value.Split(OpenIdConstants.ParameterSeparatorChar);
        return names.Any(name => name.Length == 0) ? value : Canonicalize(names);
    }

    /// <summary>
    /// Gets a value indicating whether a response type issues an authorization code: it includes <c>code</c>.
    /// </summary>
    /// <param name="names">The names of the response type, in any order.</param>
    /// <returns><see langword="true"/> when the response type issues an authorization code.</returns>
    public static bool IssuesAuthorizationCode(IReadOnlyCollection<string> names) =>
        names.Contains(OpenIdProtocol.ResponseTypes.Code, StringComparer.Ordinal);

    /// <summary>
    /// Gets a value indicating whether a response type returns a token from the authorization endpoint: it includes
    /// <c>id_token</c> or <c>token</c>, which makes it an implicit or hybrid flow that uses the implicit grant.
    /// </summary>
    /// <param name="names">The names of the response type, in any order.</param>
    /// <returns><see langword="true"/> when the response type returns a token or an ID token.</returns>
    public static bool ReturnsTokens(IReadOnlyCollection<string> names) =>
        names.Contains(OpenIdProtocol.ResponseTypes.IdToken, StringComparer.Ordinal)
        || names.Contains(OpenIdProtocol.ResponseTypes.Token, StringComparer.Ordinal);

    /// <summary>
    /// Gets the default <c>response_mode</c> of a response type (OAuth 2.0 Multiple Response Type Encoding Practices
    /// §2.1 and §5): <c>fragment</c> for a response type that returns a token or an ID token, so a token never appears
    /// in a query string, and <c>query</c> otherwise.
    /// </summary>
    /// <param name="names">The names of the response type, in any order.</param>
    /// <returns>The default <c>response_mode</c>.</returns>
    public static string GetDefaultResponseMode(IReadOnlyCollection<string> names) =>
        ReturnsTokens(names)
            ? OpenIdProtocol.ResponseModes.Fragment
            : OpenIdProtocol.ResponseModes.Query;

    /// <summary>
    /// Gets the grant types a response type uses (OpenID Connect Dynamic Client Registration 1.0 §2):
    /// <c>authorization_code</c> when it includes <c>code</c>, and <c>implicit</c> when it includes <c>id_token</c> or
    /// <c>token</c>, because a token the authorization endpoint returns is issued through the implicit grant. A hybrid
    /// response type uses both.
    /// </summary>
    /// <param name="names">The names of the response type, in any order.</param>
    /// <returns>The grant types the response type uses; empty when it includes none of the defined names.</returns>
    public static IReadOnlyList<string> GetGrantTypes(IReadOnlyCollection<string> names)
    {
        List<string> grantTypes = [];

        if (IssuesAuthorizationCode(names))
            grantTypes.Add(OpenIdProtocol.GrantTypes.AuthorizationCode);

        if (ReturnsTokens(names))
            grantTypes.Add(OpenIdProtocol.GrantTypes.Implicit);

        return grantTypes;
    }

    /// <summary>
    /// Gets the grant types a written <c>response_type</c> value uses; see
    /// <see cref="GetGrantTypes(IReadOnlyCollection{string})"/>.
    /// </summary>
    /// <param name="value">The <c>response_type</c> value, its names in any order.</param>
    /// <returns>The grant types the response type uses.</returns>
    public static IReadOnlyList<string> GetGrantTypes(string value)
    {
        var names = value.Split(OpenIdConstants.ParameterSeparatorChar);
        return GetGrantTypes(names);
    }
}
