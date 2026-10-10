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

    public ICollection<LocalAccountClaimEntity> Claims { get; init; } =
        new List<LocalAccountClaimEntity>();
}
