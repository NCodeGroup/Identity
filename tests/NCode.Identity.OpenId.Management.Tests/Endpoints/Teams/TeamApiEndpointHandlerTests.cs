// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Operations;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

public sealed class TeamApiEndpointHandlerTests : IDisposable
{
    private const string TeamId = "team-1";
    private const string ParentTeamId = "acme";
    private const string ChildTeamId = "team-1-web";
    private const string ControlPlaneTenantId = "platform-root";
    private const string TenantId = "tenant-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITeamStore> MockTeamStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ITeamValidator> MockTeamValidator { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<ISettingScopeCollectionProvider> MockSettingScopeCollectionProvider { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IClientCoherenceValidator> MockClientCoherenceValidator { get; }
    private Mock<ITeamClientCoherenceChecker> MockTeamClientCoherenceChecker { get; }
    private Mock<IResourceOwnershipService> MockResourceOwnershipService { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private Mock<IMediator> MockMediator { get; }
    private TeamApiEndpointHandler Handler { get; }

    public TeamApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTeamStore = MockRepository.Create<ITeamStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockTeamValidator = MockRepository.Create<ITeamValidator>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockSettingScopeCollectionProvider =
            MockRepository.Create<ISettingScopeCollectionProvider>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockClientCoherenceValidator = MockRepository.Create<IClientCoherenceValidator>();
        MockTeamClientCoherenceChecker = MockRepository.Create<ITeamClientCoherenceChecker>();
        MockResourceOwnershipService = MockRepository.Create<IResourceOwnershipService>();
        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();
        MockMediator = MockRepository.Create<IMediator>();

        Handler = new TeamApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockTeamValidator.Object,
            TimeProvider.System,
            MockCryptoService.Object,
            MockSettingSerializer.Object,
            MockSettingScopeCollectionProvider.Object,
            MockEffectiveSettingsResolver.Object,
            MockClientCoherenceValidator.Object,
            MockTeamClientCoherenceChecker.Object,
            MockResourceOwnershipService.Object,
            MockManagementAuditRecorder.Object,
            NullLogger<TeamApiEndpointHandler>.Instance
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private void SetupStore()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();

        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable();

        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupStoreManagerOnly()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();

        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupSave() =>
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupTeam(PersistedTeam? team) =>
        MockTeamStore
            .Setup(x => x.GetOrDefaultAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team)
            .Verifiable();

    private void SetupTeamSettings(PersistedTeamSettings? settings) =>
        MockTeamStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();

    private void SetupAuthorization(AuthorizationResult result) =>
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    private void SetupAuthorizationCapture(Action<object?> capture) =>
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
                    capture(resource)
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();

    private void SetupAuthorizationCapture(
        Action<object?, IEnumerable<IAuthorizationRequirement>> capture
    ) =>
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .Callback(
                (
                    ClaimsPrincipal _,
                    object? resource,
                    IEnumerable<IAuthorizationRequirement> requirements
                ) => capture(resource, requirements)
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();

    private sealed class CapturedAuthorization
    {
        public object? Resource { get; set; }
        public IAuthorizationRequirement[] Requirements { get; set; } = [];
    }

    private CapturedAuthorization CaptureAuthorization()
    {
        var captured = new CapturedAuthorization();
        SetupAuthorizationCapture(
            (resource, required) =>
            {
                captured.Resource = resource;
                captured.Requirements = required.ToArray();
            }
        );
        return captured;
    }

    private static void AssertTeamNodeRequest(
        CapturedAuthorization captured,
        IAuthorizationRequirement operation
    )
    {
        Assert.Same(operation, Assert.Single(captured.Requirements));
        var node = Assert.IsType<ResourceNode>(captured.Resource);
        Assert.Equal(
            (ControlPlaneTenantId, ResourceNodeTypes.Team, TeamId),
            (node.TenantId, node.ResourceType, node.ResourceId)
        );
    }

    private void SetupValidSettings() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Team,
                    It.IsAny<JsonSerializerOptions>()
                )
            )
            .Returns([])
            .Verifiable();

    private void SetupNoChangedLimits() => SetupChangedLimits([]);

    private void SetupChangedLimits(IReadOnlyList<string> limits) =>
        MockSettingSerializer
            .Setup(x =>
                x.GetChangedLimits(
                    It.Is<JsonElement>(element => JsonElement.DeepEquals(element, EmptyObject())),
                    It.Is<JsonElement>(element => JsonElement.DeepEquals(element, FooObject()))
                )
            )
            .Returns(limits)
            .Verifiable();

    private void SetupInvalidSettings() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Team,
                    It.IsAny<JsonSerializerOptions>()
                )
            )
            .Returns([
                new SettingError(
                    "unregistered_setting",
                    "'unregistered_setting' is not a registered setting."
                ),
            ])
            .Verifiable();

    private void SetupTeamChangedAudit(string changeType) =>
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordTeamChangedAsync(
                    It.IsAny<HttpContext>(),
                    TeamId,
                    changeType,
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupTeamChangedAnnouncement(bool settingsChanged) =>
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    new TeamChangedCommand(TeamId, settingsChanged),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private static void AssertSettingsValidationProblem(IResult result)
    {
        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(["unregistered_setting"], problem.ProblemDetails.Errors.Keys);
    }

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

    private static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new JsonObject());

    private static JsonElement FooObject() =>
        JsonSerializer.SerializeToElement(new JsonObject { ["foo"] = "bar" });

    private static bool HasProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out _);

    private static PersistedTeam CreateTeam(
        string concurrencyToken = "team-ct",
        string? parentTeamId = null,
        PersistedTeamSettings? settings = null
    ) =>
        new()
        {
            TeamId = TeamId,
            ConcurrencyToken = concurrencyToken,
            IsDisabled = false,
            ParentTeamId = parentTeamId,
            DisplayName = "Team One",
            Settings = settings ?? CreateSettings("settings-ct"),
        };

    private static PersistedTeamSettings CreateSettings(string concurrencyToken) =>
        new()
        {
            TeamId = TeamId,
            ConcurrencyToken = concurrencyToken,
            Value = EmptyObject(),
        };

    private static PersistedTeam CreateChildTeam(string childTeamId) =>
        new()
        {
            TeamId = childTeamId,
            ConcurrencyToken = "child-ct",
            IsDisabled = false,
            ParentTeamId = TeamId,
            DisplayName = "Team One Web",
            Settings = new PersistedTeamSettings
            {
                TeamId = childTeamId,
                ConcurrencyToken = "child-settings-ct",
                Value = EmptyObject(),
            },
        };

    private static PersistedRoleAssignment CreateAssignment(string principalId, string roleName) =>
        new()
        {
            TenantId = ControlPlaneTenantId,
            AssignmentId = $"assignment-{principalId}",
            PrincipalId = principalId,
            RoleName = roleName,
            ResourceType = ResourceNodeTypes.Team,
            ResourceId = TeamId,
            ConcurrencyToken = "assignment-ct",
        };

    private static ISettingCollection CreateSettingCollection(params Setting[] settings)
    {
        var list = settings.ToList();
        var mock = new Mock<ISettingCollection>(MockBehavior.Loose);
        mock.Setup(x => x.GetEnumerator()).Returns(() => list.GetEnumerator());
        return mock.Object;
    }

    private static JsonPatchDocument<JsonObject> CreateAddPatch(string name, string value)
    {
        var patch = new JsonPatchDocument<JsonObject>();
        patch.Operations.Add(
            new Operation<JsonObject>("add", $"/{name}", from: null, value: value)
        );
        return patch;
    }

    private static JsonPatchDocument<UpdateTeamRequest> CreateDisablePatch()
    {
        var patch = new JsonPatchDocument<UpdateTeamRequest>();
        patch.Operations.Add(
            new Operation<UpdateTeamRequest>("replace", "/isDisabled", from: null, value: true)
        );
        return patch;
    }

    #endregion

    #region GetTeamAsync Tests

    [Fact]
    public async Task GetTeamAsync_WhenFoundAndAuthorized_ReturnsJsonWithTheETag()
    {
        SetupStore();
        SetupTeam(CreateTeam("team-ct"));
        object? authorizedResource = null;
        SetupAuthorizationCapture(resource => authorizedResource = resource);

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetTeamAsync(httpContext, TeamId, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<TeamResource>>(result);
        Assert.Equal(TeamId, json.Value?.TeamId);
        Assert.Equal("Team One", json.Value?.DisplayName);
        Assert.Equal("team-ct", httpContext.Response.Headers.ETag);
        var node = Assert.IsType<ResourceNode>(authorizedResource);
        Assert.Equal(
            (ControlPlaneTenantId, ResourceNodeTypes.Team, TeamId),
            (node.TenantId, node.ResourceType, node.ResourceId)
        );
    }

    [Fact]
    public async Task GetTeamAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        SetupTeam(null);
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.GetTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetTeamAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTeam()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region ListTeamsAsync Tests

    [Fact]
    public async Task ListTeamsAsync_WhenAuthorized_ReturnsEachTeam()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockTeamStore
            .Setup(x => x.GetPageAsync(It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PagedResult<PersistedTeam> { Items = [CreateTeam()], NextCursor = "next" }
            )
            .Verifiable();

        var result = await Handler.ListTeamsAsync(
            CreateHttpContext(authenticated: true),
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<TeamResource>>>(result);
        var value = json.Value;
        Assert.NotNull(value);
        var team = Assert.Single(value.Items);
        Assert.Equal(TeamId, team.TeamId);
        Assert.Equal("next", value.ContinuationToken);
    }

    [Fact]
    public async Task ListTeamsAsync_RequestsListOnThePlatformNode()
    {
        SetupStore();
        MockTeamStore
            .Setup(x => x.GetPageAsync(It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PersistedTeam> { Items = [], NextCursor = null })
            .Verifiable();
        var captured = CaptureAuthorization();

        await Handler.ListTeamsAsync(
            CreateHttpContext(authenticated: true),
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        Assert.Same(Operations.List, Assert.Single(captured.Requirements));
        var node = Assert.IsType<ResourceNode>(captured.Resource);
        Assert.Equal(
            (ControlPlaneTenantId, ResourceNodeTypes.Platform, OpenIdConstants.PlatformResourceId),
            (node.TenantId, node.ResourceType, node.ResourceId)
        );
    }

    [Fact]
    public async Task ListTeamsAsync_WhenUnauthenticated_ReturnsUnauthorizedWithoutReadingTheStore()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.ListTeamsAsync(
            CreateHttpContext(authenticated: false),
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    #endregion

    #region ListChildTeamsAsync Tests

    [Fact]
    public async Task ListChildTeamsAsync_WhenAuthorized_ReturnsEachChildTeam()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockTeamStore
            .Setup(x =>
                x.GetChildPageAsync(TeamId, It.IsAny<PageRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new PagedResult<PersistedTeam>
                {
                    Items = [CreateChildTeam(ChildTeamId)],
                    NextCursor = "next",
                }
            )
            .Verifiable();

        var result = await Handler.ListChildTeamsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<TeamResource>>>(result);
        var value = json.Value;
        Assert.NotNull(value);
        var team = Assert.Single(value.Items);
        Assert.Equal(ChildTeamId, team.TeamId);
        Assert.Equal(TeamId, team.ParentTeamId);
        Assert.Equal("next", value.ContinuationToken);
    }

    [Fact]
    public async Task ListChildTeamsAsync_RequestsListOnTheParentTeamNode()
    {
        SetupStore();
        MockTeamStore
            .Setup(x =>
                x.GetChildPageAsync(TeamId, It.IsAny<PageRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new PagedResult<PersistedTeam> { Items = [], NextCursor = null })
            .Verifiable();
        var captured = CaptureAuthorization();

        await Handler.ListChildTeamsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        AssertTeamNodeRequest(captured, Operations.List);
    }

    [Fact]
    public async Task ListChildTeamsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheStore()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.ListChildTeamsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region ToTeamResource Tests

    [Fact]
    public void ToTeamResource_WithAParent_CarriesParentTeamId()
    {
        var resource = TeamApiEndpointHandler.ToTeamResource(CreateChildTeam(ChildTeamId));

        Assert.Equal(TeamId, resource.ParentTeamId);
    }

    [Fact]
    public void ToTeamResource_WithoutAParent_CarriesNoParentTeamId()
    {
        var resource = TeamApiEndpointHandler.ToTeamResource(CreateTeam());

        Assert.Null(resource.ParentTeamId);
    }

    #endregion

    #region CreateTeamAsync Tests

    private static CreateTeamRequest CreateCreateRequest() =>
        new()
        {
            TeamId = TeamId,
            DisplayName = "Team One",
            IsDisabled = false,
            Settings = EmptyObject(),
        };

    private void SetupCreateAuthorization(ManagementError? error) =>
        MockTeamValidator
            .Setup(x =>
                x.AuthorizeCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    ControlPlaneTenantId,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    private void SetupCreateValidation(ManagementError? error) =>
        MockTeamValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<PersistedTeam>(),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    [Fact]
    public async Task CreateTeamAsync_WhenValid_SeedsTheOwnerAndAdminAssignmentsAndReturnsCreated()
    {
        SetupCreateAuthorization(null);
        SetupStore();
        SetupCreateValidation(null);
        SetupValidSettings();
        SetupSave();
        PersistedTeam? added = null;
        MockTeamStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedTeam>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedTeam team, CancellationToken _) => added = team)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockResourceOwnershipService
            .Setup(x =>
                x.AssignCreatorAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    MockStoreManager.Object,
                    ControlPlaneTenantId,
                    ResourceNodeTypes.Team,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockResourceOwnershipService
            .Setup(x =>
                x.GetOwnersAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Team,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateAssignment("principal-1", BuiltInRoles.Owner)])
            .Verifiable();
        MockResourceOwnershipService
            .Setup(x =>
                x.AddAssignmentAsync(
                    MockStoreManager.Object,
                    ControlPlaneTenantId,
                    ResourceNodeTypes.Team,
                    TeamId,
                    "principal-1",
                    BuiltInRoles.TeamAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateAssignment("principal-1", BuiltInRoles.TeamAdmin))
            .Verifiable();
        SetupTeam(CreateTeam("reloaded-ct"));
        SetupTeamChangedAudit(ResourceChangeTypes.Created);

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateTeamAsync(
            httpContext,
            CreateCreateRequest(),
            CancellationToken.None
        );

        var created = Assert.IsType<Created<TeamResource>>(result);
        Assert.Equal("reloaded-ct", created.Value?.ConcurrencyToken);
        Assert.Equal("reloaded-ct", httpContext.Response.Headers.ETag);
        Assert.Equal($"/teams/{TeamId}", created.Location);
        Assert.Equal(TeamId, created.Value?.TeamId);
        Assert.Equal(TeamId, added?.TeamId);
        Assert.Equal(TeamId, added?.Settings.TeamId);
    }

    [Fact]
    public async Task CreateTeamAsync_WhenTheCreatorHasNoSubject_GrantsNoTeamAdmin()
    {
        SetupCreateAuthorization(null);
        SetupStore();
        SetupCreateValidation(null);
        SetupValidSettings();
        SetupSave();
        MockTeamStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedTeam>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockResourceOwnershipService
            .Setup(x =>
                x.AssignCreatorAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    MockStoreManager.Object,
                    ControlPlaneTenantId,
                    ResourceNodeTypes.Team,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        // A caller that resolves to no principal becomes no owner, so no AddAssignmentAsync setup exists: the strict
        // mock fails the test if a TeamAdmin assignment is granted.
        MockResourceOwnershipService
            .Setup(x =>
                x.GetOwnersAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Team,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([])
            .Verifiable();
        SetupTeam(CreateTeam("reloaded-ct"));
        SetupTeamChangedAudit(ResourceChangeTypes.Created);

        var result = await Handler.CreateTeamAsync(
            CreateHttpContext(authenticated: true),
            CreateCreateRequest(),
            CancellationToken.None
        );

        Assert.IsType<Created<TeamResource>>(result);
    }

    [Fact]
    public async Task CreateTeamAsync_WhenTheValidatorRefuses_ReturnsItsError()
    {
        SetupCreateAuthorization(null);
        SetupStore();
        SetupCreateValidation(
            new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "A team with the specified identifier already exists.",
            }
        );

        var result = await Handler.CreateTeamAsync(
            CreateHttpContext(authenticated: true),
            CreateCreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task CreateTeamAsync_WhenUnauthorized_ReturnsTheErrorWithoutOpeningTheStore()
    {
        SetupCreateAuthorization(
            new ManagementError
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Detail = "The caller is not authorized to create this team.",
            }
        );

        var result = await Handler.CreateTeamAsync(
            CreateHttpContext(authenticated: true),
            CreateCreateRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task CreateTeamAsync_WhenTheSettingsCannotBeHonored_ReturnsValidationProblem()
    {
        SetupCreateAuthorization(null);
        SetupStore();
        SetupCreateValidation(null);
        SetupInvalidSettings();

        var result = await Handler.CreateTeamAsync(
            CreateHttpContext(authenticated: true),
            CreateCreateRequest(),
            CancellationToken.None
        );

        AssertSettingsValidationProblem(result);
    }

    #endregion

    #region CreateChildTeamAsync Tests

    private static CreateTeamRequest CreateChildRequest() =>
        new()
        {
            TeamId = ChildTeamId,
            DisplayName = "Team One Web",
            IsDisabled = false,
            Settings = EmptyObject(),
        };

    private void SetupChildCreateAuthorization(ManagementError? error) =>
        MockTeamValidator
            .Setup(x =>
                x.AuthorizeCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    ControlPlaneTenantId,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    private void SetupLineageAndMaxDepth(IReadOnlyList<PersistedTeam> lineage, int? maxDepth)
    {
        MockTeamStore
            .Setup(x => x.GetLineageAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lineage)
            .Verifiable();

        var platformSettings = MockRepository.Create<IReadOnlySettingCollection>();
        var configuredDepth = maxDepth ?? 0;
        platformSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.MaxTeamDepth, out configuredDepth))
            .Returns(maxDepth is not null)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolvePlatformAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(platformSettings.Object)
            .Verifiable();
    }

    private void SetupAncestorMaxTeams(string ancestorId, int? maxTeams, long? descendants)
    {
        var ancestorSettings = MockRepository.Create<IReadOnlySettingCollection>();
        var configuredMaxTeams = maxTeams ?? 0;
        ancestorSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.MaxTeams, out configuredMaxTeams))
            .Returns(maxTeams is not null)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync(ancestorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ancestorSettings.Object)
            .Verifiable();

        if (descendants is not null)
        {
            MockTeamStore
                .Setup(x => x.CountDescendantsAsync(ancestorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(descendants.Value)
                .Verifiable();
        }
    }

    [Fact]
    public async Task CreateChildTeamAsync_WhenValid_AddsTheTeamBeneathItsParent()
    {
        SetupChildCreateAuthorization(null);
        SetupStore();
        SetupCreateValidation(null);
        SetupValidSettings();
        SetupLineageAndMaxDepth([CreateTeam()], maxDepth: null);
        SetupAncestorMaxTeams(TeamId, maxTeams: null, descendants: null);
        SetupSave();
        PersistedTeam? added = null;
        MockTeamStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedTeam>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedTeam team, CancellationToken _) => added = team)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockResourceOwnershipService
            .Setup(x =>
                x.AssignCreatorAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    MockStoreManager.Object,
                    ControlPlaneTenantId,
                    ResourceNodeTypes.Team,
                    ChildTeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockResourceOwnershipService
            .Setup(x =>
                x.GetOwnersAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Team,
                    ChildTeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([])
            .Verifiable();
        MockTeamStore
            .Setup(x => x.GetOrDefaultAsync(ChildTeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateChildTeam(ChildTeamId))
            .Verifiable();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordTeamChangedAsync(
                    It.IsAny<HttpContext>(),
                    ChildTeamId,
                    ResourceChangeTypes.Created,
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.CreateChildTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateChildRequest(),
            CancellationToken.None
        );

        var created = Assert.IsType<Created<TeamResource>>(result);
        Assert.Equal($"/teams/{ChildTeamId}", created.Location);
        Assert.Equal(ChildTeamId, created.Value?.TeamId);
        Assert.Equal(TeamId, created.Value?.ParentTeamId);
        Assert.Equal(ChildTeamId, added?.TeamId);
        Assert.Equal(TeamId, added?.ParentTeamId);
    }

    [Fact]
    public async Task CreateChildTeamAsync_WhenUnauthorized_ReturnsTheErrorWithoutOpeningTheStore()
    {
        SetupChildCreateAuthorization(
            new ManagementError
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Detail = "The caller is not authorized to create this team.",
            }
        );

        var result = await Handler.CreateChildTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateChildRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task CreateChildTeamAsync_WhenTheValidatorRefuses_ReturnsItsError()
    {
        SetupChildCreateAuthorization(null);
        SetupStore();
        SetupCreateValidation(
            new ManagementError
            {
                StatusCode = StatusCodes.Status404NotFound,
                Detail = "The parent team does not exist.",
            }
        );

        var result = await Handler.CreateChildTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateChildRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task CreateChildTeamAsync_WhenNestedPastTheMaximumDepth_ReturnsConflict()
    {
        SetupChildCreateAuthorization(null);
        SetupStore();
        SetupCreateValidation(null);
        SetupValidSettings();
        SetupLineageAndMaxDepth([CreateTeam()], maxDepth: 1);

        var result = await Handler.CreateChildTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateChildRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task CreateChildTeamAsync_WhenAnAncestorHoldsItsMaximumTeams_ReturnsConflict()
    {
        SetupChildCreateAuthorization(null);
        SetupStore();
        SetupCreateValidation(null);
        SetupValidSettings();
        SetupLineageAndMaxDepth([CreateTeam()], maxDepth: null);
        SetupAncestorMaxTeams(TeamId, maxTeams: 2, descendants: 2);

        var result = await Handler.CreateChildTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateChildRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    #endregion

    #region UpdateTeamAsync Tests

    private void SetupUpdateAuthorization(ManagementError? error) =>
        MockTeamValidator
            .Setup(x =>
                x.AuthorizeUpdateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    ControlPlaneTenantId,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    private void SetupUpdateValidation(ManagementError? error, string? ifMatch = null) =>
        MockTeamValidator
            .Setup(x =>
                x.ValidateUpdateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    ControlPlaneTenantId,
                    It.IsAny<PersistedTeam>(),
                    It.IsAny<UpdateTeamRequest>(),
                    ifMatch,
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    [Fact]
    public async Task UpdateTeamAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheTeam()
    {
        SetupUpdateAuthorization(
            new ManagementError
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Detail = "The caller is not authorized to update this team.",
            }
        );

        var result = await Handler.UpdateTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateDisablePatch(),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateTeamAsync_WhenDisabling_WritesTheTeamAndAnnouncesTheChange()
    {
        SetupUpdateAuthorization(null);
        SetupStore();
        var team = CreateTeam();
        SetupTeam(team);
        SetupUpdateValidation(null, ifMatch: "team-ct");
        MockTeamStore
            .Setup(x => x.UpdateAsync(team, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupSave();
        SetupTeamChangedAudit(ResourceChangeTypes.Updated);
        SetupTeamChangedAnnouncement(settingsChanged: false);

        var result = await Handler.UpdateTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateDisablePatch(),
            "team-ct",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.True(team.IsDisabled);
        Assert.Equal("Team One", team.DisplayName);
    }

    [Fact]
    public async Task UpdateTeamAsync_WhenTheIfMatchTokenIsStale_ReturnsPreconditionFailed()
    {
        SetupUpdateAuthorization(null);
        SetupStore();
        SetupTeam(CreateTeam());
        SetupUpdateValidation(
            new ManagementError
            {
                StatusCode = StatusCodes.Status412PreconditionFailed,
                Detail = "The supplied If-Match token does not match the current team.",
            },
            ifMatch: "stale"
        );

        var result = await Handler.UpdateTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateDisablePatch(),
            "stale",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateTeamAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupUpdateAuthorization(null);
        SetupStore();
        SetupTeam(null);

        var result = await Handler.UpdateTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateDisablePatch(),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task UpdateTeamAsync_WhenThePatchCannotBeApplied_ReturnsBadRequest()
    {
        SetupUpdateAuthorization(null);
        SetupStore();
        SetupTeam(CreateTeam());
        var patch = new JsonPatchDocument<UpdateTeamRequest>();
        patch.Operations.Add(
            new Operation<UpdateTeamRequest>("replace", "/missing", from: null, value: "x")
        );

        var result = await Handler.UpdateTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            patch,
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    #endregion

    #region DeleteTeamAsync Tests

    private void SetupDeleteAuthorization(ManagementError? error) =>
        MockTeamValidator
            .Setup(x =>
                x.AuthorizeDeleteAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    ControlPlaneTenantId,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    private void SetupDeleteValidation(ManagementError? error) =>
        MockTeamValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    ControlPlaneTenantId,
                    It.IsAny<PersistedTeam>(),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    [Fact]
    public async Task DeleteTeamAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheTeam()
    {
        SetupDeleteAuthorization(
            new ManagementError
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Detail = "The caller is not authorized to delete this team.",
            }
        );

        var result = await Handler.DeleteTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteTeamAsync_WhenValid_RemovesTheTeamAndAnnouncesTheChange()
    {
        SetupDeleteAuthorization(null);
        SetupStore();
        SetupTeam(CreateTeam());
        SetupDeleteValidation(null);
        MockTeamStore
            .Setup(x => x.RemoveAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        SetupSave();
        SetupTeamChangedAudit(ResourceChangeTypes.Deleted);
        SetupTeamChangedAnnouncement(settingsChanged: false);

        var result = await Handler.DeleteTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task DeleteTeamAsync_WhenTheValidatorRefuses_ReturnsConflictWithoutRemoving()
    {
        SetupDeleteAuthorization(null);
        SetupStore();
        SetupTeam(CreateTeam());
        SetupDeleteValidation(
            new ManagementError
            {
                StatusCode = StatusCodes.Status409Conflict,
                Detail = "The team cannot be deleted while it still has tenants.",
            }
        );

        var result = await Handler.DeleteTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteTeamAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupDeleteAuthorization(null);
        SetupStore();
        SetupTeam(null);

        var result = await Handler.DeleteTeamAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    #endregion

    #region GetSettingsAsync Tests

    [Fact]
    public async Task GetSettingsAsync_WhenFoundAndAuthorized_ReturnsJsonWithTheETag()
    {
        SetupStore();
        SetupTeamSettings(CreateSettings("settings-ct"));
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSettingsAsync(httpContext, TeamId, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<TeamSettingsResource>>(result);
        Assert.Equal(TeamId, json.Value?.TeamId);
        Assert.Equal("settings-ct", httpContext.Response.Headers.ETag);
    }

    [Fact]
    public async Task GetSettingsAsync_RequestsReadOnTheTeamNode()
    {
        SetupStore();
        SetupTeamSettings(CreateSettings("settings-ct"));
        var captured = CaptureAuthorization();

        await Handler.GetSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        AssertTeamNodeRequest(captured, Operations.Read);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        SetupTeamSettings(null);
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.GetSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTeam()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region GetEffectiveSettingsAsync Tests

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenFoundAndAuthorized_ReturnsFlatSettingsWithoutAnETag()
    {
        SetupStore();
        SetupTeam(CreateTeam());
        SetupAuthorization(AuthorizationResult.Success());
        var descriptor = new SettingDescriptor<bool>
        {
            Name = "claims_parameter_supported",
            AppliesTo = ["client"],
        };
        var mockScopes = MockRepository.Create<ISettingScopeCollection>();
        mockScopes
            .Setup(x => x.AppliesWithin(descriptor, OpenIdSettingScopes.Team))
            .Returns(true)
            .Verifiable();
        MockSettingScopeCollectionProvider
            .Setup(x => x.Collection)
            .Returns(mockScopes.Object)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettingCollection(descriptor.Create(true)))
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetEffectiveSettingsAsync(
            httpContext,
            TeamId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<TeamEffectiveSettingsResource>>(result);
        var value = json.Value;
        Assert.NotNull(value);
        Assert.Equal(TeamId, value.TeamId);
        Assert.True(value.Settings.GetProperty("claims_parameter_supported").GetBoolean());
        Assert.False(httpContext.Response.Headers.ContainsKey("ETag"));
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_RequestsReadOnTheTeamNode()
    {
        SetupStore();
        SetupTeam(CreateTeam());
        var captured = CaptureAuthorization();
        var mockScopes = MockRepository.Create<ISettingScopeCollection>();
        MockSettingScopeCollectionProvider
            .Setup(x => x.Collection)
            .Returns(mockScopes.Object)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettingCollection())
            .Verifiable();

        await Handler.GetEffectiveSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        AssertTeamNodeRequest(captured, Operations.Read);
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        SetupTeam(null);
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.GetEffectiveSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTeam()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetEffectiveSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region UpdateSettingsAsync Tests

    private void SetupResolveTeam(
        IReadOnlySettingCollection current,
        IReadOnlySettingCollection proposed
    )
    {
        var platform = CreateSettingCollection();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamParentAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(platform)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    platform,
                    OpenIdSettingScopes.Team,
                    It.Is<JsonElement>(e => !HasProperty(e, "foo"))
                )
            )
            .Returns(current)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    platform,
                    OpenIdSettingScopes.Team,
                    It.Is<JsonElement>(e => HasProperty(e, "foo"))
                )
            )
            .Returns(proposed)
            .Verifiable();
    }

    private void SetupSettingsWrite(PersistedTeamSettings settings)
    {
        MockTeamStore
            .Setup(x => x.UpdateSettingsAsync(settings, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupSave();
        SetupTeamChangedAudit(ResourceChangeTypes.SettingsUpdated);
        SetupTeamChangedAnnouncement(settingsChanged: true);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenNoSettingACoherenceRuleReadsChanges_SkipsTheTenantWalk()
    {
        SetupValidSettings();
        SetupNoChangedLimits();
        SetupStore();
        var settings = CreateSettings("settings-ct");
        SetupTeam(CreateTeam(settings: settings));
        SetupAuthorization(AuthorizationResult.Success());
        var current = CreateSettingCollection();
        var proposed = CreateSettingCollection();
        SetupResolveTeam(current, proposed);
        MockClientCoherenceValidator
            .Setup(x => x.ReadsChangedSetting(current, proposed))
            .Returns(false)
            .Verifiable();
        SetupSettingsWrite(settings);

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no tenant-store setup, so walking the tenants would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.Equal("settings-ct", httpContext.Response.Headers.ETag);
        Assert.Equal("bar", settings.Value.GetProperty("foo").GetString());
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenARuleReadsAChangedSetting_ChecksTheTeamsSubtreeAndAnnouncesTheChange()
    {
        SetupValidSettings();
        SetupNoChangedLimits();
        SetupStore();
        var settings = CreateSettings("settings-ct");
        SetupTeam(CreateTeam(settings: settings));
        SetupAuthorization(AuthorizationResult.Success());
        var current = CreateSettingCollection();
        var proposed = CreateSettingCollection();
        SetupResolveTeam(current, proposed);
        MockClientCoherenceValidator
            .Setup(x => x.ReadsChangedSetting(current, proposed))
            .Returns(true)
            .Verifiable();
        MockTeamClientCoherenceChecker
            .Setup(x =>
                x.FindIntroducedViolationsAsync(
                    MockStoreManager.Object,
                    TeamId,
                    current,
                    proposed,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([])
            .Verifiable();
        SetupSettingsWrite(settings);

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheReplacedDocumentCannotBeHonored_WritesWithoutWalkingTheTenants()
    {
        SetupValidSettings();
        SetupNoChangedLimits();
        SetupStore();
        var settings = CreateSettings("settings-ct");
        SetupTeam(CreateTeam(settings: settings));
        SetupAuthorization(AuthorizationResult.Success());
        var platform = CreateSettingCollection();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamParentAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(platform)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    platform,
                    OpenIdSettingScopes.Team,
                    It.Is<JsonElement>(e => !HasProperty(e, "foo"))
                )
            )
            .Throws(new JsonException("The settings are invalid."))
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    platform,
                    OpenIdSettingScopes.Team,
                    It.Is<JsonElement>(e => HasProperty(e, "foo"))
                )
            )
            .Returns(CreateSettingCollection())
            .Verifiable();
        SetupSettingsWrite(settings);

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenADocumentAboveTheTeamCannotBeHonored_ThrowsNamingItsOwner()
    {
        SetupValidSettings();
        SetupNoChangedLimits();
        SetupStore();
        SetupTeam(CreateTeam());
        SetupAuthorization(AuthorizationResult.Success());
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamParentAsync(TeamId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new UnreadableSettingsException(
                    OpenIdSettingScopes.Platform,
                    OpenIdConstants.PlatformResourceId,
                    new JsonException("The platform settings are invalid.")
                )
            )
            .Verifiable();

        // Strict mocks: no team document resolution and no write are set up, so either would fail the test.
        var exception = await Assert.ThrowsAsync<UnreadableSettingsException>(() =>
            Handler
                .UpdateSettingsAsync(
                    CreateHttpContext(authenticated: true),
                    TeamId,
                    CreateAddPatch("foo", "bar"),
                    ifMatch: null,
                    CancellationToken.None
                )
                .AsTask()
        );

        Assert.Equal(OpenIdSettingScopes.Platform, exception.ScopeName);
        Assert.Equal(OpenIdConstants.PlatformResourceId, exception.OwnerId);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenAClientWouldBecomeIncoherent_ReturnsValidationProblemWithoutWriting()
    {
        SetupValidSettings();
        SetupNoChangedLimits();
        SetupStore();
        SetupTeam(CreateTeam());
        SetupAuthorization(AuthorizationResult.Success());
        var current = CreateSettingCollection();
        var proposed = CreateSettingCollection();
        SetupResolveTeam(current, proposed);
        MockClientCoherenceValidator
            .Setup(x => x.ReadsChangedSetting(current, proposed))
            .Returns(true)
            .Verifiable();
        MockTeamClientCoherenceChecker
            .Setup(x =>
                x.FindIntroducedViolationsAsync(
                    MockStoreManager.Object,
                    TeamId,
                    current,
                    proposed,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new SettingError(
                    OpenIdSettingNames.IdTokenSigningAlgValuesSupported,
                    $"The change would make the client 'client-1' of tenant '{TenantId}' incoherent: the value 'RS256' is not permitted."
                ),
            ])
            .Verifiable();

        // Strict mocks: no UpdateSettingsAsync, SaveChangesAsync, audit or announcement setup, so a write would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(
            [OpenIdSettingNames.IdTokenSigningAlgValuesSupported],
            problem.ProblemDetails.Errors.Keys
        );
        Assert.Contains("'client-1'", problem.ProblemDetails.Detail, StringComparison.Ordinal);
        Assert.Contains(
            $"of tenant '{TenantId}'",
            problem.ProblemDetails.Detail,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenALimitChangesAndThePlatformRefuses_ReturnsForbiddenWithoutWriting()
    {
        SetupValidSettings();
        SetupChangedLimits([OpenIdSettingNames.MaxTenants]);
        SetupStore();
        SetupTeam(CreateTeam());
        SetupAuthorization(AuthorizationResult.Success());
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.Platform
                    ),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Failed())
            .Verifiable();

        // Strict mocks: no resolver or store-write setup, so continuing past the refusal would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Contains("'max_tenants'", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenAChildTeamsLimitChangesAndItsParentRefuses_ReturnsForbiddenWithoutWriting()
    {
        SetupValidSettings();
        SetupChangedLimits([OpenIdSettingNames.MaxTenants]);
        SetupStore();
        SetupTeam(CreateTeam(parentTeamId: ParentTeamId));
        SetupAuthorization(AuthorizationResult.Success());
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.Team
                        && ((ResourceNode)resource).ResourceId == ParentTeamId
                    ),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Failed())
            .Verifiable();

        // Strict mocks: no resolver or store-write setup, so continuing past the refusal would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Contains("'max_tenants'", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenAChildTeamsLimitChangesAndItsParentAuthorizes_Writes()
    {
        SetupValidSettings();
        SetupChangedLimits([OpenIdSettingNames.MaxTenants]);
        SetupStore();
        var settings = CreateSettings("settings-ct");
        SetupTeam(CreateTeam(parentTeamId: ParentTeamId, settings: settings));
        SetupAuthorization(AuthorizationResult.Success());
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.Team
                        && ((ResourceNode)resource).ResourceId == ParentTeamId
                    ),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();
        var current = CreateSettingCollection();
        var proposed = CreateSettingCollection();
        SetupResolveTeam(current, proposed);
        MockClientCoherenceValidator
            .Setup(x => x.ReadsChangedSetting(current, proposed))
            .Returns(false)
            .Verifiable();
        SetupSettingsWrite(settings);

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenALimitChangesAndThePlatformAuthorizes_Writes()
    {
        SetupValidSettings();
        SetupChangedLimits([OpenIdSettingNames.MaxTenants]);
        SetupStore();
        var settings = CreateSettings("settings-ct");
        SetupTeam(CreateTeam(settings: settings));
        SetupAuthorization(AuthorizationResult.Success());
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.Platform
                    ),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();
        var current = CreateSettingCollection();
        var proposed = CreateSettingCollection();
        SetupResolveTeam(current, proposed);
        MockClientCoherenceValidator
            .Setup(x => x.ReadsChangedSetting(current, proposed))
            .Returns(false)
            .Verifiable();
        SetupSettingsWrite(settings);

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheSettingsCannotBeHonored_ReturnsValidationProblem()
    {
        SetupInvalidSettings();
        SetupStore();
        SetupTeam(CreateTeam());
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        AssertSettingsValidationProblem(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenIfMatchMismatch_ReturnsPreconditionFailed()
    {
        SetupStore();
        SetupTeam(CreateTeam());
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: "stale",
            CancellationToken.None
        );

        var status = Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, status.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_RequestsUpdateOnTheTeamNode()
    {
        SetupStore();
        SetupTeam(CreateTeam());
        var captured = CaptureAuthorization();

        await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: "stale",
            CancellationToken.None
        );

        AssertTeamNodeRequest(captured, Operations.Update);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        SetupTeam(null);
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTeam()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region GetUsageAsync Tests

    [Fact]
    public async Task GetUsageAsync_WhenAuthorized_ReturnsTheTeamsLedgerWithEachTeamAndTenantId()
    {
        var from = new DateOnly(2026, 10, 1);
        var to = new DateOnly(2026, 10, 2);
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTeam(CreateTeam());
        var mockUsageStore = MockRepository.Create<ITenantUsageStore>();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantUsageStore>())
            .Returns(mockUsageStore.Object)
            .Verifiable();
        mockUsageStore
            .Setup(x => x.ListByTeamAsync($"/{TeamId}/", from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PersistedTenantUsage
                {
                    TenantId = "tenant-a",
                    TeamPath = $"/{TeamId}/",
                    Day = from,
                    Meter = "requests",
                    Count = 42,
                },
                new PersistedTenantUsage
                {
                    TenantId = "tenant-b",
                    TeamPath = $"/{TeamId}/",
                    Day = to,
                    Meter = "tokens",
                    Count = 7,
                },
            ])
            .Verifiable();

        var result = await Handler.GetUsageAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            from,
            to,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<TeamUsageResource>>(result);
        var value = ok.Value;
        Assert.NotNull(value);
        Assert.Equal(TeamId, value.TeamId);
        Assert.Equal(from, value.From);
        Assert.Equal(to, value.To);
        Assert.Equal(
            [(TeamId, "tenant-a", from, "requests", 42L), (TeamId, "tenant-b", to, "tokens", 7L)],
            value.Entries.Select(entry =>
                (entry.TeamId, entry.TenantId, entry.Day, entry.Meter, entry.Count)
            )
        );
    }

    [Fact]
    public async Task GetUsageAsync_WithAChildTeam_ListsItsTenantsWithTheirTeam()
    {
        var day = new DateOnly(2026, 10, 1);
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTeam(CreateTeam());
        var mockUsageStore = MockRepository.Create<ITenantUsageStore>();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantUsageStore>())
            .Returns(mockUsageStore.Object)
            .Verifiable();
        mockUsageStore
            .Setup(x => x.ListByTeamAsync($"/{TeamId}/", day, day, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PersistedTenantUsage
                {
                    TenantId = "tenant-a",
                    TeamPath = $"/{TeamId}/{ChildTeamId}/",
                    Day = day,
                    Meter = "requests",
                    Count = 5,
                },
            ])
            .Verifiable();

        var result = await Handler.GetUsageAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            day,
            day,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<TeamUsageResource>>(result);
        var value = ok.Value;
        Assert.NotNull(value);
        var entry = Assert.Single(value.Entries);
        Assert.Equal(ChildTeamId, entry.TeamId);
        Assert.Equal("tenant-a", entry.TenantId);
    }

    [Fact]
    public async Task GetUsageAsync_ForAChildTeam_ReadsTheLedgerUnderItsPathFromItsTopLevelTeam()
    {
        var day = new DateOnly(2026, 10, 1);
        var childTeam = CreateChildTeam(ChildTeamId);
        childTeam.AncestorTeamIds = [TeamId];
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockTeamStore
            .Setup(x => x.GetOrDefaultAsync(ChildTeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(childTeam)
            .Verifiable();
        var mockUsageStore = MockRepository.Create<ITenantUsageStore>();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantUsageStore>())
            .Returns(mockUsageStore.Object)
            .Verifiable();
        mockUsageStore
            .Setup(x =>
                x.ListByTeamAsync(
                    $"/{TeamId}/{ChildTeamId}/",
                    day,
                    day,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new PersistedTenantUsage
                {
                    TenantId = "tenant-a",
                    TeamPath = $"/{TeamId}/{ChildTeamId}/",
                    Day = day,
                    Meter = "requests",
                    Count = 5,
                },
                new PersistedTenantUsage
                {
                    TenantId = "tenant-b",
                    TeamPath = $"/{TeamId}/{ChildTeamId}/{ChildTeamId}-eu/",
                    Day = day,
                    Meter = "requests",
                    Count = 3,
                },
            ])
            .Verifiable();

        var result = await Handler.GetUsageAsync(
            CreateHttpContext(authenticated: true),
            ChildTeamId,
            day,
            day,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<TeamUsageResource>>(result);
        var value = ok.Value;
        Assert.NotNull(value);
        Assert.Equal(ChildTeamId, value.TeamId);
        Assert.Equal(
            [ChildTeamId, $"{ChildTeamId}-eu"],
            value.Entries.Select(entry => entry.TeamId)
        );
    }

    [Fact]
    public async Task GetUsageAsync_WhenTheTeamDoesNotExist_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTeam(null);

        var result = await Handler.GetUsageAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            null,
            null,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetUsageAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTeam()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetUsageAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            null,
            null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task GetUsageAsync_RequestsReadOnTheTeamNode()
    {
        var captured = CaptureAuthorization();
        var start = new DateOnly(2026, 1, 1);

        await Handler.GetUsageAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            start.AddDays(1),
            start,
            CancellationToken.None
        );

        AssertTeamNodeRequest(captured, Operations.Read);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 367)]
    public async Task GetUsageAsync_WhenTheRangeIsReversedOrTooLong_ReturnsValidationProblem(
        int fromDay,
        int toDay
    )
    {
        SetupAuthorization(AuthorizationResult.Success());
        var start = new DateOnly(2026, 1, 1);

        var result = await Handler.GetUsageAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            start.AddDays(fromDay - 1),
            start.AddDays(toDay - 1),
            CancellationToken.None
        );

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Contains("from", problem.ProblemDetails.Errors.Keys);
    }

    [Fact]
    public async Task GetUsageAsync_WhenTheDefaultRangeWouldPrecedeTheFirstDay_ReturnsValidationProblem()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.GetUsageAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            from: null,
            to: DateOnly.MinValue.AddDays(TeamApiEndpointHandler.DefaultUsageDays - 1),
            CancellationToken.None
        );

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Contains("to", problem.ProblemDetails.Errors.Keys);
    }

    [Fact]
    public async Task GetUsageAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetUsageAsync(
            CreateHttpContext(authenticated: false),
            TeamId,
            null,
            null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    #endregion

    #region Owners Tests

    [Fact]
    public async Task ListOwnersAsync_WhenAuthorized_ReturnsTheTeamsOwners()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTeam(CreateTeam());
        MockResourceOwnershipService
            .Setup(x =>
                x.GetOwnersAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Team,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateAssignment("principal-1", BuiltInRoles.Owner)])
            .Verifiable();

        var result = await Handler.ListOwnersAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<OwnerResource>>>(result);
        var value = json.Value;
        Assert.NotNull(value);
        Assert.Equal("principal-1", Assert.Single(value.Items).PrincipalId);
    }

    [Fact]
    public async Task ListOwnersAsync_RequestsReadOnTheTeamNode()
    {
        var captured = CaptureAuthorization();
        SetupStore();
        SetupTeam(CreateTeam());
        MockResourceOwnershipService
            .Setup(x =>
                x.GetOwnersAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Team,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateAssignment("principal-1", BuiltInRoles.Owner)])
            .Verifiable();

        await Handler.ListOwnersAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        AssertTeamNodeRequest(captured, Operations.Read);
    }

    [Fact]
    public async Task AddOwnerAsync_RequestsUpdateOnTheTeamNode()
    {
        var captured = CaptureAuthorization();
        SetupStore();
        SetupTeam(CreateTeam());
        SetupSave();
        MockResourceOwnershipService
            .Setup(x =>
                x.AddOwnerAsync(
                    MockStoreManager.Object,
                    ControlPlaneTenantId,
                    ResourceNodeTypes.Team,
                    TeamId,
                    "principal-2",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateAssignment("principal-2", BuiltInRoles.Owner))
            .Verifiable();

        await Handler.AddOwnerAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            new AddOwnerRequest { PrincipalId = "principal-2" },
            CancellationToken.None
        );

        AssertTeamNodeRequest(captured, Operations.Update);
    }

    [Fact]
    public async Task RemoveOwnerAsync_RequestsUpdateOnTheTeamNode()
    {
        var captured = CaptureAuthorization();
        SetupStoreManagerOnly();
        SetupSave();
        MockResourceOwnershipService
            .Setup(x =>
                x.RemoveOwnerAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Team,
                    TeamId,
                    "principal-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(OwnerRemovalResult.Removed)
            .Verifiable();

        await Handler.RemoveOwnerAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            "principal-1",
            CancellationToken.None
        );

        AssertTeamNodeRequest(captured, Operations.Update);
    }

    [Fact]
    public async Task AddOwnerAsync_WhenAuthorized_ReturnsCreatedBeneathTheTeamsOwners()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTeam(CreateTeam());
        SetupSave();
        MockResourceOwnershipService
            .Setup(x =>
                x.AddOwnerAsync(
                    MockStoreManager.Object,
                    ControlPlaneTenantId,
                    ResourceNodeTypes.Team,
                    TeamId,
                    "principal-2",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateAssignment("principal-2", BuiltInRoles.Owner))
            .Verifiable();

        var result = await Handler.AddOwnerAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            new AddOwnerRequest { PrincipalId = "principal-2" },
            CancellationToken.None
        );

        var created = Assert.IsType<Created<OwnerResource>>(result);
        Assert.Equal($"/teams/{TeamId}/owners/principal-2", created.Location);
    }

    [Fact]
    public async Task RemoveOwnerAsync_WhenItIsTheLastOwner_ReturnsConflict()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStoreManagerOnly();
        MockResourceOwnershipService
            .Setup(x =>
                x.RemoveOwnerAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Team,
                    TeamId,
                    "principal-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(OwnerRemovalResult.LastOwnerForbidden)
            .Verifiable();

        var result = await Handler.RemoveOwnerAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            "principal-1",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task RemoveOwnerAsync_WhenRemoved_ReturnsNoContent()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStoreManagerOnly();
        SetupSave();
        MockResourceOwnershipService
            .Setup(x =>
                x.RemoveOwnerAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Team,
                    TeamId,
                    "principal-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(OwnerRemovalResult.Removed)
            .Verifiable();

        var result = await Handler.RemoveOwnerAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            "principal-1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    #endregion

    #region Admins Tests

    [Fact]
    public async Task ListAdminsAsync_WhenAuthorized_ReturnsTheTeamAdminAssignments()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTeam(CreateTeam());
        MockResourceOwnershipService
            .Setup(x =>
                x.GetAssignmentsAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Team,
                    TeamId,
                    BuiltInRoles.TeamAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateAssignment("principal-1", BuiltInRoles.TeamAdmin)])
            .Verifiable();

        var result = await Handler.ListAdminsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<OwnerResource>>>(result);
        var value = json.Value;
        Assert.NotNull(value);
        var admin = Assert.Single(value.Items);
        Assert.Equal(("principal-1", BuiltInRoles.TeamAdmin), (admin.PrincipalId, admin.RoleName));
    }

    [Fact]
    public async Task ListAdminsAsync_WhenTheTeamDoesNotExist_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTeam(null);

        var result = await Handler.ListAdminsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task ListAdminsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTeam()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.ListAdminsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task AddAdminAsync_WhenAuthorized_AssignsTheTeamAdminRoleAndReturnsCreated()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTeam(CreateTeam());
        SetupSave();
        MockResourceOwnershipService
            .Setup(x =>
                x.AddAssignmentAsync(
                    MockStoreManager.Object,
                    ControlPlaneTenantId,
                    ResourceNodeTypes.Team,
                    TeamId,
                    "principal-2",
                    BuiltInRoles.TeamAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateAssignment("principal-2", BuiltInRoles.TeamAdmin))
            .Verifiable();

        var result = await Handler.AddAdminAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            new AddOwnerRequest { PrincipalId = "principal-2" },
            CancellationToken.None
        );

        var created = Assert.IsType<Created<OwnerResource>>(result);
        Assert.Equal($"/teams/{TeamId}/admins/principal-2", created.Location);
    }

    [Fact]
    public async Task AddAdminAsync_RequestsCreateOnTheTeamNode()
    {
        object? authorizedResource = null;
        IAuthorizationRequirement[] requirements = [];
        SetupAuthorizationCapture(
            (resource, required) =>
            {
                authorizedResource = resource;
                requirements = required.ToArray();
            }
        );
        SetupStore();
        SetupTeam(CreateTeam());
        SetupSave();
        MockResourceOwnershipService
            .Setup(x =>
                x.AddAssignmentAsync(
                    MockStoreManager.Object,
                    ControlPlaneTenantId,
                    ResourceNodeTypes.Team,
                    TeamId,
                    "principal-2",
                    BuiltInRoles.TeamAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateAssignment("principal-2", BuiltInRoles.TeamAdmin))
            .Verifiable();

        await Handler.AddAdminAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            new AddOwnerRequest { PrincipalId = "principal-2" },
            CancellationToken.None
        );

        Assert.Same(Operations.Create, Assert.Single(requirements));
        var node = Assert.IsAssignableFrom<IResourceNode>(authorizedResource);
        Assert.Equal((ResourceNodeTypes.Team, TeamId), (node.ResourceType, node.ResourceId));
    }

    [Fact]
    public async Task AddAdminAsync_WhenTheCallerIsOnlyAnOwner_ReturnsForbidden()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.AddAdminAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            new AddOwnerRequest { PrincipalId = "principal-2" },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task AddAdminAsync_WhenThePrincipalIsMissing_ReturnsBadRequest()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.AddAdminAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            new AddOwnerRequest { PrincipalId = string.Empty },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task RemoveAdminAsync_WhenRemoved_ReturnsNoContent()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStoreManagerOnly();
        SetupSave();
        MockResourceOwnershipService
            .Setup(x =>
                x.RemoveAssignmentAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Team,
                    TeamId,
                    "principal-1",
                    BuiltInRoles.TeamAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true)
            .Verifiable();

        var result = await Handler.RemoveAdminAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            "principal-1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task RemoveAdminAsync_RequestsCreateOnTheTeamNode()
    {
        object? authorizedResource = null;
        IAuthorizationRequirement[] requirements = [];
        SetupAuthorizationCapture(
            (resource, required) =>
            {
                authorizedResource = resource;
                requirements = required.ToArray();
            }
        );
        SetupStoreManagerOnly();
        SetupSave();
        MockResourceOwnershipService
            .Setup(x =>
                x.RemoveAssignmentAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Team,
                    TeamId,
                    "principal-1",
                    BuiltInRoles.TeamAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true)
            .Verifiable();

        await Handler.RemoveAdminAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            "principal-1",
            CancellationToken.None
        );

        Assert.Same(Operations.Create, Assert.Single(requirements));
        var node = Assert.IsAssignableFrom<IResourceNode>(authorizedResource);
        Assert.Equal((ResourceNodeTypes.Team, TeamId), (node.ResourceType, node.ResourceId));
    }

    [Fact]
    public async Task RemoveAdminAsync_WhenTheCallerIsOnlyAnOwner_ReturnsForbidden()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.RemoveAdminAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            "principal-1",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task RemoveAdminAsync_WhenTheAssignmentDoesNotExist_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStoreManagerOnly();
        MockResourceOwnershipService
            .Setup(x =>
                x.RemoveAssignmentAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Team,
                    TeamId,
                    "principal-1",
                    BuiltInRoles.TeamAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false)
            .Verifiable();

        var result = await Handler.RemoveAdminAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            "principal-1",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    #endregion
}
