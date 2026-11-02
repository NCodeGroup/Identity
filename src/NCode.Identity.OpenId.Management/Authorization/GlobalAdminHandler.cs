// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Authorization;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// An <see cref="AuthorizationHandler{TRequirement}"/> that automatically succeeds any
/// <see cref="IAuthorizationRequirement"/> when the user is a member of the <see cref="BuiltInRoles.GlobalAdmin"/> role.
/// This handler provides a global bypass for administrators across all authorization requirements.
/// </summary>
internal class GlobalAdminHandler : AuthorizationHandler<IAuthorizationRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement
    )
    {
        if (context.User.IsInRole(BuiltInRoles.GlobalAdmin))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
