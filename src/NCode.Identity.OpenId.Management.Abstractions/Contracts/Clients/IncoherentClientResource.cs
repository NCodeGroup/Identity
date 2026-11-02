// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Clients;

/// <summary>
/// Represents the REST resource for a stored client that the effective settings of its tenant make incoherent, so it
/// fails to authenticate or to be honored at its next request.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class IncoherentClientResource : ISupportClientId, ISupportTenantId
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <inheritdoc cref="ISupportClientId.ClientId"/>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the violations the registered rules find in the client's settings; empty when the settings document cannot
    /// be honored.
    /// </summary>
    public required IReadOnlyList<ClientCoherenceViolationResource> Violations { get; init; }

    /// <summary>
    /// Gets the reason the client's settings document cannot be honored, or <c>null</c> when it can be.
    /// </summary>
    public string? DocumentError { get; init; }
}
