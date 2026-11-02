// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

public class OwnershipHandlerTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

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

    // A requirement the handler does not decide reads nothing, and a caller without a principal reads no assignments.
    private async Task<bool> HandleAsync(
        IAuthorizationRequirement requirement,
        IResourceNode resource,
        ClaimsPrincipal user,
        PersistedRoleAssignment[] assignments,
        bool decides = true,
        bool readsAssignments = true
    )
    {
        var factory = MockRepository.Create<IStoreManagerFactory>();
        var resolver = MockRepository.Create<IPrincipalResolver>();
        var accessor = MockRepository.Create<IHttpContextAccessor>();
        if (decides)
        {
            SetupDecision(factory, resolver, accessor, assignments, readsAssignments);
        }

        var options = Options.Create(
            new TenantResolutionOptions { Root = { TenantId = RootTenantId } }
        );
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

    // The named argument selects the overload above; without it this one would call itself.
    private Task<bool> HandleAsync(
        IAuthorizationRequirement requirement,
        IResourceNode resource,
        ClaimsPrincipal user,
        params PersistedRoleAssignment[] assignments
    ) => HandleAsync(requirement, resource, user, assignments, decides: true);

    private void SetupDecision(
        Mock<IStoreManagerFactory> factory,
        Mock<IPrincipalResolver> resolver,
        Mock<IHttpContextAccessor> accessor,
        PersistedRoleAssignment[] assignments,
        bool readsAssignments
    )
    {
        var manager = MockRepository.Create<IStoreManager>();

        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manager.Object)
            .Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        // The resolver maps the caller to a stable principal id; mirror the token's subject claim (ADR-1035).
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
            )
            .Verifiable();
        if (readsAssignments)
        {
            var store = MockRepository.Create<IRoleAssignmentStore>();
            manager
                .Setup(x => x.GetStore<IRoleAssignmentStore>())
                .Returns(store.Object)
                .Verifiable();
            store
                .Setup(x =>
                    x.GetByPrincipalAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(assignments.ToList())
                .Verifiable();
        }

        // OwnershipHandler reads the ambient OpenIdContext from the request (the one blessed async-local seam).
        var openIdContext = MockRepository.Create<OpenIdContext>();
        var feature = MockRepository.Create<IOpenIdContextFeature>();
        feature.Setup(x => x.OpenIdContext).Returns(openIdContext.Object).Verifiable();
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set(feature.Object);
        accessor.Setup(x => x.HttpContext).Returns(httpContext).Verifiable();
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
            [Assignment(BuiltInRoles.Owner, ResourceNodeTypes.Client, "client-1")],
            decides: false
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
            [Assignment(BuiltInRoles.Owner, ResourceNodeTypes.Client, "client-1")],
            readsAssignments: false
        );

        Assert.False(succeeded);
    }

    #endregion
}
