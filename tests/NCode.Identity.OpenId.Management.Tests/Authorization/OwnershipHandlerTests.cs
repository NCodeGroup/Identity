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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management;

public class OwnershipHandlerTests
{
    private const string RootTenantId = "root";
    private const string TenantId = "tenant-1";
    private const string Subject = "subject-1";

    private static ClaimsPrincipal CreateUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    private static ClaimsPrincipal CreateSubject(string subjectId = Subject) =>
        CreateUser(new Claim("sub", subjectId));

    private static ResourceNode ClientNode(string clientId = "client-1") =>
        ResourceNode.For(TenantId, ResourceNodeTypes.Client, clientId);

    private static PersistedRoleAssignment Assignment(
        string roleName,
        string resourceType,
        string resourceId,
        string principalId = Subject
    ) =>
        new()
        {
            TenantId = TenantId,
            AssignmentId = "assign-1",
            PrincipalId = principalId,
            RoleName = roleName,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ConcurrencyToken = string.Empty,
        };

    private static async Task<bool> HandleAsync(
        IAuthorizationRequirement requirement,
        IResourceNode resource,
        ClaimsPrincipal user,
        params PersistedRoleAssignment[] assignments
    )
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var factory = mocks.Create<IStoreManagerFactory>();
        var manager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IRoleAssignmentStore>();
        var resolver = mocks.Create<IPrincipalResolver>();

        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manager.Object);
        manager.Setup(x => x.GetStore<IRoleAssignmentStore>()).Returns(store.Object);
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        // The resolver maps the caller to a stable principal id; mirror the token's subject claim (ADR-0035).
        resolver
            .Setup(x =>
                x.ResolvePrincipalIdOrDefaultAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    OpenIdContext _,
                    ClaimsPrincipal callerUser,
                    IStoreManager _,
                    CancellationToken _
                ) => callerUser.FindFirstValue("sub")
            );
        store
            .Setup(x => x.GetByPrincipalAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignments.ToList());

        // OwnershipHandler reads the ambient OpenIdContext from the request (the one blessed async-local seam).
        var openIdContext = mocks.Create<OpenIdContext>();
        var feature = mocks.Create<IOpenIdContextFeature>();
        feature.Setup(x => x.OpenIdContext).Returns(openIdContext.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set(feature.Object);
        var accessor = mocks.Create<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(httpContext);

        var options = Options.Create(new TenantResolutionOptions { RootTenantId = RootTenantId });
        var handler = new OwnershipHandler(
            factory.Object,
            resolver.Object,
            accessor.Object,
            options
        );
        var context = new AuthorizationHandlerContext([requirement], user, resource);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    #region HandleRequirementAsync Tests

    [Fact]
    public async Task HandleRequirementAsync_WhenOwnerAtExactNode_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.Delete,
            ClientNode(),
            CreateSubject(),
            Assignment(BuiltInRoles.Owner, ResourceNodeTypes.Client, "client-1")
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenOwnerAtAncestorTenant_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.Update,
            ClientNode(),
            CreateSubject(),
            Assignment(BuiltInRoles.Owner, ResourceNodeTypes.Tenant, TenantId)
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenAssignmentAtServerRoot_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.Delete,
            ClientNode(),
            CreateSubject(),
            Assignment(BuiltInRoles.GlobalAdmin, ResourceNodeTypes.Server, RootTenantId)
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenCreateRequirement_DoesNotSucceed()
    {
        var succeeded = await HandleAsync(
            Operations.Create,
            ClientNode(),
            CreateSubject(),
            Assignment(BuiltInRoles.Owner, ResourceNodeTypes.Client, "client-1")
        );

        Assert.False(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenOwnerOfDifferentNode_DoesNotSucceed()
    {
        var succeeded = await HandleAsync(
            Operations.Delete,
            ClientNode(),
            CreateSubject(),
            Assignment(BuiltInRoles.Owner, ResourceNodeTypes.Client, "client-2")
        );

        Assert.False(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenNonManagementRole_DoesNotSucceed()
    {
        var succeeded = await HandleAsync(
            Operations.Delete,
            ClientNode(),
            CreateSubject(),
            Assignment("SecretsManager", ResourceNodeTypes.Client, "client-1")
        );

        Assert.False(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenNoSubjectClaim_DoesNotSucceed()
    {
        var succeeded = await HandleAsync(
            Operations.Delete,
            ClientNode(),
            CreateUser(),
            Assignment(BuiltInRoles.Owner, ResourceNodeTypes.Client, "client-1")
        );

        Assert.False(succeeded);
    }

    #endregion
}
