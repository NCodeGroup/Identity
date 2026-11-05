// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Clients;

/// <summary>
/// Represents the REST resource for a <see cref="PersistedClient"/> instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ClientResource : ISupportClientId, ISupportTenantId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <inheritdoc cref="ISupportClientId.ClientId"/>
    public required string ClientId { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <inheritdoc cref="PersistedClient.IsDisabled"/>
    public required bool IsDisabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the host configures this client, in which case the management API refuses
    /// every change to it.
    /// </summary>
    public required bool IsHostManaged { get; init; }

    /// <inheritdoc cref="PersistedClient.ResourceServerId"/>
    public string? ResourceServerId { get; init; }
}
