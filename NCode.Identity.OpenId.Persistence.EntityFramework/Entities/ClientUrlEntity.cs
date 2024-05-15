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

/// <summary>
/// Represents an entity framework data contract for a relationship between a <see cref="ClientEntity"/> and a URL.
/// </summary>
[Index(nameof(TenantId), nameof(ClientId), nameof(UrlType), nameof(UrlValue), IsUnique = true)]
public class ClientUrlEntity : ISupportId, ISupportTenant
{
    /// <inheritdoc />
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <inheritdoc />
    [ForeignKey(nameof(Tenant))]
    public required long TenantId { get; init; }

    /// <summary>
    /// Gets the foreign key for the associated client.
    /// </summary>
    [ForeignKey(nameof(Client))]
    public required long ClientId { get; init; }

    /// <summary>
    /// Gets the type of the URL.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.UrlType)]
    public required string UrlType { get; init; }

    /// <summary>
    /// Gets the value of the URL.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.UrlValue)]
    public required string UrlValue { get; init; }

    //

    /// <inheritdoc />
    public required TenantEntity Tenant { get; init; }

    /// <summary>
    /// Gets the navigation property for the associated client.
    /// </summary>
    public required ClientEntity Client { get; init; }
}
