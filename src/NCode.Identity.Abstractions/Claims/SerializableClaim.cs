// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.Claims;

/// <summary>
/// Contains the serialized representation of a <see cref="Claim"/> instance.
/// </summary>
public class SerializableClaim
{
    /// <summary>
    /// Gets the value for the <see cref="Claim.Type"/> property.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the value for the <see cref="Claim.Value"/> property.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the value for the <see cref="Claim.ValueType"/> property.
    /// </summary>
    public required string? ValueType { get; init; }

    /// <summary>
    /// Gets the value for the <see cref="Claim.Issuer"/> property.
    /// </summary>
    public required string? Issuer { get; init; }

    /// <summary>
    /// Gets the value for the <see cref="Claim.OriginalIssuer"/> property.
    /// </summary>
    public required string? OriginalIssuer { get; init; }

    /// <summary>
    /// Gets the value for the <see cref="Claim.Properties"/> property.
    /// </summary>
    public required IDictionary<string, string> Properties { get; init; }

    /// <summary>
    /// Gets the reference identifier of the owning subject identity (the <see cref="Claim.Subject"/>), matching the
    /// <see cref="SerializableClaimsIdentity.ReferenceId"/> of the identity this claim belongs to.
    /// </summary>
    public string? SubjectRef { get; init; }
}
