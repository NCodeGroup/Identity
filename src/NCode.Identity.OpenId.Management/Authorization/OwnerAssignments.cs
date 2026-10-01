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
using NCode.Identity.Jose;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Writes the creator-as-owner role assignment that makes the principal who creates a resource its first
/// <see cref="BuiltInRoles.Owner"/>, so that a resource is never ownerless (ADR-0034).
/// </summary>
internal static class OwnerAssignments
{
    /// <summary>
    /// Assigns the calling principal the <see cref="BuiltInRoles.Owner"/> role at the newly-created resource node,
    /// enlisted in the supplied unit of work so it commits atomically with the resource. When the caller has no
    /// subject principal (for example a service token), the resource starts with no owner and the tenant and global
    /// realms continue to manage it.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> that created the resource.</param>
    /// <param name="storeManager">The unit of work the resource is being created in.</param>
    /// <param name="cryptoService">The service used to generate the assignment's opaque identifier.</param>
    /// <param name="tenantId">The identifier of the tenant that owns the resource.</param>
    /// <param name="resourceType">The type of the resource node (a <see cref="ResourceNodeTypes"/> value).</param>
    /// <param name="resourceId">The identifier of the resource node.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    public static async ValueTask AssignCreatorAsync(
        ClaimsPrincipal user,
        IStoreManager storeManager,
        ICryptoService cryptoService,
        string tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken
    )
    {
        var principalId = user.FindFirstValue(JoseClaimNames.Payload.Sub);
        if (string.IsNullOrEmpty(principalId))
        {
            return;
        }

        var store = storeManager.GetStore<IRoleAssignmentStore>();
        await store.AddAsync(
            new PersistedRoleAssignment
            {
                TenantId = tenantId,
                AssignmentId = cryptoService.GenerateResourceId(),
                PrincipalId = principalId,
                RoleName = BuiltInRoles.Owner,
                ResourceType = resourceType,
                ResourceId = resourceId,
                ConcurrencyToken = string.Empty,
            },
            cancellationToken
        );
    }
}
