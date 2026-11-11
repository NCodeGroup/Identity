// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using NCode.Buffers;

namespace NCode.Identity.OpenId.LocalDev.Frontend;

/// <summary>
/// Provides the toolchain versions that the frontend workspace pins: the Node major version from
/// <c>.node-version</c> and the pnpm version from the <c>packageManager</c> field of <c>package.json</c>.
/// </summary>
/// <param name="NodeMajor">The pinned Node major version.</param>
/// <param name="PnpmVersion">The pinned pnpm version without the <c>pnpm@</c> prefix or any integrity hash.</param>
internal sealed record FrontendPins(int NodeMajor, string PnpmVersion)
{
    private const string PnpmPrefix = "pnpm@";

    /// <summary>
    /// Parses the Node major version from the text of a version string such as <c>26</c>, <c>v26.1.0</c> or
    /// <c>26.1.0</c>.
    /// </summary>
    /// <param name="text">The version text, which may carry a leading <c>v</c> and surrounding whitespace.</param>
    /// <returns>The major version, or <see langword="null"/> when the text does not start with a number.</returns>
    internal static int? ParseNodeMajor(string text)
    {
        var trimmed = text.Trim().TrimStart('v');
        var dotIndex = trimmed.IndexOf('.', StringComparison.Ordinal);
        var part = dotIndex < 0 ? trimmed : trimmed[..dotIndex];

        return int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            ? major
            : null;
    }

    /// <summary>
    /// Parses the pins from the text of <c>.node-version</c> and <c>package.json</c>.
    /// </summary>
    /// <param name="nodeVersionText">The text of <c>.node-version</c>.</param>
    /// <param name="packageJsonText">The text of <c>package.json</c>.</param>
    /// <returns>The pinned Node major and pnpm version.</returns>
    /// <exception cref="InvalidOperationException">
    /// The Node version does not start with a major number, or <c>packageManager</c> does not pin pnpm.
    /// </exception>
    internal static FrontendPins Parse(string nodeVersionText, string packageJsonText)
    {
        var nodeMajor =
            ParseNodeMajor(nodeVersionText)
            ?? throw new InvalidOperationException(
                $"frontend/.node-version must start with the Node major number, found '{nodeVersionText.Trim()}'."
            );

        var jsonElement = JsonElement.Parse(packageJsonText);

        string? packageManager = null;
        if (
            jsonElement.ValueKind == JsonValueKind.Object
            && jsonElement.TryGetProperty("packageManager", out var property)
            && property.ValueKind == JsonValueKind.String
        )
        {
            packageManager = property.GetString();
        }

        if (
            packageManager is null
            || !packageManager.StartsWith(PnpmPrefix, StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                $"frontend/package.json must pin pnpm in its \"packageManager\" field, found '{packageManager ?? "missing"}'."
            );
        }

        var version = packageManager[PnpmPrefix.Length..];
        var plusIndex = version.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex >= 0)
        {
            version = version[..plusIndex];
        }

        return new FrontendPins(nodeMajor, version);
    }

    /// <summary>
    /// Reads the pins from the <c>.node-version</c> and <c>package.json</c> files of a frontend directory.
    /// </summary>
    /// <param name="frontendDirectory">The directory that contains the frontend workspace.</param>
    /// <returns>The pinned Node major and pnpm version.</returns>
    internal static FrontendPins Read(string frontendDirectory)
    {
        return Parse(
            File.ReadAllText(Path.Combine(frontendDirectory, ".node-version"), SecureEncoding.UTF8),
            File.ReadAllText(Path.Combine(frontendDirectory, "package.json"), SecureEncoding.UTF8)
        );
    }
}
