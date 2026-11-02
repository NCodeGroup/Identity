// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Principals;

/// <summary>
/// Represents the REST resource for an end user's federated principal: the server-owned end user that owns its
/// connections, and whether an administrator blocked it.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PrincipalResource : ISupportTenantId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the server-owned identifier of the end user, emitted as the <c>sub</c> claim.
    /// </summary>
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets a value indicating whether an administrator blocked the end user.
    /// </summary>
    public required bool IsBlocked { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }
}
