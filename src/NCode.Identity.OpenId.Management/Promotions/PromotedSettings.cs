// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Contains a settings document composed for a promotion and the names of the settings the composition left out.
/// </summary>
// See ADR-1425.
internal sealed class PromotedSettings
{
    /// <summary>
    /// Gets the composed settings document.
    /// </summary>
    public required JsonElement Document { get; init; }

    /// <summary>
    /// Gets the names of the environment-bound settings the source sets and the composed document lacks.
    /// </summary>
    public required List<string> UnsetSettingNames { get; init; }

    /// <summary>
    /// Gets the names of the supplied settings the composition refused because they are not environment-bound.
    /// </summary>
    public required List<string> RefusedSettingNames { get; init; }
}
