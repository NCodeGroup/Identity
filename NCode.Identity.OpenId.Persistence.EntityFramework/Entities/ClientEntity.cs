#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

[Index(nameof(TenantId), nameof(NormalizedClientId), IsUnique = true)]
internal class ClientEntity : ISupportId, ISupportTenant, ISupportConcurrencyToken
{
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    [ForeignKey(nameof(Tenant))]
    public required long TenantId { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.ClientId)]
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets or sets the value of <see cref="ClientId"/> in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ClientId)]
    public required string NormalizedClientId { get; init; }

    //

    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    public required bool IsDisabled { get; set; }

    public required JsonElement Settings { get; set; }

    //

    public required TenantEntity Tenant { get; init; }

    public required IEnumerable<ClientUrlEntity> Urls { get; init; }

    public required IEnumerable<ClientSecretEntity> Secrets { get; init; }
}
