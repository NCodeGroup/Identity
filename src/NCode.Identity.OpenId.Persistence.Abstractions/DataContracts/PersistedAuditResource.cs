// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains an entity an audited operation acted on, as an audit store keeps it.
/// </summary>
// See ADR-1303.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedAuditResource
{
    /// <summary>
    /// Gets the type of the resource, such as <c>client</c>. Resource types are an open vocabulary.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.AuditName)]
    public required string Type { get; init; }

    /// <summary>
    /// Gets the identifier of the resource, or <see langword="null"/> when it has none or it is longer than the store
    /// indexes.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public string? Id { get; init; }
}
