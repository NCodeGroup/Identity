// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Represents the limits a promoted settings document changes in one existing target document, and the node whose
/// <c>Update</c> authority the change needs.
/// </summary>
// See ADR-1425.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantPromotionLimitChange
{
    /// <summary>
    /// Gets the node above the changed document: the caller needs <c>Update</c> authority there to change its limits.
    /// </summary>
    public required ISupportTenantId AuthorityAbove { get; init; }

    /// <summary>
    /// Gets the names of the limits the promoted document changes.
    /// </summary>
    public required List<string> SettingNames { get; init; }
}
