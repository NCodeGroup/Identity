// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Describes the outcome of removing an owner from a resource and the assignments the removal deleted.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class OwnerRemoval
{
    /// <summary>
    /// Gets the outcome of the removal.
    /// </summary>
    public required OwnerRemovalResult Result { get; init; }

    /// <summary>
    /// Gets the owner assignments the removal deleted. The list is empty unless <see cref="Result"/> is
    /// <see cref="OwnerRemovalResult.Removed"/>.
    /// </summary>
    public required IReadOnlyList<PersistedRoleAssignment> RemovedAssignments { get; init; }
}
