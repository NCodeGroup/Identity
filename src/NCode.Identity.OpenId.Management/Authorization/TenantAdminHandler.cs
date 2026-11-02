// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// An <see cref="AuthorizationHandler{TRequirement,TResource}"/> that succeeds any <see cref="IAuthorizationRequirement"/>
/// when the user is a member of the <see cref="BuiltInRoles.TenantAdmin"/> role and their tenant identifier matches
/// the resource's tenant identifier. This handler enforces tenant-scoped administrative access control.
/// </summary>
internal class TenantAdminHandler
    : AuthorizationHandler<IAuthorizationRequirement, ISupportTenantId>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement,
        ISupportTenantId resource
    )
    {
        var userTenantId = context.User.FindFirstValue(JoseClaimNames.Payload.Tid);
        var userIsTenantAdmin = context.User.IsInRole(BuiltInRoles.TenantAdmin);
        var resourceTenantId = resource.TenantId;

        if (
            userIsTenantAdmin
            && string.Equals(userTenantId, resourceTenantId, StringComparison.Ordinal)
        )
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
