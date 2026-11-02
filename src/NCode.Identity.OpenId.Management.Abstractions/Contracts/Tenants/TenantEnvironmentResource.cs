// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents a tenant's effective environment within a <see cref="TenantResource"/>.
/// </summary>
// See ADR-1289.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantEnvironmentResource
{
    /// <summary>
    /// Gets the effective <c>environment_type</c>, such as <c>production</c> or <c>development</c>.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the effective <c>environment_name</c>, a display label, or <see langword="null"/> when there is none.
    /// </summary>
    public required string? Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the registered type is production-grade
    /// (its <c>ITenantEnvironmentType.IsProduction</c>). The vocabulary of types is open, so a client tells
    /// production from every other environment by this value rather than by the type's name.
    /// </summary>
    public required bool IsProduction { get; init; }
}
