// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.AspNetCore;

/// <summary>
/// Contains the names of the files and directories that a single-page bundle consists of.
/// </summary>
[PublicAPI]
public static class SinglePageBundleDefaults
{
    /// <summary>
    /// The name of the directory beneath the bundle's root that holds its content-hashed files, which are the only
    /// files a <see cref="SinglePageBundle{TIndex}"/> serves besides the index.
    /// </summary>
    public const string AssetsDirectory = "assets";

    /// <summary>
    /// The name of the bundle's index file, which sits directly beneath the bundle's root.
    /// </summary>
    public const string IndexFileName = "index.html";
}
