// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts;

/// <summary>
/// Represents the request to grant a principal the role of a role list at its node.
/// </summary>
// See ADR-1469.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class AddRoleHolderRequest
{
    /// <summary>
    /// Gets the identifier of the principal to grant the role to.
    /// </summary>
    public required string PrincipalId { get; init; }
}
