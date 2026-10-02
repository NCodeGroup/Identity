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
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted role assignment: a grant of a <see cref="RoleName"/> to a <see cref="PrincipalId"/>
/// at a node of the resource hierarchy (<see cref="ResourceType"/> + <see cref="ResourceId"/>), inherited downward.
/// Ownership is an <c>Owner</c> assignment at a specific instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedRoleAssignment : ISupportTenantId, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the identifier of the tenant that owns this role assignment.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the opaque, server-generated public identifier for this role assignment.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string AssignmentId { get; init; }

    /// <summary>
    /// Gets or sets the server-generated opaque identifier of the principal that is granted the role (a federated
    /// principal today, a service principal later).
    /// </summary>
    [MaxLength(OpenIdMaxLengths.PrincipalId)]
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets or sets the name of the role that is granted (a built-in role such as <c>Owner</c>, or an operator-defined
    /// custom role).
    /// </summary>
    [MaxLength(OpenIdMaxLengths.RoleName)]
    public required string RoleName { get; init; }

    /// <summary>
    /// Gets or sets the type of the resource node the role is granted at (for example <c>server</c>, <c>tenant</c>,
    /// <c>client</c>, <c>resource_server</c>, or <c>grant</c>).
    /// </summary>
    [MaxLength(MaxLengths.ResourceType)]
    public required string ResourceType { get; init; }

    /// <summary>
    /// Gets or sets the identifier of the resource node the role is granted at. Authority at this node is inherited by
    /// every resource beneath it.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string ResourceId { get; init; }

    /// <inheritdoc />
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string ConcurrencyToken { get; set; }
}
