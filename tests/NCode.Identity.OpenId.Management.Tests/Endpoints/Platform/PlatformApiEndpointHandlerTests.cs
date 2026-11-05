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
using NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;
using NCode.Identity.OpenId.Management.Contracts.Platform;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Operations;

namespace NCode.Identity.OpenId.Management.Endpoints.Platform;

public sealed class PlatformApiEndpointHandlerTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IPlatformStore> MockPlatformStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<ISettingScopeCollectionProvider> MockSettingScopeCollectionProvider { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IClientCoherenceValidator> MockClientCoherenceValidator { get; }
    private Mock<ITenantClientCoherenceChecker> MockTenantClientCoherenceChecker { get; }
    private Mock<ITeamStore> MockTeamStore { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IMediator> MockMediator { get; }
    private Mock<IMasterKeyManager> MockMasterKeyManager { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private PlatformApiEndpointHandler Handler { get; }

    public PlatformApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockPlatformStore = MockRepository.Create<IPlatformStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockSettingScopeCollectionProvider =
            MockRepository.Create<ISettingScopeCollectionProvider>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockClientCoherenceValidator = MockRepository.Create<IClientCoherenceValidator>();
        MockTenantClientCoherenceChecker = MockRepository.Create<ITenantClientCoherenceChecker>();
        MockTeamStore = MockRepository.Create<ITeamStore>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockMediator = MockRepository.Create<IMediator>();

        MockMasterKeyManager = MockRepository.Create<IMasterKeyManager>();
        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();

        Handler = new PlatformApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockCryptoService.Object,
            MockSettingSerializer.Object,
            MockSettingScopeCollectionProvider.Object,
            MockEffectiveSettingsResolver.Object,
            MockClientCoherenceValidator.Object,
            MockTenantClientCoherenceChecker.Object,
            MockMasterKeyManager.Object,
            MockManagementAuditRecorder.Object,
            NullLogger<PlatformApiEndpointHandler>.Instance
        );
    }

    // Only a request that changes the resource records it.
    private void SetupPlatformChangedAudit() =>
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordPlatformChangedAsync(
                    It.IsAny<HttpContext>(),
                    ResourceChangeTypes.SettingsUpdated,
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

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
            .Setup(x => x.GetStore<IPlatformStore>())
            .Returns(MockPlatformStore.Object)
            .Verifiable();

        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupAuthorization(AuthorizationResult result)
    {
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
    }

    private sealed class CapturedAuthorization
    {
        public object? Resource { get; set; }
        public IAuthorizationRequirement[] Requirements { get; set; } = [];
    }

    private CapturedAuthorization CaptureAuthorization()
    {
        var captured = new CapturedAuthorization();
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
                ) =>
                {
                    captured.Resource = resource;
                    captured.Requirements = requirements.ToArray();
                }
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();
        return captured;
    }

    private static void AssertPlatformNodeRequest(
        CapturedAuthorization captured,
        IAuthorizationRequirement operation
    )
    {
        Assert.Same(operation, Assert.Single(captured.Requirements));
        var node = Assert.IsType<ResourceNode>(captured.Resource);
        Assert.Equal(
            (TenantId, ResourceNodeTypes.Platform, OpenIdConstants.PlatformResourceId),
            (node.TenantId, node.ResourceType, node.ResourceId)
        );
    }

    private void SetupValidSettings() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Platform,
                    It.IsAny<JsonSerializerOptions>()
                )
            )
            .Returns([])
            .Verifiable();

    private void SetupInvalidSettings() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Platform,
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

    private static void AssertSettingsValidationProblem(IResult result)
    {
        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(["unregistered_setting"], problem.ProblemDetails.Errors.Keys);
        Assert.Contains(
            "'unregistered_setting' is not a registered setting.",
            problem.ProblemDetails.Detail
        );
    }

    private HttpContext CreateHttpContext(bool authenticated, string? ifNoneMatch = null)
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
                        == Mock.Of<OpenIdTenant>(tenant => tenant.TenantId == TenantId)
                )
            )
        );

        if (ifNoneMatch is not null)
        {
            httpContext.Request.Headers.IfNoneMatch = ifNoneMatch;
        }

        return httpContext;
    }

    private static JsonElement EmptyObject()
    {
        return JsonSerializer.SerializeToElement(new JsonObject());
    }

    private static PersistedPlatformSettings CreateSettings(
        string concurrencyToken,
        JsonElement value
    )
    {
        return new PersistedPlatformSettings { ConcurrencyToken = concurrencyToken, Value = value };
    }

    #endregion

    #region GetSettingsAsync Tests

    [Fact]
    public async Task GetSettingsAsync_WhenFoundAndAuthorized_ReturnsJson()
    {
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSettingsAsync(httpContext, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<PlatformSettingsResource>>(result);
    }

    [Fact]
    public async Task GetSettingsAsync_RequestsReadOnThePlatformNode()
    {
        SetupStore();
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct", EmptyObject()))
            .Verifiable();
        var captured = CaptureAuthorization();

        await Handler.GetSettingsAsync(
            CreateHttpContext(authenticated: true),
            CancellationToken.None
        );

        AssertPlatformNodeRequest(captured, Operations.Read);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSettingsAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    #endregion

    #region GetEffectiveSettingsAsync Tests

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenFoundAndAuthorized_ReturnsFlatSettings()
    {
        SetupStore();
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct", EmptyObject()))
            .Verifiable();

        var descriptor = new SettingDescriptor<bool>
        {
            Name = "claims_parameter_supported",
            AppliesTo = ["client"],
        };
        var effective = CreateSettingCollection(descriptor.Create(true));
        SetupSettingScopes(OpenIdSettingScopes.Platform, (descriptor, true));

        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(effective)
            .Verifiable();

        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContextWithEnvironment(authenticated: true);

        var result = await Handler.GetEffectiveSettingsAsync(httpContext, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<PlatformEffectiveSettingsResource>>(result);
        Assert.True(json.Value!.Settings.GetProperty("claims_parameter_supported").GetBoolean());
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_RequestsReadOnThePlatformNode()
    {
        SetupStore();
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct", EmptyObject()))
            .Verifiable();
        SetupSettingScopes(OpenIdSettingScopes.Platform);
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateSettingCollection())
            .Verifiable();
        var captured = CaptureAuthorization();

        await Handler.GetEffectiveSettingsAsync(
            CreateHttpContextWithEnvironment(authenticated: true),
            CancellationToken.None
        );

        AssertPlatformNodeRequest(captured, Operations.Read);
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetEffectiveSettingsAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenTheStoredDocumentCannotBeHonored_ThrowsNamingThePlatform()
    {
        SetupStore();
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct", EmptyObject()))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new JsonException("boom"))
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var exception = await Assert.ThrowsAsync<UnreadableSettingsException>(async () =>
            await Handler.GetEffectiveSettingsAsync(httpContext, CancellationToken.None)
        );

        Assert.Equal(OpenIdSettingScopes.Platform, exception.ScopeName);
        Assert.Equal(OpenIdConstants.PlatformResourceId, exception.OwnerId);
        Assert.Equal("boom", exception.DocumentError);
    }

    private static ISettingCollection CreateSettingCollection(params Setting[] settings)
    {
        var list = settings.ToList();
        var mock = new Mock<ISettingCollection>(MockBehavior.Loose);
        mock.Setup(x => x.GetEnumerator()).Returns(() => list.GetEnumerator());
        return mock.Object;
    }

    private void SetupSettingScopes(
        string scopeName,
        params (SettingDescriptor Descriptor, bool AppliesWithin)[] results
    )
    {
        var mockScopes = MockRepository.Create<ISettingScopeCollection>();
        foreach (var (descriptor, appliesWithin) in results)
        {
            mockScopes
                .Setup(x => x.AppliesWithin(descriptor, scopeName))
                .Returns(appliesWithin)
                .Verifiable();
        }

        MockSettingScopeCollectionProvider
            .Setup(x => x.Collection)
            .Returns(mockScopes.Object)
            .Verifiable();
    }

    private static HttpContext CreateHttpContextWithEnvironment(bool authenticated)
    {
        var identity = authenticated
            ? new ClaimsIdentity(authenticationType: "test")
            : new ClaimsIdentity();
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var mockContext = new Mock<OpenIdContext>(MockBehavior.Loose);
        mockContext.Setup(x => x.Environment).Returns(Mock.Of<OpenIdEnvironment>());
        mockContext
            .Setup(x => x.Tenant)
            .Returns(Mock.Of<OpenIdTenant>(tenant => tenant.TenantId == TenantId));

        httpContext.Features.Set<IOpenIdContextFeature>(
            Mock.Of<IOpenIdContextFeature>(feature => feature.OpenIdContext == mockContext.Object)
        );
        return httpContext;
    }

    #endregion

    #region UpdateSettingsAsync Tests

    private const string TenantId = "tenant-1";

    private static bool HasProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out _);

    private const string TeamId = "team-1";

    private static PersistedTeam CreateTeam(string teamId, JsonElement settings) =>
        new()
        {
            TeamId = teamId,
            IsDisabled = false,
            DisplayName = teamId,
            Settings = new PersistedTeamSettings
            {
                TeamId = teamId,
                ConcurrencyToken = "team-ct",
                Value = settings,
            },
        };

    // Resolves every team document onto the platform collections and pages the given teams in one page.
    private void SetupTeamWalk(
        IReadOnlySettingCollection currentPlatform,
        IReadOnlySettingCollection newPlatform,
        IReadOnlySettingCollection currentTeamSettings,
        IReadOnlySettingCollection newTeamSettings,
        PersistedTeam[] teams
    )
    {
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(currentPlatform, OpenIdSettingScopes.Team, It.IsAny<JsonElement>())
            )
            .Returns(currentTeamSettings)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.Resolve(newPlatform, OpenIdSettingScopes.Team, It.IsAny<JsonElement>()))
            .Returns(newTeamSettings)
            .Verifiable();
        SetupTeamPage(teams);
    }

    private void SetupTeamPage(PersistedTeam[] teams)
    {
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable();
        MockTeamStore
            .Setup(x =>
                x.GetPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == null),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new PagedResult<PersistedTeam> { Items = teams, NextCursor = null })
            .Verifiable();
    }

    // Pages a team's tenants: the first result answers the first page and each later one follows the cursor.
    private void SetupTenantPages(
        string teamId,
        params PagedResult<PersistedTenantSettings>[] pages
    )
    {
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        string? cursor = null;
        foreach (var result in pages)
        {
            var expectedCursor = cursor;
            MockTenantStore
                .Setup(x =>
                    x.GetSettingsPageAsync(
                        teamId,
                        It.Is<PageRequest>(page => page.Cursor == expectedCursor),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(result)
                .Verifiable();
            cursor = result.NextCursor;
        }
    }

    private void SetupClientCoherenceCheck(params ClientCoherenceFinding[] findings) =>
        SetupClientCoherenceCheckOfTenants([CreateTenantSettings()], findings);

    private void SetupClientCoherenceCheckOfTenants(
        PersistedTenantSettings[] tenants,
        params ClientCoherenceFinding[] findings
    )
    {
        // The patch adds "foo", so the proposed document has it and the committed one does not. The coherence
        // validator reports that the change touches a setting a rule reads, so the tenants are walked.
        var currentPlatform = CreateSettingCollection();
        var newPlatform = CreateSettingCollection();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(
                    It.Is<JsonElement>(e => !HasProperty(e, "foo")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(currentPlatform)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(
                    It.Is<JsonElement>(e => HasProperty(e, "foo")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(newPlatform)
            .Verifiable();
        MockClientCoherenceValidator
            .Setup(x => x.ReadsChangedSetting(currentPlatform, newPlatform))
            .Returns(true)
            .Verifiable();

        var currentTeamSettings = CreateSettingCollection();
        var newTeamSettings = CreateSettingCollection();
        SetupTeamWalk(
            currentPlatform,
            newPlatform,
            currentTeamSettings,
            newTeamSettings,
            [CreateTeam(TeamId, EmptyObject())]
        );

        var currentTenantSettings = CreateSettingCollection();
        var newTenantSettings = CreateSettingCollection();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(currentTeamSettings, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>())
            )
            .Returns(currentTenantSettings)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(newTeamSettings, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>())
            )
            .Returns(newTenantSettings)
            .Verifiable();

        // Two pages, so the check follows the cursor rather than stopping after the first page.
        SetupTenantPages(
            TeamId,
            new PagedResult<PersistedTenantSettings> { Items = [], NextCursor = "next" },
            new PagedResult<PersistedTenantSettings> { Items = tenants, NextCursor = null }
        );

        MockTenantClientCoherenceChecker
            .Setup(x =>
                x.FindIntroducedViolationsAsync(
                    MockStoreManager.Object,
                    TenantId,
                    currentTenantSettings,
                    newTenantSettings,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(findings)
            .Verifiable();
    }

    private void SetupPlatformSettingsChanged() =>
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<PlatformSettingsChangedCommand>(c =>
                        HasProperty(c.Settings, "foo") && !HasProperty(c.PreviousSettings, "foo")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private static PersistedTenantSettings CreateTenantSettings() =>
        new()
        {
            TenantId = TenantId,
            ConcurrencyToken = "settings-ct",
            Value = EmptyObject(),
        };

    private static PersistedTenantSettings CreateUnreadableTenantSettings() =>
        new()
        {
            TenantId = "tenant-0",
            ConcurrencyToken = "settings-ct",
            Value = JsonSerializer.SerializeToElement(new { retired = true }),
        };

    [Fact]
    public async Task UpdateSettingsAsync_WhenNoSettingACoherenceRuleReadsChanges_SkipsTheTenantWalk()
    {
        SetupValidSettings();
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(
                    It.Is<JsonElement>(e => !HasProperty(e, "foo")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSettingCollection())
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(
                    It.Is<JsonElement>(e => HasProperty(e, "foo")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSettingCollection())
            .Verifiable();
        MockClientCoherenceValidator
            .Setup(x =>
                x.ReadsChangedSetting(
                    It.IsAny<IReadOnlySettingCollection>(),
                    It.IsAny<IReadOnlySettingCollection>()
                )
            )
            .Returns(false)
            .Verifiable();
        MockPlatformStore
            .Setup(x => x.UpdateSettingsAsync(settings, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        SetupPlatformChangedAudit();
        SetupPlatformSettingsChanged();

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no tenant-store or FindIntroducedViolationsAsync setup, so walking the tenants would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            httpContext,
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
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(
                    It.Is<JsonElement>(e => !HasProperty(e, "foo")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(
                new JsonException(
                    "The settings are invalid: 'retired' is not a registered setting."
                )
            )
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(
                    It.Is<JsonElement>(e => HasProperty(e, "foo")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSettingCollection())
            .Verifiable();
        MockPlatformStore
            .Setup(x => x.UpdateSettingsAsync(settings, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        SetupPlatformChangedAudit();
        SetupPlatformSettingsChanged();

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no ReadsChangedSetting, tenant-store or FindIntroducedViolationsAsync setup, so checking a
        // client would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenATenantDocumentCannotBeHonored_ChecksTheOtherTenantsAndWrites()
    {
        SetupValidSettings();
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupClientCoherenceCheckOfTenants([
            CreateUnreadableTenantSettings(),
            CreateTenantSettings(),
        ]);
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    It.IsAny<IReadOnlySettingCollection>(),
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => HasProperty(e, "retired"))
                )
            )
            .Throws(
                new JsonException(
                    "The settings are invalid: 'retired' is not a registered setting."
                )
            )
            .Verifiable();
        MockPlatformStore
            .Setup(x => x.UpdateSettingsAsync(settings, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        SetupPlatformChangedAudit();
        SetupPlatformSettingsChanged();

        var httpContext = CreateHttpContext(authenticated: true);

        // The verifiable checker setup for TenantId proves the second tenant was checked.
        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WalksEachTeamsTenantsBeneathTheTeamsDocument()
    {
        SetupValidSettings();
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var currentPlatform = CreateSettingCollection();
        var newPlatform = CreateSettingCollection();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(
                    It.Is<JsonElement>(e => !HasProperty(e, "foo")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(currentPlatform)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(
                    It.Is<JsonElement>(e => HasProperty(e, "foo")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(newPlatform)
            .Verifiable();
        MockClientCoherenceValidator
            .Setup(x => x.ReadsChangedSetting(currentPlatform, newPlatform))
            .Returns(true)
            .Verifiable();

        // Each team's document is resolved onto both platform collections; the team collections differ per team.
        var teamA = CreateTeam("team-a", JsonSerializer.SerializeToElement(new { override_a = 1 }));
        var teamB = CreateTeam("team-b", JsonSerializer.SerializeToElement(new { override_b = 1 }));
        var currentTeamA = CreateSettingCollection();
        var newTeamA = CreateSettingCollection();
        var currentTeamB = CreateSettingCollection();
        var newTeamB = CreateSettingCollection();
        foreach (
            var (key, current, updated) in new[]
            {
                ("override_a", currentTeamA, newTeamA),
                ("override_b", currentTeamB, newTeamB),
            }
        )
        {
            MockEffectiveSettingsResolver
                .Setup(x =>
                    x.Resolve(
                        currentPlatform,
                        OpenIdSettingScopes.Team,
                        It.Is<JsonElement>(e => HasProperty(e, key))
                    )
                )
                .Returns(current)
                .Verifiable();
            MockEffectiveSettingsResolver
                .Setup(x =>
                    x.Resolve(
                        newPlatform,
                        OpenIdSettingScopes.Team,
                        It.Is<JsonElement>(e => HasProperty(e, key))
                    )
                )
                .Returns(updated)
                .Verifiable();
        }

        SetupTeamPage([teamA, teamB]);

        SetupTenantPages(
            "team-a",
            new PagedResult<PersistedTenantSettings>
            {
                Items =
                [
                    new PersistedTenantSettings
                    {
                        TenantId = "tenant-a",
                        ConcurrencyToken = "ct",
                        Value = EmptyObject(),
                    },
                ],
                NextCursor = null,
            }
        );
        SetupTenantPages(
            "team-b",
            new PagedResult<PersistedTenantSettings>
            {
                Items =
                [
                    new PersistedTenantSettings
                    {
                        TenantId = "tenant-b",
                        ConcurrencyToken = "ct",
                        Value = EmptyObject(),
                    },
                ],
                NextCursor = null,
            }
        );

        // The tenant document is resolved onto the team collections, so the checker receives what resolves from them.
        var tenantResults =
            new Dictionary<IReadOnlySettingCollection, IReadOnlySettingCollection>();
        foreach (var teamSettings in new[] { currentTeamA, newTeamA, currentTeamB, newTeamB })
        {
            var resolved = CreateSettingCollection();
            tenantResults[teamSettings] = resolved;
            MockEffectiveSettingsResolver
                .Setup(x =>
                    x.Resolve(teamSettings, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>())
                )
                .Returns(resolved)
                .Verifiable();
        }

        var seen =
            new Dictionary<
                string,
                (IReadOnlySettingCollection Current, IReadOnlySettingCollection New)
            >();
        foreach (var tenantId in new[] { "tenant-a", "tenant-b" })
        {
            MockTenantClientCoherenceChecker
                .Setup(x =>
                    x.FindIntroducedViolationsAsync(
                        MockStoreManager.Object,
                        tenantId,
                        It.IsAny<IReadOnlySettingCollection>(),
                        It.IsAny<IReadOnlySettingCollection>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback(
                    (
                        IStoreManager _,
                        string id,
                        IReadOnlySettingCollection currentArg,
                        IReadOnlySettingCollection newArg,
                        CancellationToken _
                    ) => seen[id] = (currentArg, newArg)
                )
                .ReturnsAsync([])
                .Verifiable();
        }

        MockPlatformStore
            .Setup(x => x.UpdateSettingsAsync(settings, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupPlatformChangedAudit();
        SetupPlatformSettingsChanged();

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.Equal(2, seen.Count);
        Assert.Same(tenantResults[currentTeamA], seen["tenant-a"].Current);
        Assert.Same(tenantResults[newTeamA], seen["tenant-a"].New);
        Assert.Same(tenantResults[currentTeamB], seen["tenant-b"].Current);
        Assert.Same(tenantResults[newTeamB], seen["tenant-b"].New);
        foreach (var teamId in new[] { "team-a", "team-b" })
        {
            MockTenantStore.Verify(
                x =>
                    x.GetSettingsPageAsync(
                        teamId,
                        It.IsAny<PageRequest>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenATeamDocumentCannotBeHonored_SkipsThatTeam()
    {
        SetupValidSettings();
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var currentPlatform = CreateSettingCollection();
        var newPlatform = CreateSettingCollection();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(
                    It.Is<JsonElement>(e => !HasProperty(e, "foo")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(currentPlatform)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolvePlatformAsync(
                    It.Is<JsonElement>(e => HasProperty(e, "foo")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(newPlatform)
            .Verifiable();
        MockClientCoherenceValidator
            .Setup(x => x.ReadsChangedSetting(currentPlatform, newPlatform))
            .Returns(true)
            .Verifiable();

        var unreadableTeam = CreateTeam(
            "team-bad",
            JsonSerializer.SerializeToElement(new { retired = true })
        );
        var goodTeam = CreateTeam("team-good", EmptyObject());
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    currentPlatform,
                    OpenIdSettingScopes.Team,
                    It.Is<JsonElement>(e => HasProperty(e, "retired"))
                )
            )
            .Throws(new JsonException("'retired' is not a registered setting."))
            .Verifiable();
        var currentTeam = CreateSettingCollection();
        var newTeam = CreateSettingCollection();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    currentPlatform,
                    OpenIdSettingScopes.Team,
                    It.Is<JsonElement>(e => !HasProperty(e, "retired"))
                )
            )
            .Returns(currentTeam)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    newPlatform,
                    OpenIdSettingScopes.Team,
                    It.Is<JsonElement>(e => !HasProperty(e, "retired"))
                )
            )
            .Returns(newTeam)
            .Verifiable();
        SetupTeamPage([unreadableTeam, goodTeam]);

        var currentTenant = CreateSettingCollection();
        var newTenant = CreateSettingCollection();
        MockEffectiveSettingsResolver
            .Setup(x => x.Resolve(currentTeam, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>()))
            .Returns(currentTenant)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.Resolve(newTeam, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>()))
            .Returns(newTenant)
            .Verifiable();

        // Strict tenant store: only the good team's tenants are paged, so paging the unreadable team's would fail.
        SetupTenantPages(
            "team-good",
            new PagedResult<PersistedTenantSettings>
            {
                Items = [CreateTenantSettings()],
                NextCursor = null,
            }
        );
        MockTenantClientCoherenceChecker
            .Setup(x =>
                x.FindIntroducedViolationsAsync(
                    MockStoreManager.Object,
                    TenantId,
                    currentTenant,
                    newTenant,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([])
            .Verifiable();

        MockPlatformStore
            .Setup(x => x.UpdateSettingsAsync(settings, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupPlatformChangedAudit();
        SetupPlatformSettingsChanged();

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheChangeExcludesAClientAlgorithm_ReturnsValidationProblem()
    {
        SetupValidSettings();
        SetupStore();
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct", EmptyObject()))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupClientCoherenceCheck(
            new ClientCoherenceFinding(
                "client-1",
                new ClientCoherenceViolation(
                    OpenIdSettingNames.IdTokenSignedResponseAlg,
                    "RS256",
                    OpenIdSettingNames.IdTokenSigningAlgValuesSupported
                )
            )
        );

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no UpdateSettingsAsync / SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            httpContext,
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
        Assert.Contains($"'{TenantId}'", problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    private static JsonPatchDocument<JsonObject> CreateAddPatch(string name, string value)
    {
        var patch = new JsonPatchDocument<JsonObject>();
        patch.Operations.Add(
            new Operation<JsonObject>("add", $"/{name}", from: null, value: value)
        );
        return patch;
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenAuthorizedAndNoIfMatch_AppliesPatchAndReturnsNoContent()
    {
        SetupValidSettings();
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupClientCoherenceCheck();
        MockPlatformStore
            .Setup(x => x.UpdateSettingsAsync(settings, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        SetupPlatformChangedAudit();
        SetupPlatformSettingsChanged();

        var httpContext = CreateHttpContext(authenticated: true);
        var patch = CreateAddPatch("foo", "bar");

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            patch,
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.Equal("settings-ct", httpContext.Response.Headers.ETag);
        Assert.Equal("bar", settings.Value.GetProperty("foo").GetString());
    }

    [Fact]
    public async Task UpdateSettingsAsync_AfterTheWriteCommits_AnnouncesThePlatformSettingsChange()
    {
        SetupValidSettings();
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupClientCoherenceCheck();
        MockPlatformStore
            .Setup(x => x.UpdateSettingsAsync(settings, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        SetupPlatformChangedAudit();
        SetupPlatformSettingsChanged();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        MockMediator.Verify(
            x =>
                x.SendAsync(
                    It.IsAny<PlatformSettingsChangedCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenIfMatchMatches_AppliesPatch()
    {
        SetupValidSettings();
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupClientCoherenceCheck();
        MockPlatformStore
            .Setup(x => x.UpdateSettingsAsync(settings, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        SetupPlatformChangedAudit();
        SetupPlatformSettingsChanged();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            CreateAddPatch("foo", "bar"),
            ifMatch: "settings-ct",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenSettingsCannotBeHonored_ReturnsValidationProblem()
    {
        SetupInvalidSettings();
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no UpdateSettingsAsync / SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            CreateAddPatch("unregistered_setting", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        AssertSettingsValidationProblem(result);
        Assert.Equal(JsonValueKind.Object, settings.Value.ValueKind);
        Assert.False(settings.Value.TryGetProperty("unregistered_setting", out _));
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenIfMatchMismatch_ReturnsPreconditionFailed()
    {
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            CreateAddPatch("foo", "bar"),
            ifMatch: "stale-token",
            CancellationToken.None
        );

        var statusCode = Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, statusCode.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_RequestsUpdateOnThePlatformNode()
    {
        SetupStore();
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct", EmptyObject()))
            .Verifiable();
        var captured = CaptureAuthorization();

        await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            CreateAddPatch("foo", "bar"),
            ifMatch: "stale-token",
            CancellationToken.None
        );

        AssertPlatformNodeRequest(captured, Operations.Update);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenPatchInvalid_ReturnsBadRequest()
    {
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        // Replace on a path that does not exist throws a JsonPatchException.
        var patch = new JsonPatchDocument<JsonObject>();
        patch.Operations.Add(
            new Operation<JsonObject>("replace", "/missing", from: null, value: "value")
        );

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            patch,
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenForbidden_ReturnsForbid()
    {
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: false);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    #endregion

    #region Encryption Keys Tests

    private static MasterKeyInfo CreateKeyInfo(int version, bool retired) =>
        new(
            Guid.NewGuid(),
            SecretOwner.Platform,
            version,
            DateTimeOffset.UnixEpoch,
            retired ? DateTimeOffset.UnixEpoch.AddDays(1) : null,
            "local",
            "root-key",
            "recovery-key"
        );

    private void SetupPlatformRow(bool exists) =>
        MockPlatformStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                exists
                    ? new PersistedPlatform
                    {
                        ConcurrencyToken = "platform-ct",
                        Settings = CreateSettings("settings-ct", EmptyObject()),
                    }
                    : null
            )
            .Verifiable();

    [Fact]
    public async Task GetEncryptionKeysAsync_WhenFoundAndAuthorized_MapsTheStatusWithoutMaterial()
    {
        SetupStore();
        SetupPlatformRow(exists: true);
        var captured = CaptureAuthorization();
        var active = CreateKeyInfo(2, retired: false);
        var retired = CreateKeyInfo(1, retired: true);
        MockMasterKeyManager
            .Setup(x => x.GetStatusAsync(SecretOwner.Platform, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<MasterKeyStatus>(
                    new MasterKeyStatus(
                        SecretOwner.Platform,
                        [active, retired],
                        RecoveryConfigured: true
                    )
                )
            )
            .Verifiable();

        var result = await Handler.GetEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            CancellationToken.None
        );

        AssertPlatformNodeRequest(captured, Operations.Read);
        var ok = Assert.IsType<Ok<EncryptionKeysResource>>(result);
        Assert.True(ok.Value?.RecoveryConfigured);
        Assert.Null(ok.Value?.RootKey);
        Assert.Empty(ok.Value?.RootKeyProviders ?? ["unexpected"]);
        Assert.Collection(
            ok.Value?.Keys ?? [],
            key =>
            {
                Assert.Equal(active.KeyId, key.KeyId);
                Assert.Equal(2, key.Version);
                Assert.Equal(EncryptionKeyStates.Active, key.State);
                Assert.Equal("local", key.RootKeyProvider);
                Assert.Equal("root-key", key.RootKeyId);
                Assert.Equal("recovery-key", key.RecoveryKeyId);
                Assert.Null(key.RetiredWhen);
            },
            key =>
            {
                Assert.Equal(EncryptionKeyStates.Retired, key.State);
                Assert.Equal(retired.RetiredWhen, key.RetiredWhen);
            }
        );
    }

    [Fact]
    public async Task GetEncryptionKeysAsync_WhenNotFound_ReturnsNotFoundWithoutTouchingTheManager()
    {
        SetupStore();
        SetupPlatformRow(exists: false);

        var result = await Handler.GetEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetEncryptionKeysAsync_WhenNotAuthorized_DoesNotReadTheStatus()
    {
        SetupStore();
        SetupPlatformRow(exists: true);
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            CancellationToken.None
        );

        Assert.IsNotType<Ok<EncryptionKeysResource>>(result);
    }

    [Fact]
    public async Task RekeyEncryptionKeysAsync_RotatesRecordsOneAuditEventAndMapsTheRotation()
    {
        SetupStore();
        SetupPlatformRow(exists: true);
        var captured = CaptureAuthorization();
        var rotation = new MasterKeyRotation(
            SecretOwner.Platform,
            CreateKeyInfo(2, retired: false),
            CreateKeyInfo(1, retired: true)
        );
        MockMasterKeyManager
            .Setup(x => x.RotateAsync(SecretOwner.Platform, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MasterKeyRotation>(rotation))
            .Verifiable();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordMasterKeyRotatedAsync(
                    It.IsAny<HttpContext>(),
                    rotation,
                    MasterKeyChangeReasons.Requested,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);

        var result = await Handler.RekeyEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            CancellationToken.None
        );

        AssertPlatformNodeRequest(captured, Operations.Update);
        var ok = Assert.IsType<Ok<EncryptionKeyRotationResource>>(result);
        Assert.Equal(2, ok.Value?.ActiveKey.Version);
        Assert.Equal(EncryptionKeyStates.Active, ok.Value?.ActiveKey.State);
        Assert.Equal(1, ok.Value?.RetiredKey?.Version);
        Assert.Equal(EncryptionKeyStates.Retired, ok.Value?.RetiredKey?.State);
    }

    [Fact]
    public async Task RekeyEncryptionKeysAsync_WhenAnotherRekeyCommittedFirst_ThrowsStoreConcurrencyExceptionForTheFilter()
    {
        SetupStore();
        SetupPlatformRow(exists: true);
        SetupAuthorization(AuthorizationResult.Success());
        MockMasterKeyManager
            .Setup(x => x.RotateAsync(SecretOwner.Platform, It.IsAny<CancellationToken>()))
            .Throws(new StoreConcurrencyException())
            .Verifiable();

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Handler.RekeyEncryptionKeysAsync(
                CreateHttpContext(authenticated: true),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task RekeyEncryptionKeysAsync_WhenTheLastRotationIsTooRecent_ReturnsConflictWithRetryAfterAndRecordsNothing()
    {
        SetupStore();
        SetupPlatformRow(exists: true);
        SetupAuthorization(AuthorizationResult.Success());
        MockMasterKeyManager
            .Setup(x => x.RotateAsync(SecretOwner.Platform, It.IsAny<CancellationToken>()))
            .Throws(
                new MasterKeyRotationTooSoonException(
                    SecretOwner.Platform,
                    TimeSpan.FromSeconds(90.2)
                )
            )
            .Verifiable();
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RekeyEncryptionKeysAsync(httpContext, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("91", httpContext.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task RekeyEncryptionKeysAsync_WhenNotFound_NeverRotates()
    {
        SetupStore();
        SetupPlatformRow(exists: false);

        var result = await Handler.RekeyEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task RekeyEncryptionKeysAsync_WhenNotAuthorized_NeverRotates()
    {
        SetupStore();
        SetupPlatformRow(exists: true);
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.RekeyEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            CancellationToken.None
        );

        Assert.IsNotType<Ok<EncryptionKeyRotationResource>>(result);
    }

    #endregion
}
