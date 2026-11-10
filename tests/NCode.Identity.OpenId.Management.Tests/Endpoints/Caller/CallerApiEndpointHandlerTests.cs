// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Caller;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Caller;

public sealed class CallerApiEndpointHandlerTests : IDisposable
{
    private const string ControlPlaneTenantId = "platform-root";
    private const string PrincipalId = "principal-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<IPrincipalResolver> MockPrincipalResolver { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private CallerApiEndpointHandler Handler { get; }

    public CallerApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockPrincipalResolver = MockRepository.Create<IPrincipalResolver>();
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();

        Handler = new CallerApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockPrincipalResolver.Object,
            MockAmbientTenantAccessor.Object
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private static HttpContext CreateHttpContext()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test"));
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Features.Set<IManagementCallerFeature>(
            new DefaultManagementCallerFeature(user)
        );
        // The shared environment filter publishes the context in the pipeline; emulate it for direct handler calls.
        httpContext.Features.Set<IOpenIdContextFeature>(
            Mock.Of<IOpenIdContextFeature>(feature =>
                feature.OpenIdContext
                == Mock.Of<OpenIdContext>(context =>
                    context.Tenant
                    == Mock.Of<OpenIdTenant>(tenant => tenant.TenantId == ControlPlaneTenantId)
                )
            )
        );
        return httpContext;
    }

    // The handler reads the caller's principal and assignments in the request's control-plane tenant scope.
    private void SetupScopeAndStoreManager()
    {
        var scope = MockRepository.Create<IDisposable>();
        scope.Setup(x => x.Dispose()).Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(ControlPlaneTenantId))
            .Returns(scope.Object)
            .Verifiable();

        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupPrincipal(string? principalId) =>
        MockPrincipalResolver
            .Setup(x =>
                x.ResolvePrincipalIdOrDefaultAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(principalId)
            .Verifiable();

    private void SetupAssignments(params PersistedRoleAssignment[] assignments)
    {
        var store = MockRepository.Create<IRoleAssignmentStore>();
        MockStoreManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(store.Object)
            .Verifiable();
        store
            .Setup(x => x.GetByPrincipalAsync(PrincipalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignments)
            .Verifiable();
    }

    private static PersistedRoleAssignment CreateAssignment(
        string assignmentId,
        string roleName,
        string resourceType,
        string resourceId
    ) =>
        new()
        {
            TenantId = ControlPlaneTenantId,
            AssignmentId = assignmentId,
            PrincipalId = PrincipalId,
            RoleName = roleName,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ConcurrencyToken = string.Empty,
        };

    private static PersistedTeam CreateTeam(string teamId) =>
        new PersistedTeamBuilder
        {
            TeamId = teamId,
            ConcurrencyToken = "team-ct",
            DisplayName = $"Team {teamId}",
            SettingsConcurrencyToken = "settings-ct",
            Settings = default,
        }.Build();

    private static PersistedTenant CreateTenant(string tenantId, string teamId) =>
        new PersistedTenantBuilder
        {
            TenantId = tenantId,
            TeamId = teamId,
            DisplayName = $"Tenant {tenantId}",
            SettingsConcurrencyToken = "settings-ct",
            Settings = default,
        }.Build();

    #endregion

    #region GetCallerAsync Tests

    [Fact]
    public async Task GetCallerAsync_WhenTheCallerIsAnonymous_ReturnsAuthenticationRequired()
    {
        var httpContext = new DefaultHttpContext();

        var result = await Handler.GetCallerAsync(httpContext, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    [Fact]
    public async Task GetCallerAsync_WhenAnotherSchemeAuthenticatedTheCaller_ReturnsAuthenticationRequired()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity("Cookies")),
        };

        var result = await Handler.GetCallerAsync(httpContext, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    [Fact]
    public async Task GetCallerAsync_WhenTheCallerHasAPrincipal_ReturnsItsPrincipalAuthorityAndAssignments()
    {
        SetupScopeAndStoreManager();
        SetupPrincipal(PrincipalId);
        SetupAssignments(
            CreateAssignment("assignment-1", "TeamAdmin", ResourceNodeTypes.Team, "team-1")
        );

        object? authorizedResource = null;
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .Callback(
                (ClaimsPrincipal _, object? resource, IEnumerable<IAuthorizationRequirement> _) =>
                    authorizedResource = resource
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();

        var result = await Handler.GetCallerAsync(CreateHttpContext(), CancellationToken.None);

        var node = Assert.IsType<ResourceNode>(authorizedResource);
        Assert.Equal(ControlPlaneTenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.Platform, node.ResourceType);
        Assert.Equal(OpenIdConstants.PlatformResourceId, node.ResourceId);

        var json = Assert.IsType<JsonHttpResult<CallerResource>>(result);
        var caller = Assert.IsType<CallerResource>(json.Value);
        Assert.Equal(PrincipalId, caller.PrincipalId);
        Assert.True(caller.IsPlatformAdministrator);
        var assignment = Assert.Single(caller.Assignments);
        Assert.Equal("assignment-1", assignment.AssignmentId);
        Assert.Equal(PrincipalId, assignment.PrincipalId);
        Assert.Equal("TeamAdmin", assignment.RoleName);
        Assert.Equal(ResourceNodeTypes.Team, assignment.ResourceType);
        Assert.Equal("team-1", assignment.ResourceId);
        Assert.Equal(ControlPlaneTenantId, assignment.TenantId);
    }

    [Fact]
    public async Task GetCallerAsync_WhenTheCallerResolvesToNoPrincipal_ReturnsNoPrincipalAndNoAssignments()
    {
        SetupScopeAndStoreManager();
        SetupPrincipal(null);
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Failed())
            .Verifiable();

        var result = await Handler.GetCallerAsync(CreateHttpContext(), CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<CallerResource>>(result);
        var caller = Assert.IsType<CallerResource>(json.Value);
        Assert.Null(caller.PrincipalId);
        Assert.False(caller.IsPlatformAdministrator);
        Assert.Empty(caller.Assignments);
    }

    #endregion

    #region ListTeamsAsync Tests

    [Fact]
    public async Task ListTeamsAsync_WhenTheCallerIsAnonymous_ReturnsAuthenticationRequired()
    {
        var httpContext = new DefaultHttpContext();

        var result = await Handler.ListTeamsAsync(httpContext, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    [Fact]
    public async Task ListTeamsAsync_ListsEachTeamTheCallerHoldsARoleAtOnce()
    {
        SetupScopeAndStoreManager();
        SetupPrincipal(PrincipalId);
        SetupAssignments(
            CreateAssignment("assignment-1", "TeamAdmin", ResourceNodeTypes.Team, "team-b"),
            CreateAssignment("assignment-2", "TeamOwner", ResourceNodeTypes.Team, "team-b"),
            CreateAssignment("assignment-3", "TeamAdmin", ResourceNodeTypes.Team, "team-a"),
            CreateAssignment("assignment-4", "Owner", ResourceNodeTypes.Tenant, "tenant-1")
        );

        var teamStore = MockRepository.Create<ITeamStore>();
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(teamStore.Object)
            .Verifiable();
        teamStore
            .Setup(x =>
                x.GetManyAsync(
                    It.Is<IReadOnlyCollection<string>>(ids =>
                        ids.SequenceEqual(new[] { "team-a", "team-b" })
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateTeam("team-a"), CreateTeam("team-b")])
            .Verifiable();

        var result = await Handler.ListTeamsAsync(CreateHttpContext(), CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<CollectionResource<TeamResource>>>(result);
        var collection = Assert.IsType<CollectionResource<TeamResource>>(json.Value);
        Assert.Equal(["team-a", "team-b"], collection.Items.Select(x => x.TeamId));
        Assert.Equal("Team team-a", collection.Items[0].DisplayName);
        Assert.Null(collection.ContinuationToken);
    }

    [Fact]
    public async Task ListTeamsAsync_SkipsATeamThatDoesNotExist()
    {
        SetupScopeAndStoreManager();
        SetupPrincipal(PrincipalId);
        SetupAssignments(
            CreateAssignment("assignment-1", "TeamAdmin", ResourceNodeTypes.Team, "team-a"),
            CreateAssignment("assignment-2", "TeamAdmin", ResourceNodeTypes.Team, "team-gone")
        );

        var teamStore = MockRepository.Create<ITeamStore>();
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(teamStore.Object)
            .Verifiable();
        teamStore
            .Setup(x =>
                x.GetManyAsync(
                    It.Is<IReadOnlyCollection<string>>(ids =>
                        ids.SequenceEqual(new[] { "team-a", "team-gone" })
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateTeam("team-a")])
            .Verifiable();

        var result = await Handler.ListTeamsAsync(CreateHttpContext(), CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<CollectionResource<TeamResource>>>(result);
        var collection = Assert.IsType<CollectionResource<TeamResource>>(json.Value);
        var team = Assert.Single(collection.Items);
        Assert.Equal("team-a", team.TeamId);
    }

    #endregion

    #region ListTenantsAsync Tests

    [Fact]
    public async Task ListTenantsAsync_WhenTheCallerIsAnonymous_ReturnsAuthenticationRequired()
    {
        var httpContext = new DefaultHttpContext();

        var result = await Handler.ListTenantsAsync(httpContext, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    [Fact]
    public async Task ListTenantsAsync_ListsEachTenantTheCallerHoldsARoleAt()
    {
        SetupScopeAndStoreManager();
        SetupPrincipal(PrincipalId);
        SetupAssignments(
            CreateAssignment("assignment-1", "TenantAdmin", ResourceNodeTypes.Tenant, "tenant-b"),
            CreateAssignment("assignment-2", "TenantAdmin", ResourceNodeTypes.Tenant, "tenant-a"),
            CreateAssignment("assignment-3", "TeamAdmin", ResourceNodeTypes.Team, "team-1")
        );

        var tenantStore = MockRepository.Create<ITenantStore>();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(tenantStore.Object)
            .Verifiable();
        tenantStore
            .Setup(x =>
                x.GetManyAsync(
                    It.Is<IReadOnlyCollection<string>>(ids =>
                        ids.SequenceEqual(new[] { "tenant-a", "tenant-b" })
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateTenant("tenant-a", "team-1"), CreateTenant("tenant-b", "team-1")])
            .Verifiable();

        var result = await Handler.ListTenantsAsync(CreateHttpContext(), CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<CollectionResource<TenantResource>>>(result);
        var collection = Assert.IsType<CollectionResource<TenantResource>>(json.Value);
        Assert.Equal(["tenant-a", "tenant-b"], collection.Items.Select(x => x.TenantId));
        Assert.All(collection.Items, tenant => Assert.Equal("team-1", tenant.TeamId));
        Assert.All(collection.Items, tenant => Assert.Null(tenant.Environment));
        Assert.Null(collection.ContinuationToken);
    }

    #endregion

    #region Map Tests

    [Theory]
    [InlineData("GET", "/me", new[] { 401 })]
    [InlineData("GET", "/me/teams", new[] { 401 })]
    [InlineData("GET", "/me/tenants", new[] { 401 })]
    public async Task Map_Always_DeclaresAndDescribesEveryProblemARouteAnswers(
        string httpMethod,
        string routePattern,
        int[] statusCodes
    )
    {
        Assert.Equal(
            statusCodes,
            await EndpointProblemMetadata.ReadStatusCodesAsync(
                Handler,
                httpMethod,
                routePattern,
                CancellationToken.None
            )
        );
        Assert.Empty(
            await EndpointProblemMetadata.ReadUndescribedStatusCodesAsync(
                Handler,
                httpMethod,
                routePattern,
                CancellationToken.None
            )
        );
    }

    #endregion
}
