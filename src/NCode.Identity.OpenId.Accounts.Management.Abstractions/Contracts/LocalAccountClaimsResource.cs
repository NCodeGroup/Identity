// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Accounts.Management.Contracts;

/// <summary>
/// Represents the REST resource for a local account's full set of profile claims.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class LocalAccountClaimsResource : ISupportTenantId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the opaque identifier of the local account (its self-issued subject).
    /// </summary>
    public required string LocalAccountId { get; init; }

    /// <summary>
    /// Gets the account's profile claims.
    /// </summary>
    public required IReadOnlyList<LocalAccountClaim> Claims { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }
}
