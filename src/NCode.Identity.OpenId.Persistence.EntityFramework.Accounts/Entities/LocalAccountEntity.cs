// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Entities;

/// <summary>
/// Entity framework data contract for a local account: the server-owned credential, profile, and status payload behind
/// a self-issued connection. The complimentary DTO is <c>PersistedLocalAccount</c>. A local account is tenant-scoped.
/// </summary>
[Index(nameof(NormalizedTenantId), nameof(NormalizedLocalAccountId), IsUnique = true)]
[Index(nameof(NormalizedTenantId), nameof(NormalizedUserName), IsUnique = true)]
[Index(nameof(NormalizedTenantId), nameof(NormalizedEmail))]
internal sealed class LocalAccountEntity : ISupportTenantEntity, ISupportConcurrencyToken
{
    [Key]
    public required long Id { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedTenantId { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string LocalAccountId { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedLocalAccountId { get; init; }

    [Unicode(false)]
    [MaxLength(AccountMaxLengths.UserName)]
    public required string UserName { get; set; }

    [Unicode(false)]
    [MaxLength(AccountMaxLengths.UserName)]
    public required string NormalizedUserName { get; set; }

    [MaxLength(AccountMaxLengths.Email)]
    public required string? Email { get; set; }

    [Unicode(false)]
    [MaxLength(AccountMaxLengths.Email)]
    public required string? NormalizedEmail { get; set; }

    public required bool EmailVerified { get; set; }

    [Unicode(false)]
    [MaxLength(AccountMaxLengths.PasswordHash)]
    public required string? PasswordHash { get; set; }

    [Unicode(false)]
    [MaxLength(AccountMaxLengths.SecurityStamp)]
    public required string SecurityStamp { get; set; }

    public required bool IsEnabled { get; set; }

    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    public ICollection<LocalAccountClaimEntity> Claims { get; init; } = [];
}
