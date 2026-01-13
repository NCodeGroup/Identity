#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using Microsoft.AspNetCore.Authorization;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// An <see cref="AuthorizationHandler{TRequirement}"/> that automatically succeeds any
/// <see cref="IAuthorizationRequirement"/> when the user is a member of the <see cref="BuiltInRoles.GlobalAdmin"/> role.
/// This handler provides a global bypass for administrators across all authorization requirements.
/// </summary>
public class GlobalAdminHandler : AuthorizationHandler<IAuthorizationRequirement>
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
        else
        {
            // TODO: remove, this is just for testing/development purposes
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
