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
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

[Index(nameof(TenantId), nameof(ClientId), nameof(SecretId), IsUnique = true)]
internal class ClientSecretEntity : ISupportId, ISupportTenant, ISupportSecret
{
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    [ForeignKey(nameof(Tenant))]
    public required long TenantId { get; init; }

    [ForeignKey(nameof(Client))]
    public required long ClientId { get; init; }

    [ForeignKey(nameof(Secret))]
    public required long SecretId { get; init; }

    //

    public required TenantEntity Tenant { get; init; }

    public required ClientEntity Client { get; init; }

    public required SecretEntity Secret { get; init; }
}
