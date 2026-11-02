// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Accounts.Management.Contracts;

/// <summary>
/// Represents the REST resource for a local account: the server-owned credential, profile, and status payload behind a
/// self-issued connection. The credential (password hash) and security stamp are never exposed.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class LocalAccountResource : ISupportTenantId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the opaque identifier of the local account (its self-issued subject).
    /// </summary>
    public required string LocalAccountId { get; init; }

    /// <summary>
    /// Gets the account's username (login handle), unique within the tenant.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the account's email address, or <c>null</c> when none is recorded.
    /// </summary>
    public required string? Email { get; init; }

    /// <summary>
    /// Gets a value indicating whether the account's <see cref="Email"/> is verified.
    /// </summary>
    public required bool EmailVerified { get; init; }

    /// <summary>
    /// Gets a value indicating whether the account is enabled.
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }
}
