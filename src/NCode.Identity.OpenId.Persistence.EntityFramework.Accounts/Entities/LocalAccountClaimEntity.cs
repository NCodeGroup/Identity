// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Entities;

/// <summary>
/// Entity framework data contract for a single profile claim owned by a <see cref="LocalAccountEntity"/>. The foreign
/// key to the owning account is a shadow property configured by the model contributor.
/// </summary>
internal sealed class LocalAccountClaimEntity : ISupportTenantEntity
{
    [Key]
    public required long Id { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedTenantId { get; init; }

    [Unicode(false)]
    [MaxLength(AccountMaxLengths.ClaimType)]
    public required string Type { get; init; }

    [MaxLength(AccountMaxLengths.ClaimValue)]
    public required string Value { get; init; }
}
