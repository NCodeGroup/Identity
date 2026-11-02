// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId;

namespace NCode.Identity.Bff.Navigation;

/// <summary>
/// Provides the rules that decide whether a return URL is a safe, same-origin path the BFF may redirect to. A return
/// URL must be a local URL and, beyond that, carry no fragment, no encoded separator or dot, and no dot segment.
/// </summary>
internal static class BffReturnUrls
{
    /// <summary>
    /// Determines whether the value is a well-formed, same-origin absolute path with an optional query.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> when the value is a safe path; otherwise, <see langword="false"/>.</returns>
    public static bool IsWellFormedPath(ReadOnlySpan<char> value)
    {
        if (!LocalUrl.IsLocal(value))
            return false;

        if (
            value.Contains('#')
            || value.Contains("%2f", StringComparison.OrdinalIgnoreCase)
            || value.Contains("%5c", StringComparison.OrdinalIgnoreCase)
            || value.Contains("%2e", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        var path = GetPath(value);
        foreach (var range in path.Split('/'))
        {
            if (path[range] is "." or "..")
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the return URL is well formed and its path falls under an entry of the allow list.
    /// </summary>
    /// <param name="returnUrl">The return URL to check.</param>
    /// <param name="allowList">The allowed path prefixes.</param>
    /// <returns><see langword="true"/> when the return URL is allowed; otherwise, <see langword="false"/>.</returns>
    public static bool IsAllowed(string returnUrl, IReadOnlyList<string> allowList)
    {
        if (!IsWellFormedPath(returnUrl))
            return false;

        var path = GetPath(returnUrl);
        foreach (var entry in allowList)
        {
            if (entry.Length == 0)
                continue;

            if (entry[^1] == '/')
            {
                if (path.StartsWith(entry, StringComparison.Ordinal))
                    return true;
            }
            else if (
                path.Equals(entry, StringComparison.Ordinal)
                || (
                    path.Length > entry.Length
                    && path[entry.Length] == '/'
                    && path.StartsWith(entry, StringComparison.Ordinal)
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static ReadOnlySpan<char> GetPath(ReadOnlySpan<char> value)
    {
        var index = value.IndexOf('?');
        return index < 0 ? value : value.Slice(0, index);
    }
}
