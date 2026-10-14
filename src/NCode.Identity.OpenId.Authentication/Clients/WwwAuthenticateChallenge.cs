#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Builds <c>WWW-Authenticate</c> response header challenges
/// (<see href="https://datatracker.ietf.org/doc/html/rfc7235#section-4.1">RFC 7235 §4.1</see>).
/// </summary>
internal static class WwwAuthenticateChallenge
{
    /// <summary>
    /// Builds a <c>Basic</c> authentication challenge with the specified realm
    /// (<see href="https://datatracker.ietf.org/doc/html/rfc7617#section-2">RFC 7617 §2</see>).
    /// </summary>
    /// <param name="realm">The realm value included in the challenge.</param>
    /// <returns>The <c>WWW-Authenticate</c> header value.</returns>
    public static string Basic(string realm) => $"Basic realm=\"{EscapeQuotedString(realm)}\"";

    // A quoted-string escapes the backslash and double-quote characters (RFC 7230 §3.2.6).
    private static string EscapeQuotedString(string value) =>
        value.Contains('\\') || value.Contains('"')
            ? value.Replace("\\", "\\\\").Replace("\"", "\\\"")
            : value;
}
