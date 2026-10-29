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

namespace NCode.Identity.OpenId.LocalDev.Frontend;

/// <summary>
/// Provides lookup of Node installs that NVS (Node Version Switcher) lays out on disk.
/// </summary>
internal static class NvsNode
{
    /// <summary>
    /// Finds the <c>x64</c> directory of the newest NVS install of a Node major version.
    /// </summary>
    /// <param name="nvsNodeRoot">The NVS <c>node</c> root directory, which holds one directory per version.</param>
    /// <param name="major">The Node major version to find.</param>
    /// <returns>
    /// The <c>x64</c> directory of the highest installed version of <paramref name="major"/>, or
    /// <see langword="null"/> when the root does not exist or has no such install.
    /// </returns>
    internal static string? FindDirectory(string nvsNodeRoot, int major)
    {
        if (!Directory.Exists(nvsNodeRoot))
            return null;

        // NVS lays each install out as <root>/<version>/<arch>; take the x64 directory of the highest version of the
        // major. Versions compare numerically (22.9.0 < 22.22.2), never as strings. build/frontend-install.ps1
        // installs the pinned pnpm into this same directory, so it resolves alongside node.
        return Directory
            .EnumerateDirectories(nvsNodeRoot, $"{major}.*")
            .Select(versionDir =>
                (
                    VersionDir: versionDir,
                    Version: Version.TryParse(Path.GetFileName(versionDir), out var version)
                        ? version
                        : null
                )
            )
            .Where(install => install.Version?.Major == major)
            .OrderByDescending(install => install.Version)
            .Select(install => Path.Combine(install.VersionDir, "x64"))
            .FirstOrDefault(Directory.Exists);
    }
}
