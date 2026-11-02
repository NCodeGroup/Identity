// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// The local path prefixes of the remote APIs mapped so far, so that one mapped in code cannot overlap one mapped from
/// configuration.
/// </summary>
internal sealed class BffRemoteApiRegistry
{
    private Lock Gate { get; } = new();

    private List<string> Paths { get; } = [];

    /// <summary>
    /// Records a remote API's path prefix unless it overlaps one recorded before.
    /// </summary>
    /// <param name="path">The path prefix to record.</param>
    /// <param name="overlapping">The recorded path prefix that <paramref name="path"/> overlaps, when it does.</param>
    /// <returns><c>true</c> when the path prefix was recorded.</returns>
    public bool TryRegister(string path, [NotNullWhen(false)] out string? overlapping)
    {
        lock (Gate)
        {
            overlapping = Paths.FirstOrDefault(other =>
                BffRemoteApiValidation.Overlaps(path, other)
            );
            if (overlapping is not null)
                return false;

            Paths.Add(path);
            return true;
        }
    }
}
