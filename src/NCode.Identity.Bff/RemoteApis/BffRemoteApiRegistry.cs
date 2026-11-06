// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// The local path prefixes of the remote APIs mapped so far, with the frontend that each belongs to, so that one mapped
/// in code cannot overlap one mapped from configuration.
/// </summary>
/// <remarks>
/// A remote API that belongs to no frontend serves whichever frontend a request selects, so it conflicts with every
/// overlapping path. Remote APIs of two different frontends may share a path.
/// </remarks>
internal sealed class BffRemoteApiRegistry
{
    private Lock Gate { get; } = new();

    private List<(string Path, string? Frontend)> Entries { get; } = [];

    /// <summary>
    /// Records a remote API's path prefix unless it overlaps one recorded before for the same frontend or for any
    /// frontend.
    /// </summary>
    /// <param name="path">The path prefix to record.</param>
    /// <param name="frontend">The name of the frontend that the remote API belongs to, or <c>null</c> for none.</param>
    /// <param name="overlapping">The recorded path prefix that <paramref name="path"/> overlaps, when it does.</param>
    /// <returns><c>true</c> when the path prefix was recorded.</returns>
    public bool TryRegister(
        string path,
        string? frontend,
        [NotNullWhen(false)] out string? overlapping
    )
    {
        lock (Gate)
        {
            overlapping = null;
            foreach (var entry in Entries)
            {
                var sharesFrontend =
                    frontend is null
                    || entry.Frontend is null
                    || string.Equals(frontend, entry.Frontend, StringComparison.Ordinal);
                if (!sharesFrontend || !BffRemoteApiValidation.Overlaps(path, entry.Path))
                    continue;

                overlapping = entry.Path;
                return false;
            }

            Entries.Add((path, frontend));
            return true;
        }
    }
}
