// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Buffers;

namespace NCode.Identity.OpenId.LocalDev.Frontend;

/// <summary>
/// Detects whether the installed pnpm workspace still matches the repository, using only the files that pnpm itself
/// writes into <c>node_modules</c>.
/// </summary>
internal static class WorkspaceInstall
{
    /// <summary>
    /// Finds the reasons why the installed pnpm workspace is stale or missing.
    /// </summary>
    /// <param name="frontendDirectory">The directory that contains the frontend workspace.</param>
    /// <param name="pnpmVersion">The pinned pnpm version without the <c>pnpm@</c> prefix.</param>
    /// <returns>The human-readable problems, which is empty when the install is healthy.</returns>
    internal static IReadOnlyList<string> FindProblems(string frontendDirectory, string pnpmVersion)
    {
        var modulesPath = Path.Combine(frontendDirectory, "node_modules", ".modules.yaml");
        if (!File.Exists(modulesPath))
        {
            return
            [
                "The pnpm workspace is not installed (src/frontend/node_modules/.modules.yaml is missing).",
            ];
        }

        string? installedPackageManager;
        try
        {
            installedPackageManager = ReadPackageManager(
                File.ReadAllText(modulesPath, SecureEncoding.UTF8)
            );
        }
        catch (JsonException)
        {
            return
            [
                "src/frontend/node_modules/.modules.yaml is unreadable, so the workspace install cannot be verified.",
            ];
        }

        var problems = new List<string>();

        var expectedPackageManager = $"pnpm@{pnpmVersion}";
        if (
            !string.Equals(
                installedPackageManager,
                expectedPackageManager,
                StringComparison.Ordinal
            )
        )
        {
            problems.Add(
                $"node_modules was installed with {installedPackageManager ?? "an unknown pnpm"}, but src/frontend/package.json pins {expectedPackageManager}."
            );
        }

        var lockPath = Path.Combine(frontendDirectory, "pnpm-lock.yaml");
        if (File.Exists(lockPath))
        {
            var installedLockPath = Path.Combine(
                frontendDirectory,
                "node_modules",
                ".pnpm",
                "lock.yaml"
            );

            if (
                !File.Exists(installedLockPath)
                || !string.Equals(
                    ReadNormalized(lockPath),
                    ReadNormalized(installedLockPath),
                    StringComparison.Ordinal
                )
            )
            {
                problems.Add(
                    "pnpm-lock.yaml changed since the last install (new or updated dependencies or workspace packages)."
                );
            }
        }

        return problems;
    }

    private static string? ReadPackageManager(string modulesText)
    {
        var jsonElement = JsonElement.Parse(modulesText);

        if (
            jsonElement.ValueKind == JsonValueKind.Object
            && jsonElement.TryGetProperty("packageManager", out var property)
            && property.ValueKind == JsonValueKind.String
        )
        {
            return property.GetString();
        }

        return null;
    }

    private static string ReadNormalized(string path)
    {
        return File.ReadAllText(path, SecureEncoding.UTF8).ReplaceLineEndings("\n");
    }
}
