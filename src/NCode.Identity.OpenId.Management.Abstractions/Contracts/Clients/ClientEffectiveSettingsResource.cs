// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Clients;

/// <summary>
/// Represents the REST resource for an OpenID Client's <b>effective</b> (merged) settings: the client's persisted
/// settings merged onto the tenant's effective settings through the settings merge pipeline (the same view the
/// authorization server resolves for the client at runtime), including settings that are not advertised in discovery.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ClientEffectiveSettingsResource : ISupportClientId, ISupportTenantId
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <inheritdoc cref="ISupportClientId.ClientId"/>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the client's effective settings as a flat JSON object of setting name to formatted value, using the same
    /// value representation as the discovery document.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
