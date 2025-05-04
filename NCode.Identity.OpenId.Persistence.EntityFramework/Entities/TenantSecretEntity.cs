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

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

/// <summary>
/// Represents an entity framework data contract for a relationship between a <see cref="TenantEntity"/> and a <see cref="SecretEntity"/>.
/// </summary>
[Index(nameof(TenantId), nameof(SecretId), IsUnique = true)]
public class TenantSecretEntity : ISupportTenantEntity, ISupportSecretEntity
{
    /// <summary>
    /// Gets or sets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <inheritdoc />
    [ForeignKey(nameof(Tenant))]
    public required long TenantId { get; init; }

    /// <inheritdoc />
    [ForeignKey(nameof(Secret))]
    public required long SecretId { get; init; }

    // navigation properties

    /// <inheritdoc />
    public required TenantEntity Tenant { get; init; }

    // ReSharper disable once EntityFramework.ModelValidation.CircularDependency
    // We use DTOs to avoid circular dependencies.
    /// <inheritdoc />
    public required SecretEntity Secret { get; init; }
}
