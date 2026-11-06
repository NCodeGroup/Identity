// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Persistence;

/// <summary>
/// Contains the format of a team path: it names a team by the identifiers of the teams from its top-level team down to
/// itself, each followed by <c>/</c>, after a leading <c>/</c> (such as <c>/acme/acme-web/</c>).
/// </summary>
// See ADR-1426.
[PublicAPI]
public static class TeamPaths
{
    private const char Separator = '/';

    /// <summary>
    /// Specifies the deepest a team is nested. A top-level team is at depth 1.
    /// </summary>
    public const int MaxDepth = 8;

    /// <summary>
    /// Formats the specified team identifiers, top-level team first, as a team path.
    /// </summary>
    /// <param name="teamIds">The identifiers of the teams from the top-level team down to the team itself.</param>
    /// <returns>The team path.</returns>
    /// <exception cref="ArgumentException">
    /// The sequence is empty, or an identifier is empty or contains <c>/</c>.
    /// </exception>
    public static string Format(IEnumerable<string> teamIds)
    {
        ArgumentNullException.ThrowIfNull(teamIds);

        var ids = teamIds.ToList();
        if (ids.Count == 0)
            throw new ArgumentException(
                "A team path holds at least one team identifier.",
                nameof(teamIds)
            );

        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id) || id.Contains(Separator))
                throw new ArgumentException(
                    "A team identifier is not empty and does not contain '/'.",
                    nameof(teamIds)
                );
        }

        return Separator + string.Join(Separator, ids) + Separator;
    }

    /// <summary>
    /// Parses the specified team path into the identifiers of the teams it names, top-level team first.
    /// </summary>
    /// <param name="teamPath">The team path to parse.</param>
    /// <returns>The team identifiers, top-level team first.</returns>
    /// <exception cref="ArgumentException">
    /// The path does not start and end with <c>/</c>, holds no team, or holds an empty segment.
    /// </exception>
    public static IReadOnlyList<string> Parse(string teamPath)
    {
        ArgumentNullException.ThrowIfNull(teamPath);

        if (teamPath.Length < 3 || teamPath[0] != Separator || teamPath[^1] != Separator)
            throw new ArgumentException(
                "A team path starts and ends with '/' and holds at least one team identifier.",
                nameof(teamPath)
            );

        var ids = teamPath[1..^1].Split(Separator);
        if (ids.Any(string.IsNullOrEmpty))
            throw new ArgumentException("A team path holds no empty segment.", nameof(teamPath));

        return ids;
    }
}
