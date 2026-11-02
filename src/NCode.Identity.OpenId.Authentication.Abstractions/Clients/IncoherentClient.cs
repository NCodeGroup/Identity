// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Describes a stored client whose settings the effective settings of its tenant make incoherent, as an
/// <see cref="IClientCoherenceReporter"/> finds it.
/// </summary>
// See ADR-1233.
[PublicAPI]
public sealed class IncoherentClient
{
    /// <summary>
    /// Gets the identifier of the client.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the violations the registered rules find in the client's settings; empty when the settings document cannot
    /// be honored.
    /// </summary>
    public required IReadOnlyList<ClientCoherenceViolation> Violations { get; init; }

    /// <summary>
    /// Gets the reason the client's settings document cannot be honored, or <c>null</c> when it can be.
    /// </summary>
    public string? DocumentError { get; init; }
}
