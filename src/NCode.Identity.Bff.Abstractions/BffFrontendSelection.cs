// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Describes what a frontend's selection rule is given: the rule's configured values, the frontend's public origin, and
/// the local path prefixes the frontend serves (its endpoint prefix, then each remote API's path).
/// </summary>
/// <param name="values">The values that the frontend configures for the selection rule.</param>
/// <param name="publicOrigin">The public origin of the frontend.</param>
/// <param name="paths">The local path prefixes that the frontend serves.</param>
[PublicAPI]
public sealed class BffFrontendSelection(
    IReadOnlyList<string> values,
    string publicOrigin,
    IReadOnlyList<string> paths
)
{
    /// <summary>
    /// Gets the values that the frontend configures for the selection rule.
    /// </summary>
    public IReadOnlyList<string> Values { get; } =
    [.. values ?? throw new ArgumentNullException(nameof(values))];

    /// <summary>
    /// Gets the public origin of the frontend.
    /// </summary>
    public string PublicOrigin { get; } =
        publicOrigin ?? throw new ArgumentNullException(nameof(publicOrigin));

    /// <summary>
    /// Gets the local path prefixes that the frontend serves: its endpoint prefix, then the path of each remote API.
    /// </summary>
    public IReadOnlyList<string> Paths { get; } =
    [.. paths ?? throw new ArgumentNullException(nameof(paths))];
}
