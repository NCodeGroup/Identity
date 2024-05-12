#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

[Index(nameof(NormalizedTenantId), IsUnique = true)]
[Index(nameof(NormalizedDomainName), IsUnique = true)]
internal class TenantEntity : ISupportId, ISupportConcurrencyToken
{
    [Key]
    public required long Id { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.TenantId)]
    public required string TenantId { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.TenantId)]
    public required string NormalizedTenantId { get; init; }

    //

    [Unicode(false)]
    [MaxLength(MaxLengths.TenantDomainName)]
    public required string? DomainName { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.TenantDomainName)]
    public required string? NormalizedDomainName { get; init; }

    //

    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    public required bool IsDisabled { get; set; }

    [Unicode]
    public required string DisplayName { get; set; }

    public required JsonElement Settings { get; set; }

    //

    public required IEnumerable<TenantSecretEntity> Secrets { get; init; }
}
