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

using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Identifies a node of the resource hierarchy — a resource type and identifier within a tenant — so that
/// resource-scoped role assignments (including ownership) can be evaluated against it and its ancestors (ADR-0034).
/// </summary>
internal interface IResourceNode : ISupportTenantId
{
    /// <summary>
    /// Gets the type of the resource node (a <see cref="ResourceNodeTypes"/> value).
    /// </summary>
    string ResourceType { get; }

    /// <summary>
    /// Gets the identifier of the resource node.
    /// </summary>
    string ResourceId { get; }
}

/// <summary>
/// A minimal <see cref="IResourceNode"/> an endpoint passes to <c>IAuthorizationService.AuthorizeAsync</c> so that the
/// <see cref="OwnershipHandler"/> can evaluate resource-scoped assignments for the resource being acted on.
/// </summary>
internal sealed class ResourceNode : IResourceNode
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <inheritdoc />
    public required string ResourceType { get; init; }

    /// <inheritdoc />
    public required string ResourceId { get; init; }

    /// <summary>
    /// Creates a <see cref="ResourceNode"/> for the specified type and identifier within a tenant.
    /// </summary>
    /// <param name="tenantId">The identifier of the owning tenant.</param>
    /// <param name="resourceType">The type of the resource node (a <see cref="ResourceNodeTypes"/> value).</param>
    /// <param name="resourceId">The identifier of the resource node.</param>
    /// <returns>The resource node.</returns>
    public static ResourceNode For(string tenantId, string resourceType, string resourceId) =>
        new()
        {
            TenantId = tenantId,
            ResourceType = resourceType,
            ResourceId = resourceId,
        };
}
