// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents the environment-bound setting values for one resource a promotion writes to the target tenant.
/// </summary>
// See ADR-1425.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantPromotionEnvironmentSettings
{
    /// <summary>
    /// Gets the type of the resource, such as <c>tenant</c>, <c>resource_server</c> or <c>client</c>.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    /// Gets the identifier of the resource in the target tenant.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// Gets the resource's environment-bound settings as a JSON object of setting name to value. It carries only
    /// environment-bound settings.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
