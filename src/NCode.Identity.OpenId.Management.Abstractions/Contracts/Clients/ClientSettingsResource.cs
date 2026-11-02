// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Clients;

/// <summary>
/// Represents the REST resource for a <see cref="PersistedClientSettings"/> instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ClientSettingsResource
    : ISupportClientId,
        ISupportTenantId,
        ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <inheritdoc cref="ISupportClientId.ClientId"/>
    public required string ClientId { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <inheritdoc cref="PersistedClientResource{TValue}.Value"/>
    public required JsonElement Settings { get; init; }
}
