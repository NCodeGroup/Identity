// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents a note about a resource a promotion covers or leaves out. A note never refuses the promotion.
/// </summary>
// See ADR-1425.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantPromotionNoteResource
{
    /// <summary>
    /// Gets the type of the resource the note is about.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    /// Gets the identifier of the resource the note is about.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// Gets the code of the note. See <see cref="TenantPromotionNoteCodes"/> for the known values.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the human-readable detail of the note.
    /// </summary>
    public required string Detail { get; init; }

    /// <summary>
    /// Gets the name of the setting the note is about, when it is about one.
    /// </summary>
    public string? SettingName { get; init; }
}
