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
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

[Index(nameof(TenantId), nameof(GrantType), nameof(HashedKey), IsUnique = true)]
[Index(nameof(TenantId), nameof(ClientId), IsUnique = false)]
[Index(nameof(TenantId), nameof(NormalizedSubjectId), IsUnique = false)]
internal class GrantEntity : ISupportId, ISupportConcurrencyToken
{
    [Key]
    public required long Id { get; init; }

    [ForeignKey(nameof(Tenant))]
    public required long TenantId { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.GrantType)]
    public required string GrantType { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.HashedKey)]
    public required string HashedKey { get; init; }

    //

    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    [ForeignKey(nameof(Client))]
    public required long? ClientId { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.SubjectId)]
    public required string? SubjectId { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.SubjectId)]
    public required string? NormalizedSubjectId { get; init; }

    public required DateTimeOffset CreatedWhen { get; init; }

    public required DateTimeOffset ExpiresWhen { get; init; }

    public required DateTimeOffset? ConsumedWhen { get; set; }

    public required JsonElement Payload { get; init; }

    //

    public required TenantEntity Tenant { get; init; }

    public required ClientEntity? Client { get; init; }
}
