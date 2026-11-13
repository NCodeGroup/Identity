// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts;

/// <summary>
/// Represents the request to grant a principal the role of a role list at its node.
/// </summary>
// See ADR-1465.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class AddRoleHolderRequest
{
    /// <summary>
    /// Gets the identifier of the principal to grant the role to: the <c>principalId</c> of the resource of the local account or client to grant, which names the principal that account or client acts as.
    /// </summary>
    public required string PrincipalId { get; init; }
}
