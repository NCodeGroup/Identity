#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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
