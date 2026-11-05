// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores;

/// <summary>
/// Validates the master key prefixes that a page of protected values is filtered by, for the stores that page them.
/// </summary>
// See ADR-1407.
internal static class ProtectedValuePrefixes
{
    /// <summary>
    /// Checks the master key prefixes that a protected-value page filters by: at least one, none empty, all the same
    /// length, so the store compares one fixed-length prefix of each value.
    /// </summary>
    /// <param name="prefixes">The prefixes to check.</param>
    /// <param name="paramName">The name of the parameter that holds the prefixes.</param>
    /// <returns>A copy of the prefixes and their common length.</returns>
    /// <exception cref="ArgumentException">Thrown when a prefix is empty or the lengths differ.</exception>
    public static (List<string> Prefixes, int Length) Validate(
        IReadOnlyCollection<string> prefixes,
        string paramName
    )
    {
        ArgumentNullException.ThrowIfNull(prefixes);

        var list = new List<string>(prefixes);
        if (list.Count == 0 || list[0].Length == 0)
            throw new ArgumentException("At least one non-empty prefix is required.", paramName);

        var length = list[0].Length;
        if (list.Any(prefix => prefix.Length != length))
            throw new ArgumentException("The prefixes must all have the same length.", paramName);

        return (list, length);
    }
}
