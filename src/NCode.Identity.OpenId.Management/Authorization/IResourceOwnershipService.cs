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

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Manages resource ownership — the <see cref="BuiltInRoles.Owner"/> role assignments at a resource node (ADR-0034).
/// Applications may use this to grant ownership of their own resource types by passing an application-defined
/// <c>resourceType</c>.
/// </summary>
[PublicAPI]
public interface IResourceOwnershipService
{
    /// <summary>
    /// Assigns the calling principal the <see cref="BuiltInRoles.Owner"/> role at the newly-created resource node,
    /// enlisted in the supplied unit of work so it commits atomically with the resource. When the caller has no
    /// subject principal (for example a service token), the resource starts with no owner and the tenant and global
    /// realms continue to manage it.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> that created the resource.</param>
    /// <param name="storeManager">The unit of work the resource is being created in.</param>
    /// <param name="tenantId">The identifier of the tenant that owns the resource.</param>
    /// <param name="resourceType">The type of the resource node (a <see cref="ResourceNodeTypes"/> value).</param>
    /// <param name="resourceId">The identifier of the resource node.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AssignCreatorAsync(
        ClaimsPrincipal user,
        IStoreManager storeManager,
        string tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken
    );
}
