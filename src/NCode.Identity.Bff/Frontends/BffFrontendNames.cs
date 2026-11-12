// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Bff.Frontends;

/// <summary>
/// Checks the form of a frontend name: 1 to 63 lowercase ASCII letters, digits and hyphens that start and end with a
/// letter or digit.
/// </summary>
// See ADR-1442.
internal static class BffFrontendNames
{
    /// <summary>
    /// Determines whether <paramref name="name"/> is a valid frontend name.
    /// </summary>
    /// <param name="name">The frontend name to check.</param>
    /// <returns><see langword="true"/> when <paramref name="name"/> has the form of a frontend name.</returns>
    public static bool IsValid([NotNullWhen(true)] string? name)
    {
        if (name is null)
            return false;

        if (name.Length is < 1 or > 63)
            return false;

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            var isAlphanumeric = c is (>= 'a' and <= 'z') or (>= '0' and <= '9');
            if (isAlphanumeric)
                continue;

            var isInterior = c == '-' && i != 0 && i != name.Length - 1;
            if (!isInterior)
                return false;
        }

        return true;
    }
}
