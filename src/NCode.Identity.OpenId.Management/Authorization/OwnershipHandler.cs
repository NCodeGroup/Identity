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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// An <see cref="AuthorizationHandler{TRequirement,TResource}"/> that grants a principal authority over an
/// <see cref="IResourceNode"/> when the principal holds a role assignment at that node or an ancestor (its tenant, or
/// the server root). This realizes ownership: an <c>Owner</c> assignment at an instance lets the principal manage that
/// instance (and everything beneath it) without any tenant-wide role. Ownership grants every operation except
/// <see cref="Operations.Create"/> — creating a new resource is authority at the parent node (ADR-0034).
/// </summary>
internal class OwnershipHandler(
    IStoreManagerFactory storeManagerFactory,
    IOptions<TenantResolutionOptions> optionsAccessor
) : AuthorizationHandler<IAuthorizationRequirement, IResourceNode>
{
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IOptions<TenantResolutionOptions> OptionsAccessor { get; } = optionsAccessor;

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement,
        IResourceNode resource
    )
    {
        // Ownership never grants creation of a new resource; that is authority at the parent node (ADR-0034).
        if (
            requirement is OperationAuthorizationRequirement operation
            && string.Equals(operation.Name, Operations.Create.Name, StringComparison.Ordinal)
        )
        {
            return;
        }

        var principalId = context.User.FindFirstValue(JoseClaimNames.Payload.Sub);
        if (string.IsNullOrEmpty(principalId))
        {
            return;
        }

        var ancestorNodes = GetAncestorNodes(resource, OptionsAccessor.Value.RootTenantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<IRoleAssignmentStore>();
        var assignments = await store.GetByPrincipalAsync(principalId, CancellationToken.None);

        foreach (var assignment in assignments)
        {
            if (
                IsManagementRole(assignment.RoleName)
                && ancestorNodes.Contains(
                    (assignment.ResourceType, Normalize(assignment.ResourceId))
                )
            )
            {
                context.Succeed(requirement);
                return;
            }
        }
    }

    private static HashSet<(string ResourceType, string NormalizedResourceId)> GetAncestorNodes(
        IResourceNode resource,
        string rootTenantId
    ) =>
        // A resource's authority chain is fixed: the node itself, its tenant, then the server root (ADR-0034).
        [
            (resource.ResourceType, Normalize(resource.ResourceId)),
            (ResourceNodeTypes.Tenant, Normalize(resource.TenantId)),
            (ResourceNodeTypes.Server, Normalize(rootTenantId)),
        ];

    private static bool IsManagementRole(string roleName) =>
        string.Equals(roleName, BuiltInRoles.Owner, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, BuiltInRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, BuiltInRoles.GlobalAdmin, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) => value.ToLowerInvariant();
}
