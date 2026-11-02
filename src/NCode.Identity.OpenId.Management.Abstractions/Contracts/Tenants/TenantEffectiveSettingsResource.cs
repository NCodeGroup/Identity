// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents the REST resource for a tenant's <b>effective</b> (merged) settings: the server baseline, the tenant's
/// persisted overrides, and descriptor defaults, resolved through the settings merge pipeline. This is the read-only
/// view the authorization server itself resolves at runtime, and — unlike the discovery document — it includes
/// settings that are not advertised (not discoverable). It is the gated replacement for the removed anonymous
/// discovery override.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantEffectiveSettingsResource : ISupportTenantId
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the tenant's effective settings as a flat JSON object of setting name to formatted value, using the same
    /// value representation as the discovery document.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
