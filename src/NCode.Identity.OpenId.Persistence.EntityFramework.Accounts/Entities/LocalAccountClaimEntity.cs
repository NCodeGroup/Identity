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
