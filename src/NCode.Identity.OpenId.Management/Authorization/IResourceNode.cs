// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Identifies a node of the resource hierarchy — a resource type and identifier within a tenant — so that
/// resource-scoped role assignments (including ownership) can be evaluated against it and its ancestors.
/// </summary>
[PublicAPI]
public interface IResourceNode : ISupportTenantId
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
