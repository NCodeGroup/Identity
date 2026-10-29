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

namespace NCode.Identity.Bff.Navigation;

/// <summary>
/// Provides the rules that decide whether a return URL is a safe, same-origin path the BFF may redirect to.
/// </summary>
internal static class BffReturnUrls
{
    /// <summary>
    /// Determines whether the value is a well-formed, same-origin absolute path with an optional query.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> when the value is a safe path; otherwise, <see langword="false"/>.</returns>
    public static bool IsWellFormedPath(string value)
    {
        if (string.IsNullOrEmpty(value) || value[0] != '/')
            return false;

        if (value.Length > 1 && value[1] == '/')
            return false;

        foreach (var c in value)
        {
            if (c is '\\' or '#' || char.IsWhiteSpace(c) || char.IsControl(c))
                return false;
        }

        if (
            value.Contains("%2f", StringComparison.OrdinalIgnoreCase)
            || value.Contains("%5c", StringComparison.OrdinalIgnoreCase)
            || value.Contains("%2e", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        foreach (var segment in GetPath(value).Split('/'))
        {
            if (segment is "." or "..")
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
                string.Equals(path, entry, StringComparison.Ordinal)
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

    private static string GetPath(string value)
    {
        var index = value.IndexOf('?', StringComparison.Ordinal);
        return index < 0 ? value : value[..index];
    }
}
