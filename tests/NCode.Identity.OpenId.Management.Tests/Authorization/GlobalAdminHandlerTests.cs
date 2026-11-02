// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NCode.Identity.OpenId.Management.Authorization;

namespace NCode.Identity.OpenId.Management;

public class GlobalAdminHandlerTests
{
    private sealed class TestRequirement : IAuthorizationRequirement;

    #region HandleRequirementAsync Tests

    [Fact]
    public async Task HandleRequirementAsync_WhenGlobalAdmin_Succeeds()
    {
        var user = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Role, BuiltInRoles.GlobalAdmin)], "test")
        );
        var requirement = new TestRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new GlobalAdminHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    #endregion
}
