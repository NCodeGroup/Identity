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
/// Provides the comparisons that decide whether a requested <c>redirect_uri</c> is one the client registered: an exact
/// comparison of the strings as written, and the one exception that RFC 8252 permits for a loopback IP redirect URI.
/// </summary>
/// <remarks>
/// Both comparisons work on the raw strings and never on parsed or normalized URIs, so a requested value that differs
/// from a registered value in any character (case, a trailing slash, a query) does not match.
/// </remarks>
// See ADR-0203.
internal static class RedirectUriMatcher
{
    /// <summary>
    /// The prefixes of the loopback IP redirect URIs whose port may vary (RFC 8252 §7.3). <c>localhost</c> is not one
    /// of them.
    /// </summary>
    private static readonly string[] LoopbackPrefixes = ["http://127.0.0.1", "http://[::1]"];

    /// <summary>
    /// Gets a value indicating whether a requested redirect URI equals a registered redirect URI exactly, as ordinal
    /// strings (RFC 9700 §4.1.3 and OAuth 2.1 require simple string comparison).
    /// </summary>
    /// <param name="requested">The <c>redirect_uri</c> as written in the request.</param>
    /// <param name="registered">The redirect URI as registered for the client.</param>
    /// <returns><see langword="true"/> when the two strings are ordinally equal.</returns>
    public static bool MatchesExactly(string requested, string registered) =>
        string.Equals(requested, registered, StringComparison.Ordinal);

    /// <summary>
    /// Gets a value indicating whether a requested redirect URI matches a registered loopback IP redirect URI when the
    /// port is ignored (RFC 8252 §7.3). Both URIs must use the same loopback prefix (<c>http://127.0.0.1</c> or
    /// <c>http://[::1]</c>) and be ordinally equal after the optional port, including the path and the query.
    /// </summary>
    /// <param name="requested">The <c>redirect_uri</c> as written in the request.</param>
    /// <param name="registered">The redirect URI as registered for the client.</param>
    /// <returns>
    /// <see langword="true"/> when both URIs are loopback IP redirect URIs that differ at most in their port.
    /// </returns>
    public static bool MatchesLoopbackWithAnyPort(string requested, string registered) =>
        TrySplitLoopback(requested, out var requestedPrefix, out var requestedRest)
        && TrySplitLoopback(registered, out var registeredPrefix, out var registeredRest)
        && string.Equals(requestedPrefix, registeredPrefix, StringComparison.Ordinal)
        && requestedRest.SequenceEqual(registeredRest);

    /// <summary>
    /// Splits a loopback IP redirect URI into its loopback prefix and the part that follows its optional port.
    /// </summary>
    /// <param name="value">The redirect URI as written.</param>
    /// <param name="prefix">The loopback prefix, or empty when <paramref name="value"/> is not a loopback IP redirect URI.</param>
    /// <param name="rest">A span over <paramref name="value"/> for the part after the optional port (empty, or starting with <c>/</c> or <c>?</c>), or empty when <paramref name="value"/> is not a loopback IP redirect URI.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a loopback IP redirect URI.</returns>
    private static bool TrySplitLoopback(
        string value,
        out string prefix,
        out ReadOnlySpan<char> rest
    )
    {
        foreach (var candidate in LoopbackPrefixes)
        {
            if (!value.StartsWith(candidate, StringComparison.Ordinal))
                continue;

            var index = candidate.Length;
            if (index < value.Length && value[index] == ':')
            {
                var digitsStart = index + 1;
                index = digitsStart;
                while (index < value.Length && char.IsAsciiDigit(value[index]))
                    index++;

                if (index == digitsStart)
                    break;
            }

            if (index != value.Length && value[index] is not ('/' or '?'))
                break;

            prefix = candidate;
            rest = value.AsSpan(index);
            return true;
        }

        prefix = string.Empty;
        rest = default;
        return false;
    }
}
