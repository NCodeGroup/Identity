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

public class OpenIdManagementLibraryTests
{
    [Fact]
    public void Metadata_ReturnsExpectedValues()
    {
        var library = new OpenIdManagementLibrary();

        Assert.Equal("NCode.Identity.OpenId.Management", library.DisplayName);
        Assert.Equal("AddOpenIdManagementLibrary", library.ConfigureMethod);
    }
}
