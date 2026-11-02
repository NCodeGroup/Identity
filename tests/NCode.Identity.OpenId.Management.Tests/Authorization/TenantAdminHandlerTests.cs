// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management;

public class TenantAdminHandlerTests
{
    private sealed class TestRequirement : IAuthorizationRequirement;

    private sealed class TestResource(string tenantId) : ISupportTenantId
    {
        public string TenantId { get; } = tenantId;
    }

    private static ClaimsPrincipal CreateUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    private static async Task<bool> HandleAsync(TestResource resource, ClaimsPrincipal user)
    {
        var requirement = new TestRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, resource);

        await new TenantAdminHandler().HandleAsync(context);

        return context.HasSucceeded;
    }

    #region HandleRequirementAsync Tests

    [Fact]
    public async Task HandleRequirementAsync_WhenTenantAdminAndMatchingTenant_Succeeds()
    {
        var user = CreateUser(
            new Claim(ClaimTypes.Role, BuiltInRoles.TenantAdmin),
            new Claim("tid", "tenant-1")
        );

        Assert.True(await HandleAsync(new TestResource("tenant-1"), user));
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenTenantMismatch_DoesNotSucceed()
    {
        var user = CreateUser(
            new Claim(ClaimTypes.Role, BuiltInRoles.TenantAdmin),
            new Claim("tid", "tenant-1")
        );

        Assert.False(await HandleAsync(new TestResource("tenant-2"), user));
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenNotTenantAdmin_DoesNotSucceed()
    {
        var user = CreateUser(new Claim("tid", "tenant-1"));

        Assert.False(await HandleAsync(new TestResource("tenant-1"), user));
    }

    #endregion
}
