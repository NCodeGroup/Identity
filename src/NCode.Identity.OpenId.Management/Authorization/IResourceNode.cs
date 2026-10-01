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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Identifies a node of the resource hierarchy — a resource type and identifier within a tenant — so that
/// resource-scoped role assignments (including ownership) can be evaluated against it and its ancestors (ADR-0034).
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
