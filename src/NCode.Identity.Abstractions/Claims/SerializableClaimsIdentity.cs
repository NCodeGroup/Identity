// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;

namespace NCode.Identity.Claims;

/// <summary>
/// Contains the serialized representation of a <see cref="ClaimsIdentity"/> instance.
/// </summary>
public class SerializableClaimsIdentity
{
    /// <summary>
    /// Gets the unique reference identifier for the <see cref="ClaimsIdentity"/> instance.
    /// This value is used for <c>subject</c> references from <see cref="Claim"/>.
    /// </summary>
    public required string ReferenceId { get; init; }

    /// <summary>
    /// Gets the value for the <see cref="ClaimsIdentity.Label"/> property.
    /// </summary>
    public required string? Label { get; init; }

    /// <summary>
    /// Gets the value for the <see cref="ClaimsIdentity.BootstrapContext"/> property.
    /// </summary>
    public required JsonElement BootstrapContext { get; init; }

    /// <summary>
    /// Gets the value for the <see cref="ClaimsIdentity.AuthenticationType"/> property.
    /// </summary>
    public required string? AuthenticationType { get; init; }

    /// <summary>
    /// Gets the value for the <see cref="ClaimsIdentity.NameClaimType"/> property.
    /// </summary>
    public required string? NameClaimType { get; init; }

    /// <summary>
    /// Gets the value for the <see cref="ClaimsIdentity.RoleClaimType"/> property.
    /// </summary>
    public required string? RoleClaimType { get; init; }

    /// <summary>
    /// Gets the value for the <see cref="ClaimsIdentity.Actor"/> property.
    /// </summary>
    public required SerializableClaimsIdentity? Actor { get; init; }

    /// <summary>
    /// Gets the value for the <see cref="ClaimsIdentity.Claims"/> property.
    /// </summary>
    public required IReadOnlyCollection<SerializableClaim> Claims { get; init; }
}
