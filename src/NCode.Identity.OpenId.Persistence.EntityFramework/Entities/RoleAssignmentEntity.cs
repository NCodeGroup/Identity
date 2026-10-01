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
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

/// <summary>
/// Represents an entity framework data contract for a resource-scoped role assignment: a grant of a role to a
/// principal at a node of the resource hierarchy (<see cref="ResourceType"/> + <see cref="ResourceId"/>). Ownership is
/// an <c>Owner</c> assignment at a specific instance. The complimentary DTO for this entity is
/// <c>PersistedRoleAssignment</c>.
/// </summary>
[Index(nameof(TenantId), nameof(NormalizedAssignmentId), IsUnique = true)]
[Index(
    nameof(TenantId),
    nameof(NormalizedPrincipalId),
    nameof(NormalizedRoleName),
    nameof(ResourceType),
    nameof(NormalizedResourceId),
    IsUnique = true
)]
[Index(nameof(TenantId), nameof(ResourceType), nameof(NormalizedResourceId))]
public sealed class RoleAssignmentEntity : ISupportTenantEntity, ISupportConcurrencyToken
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

    /// <summary>
    /// Gets the opaque, server-generated public identifier for this role assignment.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string AssignmentId { get; init; }

    /// <summary>
    /// Gets the value of <see cref="AssignmentId"/> in lowercase so that lookups can be sargable for DBMS engines that
    /// don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedAssignmentId { get; init; }

    /// <summary>
    /// Gets the identifier of the principal that is granted the role.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.SubjectId)]
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the value of <see cref="PrincipalId"/> in lowercase so that lookups can be sargable for DBMS engines that
    /// don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.SubjectId)]
    public required string NormalizedPrincipalId { get; init; }

    /// <summary>
    /// Gets the name of the role that is granted.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.RoleName)]
    public required string RoleName { get; init; }

    /// <summary>
    /// Gets the value of <see cref="RoleName"/> in lowercase so that lookups can be sargable for DBMS engines that
    /// don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.RoleName)]
    public required string NormalizedRoleName { get; init; }

    /// <summary>
    /// Gets the type of the resource node the role is granted at (a controlled vocabulary such as <c>server</c>,
    /// <c>tenant</c>, <c>client</c>, <c>resource_server</c>, or <c>grant</c>).
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceType)]
    public required string ResourceType { get; init; }

    /// <summary>
    /// Gets the identifier of the resource node the role is granted at.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string ResourceId { get; init; }

    /// <summary>
    /// Gets the value of <see cref="ResourceId"/> in lowercase so that lookups can be sargable for DBMS engines that
    /// don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedResourceId { get; init; }

    //

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    // navigational properties

    /// <inheritdoc />
    public required TenantEntity Tenant { get; init; }
}
