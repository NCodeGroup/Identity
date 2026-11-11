// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.Claims;

/// <summary>
/// Contains the serialized representation of a <see cref="ClaimsPrincipal"/> instance.
/// </summary>
public class SerializableClaimsPrincipal
{
    /// <summary>
    /// Gets the value for the <see cref="ClaimsPrincipal.Identities"/> property.
    /// </summary>
    public required IReadOnlyCollection<SerializableClaimsIdentity> Identities { get; init; }
}
