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
        using var document = JsonDocument.Parse(modulesText);

        if (
            document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("packageManager", out var property)
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
