// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted scope (a permission, in Auth0 terms) owned by a resource server.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedScope
{
    /// <summary>
    /// Gets the value of this scope (for example, <c>read:messages</c>).
    /// </summary>
    [MaxLength(OpenIdMaxLengths.ScopeValue)]
    public required string Value { get; init; }

    /// <summary>
    /// Gets or sets the human-readable description for this scope.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.Description)]
    public required string? Description { get; set; }

    /// <summary>
    /// Gets a value indicating whether this scope is a reserved, system-owned scope that cannot be deleted.
    /// </summary>
    public required bool IsSystem { get; init; }
}
