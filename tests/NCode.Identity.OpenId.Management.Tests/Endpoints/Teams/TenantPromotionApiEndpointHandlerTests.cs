// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Management.Promotions;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

public sealed class TenantPromotionApiEndpointHandlerTests : IDisposable
{
    private const string TeamId = "team-1";
    private const string ControlPlaneTenantId = "platform-root";
    private const string SourceTenantId = "source-tenant";
    private const string TargetTenantId = "target-tenant";
    private const string OtherTenantId = "other-tenant";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<ITenantPromoter> MockTenantPromoter { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private Mock<IMediator> MockMediator { get; }
    private TenantPromotionApiEndpointHandler Handler { get; }

    public TenantPromotionApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockTenantPromoter = MockRepository.Create<ITenantPromoter>();
        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();
        MockMediator = MockRepository.Create<IMediator>();

        Handler = new TenantPromotionApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockCryptoService.Object,
            MockAmbientTenantAccessor.Object,
            MockTenantPromoter.Object,
            MockManagementAuditRecorder.Object
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private void SetupStore(PersistedTenant? source, PersistedTenant? target)
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();

        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();

        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(SourceTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(source)
            .Verifiable();

        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TargetTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target)
            .Verifiable();
    }

    private void SetupStore() =>
        SetupStore(CreateTenant(SourceTenantId), CreateTenant(TargetTenantId));

    private void SetupAuthorize(
        string tenantId,
        string resourceType,
        string resourceId,
        IAuthorizationRequirement requirement,
        AuthorizationResult result
    ) =>
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).TenantId == tenantId
                        && ((ResourceNode)resource).ResourceType == resourceType
                        && ((ResourceNode)resource).ResourceId == resourceId
                    ),
                    It.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                        requirements.Contains(requirement)
                    )
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    private void SetupRouteAuthorization()
    {
        SetupAuthorize(
            SourceTenantId,
            ResourceNodeTypes.Tenant,
            SourceTenantId,
            Operations.Read,
            AuthorizationResult.Success()
        );
        SetupAuthorize(
            TargetTenantId,
            ResourceNodeTypes.Tenant,
            TargetTenantId,
            Operations.Create,
            AuthorizationResult.Success()
        );
    }

    private void SetupPlan(Action<TenantPromotionContext> plan) =>
        MockTenantPromoter
            .Setup(x =>
                x.PlanAsync(It.IsAny<TenantPromotionContext>(), It.IsAny<CancellationToken>())
            )
            .Callback<TenantPromotionContext, CancellationToken>((context, _) => plan(context))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupApply() =>
        MockTenantPromoter
            .Setup(x =>
                x.ApplyAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<TenantPromotionContext>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private HttpContext CreateHttpContext(bool authenticated)
    {
        var identity = authenticated
            ? new ClaimsIdentity(authenticationType: "test")
            : new ClaimsIdentity();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = new ServiceCollection()
                .AddSingleton(MockMediator.Object)
                .BuildServiceProvider(),
        };
        // The shared environment filter publishes the context in the pipeline; emulate it for direct handler calls.
        httpContext.Features.Set<IOpenIdContextFeature>(
            Mock.Of<IOpenIdContextFeature>(feature =>
                feature.OpenIdContext
                == Mock.Of<OpenIdContext>(context =>
                    context.Environment
                        == Mock.Of<OpenIdEnvironment>(environment =>
                            environment.JsonSerializerOptions == JsonSerializerOptions.Default
                        )
                    && context.Tenant
                        == Mock.Of<OpenIdTenant>(tenant => tenant.TenantId == ControlPlaneTenantId)
                )
            )
        );
        return httpContext;
    }

    private static PersistedTenant CreateTenant(string tenantId, string teamId = TeamId) =>
        new PersistedTenantBuilder
        {
            TenantId = tenantId,
            TeamId = teamId,
            Settings = JsonElement.Parse("""{"a":1}"""),
        }.Build();

    private static PromoteTenantRequest CreateRequest(
        string sourceTenantId = SourceTenantId,
        string targetTenantId = TargetTenantId
    ) => new() { SourceTenantId = sourceTenantId, TargetTenantId = targetTenantId };

    private static void AddChange(TenantPromotionContext context) =>
        context.AddChange(
            new TenantPromotionChangeResource
            {
                ResourceType = ManagementResourceTypes.Tenant,
                ResourceId = TargetTenantId,
                Action = TenantPromotionActions.Updated,
            }
        );

    private static void AddError(TenantPromotionContext context) =>
        context.AddError(
            new TenantPromotionErrorResource
            {
                ResourceType = ManagementResourceTypes.Client,
                ResourceId = "client-1",
                Detail = "The client cannot be promoted.",
            }
        );

    private static ResourceNode CreateOtherNode(string tenantId = OtherTenantId) =>
        ResourceNode.For(tenantId, ResourceNodeTypes.Tenant, tenantId);

    #endregion

    #region PreviewPromotionAsync Tests

    [Theory]
    [InlineData("", TargetTenantId)]
    [InlineData(SourceTenantId, " ")]
    [InlineData(SourceTenantId, SourceTenantId)]
    public async Task PreviewPromotionAsync_WhenTheTenantsAreBlankOrEqual_ReturnsBadRequestBeforeAnyRead(
        string sourceTenantId,
        string targetTenantId
    )
    {
        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(sourceTenantId, targetTenantId),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task PreviewPromotionAsync_WhenTheCallerMayNotReadTheSource_ReturnsForbiddenBeforeAnyRead()
    {
        SetupAuthorize(
            SourceTenantId,
            ResourceNodeTypes.Tenant,
            SourceTenantId,
            Operations.Read,
            AuthorizationResult.Failed()
        );

        // Strict mocks: no store factory setup, so reading before refusing would fail the test.
        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task PreviewPromotionAsync_WhenTheCallerIsAnonymous_ReturnsUnauthorizedBeforeAnyRead()
    {
        SetupAuthorize(
            SourceTenantId,
            ResourceNodeTypes.Tenant,
            SourceTenantId,
            Operations.Read,
            AuthorizationResult.Failed()
        );

        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: false),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    [Fact]
    public async Task PreviewPromotionAsync_WhenTheCallerMayNotCreateInTheTarget_ReturnsForbiddenBeforeAnyRead()
    {
        SetupAuthorize(
            SourceTenantId,
            ResourceNodeTypes.Tenant,
            SourceTenantId,
            Operations.Read,
            AuthorizationResult.Success()
        );
        SetupAuthorize(
            TargetTenantId,
            ResourceNodeTypes.Tenant,
            TargetTenantId,
            Operations.Create,
            AuthorizationResult.Failed()
        );

        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task PreviewPromotionAsync_WhenTheSourceDoesNotExist_ReturnsNotFound()
    {
        SetupRouteAuthorization();
        SetupStore(source: null, CreateTenant(TargetTenantId));

        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var notFound = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task PreviewPromotionAsync_WhenTheTargetDoesNotExist_ReturnsNotFound()
    {
        SetupRouteAuthorization();
        SetupStore(CreateTenant(SourceTenantId), target: null);

        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var notFound = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task PreviewPromotionAsync_WhenATenantBelongsToAnotherTeam_ReturnsNotFound()
    {
        SetupRouteAuthorization();
        SetupStore(CreateTenant(SourceTenantId), CreateTenant(TargetTenantId, "team-2"));

        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var notFound = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task PreviewPromotionAsync_WhenPlanned_ReturnsTheReportWithoutApplying()
    {
        SetupRouteAuthorization();
        SetupStore();
        TenantPromotionContext? planned = null;
        SetupPlan(context =>
        {
            planned = context;
            AddChange(context);
            AddError(context);
        });

        // Strict mocks: no ApplyAsync, audit or announcement setup, so a preview that writes would fail the test.
        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<TenantPromotionResource>>(result);
        var report = ok.Value;
        Assert.NotNull(report);
        Assert.Equal(TeamId, report.TeamId);
        Assert.Equal(SourceTenantId, report.SourceTenantId);
        Assert.Equal(TargetTenantId, report.TargetTenantId);
        Assert.False(report.IsApplied);
        Assert.Single(report.Changes);
        Assert.Single(report.Errors);
        Assert.Empty(report.Notes);

        Assert.NotNull(planned);
        Assert.Equal(SourceTenantId, planned.SourceTenant.TenantId);
        Assert.Equal(TargetTenantId, planned.TargetTenant.TenantId);
        Assert.Equal(
            (ControlPlaneTenantId, ResourceNodeTypes.Team, TeamId),
            (
                planned.TeamNode.TenantId,
                ((ResourceNode)planned.TeamNode).ResourceType,
                ((ResourceNode)planned.TeamNode).ResourceId
            )
        );
        Assert.Equal(
            (TargetTenantId, ResourceNodeTypes.Tenant, TargetTenantId),
            (
                planned.TargetTenantNode.TenantId,
                ((ResourceNode)planned.TargetTenantNode).ResourceType,
                ((ResourceNode)planned.TargetTenantNode).ResourceId
            )
        );
        Assert.True(
            JsonElement.DeepEquals(
                planned.TargetTenantSettings,
                CreateTenant(TargetTenantId).Settings.Value
            )
        );
    }

    [Fact]
    public async Task PreviewPromotionAsync_WhenALimitChangesAndTheTeamRefuses_ReturnsForbiddenNamingTheLimit()
    {
        SetupRouteAuthorization();
        SetupStore();
        SetupPlan(context =>
            context.AddChangedLimits(context.TeamNode, [OpenIdSettingNames.MaxTenants])
        );
        SetupAuthorize(
            ControlPlaneTenantId,
            ResourceNodeTypes.Team,
            TeamId,
            Operations.Update,
            AuthorizationResult.Failed()
        );

        // Strict mocks: no ApplyAsync setup, so a refused preview that applies would fail the test.
        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Contains("'max_tenants'", problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewPromotionAsync_WhenLimitsChangeAtOneNode_AuthorizesOnceNamingTheUnion()
    {
        SetupRouteAuthorization();
        SetupStore();
        SetupPlan(context =>
        {
            context.AddChangedLimits(context.TeamNode, ["b", "a"]);
            context.AddChangedLimits(context.TeamNode, ["a", "c"]);
        });
        SetupAuthorize(
            ControlPlaneTenantId,
            ResourceNodeTypes.Team,
            TeamId,
            Operations.Update,
            AuthorizationResult.Failed()
        );

        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Contains("'a', 'b', 'c'", problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewPromotionAsync_WhenTheFirstNodeAuthorizesAndTheSecondRefuses_ReturnsForbiddenNamingTheSecondLimit()
    {
        SetupRouteAuthorization();
        SetupStore();
        SetupPlan(context =>
        {
            context.AddChangedLimits(context.TeamNode, [OpenIdSettingNames.MaxTenants]);
            context.AddChangedLimits(
                context.TargetTenantNode,
                [OpenIdSettingNames.MaxConcurrentSessions]
            );
        });
        SetupAuthorize(
            ControlPlaneTenantId,
            ResourceNodeTypes.Team,
            TeamId,
            Operations.Update,
            AuthorizationResult.Success()
        );
        SetupAuthorize(
            TargetTenantId,
            ResourceNodeTypes.Tenant,
            TargetTenantId,
            Operations.Update,
            AuthorizationResult.Failed()
        );

        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Contains(
            "'max_concurrent_sessions'",
            problem.ProblemDetails.Detail,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "'max_tenants'",
            problem.ProblemDetails.Detail,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task PreviewPromotionAsync_WhenTheCallerMayNotGrantARoleTheClientHolds_ReturnsForbidden()
    {
        SetupRouteAuthorization();
        SetupStore();
        SetupPlan(context => context.AddRequiredCreateAuthority(CreateOtherNode()));
        SetupAuthorize(
            OtherTenantId,
            ResourceNodeTypes.Tenant,
            OtherTenantId,
            Operations.Create,
            AuthorizationResult.Failed()
        );

        // Strict mocks: no ApplyAsync setup, so a refused preview that applies would fail the test.
        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task PreviewPromotionAsync_WhenNodesDifferOnlyByCase_AuthorizesTheNodeOnce()
    {
        SetupRouteAuthorization();
        SetupStore();
        SetupPlan(context =>
        {
            context.AddRequiredCreateAuthority(CreateOtherNode());
            context.AddRequiredCreateAuthority(
                ResourceNode.For("OTHER-TENANT", "TENANT", "OTHER-TENANT")
            );
        });

        // Strict mocks: a second authorization for the upper-case node matches no setup, so it would fail the test.
        SetupAuthorize(
            OtherTenantId,
            ResourceNodeTypes.Tenant,
            OtherTenantId,
            Operations.Create,
            AuthorizationResult.Success()
        );

        var result = await Handler.PreviewPromotionAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        Assert.IsType<Ok<TenantPromotionResource>>(result);
    }

    #endregion

    #region PromoteAsync Tests

    [Fact]
    public async Task PromoteAsync_WhenTheTenantsAreEqual_ReturnsBadRequestBeforeAnyRead()
    {
        var result = await Handler.PromoteAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(SourceTenantId, SourceTenantId),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task PromoteAsync_WhenTheCallerMayNotReadTheSource_ReturnsForbiddenBeforeAnyRead()
    {
        SetupAuthorize(
            SourceTenantId,
            ResourceNodeTypes.Tenant,
            SourceTenantId,
            Operations.Read,
            AuthorizationResult.Failed()
        );

        var result = await Handler.PromoteAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task PromoteAsync_WhenTheCallerMayNotCreateInTheTarget_ReturnsForbiddenBeforeAnyRead()
    {
        SetupAuthorize(
            SourceTenantId,
            ResourceNodeTypes.Tenant,
            SourceTenantId,
            Operations.Read,
            AuthorizationResult.Success()
        );
        SetupAuthorize(
            TargetTenantId,
            ResourceNodeTypes.Tenant,
            TargetTenantId,
            Operations.Create,
            AuthorizationResult.Failed()
        );

        var result = await Handler.PromoteAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task PromoteAsync_WhenATenantDoesNotExist_ReturnsNotFound()
    {
        SetupRouteAuthorization();
        SetupStore(source: null, target: null);

        var result = await Handler.PromoteAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var notFound = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task PromoteAsync_WhenALimitChangesAndTheTeamRefuses_ReturnsForbiddenWithoutApplying()
    {
        SetupRouteAuthorization();
        SetupStore();
        SetupPlan(context =>
            context.AddChangedLimits(context.TeamNode, [OpenIdSettingNames.MaxTenants])
        );
        SetupAuthorize(
            ControlPlaneTenantId,
            ResourceNodeTypes.Team,
            TeamId,
            Operations.Update,
            AuthorizationResult.Failed()
        );

        // Strict mocks: no ApplyAsync setup, so applying a refused promotion would fail the test.
        var result = await Handler.PromoteAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Contains("'max_tenants'", problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PromoteAsync_WhenTheCallerMayNotGrantARoleTheClientHolds_ReturnsForbiddenWithoutApplying()
    {
        SetupRouteAuthorization();
        SetupStore();
        SetupPlan(context => context.AddRequiredCreateAuthority(CreateOtherNode()));
        SetupAuthorize(
            OtherTenantId,
            ResourceNodeTypes.Tenant,
            OtherTenantId,
            Operations.Create,
            AuthorizationResult.Failed()
        );

        var result = await Handler.PromoteAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task PromoteAsync_WhenThePlanHasErrors_ReturnsConflictWithTheErrorsWithoutApplying()
    {
        SetupRouteAuthorization();
        SetupStore();
        SetupPlan(AddError);

        // Strict mocks: no ApplyAsync setup, so applying a promotion with errors would fail the test.
        var result = await Handler.PromoteAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        var errors = Assert.IsAssignableFrom<IReadOnlyList<TenantPromotionErrorResource>>(
            problem.ProblemDetails.Extensions["errors"]
        );
        var error = Assert.Single(errors);
        Assert.Equal("client-1", error.ResourceId);
    }

    [Fact]
    public async Task PromoteAsync_WhenPlanned_AppliesRecordsTheAuditAndAnnouncesTheTarget()
    {
        SetupRouteAuthorization();
        SetupStore();
        SetupPlan(AddChange);
        SetupApply();

        JsonElement? resourceValues = null;
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordTenantChangedAsync(
                    It.IsAny<HttpContext>(),
                    TeamId,
                    TargetTenantId,
                    ResourceChangeTypes.Promoted,
                    It.IsAny<JsonElement?>()
                )
            )
            .Callback<HttpContext, string, string, string, JsonElement?>(
                (_, _, _, _, values) => resourceValues = values
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    new TenantChangedCommand(TargetTenantId, true),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.PromoteAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateRequest(),
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<TenantPromotionResource>>(result);
        var report = ok.Value;
        Assert.NotNull(report);
        Assert.True(report.IsApplied);
        Assert.Equal(TargetTenantId, report.TargetTenantId);
        Assert.Single(report.Changes);

        Assert.NotNull(resourceValues);
        Assert.True(resourceValues.Value.GetProperty("isApplied").GetBoolean());
        Assert.Equal(
            TargetTenantId,
            resourceValues.Value.GetProperty("targetTenantId").GetString()
        );
    }

    #endregion

    #region Map Tests

    [Theory]
    [InlineData("POST", "/teams/{teamId}/promotions/preview", new[] { 400, 401, 403, 404, 415 })]
    [InlineData("POST", "/teams/{teamId}/promotions", new[] { 400, 401, 403, 404, 409, 412, 415 })]
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
