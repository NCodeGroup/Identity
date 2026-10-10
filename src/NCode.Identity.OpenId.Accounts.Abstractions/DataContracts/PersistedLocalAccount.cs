#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Accounts.DataContracts;

/// <summary>
/// Contains the data for a persisted local account: the server-owned credential, profile, and status payload behind a
/// self-issued connection identity. The <see cref="LocalAccountId"/> is both this record's opaque public identifier and
/// the <c>subject</c> of its one-to-one self-issued federated identity, from which the owning principal is resolved.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedLocalAccount : ISupportTenantId, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the identifier of the tenant that owns this account.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the server-generated opaque public identifier of this local account, used as the subject of its
    /// self-issued federated identity.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string LocalAccountId { get; init; }

    /// <summary>
    /// Gets or sets the account's username (login handle), unique within the tenant.
    /// </summary>
    [MaxLength(AccountMaxLengths.UserName)]
    public required string UserName { get; set; }

    /// <summary>
    /// Gets or sets the account's email address, or <c>null</c> when none is recorded. A verified email serves as the
    /// join key used by the linking policy to attach the account to an existing principal.
    /// </summary>
    [MaxLength(AccountMaxLengths.Email)]
    public required string? Email { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account's <see cref="Email"/> has been verified.
    /// </summary>
    public required bool EmailVerified { get; set; }

    /// <summary>
    /// Gets or sets the account's hashed password, or <c>null</c> when the account has no password (such as a
    /// passwordless account).
    /// </summary>
    [MaxLength(AccountMaxLengths.PasswordHash)]
    public required string? PasswordHash { get; set; }

    /// <summary>
    /// Gets or sets the account's security stamp, changed whenever credentials change so that outstanding sessions and
    /// tokens can be invalidated.
    /// </summary>
    [MaxLength(AccountMaxLengths.SecurityStamp)]
    public required string SecurityStamp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account is enabled.
    /// </summary>
    public required bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the account's profile claims.
    /// </summary>
    public required IReadOnlyList<PersistedLocalAccountClaim> Claims { get; set; }

    /// <inheritdoc />
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string ConcurrencyToken { get; set; }
}
