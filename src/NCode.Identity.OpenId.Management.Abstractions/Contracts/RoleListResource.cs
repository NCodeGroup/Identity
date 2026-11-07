// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts;

/// <summary>
/// Represents the principals that hold one role at a node and whether the caller may grant and revoke that role there.
/// </summary>
// See ADR-1465.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class RoleListResource
{
    /// <summary>
    /// Gets the assignments of the role at the node.
    /// </summary>
    public required IReadOnlyList<RoleHolderResource> Items { get; init; }

    /// <summary>
    /// Gets a value indicating whether the caller may grant and revoke the role at the node: whether a grant or a
    /// revoke by the caller passes every refusal that does not depend on the principal it names.
    /// </summary>
    public required bool CanChange { get; init; }
}
