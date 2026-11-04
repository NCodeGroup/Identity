// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management;

public class OwnershipHandlerTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    private const string TeamId = "team-1";
    private const string TenantId = "tenant-1";
    private const string Subject = "subject-1";

    private static ClaimsPrincipal CreateUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    private static ClaimsPrincipal CreateSubject(string subjectId = Subject) =>
        CreateUser(new Claim("sub", subjectId));

    private static ResourceNode ClientNode(string clientId = "client-1") =>
        ResourceNode.For(TenantId, ResourceNodeTypes.Client, clientId);

    private static ResourceNode TeamNode() =>
        ResourceNode.For(TenantId, ResourceNodeTypes.Team, TeamId);

    // A tenant-plane entity that is not a node, such as a persisted client.
    private sealed record TenantEntity(string TenantId) : ISupportTenantId;

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

    private static PersistedTenant CreateTenant(string teamId) =>
        new()
        {
            TenantId = TenantId,
            TeamId = teamId,
            ConcurrencyToken = string.Empty,
            Issuer = null,
            IsDisabled = false,
            DisplayName = TenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = default,
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = TenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    // A requirement the handler does not decide reads nothing, a caller without a principal reads no assignments, and
    // a team or platform node reads no tenant.
    private async Task<bool> HandleAsync(
        IAuthorizationRequirement requirement,
        ISupportTenantId resource,
        ClaimsPrincipal user,
        PersistedRoleAssignment[] assignments,
        bool decides = true,
        bool readsAssignments = true,
        bool readsTenant = true,
        string? tenantTeamId = TeamId
    )
    {
        var factory = MockRepository.Create<IStoreManagerFactory>();
        var resolver = MockRepository.Create<IPrincipalResolver>();
        var accessor = MockRepository.Create<IHttpContextAccessor>();
        var ambient = MockRepository.Create<IAmbientTenantAccessor>();
        if (decides)
        {
            SetupDecision(
                factory,
                resolver,
                accessor,
                ambient,
                assignments,
                readsAssignments,
                readsTenant,
                tenantTeamId
            );
        }

        var handler = new OwnershipHandler(
            factory.Object,
            resolver.Object,
            accessor.Object,
            ambient.Object
        );
        var context = new AuthorizationHandlerContext([requirement], user, resource);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    // The named argument selects the overload above; without it this one would call itself.
    private Task<bool> HandleAsync(
        IAuthorizationRequirement requirement,
        ISupportTenantId resource,
        ClaimsPrincipal user,
        params PersistedRoleAssignment[] assignments
    ) => HandleAsync(requirement, resource, user, assignments, decides: true);

    private void SetupDecision(
        Mock<IStoreManagerFactory> factory,
        Mock<IPrincipalResolver> resolver,
        Mock<IHttpContextAccessor> accessor,
        Mock<IAmbientTenantAccessor> ambient,
        PersistedRoleAssignment[] assignments,
        bool readsAssignments,
        bool readsTenant,
        string? tenantTeamId
    )
    {
        var manager = MockRepository.Create<IStoreManager>();

        // The handler reads the principal and the node chain in the request's tenant scope.
        var scope = MockRepository.Create<IDisposable>();
        scope.Setup(x => x.Dispose()).Verifiable();
        ambient.Setup(x => x.BeginScope(TenantId)).Returns(scope.Object).Verifiable();

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
            if (readsTenant)
            {
                var tenantStore = MockRepository.Create<ITenantStore>();
                manager
                    .Setup(x => x.GetStore<ITenantStore>())
                    .Returns(tenantStore.Object)
                    .Verifiable();
                tenantStore
                    .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(tenantTeamId is null ? null : CreateTenant(tenantTeamId))
                    .Verifiable();
            }

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
        openIdContext
            .Setup(x => x.Tenant)
            .Returns(Mock.Of<OpenIdTenant>(tenant => tenant.TenantId == TenantId));
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
    public async Task HandleRequirementAsync_WhenAssignmentAtThePlatformRoot_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.Delete,
            ClientNode(),
            CreateSubject(),
            Assignment(BuiltInRoles.GlobalAdmin, ResourceNodeTypes.Platform, "platform")
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenTeamAdminAtTheTenantsTeam_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.Update,
            ClientNode(),
            CreateSubject(),
            Assignment(BuiltInRoles.TeamAdmin, ResourceNodeTypes.Team, TeamId)
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenTheTenantIsUnknown_SkipsTheTeamNode()
    {
        var succeeded = await HandleAsync(
            Operations.Update,
            ClientNode(),
            CreateSubject(),
            [Assignment(BuiltInRoles.TeamAdmin, ResourceNodeTypes.Team, TeamId)],
            tenantTeamId: null
        );

        Assert.False(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenCreateRequirementAndTeamAdminAtTheNode_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.Create,
            ResourceNode.For(TenantId, ResourceNodeTypes.Tenant, TenantId),
            CreateSubject(),
            Assignment(BuiltInRoles.TeamAdmin, ResourceNodeTypes.Tenant, TenantId)
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenCreateRequirementAndOwnerAtTheNode_DoesNotSucceed()
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
    public async Task HandleRequirementAsync_WhenCreateOnTheTeamNodeAndTeamAdminAtThatTeam_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.Create,
            TeamNode(),
            CreateSubject(),
            [Assignment(BuiltInRoles.TeamAdmin, ResourceNodeTypes.Team, TeamId)],
            readsTenant: false
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenCreateOnTheTeamNodeAndOwnerAtThatTeam_DoesNotSucceed()
    {
        var succeeded = await HandleAsync(
            Operations.Create,
            TeamNode(),
            CreateSubject(),
            [Assignment(BuiltInRoles.Owner, ResourceNodeTypes.Team, TeamId)],
            readsTenant: false
        );

        Assert.False(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenCreateOnTheTeamNodeAndGlobalAdminAtThePlatform_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.Create,
            TeamNode(),
            CreateSubject(),
            [Assignment(BuiltInRoles.GlobalAdmin, ResourceNodeTypes.Platform, "platform")],
            readsTenant: false
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenCreateOnTheTeamNodeAndTeamAdminAtAnotherTeam_DoesNotSucceed()
    {
        var succeeded = await HandleAsync(
            Operations.Create,
            TeamNode(),
            CreateSubject(),
            [Assignment(BuiltInRoles.TeamAdmin, ResourceNodeTypes.Team, "team-2")],
            readsTenant: false
        );

        Assert.False(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenTeamAdminAtAnotherTeamOnATenantsClient_DoesNotSucceed()
    {
        var succeeded = await HandleAsync(
            Operations.Update,
            ClientNode(),
            CreateSubject(),
            Assignment(BuiltInRoles.TeamAdmin, ResourceNodeTypes.Team, "team-2")
        );

        Assert.False(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenCreateOnThePlatformNodeAndTeamAdminAtATeam_DoesNotSucceed()
    {
        var succeeded = await HandleAsync(
            Operations.Create,
            ResourceNode.For(TenantId, ResourceNodeTypes.Platform, "platform"),
            CreateSubject(),
            [Assignment(BuiltInRoles.TeamAdmin, ResourceNodeTypes.Team, TeamId)],
            readsTenant: false
        );

        Assert.False(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenTenantAdminAtTheTenantOnTheTeamNode_DoesNotSucceed()
    {
        var succeeded = await HandleAsync(
            Operations.Update,
            TeamNode(),
            CreateSubject(),
            [Assignment(BuiltInRoles.TenantAdmin, ResourceNodeTypes.Tenant, TenantId)],
            readsTenant: false
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

    [Fact]
    public async Task HandleRequirementAsync_WhenAnEntityAndCreateAndTeamAdminAtTheTenantsTeam_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.Create,
            new TenantEntity(TenantId),
            CreateSubject(),
            Assignment(BuiltInRoles.TeamAdmin, ResourceNodeTypes.Team, TeamId)
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenAnEntityAndCreateAndOwnerAtTheTenant_DoesNotSucceed()
    {
        var succeeded = await HandleAsync(
            Operations.Create,
            new TenantEntity(TenantId),
            CreateSubject(),
            Assignment(BuiltInRoles.Owner, ResourceNodeTypes.Tenant, TenantId)
        );

        Assert.False(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenAnEntityAndUpdateAndOwnerAtTheTenant_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.Update,
            new TenantEntity(TenantId),
            CreateSubject(),
            Assignment(BuiltInRoles.Owner, ResourceNodeTypes.Tenant, TenantId)
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenATenantScopeAndAssignmentAtThePlatform_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.List,
            TenantScopeResource.For(TenantId),
            CreateSubject(),
            Assignment(BuiltInRoles.GlobalAdmin, ResourceNodeTypes.Platform, "platform")
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenACustomNodeTypeAndOwnerAtTheTenant_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.Update,
            ResourceNode.For(TenantId, "webhook", "w1"),
            CreateSubject(),
            Assignment(BuiltInRoles.Owner, ResourceNodeTypes.Tenant, TenantId)
        );

        Assert.True(succeeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenACustomNodeTypeAndTeamAdminAtTheTeam_Succeeds()
    {
        var succeeded = await HandleAsync(
            Operations.Update,
            ResourceNode.For(TenantId, "webhook", "w1"),
            CreateSubject(),
            Assignment(BuiltInRoles.TeamAdmin, ResourceNodeTypes.Team, TeamId)
        );

        Assert.True(succeeded);
    }

    #endregion
}
