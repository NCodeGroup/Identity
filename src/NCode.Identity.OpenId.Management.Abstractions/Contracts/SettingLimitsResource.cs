// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts;

/// <summary>
/// Represents the limits a settings document may carry and whether the caller may set them. A limit is set by the
/// authority above the scope whose document carries it.
/// </summary>
// See ADR-1448.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SettingLimitsResource
{
    /// <summary>
    /// Gets the names of the limits the settings may carry, in ordinal order.
    /// </summary>
    public required IReadOnlyList<string> Names { get; init; }

    /// <summary>
    /// Gets a value indicating whether the caller may set the limits: whether it holds update authority at the node
    /// above the one whose settings carry them.
    /// </summary>
    public required bool CanChange { get; init; }
}
