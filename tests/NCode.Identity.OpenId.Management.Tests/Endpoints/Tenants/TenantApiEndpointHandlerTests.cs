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
using NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Operations;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

public sealed class TenantApiEndpointHandlerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string TeamId = "platform";
    private const string ControlPlaneTenantId = "platform-root";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<ITeamStore> MockTeamStore { get; }
    private Mock<IReadOnlySettingCollection>? MockTeamSettings { get; set; }
    private List<string> Events { get; } = [];
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ISecretGenerator> MockSecretGenerator { get; }
    private Mock<IClientKeyImporter> MockKeyImporter { get; }
    private Mock<ISecretPublicKeyConverter> MockSecretPublicKeyConverter { get; }
    private Mock<ISecretProtectorProvider> MockSecretProtectorProvider { get; }
    private Mock<IMasterKeyManager> MockMasterKeyManager { get; }
    private Mock<IOwnerRootKeyManager> MockOwnerRootKeyManager { get; }
    private Mock<ITenantValidator> MockTenantValidator { get; }
    private Mock<ITenantResolver> MockTenantResolver { get; }
    private Mock<ITenantProvisioner> MockTenantProvisioner { get; }
    private Mock<ISecretValidator> MockSecretValidator { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<ISettingScopeCollectionProvider> MockSettingScopeCollectionProvider { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<ITenantEnvironmentCatalog> MockTenantEnvironmentCatalog { get; }
    private Mock<ITenantClientCoherenceChecker> MockTenantClientCoherenceChecker { get; }
    private Mock<IResourceOwnershipService> MockResourceOwnershipService { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private Mock<IMediator> MockMediator { get; }
    private TenantApiEndpointHandler Handler { get; }

    public TenantApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockTeamStore = MockRepository.Create<ITeamStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockSecretGenerator = MockRepository.Create<ISecretGenerator>();
        MockKeyImporter = MockRepository.Create<IClientKeyImporter>();
        MockSecretPublicKeyConverter = MockRepository.Create<ISecretPublicKeyConverter>();
        MockSecretProtectorProvider = MockRepository.Create<ISecretProtectorProvider>();
        MockMasterKeyManager = MockRepository.Create<IMasterKeyManager>();
        MockOwnerRootKeyManager = MockRepository.Create<IOwnerRootKeyManager>();
        MockTenantValidator = MockRepository.Create<ITenantValidator>();
        MockTenantResolver = MockRepository.Create<ITenantResolver>();
        MockTenantProvisioner = MockRepository.Create<ITenantProvisioner>();
        MockSecretValidator = MockRepository.Create<ISecretValidator>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockSettingScopeCollectionProvider =
            MockRepository.Create<ISettingScopeCollectionProvider>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockTenantEnvironmentCatalog = MockRepository.Create<ITenantEnvironmentCatalog>();
        MockTenantClientCoherenceChecker = MockRepository.Create<ITenantClientCoherenceChecker>();
        MockResourceOwnershipService = MockRepository.Create<IResourceOwnershipService>();

        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();

        // A write announces the change after it commits; the tests that write verify the announcement.
        MockMediator = MockRepository.Create<IMediator>();

        Handler = new TenantApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockTenantValidator.Object,
            MockTenantResolver.Object,
            MockSecretValidator.Object,
            MockSecretGenerator.Object,
            MockKeyImporter.Object,
            MockSecretPublicKeyConverter.Object,
            MockSecretProtectorProvider.Object,
            MockMasterKeyManager.Object,
            MockOwnerRootKeyManager.Object,
            TimeProvider.System,
            MockCryptoService.Object,
            MockSettingSerializer.Object,
            MockSettingScopeCollectionProvider.Object,
            MockEffectiveSettingsResolver.Object,
            MockTenantEnvironmentCatalog.Object,
            MockTenantClientCoherenceChecker.Object,
            MockResourceOwnershipService.Object,
            MockManagementAuditRecorder.Object,
            NullLogger<TenantApiEndpointHandler>.Instance
        );
    }

    // Only a request that changes the tenant announces the change to every replica.
    private void SetupTenantChangedAnnouncement() =>
        MockMediator
            .Setup(x =>
                x.SendAsync(It.IsAny<TenantChangedCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    // Only a request that updates a secret validates the update.
    private void SetupSecretUpdateValidation() =>
        MockSecretValidator
            .Setup(x =>
                x.ValidateUpdate(It.IsAny<PersistedSecret>(), It.IsAny<UpdateSecretRequest>())
            )
            .Returns((ManagementError?)null)
            .Verifiable();

    // Only a request that adds a secret validates its creation.
    private void SetupSecretCreateValidation()
    {
        MockSecretValidator
            .Setup(x =>
                x.ValidateCreate(It.IsAny<CreateSecretRequest>(), It.IsAny<DateTimeOffset>())
            )
            .Returns(
                (CreateSecretRequest request, DateTimeOffset _) =>
                    SecretCreateValidation.Valid(
                        new SecretKeyGenerationParameters
                        {
                            KeySizeBits = request.KeySizeBits,
                            Curve = request.Curve,
                        }
                    )
            )
            .Verifiable();
    }

    // Only a request that creates the resource assigns its creator as its owner.
    private void SetupCreatorAssignment(PersistedRoleAssignment? assignment = null) =>
        MockResourceOwnershipService
            .Setup(x =>
                x.AssignCreatorAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(assignment)
            .Verifiable();

    // Only a request that grants or revokes a role records it.
    private void SetupRoleAssignmentChangedAudit() =>
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordResourceChangeAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<RoleAssignmentChangedAuditEvent>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupRoleAssignmentChangedAuditCapture(
        Action<RoleAssignmentChangedAuditEvent> capture
    ) =>
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordResourceChangeAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<RoleAssignmentChangedAuditEvent>()
                )
            )
            .Callback(
                (HttpContext _, ResourceChangeAuditEvent auditEvent) =>
                    capture((RoleAssignmentChangedAuditEvent)auditEvent)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    // Only a request that changes a secret records it.
    private void SetupTenantSecretChangedAudit() =>
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordTenantSecretChangedAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement?>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    // Only a request that changes the resource records it.
    private void SetupTenantChangedAudit() =>
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordTenantChangedAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement?>()
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
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
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

    private void SetupTenantCreateAuthorization(ManagementError? error) =>
        MockTenantValidator
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

    private void SetupTenantUpdateAuthorization(ManagementError? error) =>
        MockTenantValidator
            .Setup(x =>
                x.AuthorizeUpdateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    private void SetupTenantDeleteAuthorization(ManagementError? error) =>
        MockTenantValidator
            .Setup(x =>
                x.AuthorizeDeleteAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    private void SetupTenant(PersistedTenant? tenant) =>
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant)
            .Verifiable();

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

    private void SetupResourceId(string id = "generated-id")
    {
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns(id)
            .Verifiable();
    }

    private void SetupAuthorizationCapture(Action<object?> capture)
    {
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
    }

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

    private void SetupValidSettings() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Tenant,
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

    private void SetupTenantLimits(params string[] limits) =>
        MockSettingSerializer
            .Setup(x => x.GetLimits(OpenIdSettingScopes.Tenant))
            .Returns(limits)
            .Verifiable();

    private void SetupInvalidSettings() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Tenant,
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

    private HttpContext CreateHttpContext(bool authenticated)
    {
        var identity = authenticated
            ? new ClaimsIdentity(authenticationType: "test")
            : new ClaimsIdentity();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = new ServiceCollection()
                .AddSingleton(MockTenantProvisioner.Object)
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

    private void VerifyTenantChanged(bool settingsChanged) =>
        MockMediator.Verify(
            x =>
                x.SendAsync(
                    new TenantChangedCommand(TenantId, settingsChanged),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

    private static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new JsonObject());

    private static JsonElement FooObject() =>
        JsonSerializer.SerializeToElement(new JsonObject { ["foo"] = "bar" });

    private static PersistedTenant CreateTenant(string concurrencyToken, string? issuer = null) =>
        new()
        {
            TenantId = TenantId,
            TeamId = "platform",
            ConcurrencyToken = concurrencyToken,
            Issuer = issuer,
            IsDisabled = false,
            DisplayName = "Tenant One",
            Settings = CreateSettings("settings-ct"),
            Secrets = CreateSecretsCollection("secrets-ct"),
        };

    private static PersistedTenantSettings CreateSettings(string concurrencyToken) =>
        new()
        {
            TenantId = TenantId,
            ConcurrencyToken = concurrencyToken,
            Value = EmptyObject(),
        };

    private static PersistedTenantSecrets CreateSecretsCollection(string concurrencyToken) =>
        new()
        {
            TenantId = TenantId,
            ConcurrencyToken = concurrencyToken,
            Value = [],
        };

    private static PersistedSecret CreatePersistedSecret(
        string secretId = "secret-1",
        string concurrencyToken = "secret-ct"
    ) =>
        new()
        {
            SecretId = secretId,
            ConcurrencyToken = concurrencyToken,
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            ActivatesWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.SymmetricRaw,
            Origin = SecretOrigins.Generated,
            KeySizeBits = 256,
            EncodedValue = "encoded",
        };

    private static CreateSecretRequest CreateSecretRequest() =>
        new()
        {
            SecretType = SecretTypes.Symmetric,
            KeySizeBits = 256,
            Use = "sig",
            Algorithm = "RS256",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    private static UpdateSecretRequest UpdateSecretRequest() =>
        new()
        {
            Use = "enc",
            Algorithm = "RS512",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(2),
        };

    private static PersistedRoleAssignment CreateAssignment(string principalId, string roleName) =>
        new()
        {
            TenantId = TenantId,
            AssignmentId = $"assignment-{principalId}",
            PrincipalId = principalId,
            RoleName = roleName,
            ResourceType = ResourceNodeTypes.Tenant,
            ResourceId = TenantId,
            ConcurrencyToken = "assignment-ct",
        };

    private static JsonPatchDocument<JsonObject> CreateAddPatch(string name, string value)
    {
        var patch = new JsonPatchDocument<JsonObject>();
        patch.Operations.Add(
            new Operation<JsonObject>("add", $"/{name}", from: null, value: value)
        );
        return patch;
    }

    #endregion

    #region GetTenantAsync Tests

    [Fact]
    public async Task GetTenantAsync_WhenFoundAndAuthorized_ReturnsJson()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantEnvironment("development", "Dev EU", isProduction: false);

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetTenantAsync(httpContext, TenantId, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<TenantResource>>(result);
        Assert.Equal(TenantId, json.Value?.TenantId);
        Assert.Equal("Tenant One", json.Value?.DisplayName);
        Assert.NotNull(json.Value?.Environment);
        Assert.Equal("development", json.Value.Environment.Type);
        Assert.Equal("Dev EU", json.Value.Environment.Name);
        Assert.False(json.Value.Environment.IsProduction);
        Assert.Equal("tenant-ct", httpContext.Response.Headers.ETag);
    }

    [Fact]
    public async Task GetTenantAsync_WhenEnvironmentHasNoName_OmitsTheName()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantEnvironment();

        var result = await Handler.GetTenantAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<TenantResource>>(result);
        Assert.NotNull(json.Value?.Environment);
        Assert.Equal("production", json.Value.Environment.Type);
        Assert.Null(json.Value.Environment.Name);
        Assert.True(json.Value.Environment.IsProduction);
    }

    [Fact]
    public async Task GetTenantAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetTenantAsync(httpContext, TenantId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetTenantAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: false);

        // Strict mocks: no store setup, so reading the tenant before authorization would fail the test.
        var result = await Handler.GetTenantAsync(httpContext, TenantId, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    [Fact]
    public async Task GetTenantAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetTenantAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region ListTenantsAsync Tests

    [Fact]
    public async Task ListTenantsAsync_WhenAuthorized_ReturnsEachTenantWithItsEnvironment()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetPageAsync(It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PagedResult<PersistedTenant>
                {
                    Items = [CreateTenant("tenant-ct")],
                    NextCursor = "next",
                }
            )
            .Verifiable();
        SetupTenantEnvironment("staging", "Staging", isProduction: false);

        var result = await Handler.ListTenantsAsync(
            CreateHttpContext(authenticated: true),
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<TenantResource>>>(result);
        var tenant = Assert.Single(json.Value!.Items);
        Assert.Equal(TenantId, tenant.TenantId);
        Assert.Equal("staging", tenant.Environment?.Type);
        Assert.Equal("Staging", tenant.Environment?.Name);
        Assert.False(tenant.Environment?.IsProduction);
        Assert.Equal("next", json.Value.ContinuationToken);
    }

    [Fact]
    public async Task ListTenantsAsync_RequestsListOnThePlatformNode()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetPageAsync(It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PersistedTenant> { Items = [], NextCursor = null })
            .Verifiable();
        object? authorizedResource = null;
        IEnumerable<IAuthorizationRequirement>? requirements = null;
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
                    IEnumerable<IAuthorizationRequirement> value
                ) =>
                {
                    authorizedResource = resource;
                    requirements = value;
                }
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();

        await Handler.ListTenantsAsync(
            CreateHttpContext(authenticated: true),
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var node = Assert.IsType<ResourceNode>(authorizedResource);
        Assert.Equal(
            (ControlPlaneTenantId, ResourceNodeTypes.Platform, OpenIdConstants.PlatformResourceId),
            (node.TenantId, node.ResourceType, node.ResourceId)
        );
        Assert.Same(Operations.List, Assert.Single(requirements!));
    }

    [Fact]
    public async Task ListTenantsAsync_WhenUnauthenticated_ReturnsUnauthorizedWithoutReadingSettings()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        // Strict mocks: neither the store nor the IEffectiveSettingsResolver is set up, so reading either before
        // authorization would fail the test.
        var result = await Handler.ListTenantsAsync(
            CreateHttpContext(authenticated: false),
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    private void SetupTenantPage()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetPageAsync(It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PagedResult<PersistedTenant>
                {
                    Items = [CreateTenant("tenant-ct")],
                    NextCursor = null,
                }
            )
            .Verifiable();
    }

    [Fact]
    public async Task ListTenantsAsync_WhenTenantSettingsAreUnreadable_ListsTheTenantWithoutItsEnvironment()
    {
        SetupTenantPage();
        var teamSettings = SetupTeamSettings();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    teamSettings,
                    OpenIdSettingScopes.Tenant,
                    TenantId,
                    It.IsAny<JsonElement>()
                )
            )
            .Throws(CreateUnreadableSettingsException(OpenIdSettingScopes.Tenant, TenantId))
            .Verifiable();

        var result = await Handler.ListTenantsAsync(
            CreateHttpContext(authenticated: true),
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<TenantResource>>>(result);
        var tenant = Assert.Single(json.Value!.Items);
        Assert.Equal(TenantId, tenant.TenantId);
        Assert.Null(tenant.Environment);
    }

    [Fact]
    public async Task ListTenantsAsync_WhenATeamsSettingsAreUnreadable_ListsItsTenantsWithoutTheirEnvironment()
    {
        SetupTenantPage();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateUnreadableSettingsException(OpenIdSettingScopes.Team, "platform"))
            .Verifiable();

        var result = await Handler.ListTenantsAsync(
            CreateHttpContext(authenticated: true),
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<TenantResource>>>(result);
        Assert.Null(Assert.Single(json.Value!.Items).Environment);
    }

    [Fact]
    public async Task ListTenantsAsync_WhenEnvironmentTypeIsNotRegistered_ListsTheTenantWithoutItsEnvironment()
    {
        SetupTenantPage();
        var teamSettings = SetupTeamSettings();
        var tenantSettings = MockRepository.Create<IReadOnlySettingCollection>();
        tenantSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.EnvironmentType))
            .Returns("retired")
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    teamSettings,
                    OpenIdSettingScopes.Tenant,
                    TenantId,
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(tenantSettings.Object)
            .Verifiable();
        ITenantEnvironmentType? unregistered = null;
        MockTenantEnvironmentCatalog
            .Setup(x => x.TryGetType("retired", out unregistered))
            .Returns(false)
            .Verifiable();

        var result = await Handler.ListTenantsAsync(
            CreateHttpContext(authenticated: true),
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<TenantResource>>>(result);
        Assert.Null(Assert.Single(json.Value!.Items).Environment);
    }

    [Fact]
    public async Task ListTenantsAsync_WithATeam_ReturnsOnlyItsTenants()
    {
        object? authorizedResource = null;
        SetupAuthorizationCapture(resource => authorizedResource = resource);
        SetupStore();
        MockTenantStore
            .Setup(x =>
                x.GetPageAsync(TeamId, It.IsAny<PageRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new PagedResult<PersistedTenant>
                {
                    Items = [CreateTenant("tenant-ct")],
                    NextCursor = null,
                }
            )
            .Verifiable();
        SetupTenantEnvironment();

        var result = await Handler.ListTeamTenantsAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<TenantResource>>>(result);
        var tenant = Assert.Single(json.Value!.Items);
        Assert.Equal(TenantId, tenant.TenantId);
        Assert.Equal(TeamId, tenant.TeamId);
        var node = Assert.IsType<ResourceNode>(authorizedResource);
        Assert.Equal(
            (ControlPlaneTenantId, ResourceNodeTypes.Team, TeamId),
            (node.TenantId, node.ResourceType, node.ResourceId)
        );
    }

    #endregion

    #region GetUsageAsync Tests

    [Fact]
    public async Task GetUsageAsync_WhenAuthorized_ReturnsTheLedgerForTheRange()
    {
        var from = new DateOnly(2026, 10, 1);
        var to = new DateOnly(2026, 10, 2);
        SetupAuthorization(AuthorizationResult.Success());
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        var mockUsageStore = MockRepository.Create<ITenantUsageStore>();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantUsageStore>())
            .Returns(mockUsageStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        mockUsageStore
            .Setup(x => x.ListAsync(TenantId, from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PersistedTenantUsage
                {
                    TenantId = TenantId,
                    TeamPath = "/platform/",
                    Day = from,
                    Meter = "requests",
                    Count = 42,
                },
            ])
            .Verifiable();

        var result = await Handler.GetUsageAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            from,
            to,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<TenantUsageResource>>(result);
        Assert.Equal(TenantId, ok.Value?.TenantId);
        Assert.Equal(from, ok.Value?.From);
        Assert.Equal(to, ok.Value?.To);
        var entry = Assert.Single(ok.Value!.Entries);
        Assert.Equal((from, "requests", 42L), (entry.Day, entry.Meter, entry.Count));
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
            TenantId,
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
            TenantId,
            from: null,
            to: DateOnly.MinValue.AddDays(TenantApiEndpointHandler.DefaultUsageDays - 1),
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
            TenantId,
            null,
            null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    #endregion

    #region GetSettingsAsync Tests

    [Fact]
    public async Task GetSettingsAsync_WhenFoundAndAuthorized_ReturnsJson()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSettingsAsync(httpContext, TenantId, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<TenantSettingsResource>>(result);
        Assert.Equal(TenantId, json.Value?.TenantId);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenantSettings?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSettingsAsync(httpContext, TenantId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region GetSettingLimitsAsync Tests

    [Fact]
    public async Task GetSettingLimitsAsync_WhenTheCallerMayUpdateTheTeam_ReportsCanChange()
    {
        SetupTenantLimits(OpenIdSettingNames.RateLimitTenantPerMinute);
        SetupStore();
        SetupTenant(CreateTenant("tenant-ct"));
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSettingLimitsAsync(
            httpContext,
            TenantId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<SettingLimitsResource>>(result);
        Assert.Equal([OpenIdSettingNames.RateLimitTenantPerMinute], json.Value?.Names);
        Assert.True(json.Value?.CanChange);
        Assert.False(httpContext.Response.Headers.ContainsKey("ETag"));
    }

    [Fact]
    public async Task GetSettingLimitsAsync_WhenTheCallerMayNotUpdateTheTeam_ReportsCannotChange()
    {
        SetupTenantLimits(OpenIdSettingNames.RateLimitTenantPerMinute);
        SetupStore();
        SetupTenant(CreateTenant("tenant-ct"));
        SetupAuthorization(AuthorizationResult.Success());
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.Team
                        && ((ResourceNode)resource).ResourceId == TeamId
                    ),
                    It.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                        requirements.Single() == Operations.Update
                    )
                )
            )
            .ReturnsAsync(AuthorizationResult.Failed())
            .Verifiable();

        var result = await Handler.GetSettingLimitsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<SettingLimitsResource>>(result);
        Assert.Equal([OpenIdSettingNames.RateLimitTenantPerMinute], json.Value?.Names);
        Assert.False(json.Value?.CanChange);
    }

    [Fact]
    public async Task GetSettingLimitsAsync_WhenTheTenantIsMissing_ReturnsNotFound()
    {
        SetupTenantLimits(OpenIdSettingNames.RateLimitTenantPerMinute);
        SetupStore();
        SetupTenant(null);
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.GetSettingLimitsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetSettingLimitsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupTenantLimits(OpenIdSettingNames.RateLimitTenantPerMinute);
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetSettingLimitsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region GetEffectiveSettingsAsync Tests

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenFoundAndAuthorized_ReturnsFlatSettings()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();

        var descriptor = new SettingDescriptor<bool>
        {
            Name = "claims_parameter_supported",
            AppliesTo = ["client"],
        };
        var effective = CreateSettingCollection(descriptor.Create(true));
        SetupSettingScopes(OpenIdSettingScopes.Tenant, (descriptor, true));
        SetupEffectiveSettingsResolver(effective);
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContextWithEnvironment(authenticated: true);

        var result = await Handler.GetEffectiveSettingsAsync(
            httpContext,
            TenantId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<TenantEffectiveSettingsResource>>(result);
        Assert.Equal(TenantId, json.Value?.TenantId);
        Assert.True(json.Value!.Settings.GetProperty("claims_parameter_supported").GetBoolean());
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetEffectiveSettingsAsync(
            httpContext,
            TenantId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenUnauthenticated_ReturnsUnauthorizedWithoutResolvingTheSettings()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContextWithEnvironment(authenticated: false);

        var result = await Handler.GetEffectiveSettingsAsync(
            httpContext,
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetEffectiveSettingsAsync(
            CreateHttpContextWithEnvironment(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    private void SetupEffectiveSettingsResolver(ISettingCollection effectiveSettings)
    {
        var teamSettings = MockRepository.Create<IReadOnlySettingCollection>();

        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamSettings.Object)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    teamSettings.Object,
                    OpenIdSettingScopes.Tenant,
                    TenantId,
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(effectiveSettings)
            .Verifiable();
    }

    private IReadOnlySettingCollection SetupTeamSettings()
    {
        if (MockTeamSettings is not null)
        {
            return MockTeamSettings.Object;
        }

        MockTeamSettings = MockRepository.Create<IReadOnlySettingCollection>();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockTeamSettings.Object)
            .Verifiable();
        return MockTeamSettings.Object;
    }

    // A creation is authorized at the team's node, then reads the team and its lineage, and resolves each team's
    // settings from the lineage's documents for the 'max_tenants' ceiling.
    private void SetupTeamForCreate(
        bool resolveSettings,
        int? maxTenants = null,
        long? tenantCount = null,
        IReadOnlyList<string>? ancestorTeamIds = null
    )
    {
        SetupTenantCreateAuthorization(null);
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable();
        MockTeamStore
            .Setup(x => x.GetOrDefaultAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTeam(ancestorTeamIds))
            .Verifiable();

        if (resolveSettings)
        {
            IReadOnlyList<PersistedTeam> lineage =
            [
                .. (ancestorTeamIds ?? []).Select(CreateLineageTeam),
                CreateTeam(),
            ];
            MockTeamStore
                .Setup(x => x.GetLineageAsync(TeamId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(lineage)
                .Verifiable();
            var platformSettings = MockRepository.Create<IReadOnlySettingCollection>();
            MockEffectiveSettingsResolver
                .Setup(x => x.ResolvePlatformAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(platformSettings.Object)
                .Verifiable();
            SetupLineageMaxTenants(TeamId, maxTenants);
        }

        if (tenantCount is not null)
        {
            MockTenantStore
                .Setup(x => x.CountAsync(TeamId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenantCount.Value)
                .Verifiable();
        }
    }

    // The effective settings of one team in the lineage a creation folds: its 'max_tenants' is the ceiling the creation
    // checks for the team.
    private void SetupLineageMaxTenants(string lineageTeamId, int? maxTenants)
    {
        var lineageSettings = MockRepository.Create<IReadOnlySettingCollection>();
        var configured = maxTenants ?? 0;
        lineageSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.MaxTenants, out configured))
            .Returns(maxTenants is not null)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    It.IsAny<IReadOnlySettingCollection>(),
                    OpenIdSettingScopes.Team,
                    lineageTeamId,
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(lineageSettings.Object)
            .Verifiable();
    }

    private static PersistedTeam CreateLineageTeam(string lineageTeamId) =>
        new()
        {
            TeamId = lineageTeamId,
            ConcurrencyToken = "lineage-ct",
            IsDisabled = false,
            DisplayName = "Customer",
            Settings = new PersistedTeamSettings
            {
                TeamId = lineageTeamId,
                ConcurrencyToken = "lineage-settings-ct",
                Value = EmptyObject(),
            },
        };

    private static PersistedTeam CreateTeam(IReadOnlyList<string>? ancestorTeamIds = null) =>
        new()
        {
            TeamId = TeamId,
            ConcurrencyToken = "team-ct",
            IsDisabled = false,
            DisplayName = "Team One",
            AncestorTeamIds = ancestorTeamIds ?? [],
            Settings = new PersistedTeamSettings
            {
                TeamId = TeamId,
                ConcurrencyToken = "team-settings-ct",
                Value = EmptyObject(),
            },
        };

    private void SetupTenantEnvironment(
        string environmentType = "production",
        string environmentName = "",
        bool isProduction = true,
        string tenantId = TenantId
    )
    {
        var teamSettings = SetupTeamSettings();

        var tenantSettings = MockRepository.Create<IReadOnlySettingCollection>();
        tenantSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.EnvironmentType))
            .Returns(environmentType)
            .Verifiable();
        tenantSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.EnvironmentName))
            .Returns(environmentName)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    teamSettings,
                    OpenIdSettingScopes.Tenant,
                    tenantId,
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(tenantSettings.Object)
            .Verifiable();

        var type = MockRepository.Create<ITenantEnvironmentType>();
        type.Setup(x => x.TypeName).Returns(environmentType).Verifiable();
        type.Setup(x => x.IsProduction).Returns(isProduction).Verifiable();
        var registered = type.Object;
        MockTenantEnvironmentCatalog
            .Setup(x => x.TryGetType(environmentType, out registered))
            .Returns(true)
            .Verifiable();
    }

    private static UnreadableSettingsException CreateUnreadableSettingsException(
        string scopeName,
        string ownerId
    ) => new(scopeName, ownerId, new JsonException("The document cannot be honored."));

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

        var mockEnvironment = new Mock<OpenIdEnvironment>(MockBehavior.Loose);
        mockEnvironment.Setup(x => x.JsonSerializerOptions).Returns(JsonSerializerOptions.Default);

        var mockContext = new Mock<OpenIdContext>(MockBehavior.Loose);
        mockContext.Setup(x => x.Environment).Returns(mockEnvironment.Object);

        httpContext.Features.Set<IOpenIdContextFeature>(
            Mock.Of<IOpenIdContextFeature>(feature => feature.OpenIdContext == mockContext.Object)
        );
        return httpContext;
    }

    #endregion

    #region UpdateSettingsAsync Tests

    private static bool HasProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out _);

    private void SetupClientCoherenceCheck(params ClientCoherenceFinding[] findings)
    {
        var teamSettings = MockRepository.Create<IReadOnlySettingCollection>();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamSettings.Object)
            .Verifiable();

        var currentEffective = CreateSettingCollection();
        var newEffective = CreateSettingCollection();

        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    teamSettings.Object,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => !HasProperty(e, "foo"))
                )
            )
            .Returns(currentEffective)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    teamSettings.Object,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => HasProperty(e, "foo"))
                )
            )
            .Returns(newEffective)
            .Verifiable();

        MockTenantClientCoherenceChecker
            .Setup(x =>
                x.FindIntroducedViolationsAsync(
                    MockStoreManager.Object,
                    TenantId,
                    currentEffective,
                    newEffective,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(findings)
            .Verifiable();
    }

    private void SetupSettingsWrite(PersistedTenantSettings settings)
    {
        MockTenantStore
            .Setup(x => x.UpdateSettingsAsync(settings, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenAuthorized_ReturnsNoContent()
    {
        SetupTenantChangedAnnouncement();
        SetupTenantChangedAudit();
        SetupValidSettings();
        SetupNoChangedLimits();
        SetupStore();
        var settings = CreateSettings("settings-ct");
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupClientCoherenceCheck();
        SetupSettingsWrite(settings);

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        VerifyTenantChanged(settingsChanged: true);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenALimitChangesAndTheTeamRefuses_ReturnsForbiddenWithoutWriting()
    {
        SetupValidSettings();
        SetupChangedLimits([OpenIdSettingNames.RateLimitTenantPerMinute]);
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct"))
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.Team
                        && ((ResourceNode)resource).ResourceId == TeamId
                    ),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Failed())
            .Verifiable();

        // Strict mocks: no coherence or store-write setup, so continuing past the refusal would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Contains("'rate_limit_tenant_per_minute'", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenALimitChangesAndTheTeamAuthorizes_ReturnsNoContent()
    {
        SetupTenantChangedAnnouncement();
        SetupTenantChangedAudit();
        SetupValidSettings();
        SetupChangedLimits([OpenIdSettingNames.RateLimitTenantPerMinute]);
        SetupStore();
        var settings = CreateSettings("settings-ct");
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.Team
                        && ((ResourceNode)resource).ResourceId == TeamId
                    ),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();
        SetupClientCoherenceCheck();
        SetupSettingsWrite(settings);

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        VerifyTenantChanged(settingsChanged: true);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheReplacedDocumentCannotBeHonored_WritesWithoutCheckingTheClients()
    {
        SetupTenantChangedAnnouncement();
        SetupTenantChangedAudit();
        SetupValidSettings();
        SetupNoChangedLimits();
        SetupStore();
        var settings = CreateSettings("settings-ct");
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var teamSettings = MockRepository.Create<IReadOnlySettingCollection>();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamSettings.Object)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    teamSettings.Object,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => HasProperty(e, "foo"))
                )
            )
            .Returns(CreateSettingCollection())
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    teamSettings.Object,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => !HasProperty(e, "foo"))
                )
            )
            .Throws(
                new JsonException(
                    "The settings are invalid: 'retired' is not a registered setting."
                )
            )
            .Verifiable();
        SetupSettingsWrite(settings);

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no FindIntroducedViolationsAsync setup, so checking a client would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        VerifyTenantChanged(settingsChanged: true);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheChangeMakesAClientIncoherent_ReturnsValidationProblem()
    {
        SetupValidSettings();
        SetupNoChangedLimits();
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct"))
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
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
            TenantId,
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
        Assert.Contains("'RS256'", problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheChangeMakesManyClientsIncoherent_ListsTenAndCountsTheRest()
    {
        SetupValidSettings();
        SetupNoChangedLimits();
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct"))
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupClientCoherenceCheck(
            Enumerable
                .Range(1, 12)
                .Select(index => new ClientCoherenceFinding(
                    $"client-{index}",
                    new ClientCoherenceViolation(
                        OpenIdSettingNames.IdTokenSignedResponseAlg,
                        "RS256",
                        OpenIdSettingNames.IdTokenSigningAlgValuesSupported
                    )
                ))
                .ToArray()
        );

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no UpdateSettingsAsync / SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var messages = problem.ProblemDetails.Errors[
            OpenIdSettingNames.IdTokenSigningAlgValuesSupported
        ];
        Assert.Equal(11, messages.Length);
        Assert.Contains("'client-10'", messages[9], StringComparison.Ordinal);
        Assert.Equal(
            $"Errors not listed for '{OpenIdSettingNames.IdTokenSigningAlgValuesSupported}': 2.",
            messages[^1]
        );
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenSettingsCannotBeHonored_ReturnsValidationProblem()
    {
        SetupInvalidSettings();
        SetupStore();
        var settings = CreateSettings("settings-ct");
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no UpdateSettingsAsync / SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            CreateAddPatch("unregistered_setting", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        AssertSettingsValidationProblem(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenIfMatchMismatch_ReturnsPreconditionFailed()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            CreateAddPatch("foo", "bar"),
            ifMatch: "stale-token",
            CancellationToken.None
        );

        var statusCode = Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, statusCode.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_RequestsUpdateOnTheTenantNode()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct"))
            .Verifiable();
        object? authorizedResource = null;
        IEnumerable<IAuthorizationRequirement>? authorizedRequirements = null;
        SetupAuthorizationCapture(
            (resource, requirements) =>
            {
                authorizedResource ??= resource;
                authorizedRequirements ??= requirements;
            }
        );

        // The stale If-Match ends the route right after its own authorization.
        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateAddPatch("foo", "bar"),
            ifMatch: "stale-token",
            CancellationToken.None
        );

        Assert.IsType<StatusCodeHttpResult>(result);
        var node = Assert.IsType<ResourceNode>(authorizedResource);
        Assert.Equal(ResourceNodeTypes.Tenant, node.ResourceType);
        Assert.Equal(TenantId, node.ResourceId);
        Assert.Equal(TenantId, node.TenantId);
        Assert.Equal([Operations.Update], authorizedRequirements);
    }

    #endregion

    #region GetSecretsAsync Tests

    [Fact]
    public async Task GetSecretsAsync_WhenFoundAndAuthorized_ReturnsJson()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSecretsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSecretsCollection("secrets-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretsAsync(httpContext, TenantId, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<TenantSecretsResource>>(result);
        Assert.Equal(TenantId, json.Value?.TenantId);
    }

    [Fact]
    public async Task GetSecretsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSecretsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenantSecrets?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretsAsync(httpContext, TenantId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetSecretsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetSecretsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region CreateSecretAsync Tests

    private ISecretProtector SetupCreateSecretProtector()
    {
        var protector = MockRepository.Create<ISecretProtector>().Object;
        MockSecretProtectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForTenant(TenantId),
                    It.Is<IReadOnlyCollection<string>>(v => v.Count == 0),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(protector)
            .Verifiable();
        return protector;
    }

    [Fact]
    public async Task CreateSecretAsync_WhenAuthorized_GeneratesAndReturnsCreated()
    {
        SetupTenantChangedAnnouncement();
        SetupSecretCreateValidation();
        SetupTenantSecretChangedAudit();
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId();

        var protector = SetupCreateSecretProtector();
        var generated = CreatePersistedSecret();
        MockSecretGenerator
            .Setup(x => x.GenerateSecret(protector, It.IsAny<GenerateSecretRequest>()))
            .Returns(new GeneratedSecret { Secret = generated, SecretMaterial = null })
            .Verifiable();
        MockTenantStore
            .Setup(x => x.AddSecretAsync(TenantId, generated, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            TenantId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var created = Assert.IsType<Created<SecretResource>>(result);
        Assert.Equal("secret-1", created.Value?.SecretId);
        Assert.Equal("secret-ct", httpContext.Response.Headers.ETag);
        VerifyTenantChanged(settingsChanged: false);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenTheRequestNamesALifecycle_GeneratesTheKeyWithIt()
    {
        SetupTenantChangedAnnouncement();
        SetupSecretCreateValidation();
        SetupTenantSecretChangedAudit();
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId();

        var protector = SetupCreateSecretProtector();
        var generated = CreatePersistedSecret();
        GenerateSecretRequest? generateRequest = null;
        MockSecretGenerator
            .Setup(x => x.GenerateSecret(protector, It.IsAny<GenerateSecretRequest>()))
            .Callback(
                (ISecretProtector _, GenerateSecretRequest request) => generateRequest = request
            )
            .Returns(new GeneratedSecret { Secret = generated, SecretMaterial = null })
            .Verifiable();
        MockTenantStore
            .Setup(x => x.AddSecretAsync(TenantId, generated, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var request = new CreateSecretRequest
        {
            SecretType = SecretTypes.Rsa,
            KeySizeBits = 2048,
            Use = "sig",
            Algorithm = "RS256",
            ActivatesWhen = DateTimeOffset.UnixEpoch.AddDays(2),
            RetiresWhen = DateTimeOffset.UnixEpoch.AddDays(92),
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
        };

        var result = await Handler.CreateSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            request,
            CancellationToken.None
        );

        Assert.IsType<Created<SecretResource>>(result);
        Assert.NotNull(generateRequest);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(2), generateRequest.ActivatesWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(92), generateRequest.RetiresWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(99), generateRequest.ExpiresWhen);
    }

    [Fact]
    public async Task ImportSecretAsync_WhenTheRequestNamesALifecycle_ImportsTheKeyWithIt()
    {
        SetupTenantChangedAnnouncement();
        SetupTenantSecretChangedAudit();
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockSecretValidator
            .Setup(x =>
                x.ValidateImport(It.IsAny<ImportSecretRequest>(), It.IsAny<DateTimeOffset>())
            )
            .Returns((ManagementError?)null)
            .Verifiable();
        SetupResourceId("secret-2");
        var imported = CreatePersistedSecret("secret-2");
        var importedSecret = (PersistedSecret?)imported;
        string? error = null;
        // Moq cannot capture beside out arguments, so the import request is matched by the instants it carries.
        MockKeyImporter
            .Setup(x =>
                x.TryImportKey(
                    It.Is<SecretKeyImportRequest>(request =>
                        request.SecretId == "secret-2"
                        && request.ActivatesWhen == DateTimeOffset.UnixEpoch.AddDays(2)
                        && request.RetiresWhen == DateTimeOffset.UnixEpoch.AddDays(92)
                        && request.ExpiresWhen == DateTimeOffset.UnixEpoch.AddDays(99)
                    ),
                    out importedSecret,
                    out error
                )
            )
            .Returns(true)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.AddSecretAsync(TenantId, imported, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var request = new ImportSecretRequest
        {
            SecretType = SecretTypes.Rsa,
            Format = "jwk",
            KeyMaterial = """{"kty":"RSA","n":"AQAB","e":"AQAB"}""",
            Use = "sig",
            Algorithm = "RS256",
            ActivatesWhen = DateTimeOffset.UnixEpoch.AddDays(2),
            RetiresWhen = DateTimeOffset.UnixEpoch.AddDays(92),
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
        };

        var result = await Handler.ImportSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            request,
            CancellationToken.None
        );

        var created = Assert.IsType<Created<SecretResource>>(result);
        Assert.Equal("secret-2", created.Value?.SecretId);
        VerifyTenantChanged(settingsChanged: false);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenTenantNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            TenantId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.CreateSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task ImportSecretAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.ImportSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            new ImportSecretRequest
            {
                SecretType = SecretTypes.Rsa,
                Format = "jwk",
                KeyMaterial = """{"kty":"RSA","n":"AQAB","e":"AQAB"}""",
                ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
            },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenDuplicate_ReturnsConflict()
    {
        SetupSecretCreateValidation();
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId();

        var protector = SetupCreateSecretProtector();
        var generated = CreatePersistedSecret();
        MockSecretGenerator
            .Setup(x => x.GenerateSecret(protector, It.IsAny<GenerateSecretRequest>()))
            .Returns(new GeneratedSecret { Secret = generated, SecretMaterial = null })
            .Verifiable();
        MockTenantStore
            .Setup(x => x.AddSecretAsync(TenantId, generated, It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("duplicate"))
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            TenantId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenValidationFails_ReturnsBadRequest()
    {
        SetupSecretCreateValidation();
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockSecretValidator
            .Setup(x =>
                x.ValidateCreate(It.IsAny<CreateSecretRequest>(), It.IsAny<DateTimeOffset>())
            )
            .Returns(
                SecretCreateValidation.Invalid(
                    new ManagementError
                    {
                        StatusCode = StatusCodes.Status400BadRequest,
                        Detail = "incoherent",
                    }
                )
            )
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            TenantId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    #endregion

    #region GetPublicKeyAsync Tests

    private static PublicKeyResource CreatePublicKeyResource() =>
        new()
        {
            SecretId = "secret-1",
            SecretType = SecretTypes.Rsa,
            Jwk = EmptyObject(),
            Pem = "-----BEGIN PUBLIC KEY-----\nMII=\n-----END PUBLIC KEY-----",
        };

    [Fact]
    public async Task GetPublicKeyAsync_WhenAsymmetricAndAuthorized_ReturnsJson()
    {
        SetupStore();
        MockTenantStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(TenantId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockSecretPublicKeyConverter
            .Setup(x => x.Convert(It.IsAny<PersistedSecret>()))
            .Returns(CreatePublicKeyResource())
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetPublicKeyAsync(
            httpContext,
            TenantId,
            "secret-1",
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<PublicKeyResource>>(result);
        Assert.Equal("secret-1", json.Value?.SecretId);
        Assert.StartsWith("-----BEGIN PUBLIC KEY-----", json.Value?.Pem);
    }

    [Fact]
    public async Task GetPublicKeyAsync_WhenSymmetric_ReturnsNotFound()
    {
        SetupStore();
        MockTenantStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(TenantId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockSecretPublicKeyConverter
            .Setup(x => x.Convert(It.IsAny<PersistedSecret>()))
            .Returns((PublicKeyResource?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetPublicKeyAsync(
            httpContext,
            TenantId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetPublicKeyAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockTenantStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(TenantId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedSecret?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetPublicKeyAsync(
            httpContext,
            TenantId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetPublicKeyAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetPublicKeyAsync(
            httpContext,
            TenantId,
            "secret-1",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region GetSecretAsync / UpdateSecretAsync / DeleteSecretAsync Tests

    [Fact]
    public async Task GetSecretAsync_WhenFoundAndAuthorized_ReturnsJson()
    {
        SetupStore();
        MockTenantStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(TenantId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretAsync(
            httpContext,
            TenantId,
            "secret-1",
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<SecretResource>>(result);
        Assert.Equal("secret-1", json.Value?.SecretId);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenAuthorized_ReturnsNoContent()
    {
        SetupTenantChangedAnnouncement();
        SetupSecretUpdateValidation();
        SetupTenantSecretChangedAudit();
        SetupStore();
        MockTenantStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(TenantId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockTenantStore
            .Setup(x =>
                x.UpdateSecretAsync(
                    TenantId,
                    It.IsAny<PersistedSecret>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSecretAsync(
            httpContext,
            TenantId,
            "secret-1",
            UpdateSecretRequest(),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        VerifyTenantChanged(settingsChanged: false);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenTheRequestOmitsTheInstants_KeepsTheStoredOnesBoundedByTheExpiry()
    {
        // A retired key stays retired when only its expiry is extended; the retirement follows a shortened expiry.
        SetupTenantChangedAnnouncement();
        SetupSecretUpdateValidation();
        SetupTenantSecretChangedAudit();
        SetupStore();
        var stored = new PersistedSecret
        {
            SecretId = "secret-1",
            ConcurrencyToken = "secret-ct",
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ActivatesWhen = DateTimeOffset.UnixEpoch.AddDays(2),
            RetiresWhen = DateTimeOffset.UnixEpoch.AddDays(92),
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.SymmetricRaw,
            Origin = SecretOrigins.Generated,
            ManagedBy = "rotation",
            KeySizeBits = 256,
            EncodedValue = "encoded",
        };
        MockTenantStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(TenantId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(stored)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        PersistedSecret? updated = null;
        MockTenantStore
            .Setup(x =>
                x.UpdateSecretAsync(
                    TenantId,
                    It.IsAny<PersistedSecret>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback((string _, PersistedSecret secret, CancellationToken _) => updated = secret)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var request = new UpdateSecretRequest
        {
            Use = "sig",
            Algorithm = "RS256",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(50),
        };

        var result = await Handler.UpdateSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            "secret-1",
            request,
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.NotNull(updated);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(2), updated.ActivatesWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(50), updated.RetiresWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(50), updated.ExpiresWhen);
        Assert.Equal("rotation", updated.ManagedBy);
    }

    [Fact]
    public async Task DeleteSecretAsync_WhenAuthorized_ReturnsNoContent()
    {
        SetupTenantChangedAnnouncement();
        SetupTenantSecretChangedAudit();
        SetupStore();
        MockTenantStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(TenantId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockTenantStore
            .Setup(x => x.RemoveSecretAsync(TenantId, "secret-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteSecretAsync(
            httpContext,
            TenantId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        VerifyTenantChanged(settingsChanged: false);
    }

    [Fact]
    public async Task DeleteSecretAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockTenantStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(TenantId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedSecret?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteSecretAsync(
            httpContext,
            TenantId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetSecretAsync_AuthorizesAgainstTheTenantNode()
    {
        SetupStore();
        MockTenantStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(TenantId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedSecret())
            .Verifiable();
        object? capturedResource = null;
        SetupAuthorizationCapture(resource => capturedResource = resource);

        var httpContext = CreateHttpContext(authenticated: true);

        await Handler.GetSecretAsync(httpContext, TenantId, "secret-1", CancellationToken.None);

        var node = Assert.IsType<ResourceNode>(capturedResource);
        Assert.Equal(TenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.Tenant, node.ResourceType);
        Assert.Equal(TenantId, node.ResourceId);
    }

    [Fact]
    public async Task DeleteSecretAsync_AuthorizesAgainstTheTenantNode()
    {
        SetupTenantChangedAnnouncement();
        SetupTenantSecretChangedAudit();
        SetupStore();
        MockTenantStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(TenantId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedSecret())
            .Verifiable();
        object? capturedResource = null;
        SetupAuthorizationCapture(resource => capturedResource = resource);
        MockTenantStore
            .Setup(x => x.RemoveSecretAsync(TenantId, "secret-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteSecretAsync(
            httpContext,
            TenantId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        var node = Assert.IsType<ResourceNode>(capturedResource);
        Assert.Equal(TenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.Tenant, node.ResourceType);
        Assert.Equal(TenantId, node.ResourceId);
    }

    [Fact]
    public async Task GetSecretAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            "secret-1",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.UpdateSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            "secret-1",
            UpdateSecretRequest(),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteSecretAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.DeleteSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            "secret-1",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region DeleteTenantAsync Tests

    [Fact]
    public async Task DeleteTenantAsync_WhenPreconditionsPass_ReturnsNoContent()
    {
        SetupTenantDeleteAuthorization(null);
        SetupTenantChangedAnnouncement();
        SetupTenantChangedAudit();
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        MockTenantValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<PersistedTenant>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.RemoveAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupEffectiveSettingsResolver(CreateSettingCollection());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteTenantAsync(httpContext, TenantId, CancellationToken.None);

        Assert.IsType<NoContent>(result);
        VerifyTenantChanged(settingsChanged: false);
    }

    [Fact]
    public async Task DeleteTenantAsync_WhenDeletionProtectionIsOn_ReturnsConflictAndKeepsTheTenant()
    {
        SetupTenantDeleteAuthorization(null);
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        MockTenantValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<PersistedTenant>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        var protectedSettings = new Mock<ISettingCollection>(MockBehavior.Loose);
        protectedSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.DeletionProtection))
            .Returns(true)
            .Verifiable();
        SetupEffectiveSettingsResolver(protectedSettings.Object);

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteTenantAsync(httpContext, TenantId, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        MockTenantStore.Verify(
            x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DeleteTenantAsync_WhenValidatorReportsConflict_ReturnsProblem()
    {
        SetupTenantDeleteAuthorization(null);
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        MockTenantValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<PersistedTenant>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ManagementError
                {
                    StatusCode = StatusCodes.Status409Conflict,
                    Detail = "conflict",
                }
            )
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteTenantAsync(httpContext, TenantId, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteTenantAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupTenantDeleteAuthorization(null);
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteTenantAsync(httpContext, TenantId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task DeleteTenantAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheTenant()
    {
        SetupTenantDeleteAuthorization(
            new ManagementError
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Detail = "The caller is not authorized to delete this tenant.",
            }
        );

        // Strict mocks: no IStoreManagerFactory.CreateAsync setup, so reading the tenant would fail the test.
        var result = await Handler.DeleteTenantAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region CreateTenantAsync / UpdateTenantAsync Tests

    private const string DerivedIssuer = "https://op.example/acme";

    private static CreateTenantRequest CreateTenantRequest(string? issuer = null) =>
        new()
        {
            TenantId = "acme",
            Issuer = issuer,
            DisplayName = "Tenant One",
            IsDisabled = false,
            Settings = EmptyObject(),
        };

    private void SetupDefaultAlias(string tenantId, string? alias) =>
        MockTenantResolver
            .Setup(x =>
                x.GetDefaultMtlsEndpointAliasAuthorityAsync(tenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(alias)
            .Verifiable();

    private void SetupDefaultIssuer(string tenantId, string? issuer) =>
        MockTenantResolver
            .Setup(x => x.GetDefaultIssuerAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(issuer)
            .Verifiable();

    private Action<Action<PersistedTenant?>> SetupCreateValidation()
    {
        PersistedTenant? validated = null;
        MockTenantValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<PersistedTenant>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (PersistedTenant tenant, IStoreManager _, CancellationToken _) => validated = tenant
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        return assert => assert(validated);
    }

    private void SetupCreatePersistence(PersistedTenant seeded)
    {
        MockTenantStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedTenant>(), It.IsAny<CancellationToken>()))
            .Callback(() => Events.Add("add-tenant"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Events.Add("save"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTenantProvisioner
            .Setup(x => x.SeedAsync("acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync(seeded)
            .Verifiable();
    }

    private static PersistedTenant CreateSeededTenant(string issuer) =>
        new()
        {
            TenantId = "acme",
            TeamId = "platform",
            ConcurrencyToken = "seeded-ct",
            Issuer = issuer,
            IsDisabled = false,
            DisplayName = "Tenant One",
            Settings = CreateSettings("settings-ct"),
            Secrets = CreateSecretsCollection("secrets-ct"),
        };

    [Fact]
    public async Task CreateTenantAsync_WhenTheTeamIsMissing_ReturnsNotFound()
    {
        SetupTenantCreateAuthorization(null);
        SetupStore();
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable();
        MockTeamStore
            .Setup(x => x.GetOrDefaultAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTeam?)null)
            .Verifiable();

        // Strict mocks: nothing else is set up, so validating or persisting under a missing team would fail the test.
        var result = await Handler.CreateTenantAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateTenantRequest(),
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheTeam()
    {
        SetupTenantCreateAuthorization(
            new ManagementError
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Detail = "The caller is not authorized to create this tenant.",
            }
        );

        // Strict mocks: no IStoreManagerFactory.CreateAsync setup, so reading the team would fail the test.
        var result = await Handler.CreateTenantAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateTenantRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenTheTeamIsFull_ReturnsConflict()
    {
        SetupTeamForCreate(resolveSettings: true, maxTenants: 2, tenantCount: 2);
        SetupDefaultAlias("acme", alias: null);
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        SetupCreateValidation();

        // Strict mocks: no AddAsync / SaveChangesAsync setup, so persisting the tenant would fail the test.
        var result = await Handler.CreateTenantAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateTenantRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal(
            "The team 'platform' holds its maximum of 2 tenants.",
            problem.ProblemDetails.Detail
        );
    }

    [Fact]
    public async Task CreateTenantAsync_WhenTheParentTeamIsFull_ReturnsConflict()
    {
        SetupTeamForCreate(resolveSettings: true, ancestorTeamIds: ["acme"]);
        SetupLineageMaxTenants("acme", maxTenants: 1);
        MockTenantStore
            .Setup(x => x.CountAsync("acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Verifiable();
        SetupDefaultAlias("acme", alias: null);
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        SetupCreateValidation();

        // Strict mocks: no AddAsync / SaveChangesAsync setup, so persisting the tenant would fail the test.
        var result = await Handler.CreateTenantAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateTenantRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal(
            "The team 'acme' holds its maximum of 1 tenants.",
            problem.ProblemDetails.Detail
        );
    }

    [Fact]
    public async Task CreateTenantAsync_WhenAnAncestorTeamsMaxTenantsIsReached_Returns409NamingTheAncestor()
    {
        SetupTeamForCreate(resolveSettings: true, ancestorTeamIds: ["customer", "acme"]);
        SetupLineageMaxTenants("customer", maxTenants: 4);
        SetupLineageMaxTenants("acme", maxTenants: null);
        MockTenantStore
            .Setup(x => x.CountAsync("customer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(4)
            .Verifiable();
        SetupDefaultAlias("acme", alias: null);
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        SetupCreateValidation();

        // Strict mocks: no AddAsync / SaveChangesAsync setup, so persisting the tenant would fail the test.
        var result = await Handler.CreateTenantAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateTenantRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal(
            "The team 'customer' holds its maximum of 4 tenants.",
            problem.ProblemDetails.Detail
        );
    }

    [Fact]
    public async Task CreateTenantAsync_WhenIssuerIsOmitted_CreatesTheTenantAtItsDerivedIssuer()
    {
        SetupTeamForCreate(resolveSettings: true);
        SetupCreatorAssignment();
        SetupTenantChangedAudit();
        SetupDefaultAlias("acme", alias: null);
        SetupValidSettings();
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        var assertValidated = SetupCreateValidation();
        SetupCreatePersistence(CreateSeededTenant(DerivedIssuer));
        SetupTenantEnvironment(tenantId: "acme");

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateTenantAsync(
            httpContext,
            TeamId,
            CreateTenantRequest(issuer: string.Empty),
            CancellationToken.None
        );

        var created = Assert.IsType<Created<TenantResource>>(result);
        Assert.Equal("/tenants/acme", created.Location);
        Assert.NotNull(created.Value);
        Assert.Equal(DerivedIssuer, created.Value.Issuer);
        assertValidated(validated =>
        {
            Assert.NotNull(validated);
            Assert.Equal("acme", validated.TenantId);
            Assert.Equal(DerivedIssuer, validated.Issuer);
        });
    }

    [Fact]
    public async Task CreateTenantAsync_WhenAliasIsOmitted_CreatesTheTenantAtItsDerivedAlias()
    {
        SetupTeamForCreate(resolveSettings: true);
        SetupCreatorAssignment();
        SetupTenantChangedAudit();
        const string derivedAlias = "https://acme.mtls.example";

        SetupValidSettings();
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        SetupDefaultAlias("acme", derivedAlias);
        var assertValidated = SetupCreateValidation();
        SetupCreatePersistence(CreateSeededTenant(DerivedIssuer));
        SetupTenantEnvironment(tenantId: "acme");

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateTenantAsync(
            httpContext,
            TeamId,
            CreateTenantRequest(issuer: string.Empty),
            CancellationToken.None
        );

        Assert.IsType<Created<TenantResource>>(result);
        assertValidated(validated =>
        {
            Assert.NotNull(validated);
            Assert.Equal(derivedAlias, validated.MtlsEndpointAliasAuthority);
        });
    }

    [Fact]
    public async Task CreateTenantAsync_WhenIssuerIsGiven_KeepsItWithoutDerivingOne()
    {
        SetupTeamForCreate(resolveSettings: true);
        SetupCreatorAssignment();
        SetupTenantChangedAudit();
        SetupDefaultAlias("acme", alias: null);
        const string issuer = "https://acme.example";

        SetupValidSettings();
        SetupStore();
        var assertValidated = SetupCreateValidation();
        SetupCreatePersistence(CreateSeededTenant(issuer));
        SetupTenantEnvironment(tenantId: "acme");

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no GetDefaultIssuerAsync setup, so deriving an issuer would fail the test.
        var result = await Handler.CreateTenantAsync(
            httpContext,
            TeamId,
            CreateTenantRequest(issuer),
            CancellationToken.None
        );

        Assert.IsType<Created<TenantResource>>(result);
        assertValidated(validated => Assert.Equal(issuer, validated?.Issuer));
    }

    [Fact]
    public async Task CreateTenantAsync_WhenCreated_SeedsTheTenantAndReturnsItAsSeeded()
    {
        SetupTeamForCreate(resolveSettings: true);
        SetupCreatorAssignment();
        SetupTenantChangedAudit();
        SetupDefaultAlias("acme", alias: null);
        SetupValidSettings();
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        SetupCreateValidation();
        SetupCreatePersistence(CreateSeededTenant(DerivedIssuer));
        SetupTenantEnvironment(tenantId: "acme");

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateTenantAsync(
            httpContext,
            TeamId,
            CreateTenantRequest(),
            CancellationToken.None
        );

        // The seed returns the row it committed, whose concurrency token a seed contributor may have replaced, so the response carries it.
        var created = Assert.IsType<Created<TenantResource>>(result);
        Assert.NotNull(created.Value);
        Assert.Equal("seeded-ct", created.Value.ConcurrencyToken);
        Assert.Equal("production", created.Value.Environment?.Type);
        Assert.Equal("seeded-ct", httpContext.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task CreateTenantAsync_WhenTheCreatorIsAssignedOwner_RecordsTheTenantThenTheCreatorsAssignment()
    {
        SetupTeamForCreate(resolveSettings: true);
        SetupCreatorAssignment(CreateAssignment("principal-1", BuiltInRoles.Owner));
        SetupDefaultAlias("acme", alias: null);
        SetupValidSettings();
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        SetupCreateValidation();
        SetupCreatePersistence(CreateSeededTenant(DerivedIssuer));
        SetupTenantEnvironment(tenantId: "acme");
        var recorded = new List<string>();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordTenantChangedAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    ResourceChangeTypes.Created,
                    It.IsAny<JsonElement?>()
                )
            )
            .Callback(
                (HttpContext _, string _, string _, string changeType, JsonElement? _) =>
                    recorded.Add($"tenant:{changeType}")
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupRoleAssignmentChangedAuditCapture(auditEvent =>
            recorded.Add(
                $"{auditEvent.ChangeType}:{auditEvent.NodeType}/{auditEvent.NodeId}:{auditEvent.ResourceValues?.GetProperty("roleName").GetString()}"
            )
        );

        await Handler.CreateTenantAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            CreateTenantRequest(),
            CancellationToken.None
        );

        Assert.Equal(
            ["tenant:created", $"created:tenant/{TenantId}:{BuiltInRoles.Owner}"],
            recorded
        );
    }

    [Fact]
    public async Task CreateTenantAsync_WhenSettingsCannotBeHonored_ReturnsValidationProblem()
    {
        SetupTeamForCreate(resolveSettings: true);
        SetupDefaultAlias("acme", alias: null);
        SetupInvalidSettings();
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        MockTenantValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<PersistedTenant>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no AddAsync / SaveChangesAsync / SeedAsync setup, so persisting the tenant would fail the test.
        var result = await Handler.CreateTenantAsync(
            httpContext,
            TeamId,
            CreateTenantRequest(),
            CancellationToken.None
        );

        AssertSettingsValidationProblem(result);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenValidatorReportsConflict_ReturnsProblem()
    {
        SetupTeamForCreate(resolveSettings: false);
        SetupDefaultAlias("acme", alias: null);
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        MockTenantValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<PersistedTenant>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ManagementError
                {
                    StatusCode = StatusCodes.Status409Conflict,
                    Detail = "conflict",
                }
            )
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateTenantAsync(
            httpContext,
            TeamId,
            CreateTenantRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    private PersistedTenant SetupUpdate(PersistedTenant tenant, Action<UpdateTenantRequest> capture)
    {
        SetupTenantUpdateAuthorization(null);
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant)
            .Verifiable();
        MockTenantValidator
            .Setup(x =>
                x.ValidateUpdateAsync(
                    It.IsAny<PersistedTenant>(),
                    It.IsAny<UpdateTenantRequest>(),
                    It.IsAny<string?>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    PersistedTenant _,
                    UpdateTenantRequest model,
                    string? _,
                    IStoreManager _,
                    CancellationToken _
                ) => capture(model)
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.UpdateAsync(It.IsAny<PersistedTenant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        return tenant;
    }

    [Fact]
    public async Task UpdateTenantAsync_WhenPreconditionsPass_ReturnsNoContent()
    {
        SetupTenantChangedAnnouncement();
        SetupTenantChangedAudit();
        SetupDefaultAlias(TenantId, alias: null);
        UpdateTenantRequest? validated = null;
        var tenant = SetupUpdate(
            CreateTenant("tenant-ct", issuer: "https://op.example/tenant-1"),
            model => validated = model
        );

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no GetDefaultIssuerAsync setup, so an untouched issuer is kept as it is.
        var result = await Handler.UpdateTenantAsync(
            httpContext,
            TenantId,
            new JsonPatchDocument<UpdateTenantRequest>(),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.Equal("https://op.example/tenant-1", validated?.Issuer);
        Assert.Equal("https://op.example/tenant-1", tenant.Issuer);
        VerifyTenantChanged(settingsChanged: false);
    }

    [Fact]
    public async Task UpdateTenantAsync_WhenIssuerIsCleared_RestoresTheDerivedIssuer()
    {
        SetupTenantChangedAnnouncement();
        SetupTenantChangedAudit();
        SetupDefaultAlias(TenantId, alias: null);
        const string derived = "https://op.example/tenant-1";

        UpdateTenantRequest? validated = null;
        var tenant = SetupUpdate(
            CreateTenant("tenant-ct", issuer: "https://custom.example"),
            model => validated = model
        );
        SetupDefaultIssuer(TenantId, derived);

        var patch = new JsonPatchDocument<UpdateTenantRequest>();
        patch.Replace(x => x.Issuer, null);

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateTenantAsync(
            httpContext,
            TenantId,
            patch,
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.Equal(derived, validated?.Issuer);
        Assert.Equal(derived, tenant.Issuer);
    }

    [Fact]
    public async Task UpdateTenantAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupTenantUpdateAuthorization(null);
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateTenantAsync(
            httpContext,
            TenantId,
            new JsonPatchDocument<UpdateTenantRequest>(),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task UpdateTenantAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheTenant()
    {
        SetupTenantUpdateAuthorization(
            new ManagementError
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Detail = "The caller is not authorized to update this tenant.",
            }
        );

        // Strict mocks: no IStoreManagerFactory.CreateAsync setup, so reading the tenant would fail the test.
        var result = await Handler.UpdateTenantAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            new JsonPatchDocument<UpdateTenantRequest>(),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region GetAddressAsync Tests

    private void SetupAddressValidation(
        string tenantId,
        string? issuer,
        params TenantValidationFailure[] failures
    ) =>
        MockTenantResolver
            .Setup(x =>
                x.ValidateTenantAsync(
                    tenantId,
                    issuer,
                    null,
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(failures)
            .Verifiable();

    // The team route reads the team and then the tenant; the strict mocks fail a read the route does not make.
    private void SetupTeamPreviewStore(PersistedTeam? team, bool readsTenant)
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable();
        MockTeamStore
            .Setup(x => x.GetOrDefaultAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team)
            .Verifiable();

        if (!readsTenant)
        {
            return;
        }

        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
    }

    private void SetupNoTenant(string tenantId) =>
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

    [Fact]
    public async Task GetAddressAsync_WhenTheTenantDoesNotExist_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync("acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        // Strict mocks: a missing tenant is not previewed.
        var result = await Handler.GetAddressAsync(
            CreateHttpContext(authenticated: true),
            "acme",
            issuer: null,
            mtlsEndpointAliasAuthority: null,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetAddressAsync_RequestsReadOnTheTenantNode()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct", issuer: "https://op.example/tenant-1"))
            .Verifiable();
        SetupDefaultAlias(TenantId, alias: null);
        SetupAddressValidation(TenantId, "https://op.example/tenant-1");
        object? authorizedResource = null;
        IEnumerable<IAuthorizationRequirement>? requirements = null;
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
                    IEnumerable<IAuthorizationRequirement> value
                ) =>
                {
                    authorizedResource = resource;
                    requirements = value;
                }
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();

        var result = await Handler.GetAddressAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            issuer: null,
            mtlsEndpointAliasAuthority: null,
            CancellationToken.None
        );

        Assert.IsType<Ok<TenantAddressResource>>(result);
        var node = Assert.IsType<ResourceNode>(authorizedResource);
        Assert.Equal(
            (TenantId, ResourceNodeTypes.Tenant, TenantId),
            (node.TenantId, node.ResourceType, node.ResourceId)
        );
        Assert.Same(Operations.Read, Assert.Single(requirements!));
    }

    [Fact]
    public async Task GetTeamTenantAddressAsync_RequestsCreateOnTheTeamNode()
    {
        SetupTeamPreviewStore(CreateTeam(), readsTenant: true);
        SetupNoTenant("acme");
        SetupDefaultAlias("acme", alias: null);
        SetupDefaultIssuer("acme", DerivedIssuer);
        SetupAddressValidation("acme", DerivedIssuer);
        object? authorizedResource = null;
        IEnumerable<IAuthorizationRequirement>? requirements = null;
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
                    IEnumerable<IAuthorizationRequirement> value
                ) =>
                {
                    authorizedResource = resource;
                    requirements = value;
                }
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();

        var result = await Handler.GetTeamTenantAddressAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            "acme",
            issuer: null,
            mtlsEndpointAliasAuthority: null,
            CancellationToken.None
        );

        Assert.IsType<Ok<TenantAddressResource>>(result);
        var node = Assert.IsType<ResourceNode>(authorizedResource);
        Assert.Equal(
            (ControlPlaneTenantId, ResourceNodeTypes.Team, TeamId),
            (node.TenantId, node.ResourceType, node.ResourceId)
        );
        Assert.Same(Operations.Create, Assert.Single(requirements!));
    }

    [Fact]
    public async Task GetTeamTenantAddressAsync_WhenTheTeamIsMissing_ReturnsNotFound()
    {
        SetupTeamPreviewStore(null, readsTenant: false);
        SetupAuthorization(AuthorizationResult.Success());

        // Strict mocks: a missing team reads no tenant and previews nothing.
        var result = await Handler.GetTeamTenantAddressAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            "acme",
            issuer: null,
            mtlsEndpointAliasAuthority: null,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetTeamTenantAddressAsync_WhenTheTenantExists_ReportsItTaken()
    {
        SetupDefaultAlias(TenantId, alias: null);
        SetupTeamPreviewStore(CreateTeam(), readsTenant: true);
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct", issuer: "https://login.existing.example"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupDefaultIssuer(TenantId, DerivedIssuer);
        SetupAddressValidation(TenantId, DerivedIssuer);

        var result = await Handler.GetTeamTenantAddressAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            TenantId,
            issuer: null,
            mtlsEndpointAliasAuthority: null,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<TenantAddressResource>>(result);
        var address = Assert.IsType<TenantAddressResource>(ok.Value);
        Assert.True(address.Exists);
        Assert.Equal(DerivedIssuer, address.Issuer);
        Assert.True(address.IsIssuerDerived);
    }

    [Fact]
    public async Task GetTeamTenantAddressAsync_WhenNotAuthorized_ReturnsForbidden()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no store is opened for an unauthorized caller.
        var result = await Handler.GetTeamTenantAddressAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            "acme",
            issuer: null,
            mtlsEndpointAliasAuthority: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task GetTeamTenantAddressAsync_PreviewsTheAddressANewTenantWouldHave()
    {
        SetupDefaultAlias("acme", alias: null);
        SetupTeamPreviewStore(CreateTeam(), readsTenant: true);
        SetupNoTenant("acme");
        SetupAuthorization(AuthorizationResult.Success());
        SetupDefaultIssuer("acme", DerivedIssuer);
        SetupAddressValidation("acme", DerivedIssuer);

        var result = await Handler.GetTeamTenantAddressAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            "acme",
            issuer: null,
            mtlsEndpointAliasAuthority: null,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<TenantAddressResource>>(result);
        var address = Assert.IsType<TenantAddressResource>(ok.Value);
        Assert.Equal("acme", address.TenantId);
        Assert.Equal(DerivedIssuer, address.Issuer);
        Assert.True(address.IsIssuerDerived);
        Assert.Null(address.MtlsEndpointAliasAuthority);
        Assert.False(address.IsMtlsEndpointAliasAuthorityDerived);
        Assert.False(address.Exists);
        Assert.Empty(address.Failures);
    }

    [Theory]
    [InlineData(null, "https://acme.mtls.example", true)]
    [InlineData("https://other.mtls.example", "https://other.mtls.example", false)]
    public async Task GetTeamTenantAddressAsync_PreviewsTheProposedOrDerivedAlias(
        string? proposedAlias,
        string expectedAlias,
        bool expectedDerived
    )
    {
        if (proposedAlias is null)
            SetupDefaultAlias("acme", "https://acme.mtls.example");
        SetupTeamPreviewStore(CreateTeam(), readsTenant: true);
        SetupNoTenant("acme");
        SetupAuthorization(AuthorizationResult.Success());
        SetupDefaultIssuer("acme", DerivedIssuer);
        MockTenantResolver
            .Setup(x =>
                x.ValidateTenantAsync(
                    "acme",
                    DerivedIssuer,
                    expectedAlias,
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([])
            .Verifiable();

        var result = await Handler.GetTeamTenantAddressAsync(
            CreateHttpContext(authenticated: true),
            TeamId,
            "acme",
            issuer: null,
            mtlsEndpointAliasAuthority: proposedAlias,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<TenantAddressResource>>(result);
        var address = Assert.IsType<TenantAddressResource>(ok.Value);
        Assert.Equal(expectedAlias, address.MtlsEndpointAliasAuthority);
        Assert.Equal(expectedDerived, address.IsMtlsEndpointAliasAuthorityDerived);
        Assert.Empty(address.Failures);
    }

    [Fact]
    public async Task GetAddressAsync_WhenIssuerIsProposed_ReportsItsFailuresWithoutDerivingOne()
    {
        SetupDefaultAlias(TenantId, alias: null);
        const string proposed = "https://taken.example";

        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct", issuer: "https://op.example/tenant-1"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupAddressValidation(
            TenantId,
            proposed,
            new TenantValidationFailure { Detail = "taken", IsConflict = true }
        );

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetAddressAsync(
            httpContext,
            TenantId,
            proposed,
            mtlsEndpointAliasAuthority: null,
            CancellationToken.None
        );

        var address = Assert.IsType<TenantAddressResource>(
            Assert.IsType<Ok<TenantAddressResource>>(result).Value
        );
        Assert.Equal(proposed, address.Issuer);
        Assert.False(address.IsIssuerDerived);
        Assert.True(address.Exists);
        var failure = Assert.Single(address.Failures);
        Assert.Equal("taken", failure.Detail);
        Assert.True(failure.IsConflict);
    }

    [Fact]
    public async Task GetAddressAsync_WhenTenantExistsAndNoIssuerIsProposed_PreviewsItsCurrentIssuer()
    {
        SetupDefaultAlias(TenantId, alias: null);
        const string current = "https://op.example/tenant-1";

        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct", issuer: current))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupAddressValidation(TenantId, current);

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetAddressAsync(
            httpContext,
            TenantId,
            issuer: null,
            mtlsEndpointAliasAuthority: null,
            CancellationToken.None
        );

        var address = Assert.IsType<TenantAddressResource>(
            Assert.IsType<Ok<TenantAddressResource>>(result).Value
        );
        Assert.Equal(current, address.Issuer);
        Assert.False(address.IsIssuerDerived);
        Assert.True(address.Exists);
    }

    [Fact]
    public async Task GetAddressAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no store setup, so reading the tenant for an unauthorized caller would fail the test.
        var result = await Handler.GetAddressAsync(
            httpContext,
            "acme",
            issuer: null,
            mtlsEndpointAliasAuthority: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region Encryption Keys Tests

    private const string RootKeyRefusal =
        "The management API authenticates against the control-plane tenant, so a revoked root key would lock out the administrators who restore it. Its keys stay under the deployment's root key.";

    // Only a request that reaches the root key rule asks the manager whether the tenant can bring a root key.
    private void SetupCanBringRootKey() =>
        MockOwnerRootKeyManager
            .Setup(x => x.CanBringRootKey(TenantOwner, out It.Ref<string?>.IsAny))
            .Returns(true)
            .Verifiable();

    private void SetupRootKeyRefused()
    {
        string? reason = RootKeyRefusal;
        MockOwnerRootKeyManager
            .Setup(x => x.CanBringRootKey(TenantOwner, out reason))
            .Returns(false)
            .Verifiable();
    }

    private static MasterKeyInfo CreateKeyInfo(int version, bool retired) =>
        new(
            Guid.NewGuid(),
            SecretOwner.ForTenant(TenantId),
            version,
            DateTimeOffset.UnixEpoch,
            retired ? DateTimeOffset.UnixEpoch.AddDays(1) : null,
            "local",
            "root-key",
            "recovery-key"
        );

    [Fact]
    public async Task GetEncryptionKeysAsync_WhenFoundAndAuthorized_MapsTheStatusWithoutMaterial()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms");
        SetupAccessBinding();
        var active = CreateKeyInfo(2, retired: false);
        var retired = CreateKeyInfo(1, retired: true);
        MockMasterKeyManager
            .Setup(x =>
                x.GetStatusAsync(SecretOwner.ForTenant(TenantId), It.IsAny<CancellationToken>())
            )
            .Returns(
                new ValueTask<MasterKeyStatus>(
                    new MasterKeyStatus(
                        SecretOwner.ForTenant(TenantId),
                        [active, retired],
                        RecoveryConfigured: true
                    )
                )
            )
            .Verifiable();

        var result = await Handler.GetEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<EncryptionKeysResource>>(result);
        Assert.True(ok.Value?.RecoveryConfigured);
        Assert.Null(ok.Value?.RootKey);
        Assert.Equal(["aws-kms"], ok.Value?.RootKeyProviders);
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
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.GetEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetEncryptionKeysAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task RekeyEncryptionKeysAsync_RotatesRecordsOneAuditEventAndMapsTheRotation()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        var rotation = new MasterKeyRotation(
            SecretOwner.ForTenant(TenantId),
            CreateKeyInfo(2, retired: false),
            CreateKeyInfo(1, retired: true)
        );
        MockMasterKeyManager
            .Setup(x =>
                x.RotateAsync(SecretOwner.ForTenant(TenantId), It.IsAny<CancellationToken>())
            )
            .Returns(new ValueTask<MasterKeyRotation>(rotation))
            .Verifiable();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordMasterKeyRotatedAsync(
                    It.IsAny<HttpContext>(),
                    rotation,
                    MasterKeyChangeReasons.Requested
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);

        var result = await Handler.RekeyEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

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
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockMasterKeyManager
            .Setup(x =>
                x.RotateAsync(SecretOwner.ForTenant(TenantId), It.IsAny<CancellationToken>())
            )
            .Throws(new StoreConcurrencyException())
            .Verifiable();

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Handler.RekeyEncryptionKeysAsync(
                CreateHttpContext(authenticated: true),
                TenantId,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task RekeyEncryptionKeysAsync_WhenTheLastRotationIsTooRecent_ReturnsConflictWithRetryAfterAndRecordsNothing()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockMasterKeyManager
            .Setup(x =>
                x.RotateAsync(SecretOwner.ForTenant(TenantId), It.IsAny<CancellationToken>())
            )
            .Throws(
                new MasterKeyRotationTooSoonException(
                    SecretOwner.ForTenant(TenantId),
                    TimeSpan.FromSeconds(90.2)
                )
            )
            .Verifiable();
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RekeyEncryptionKeysAsync(
            httpContext,
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("91", httpContext.Response.Headers.RetryAfter.ToString());
        Assert.Equal(
            91L,
            problem.ProblemDetails.Extensions[ManagementProblemMembers.RetryAfterSeconds]
        );
    }

    [Fact]
    public async Task RekeyEncryptionKeysAsync_WhenNotFound_NeverRotates()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
        SetupUpdateAuthorization(AuthorizationResult.Success());

        var result = await Handler.RekeyEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task RekeyEncryptionKeysAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupUpdateAuthorization(AuthorizationResult.Failed());

        var result = await Handler.RekeyEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region Root Key Tests

    private static readonly SecretOwner TenantOwner = SecretOwner.ForTenant(TenantId);

    private const string RootKeyReference = "arn:aws:kms:us-west-2:111122223333:key/abcd";

    private static readonly OwnerRootKeyRequest RootKeyRequest = new("aws-kms", RootKeyReference);

    private void SetupAllowedKeyWrappers(params string[] names) =>
        MockOwnerRootKeyManager.SetupGet(x => x.AllowedKeyWrapperNames).Returns(names).Verifiable();

    private void SetupUpdateAuthorization(AuthorizationResult result)
    {
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.Is<IEnumerable<IAuthorizationRequirement>>(r =>
                        r.Contains(Operations.Update)
                    )
                )
            )
            .ReturnsAsync(result)
            .Verifiable();
    }

    private void SetupAuthorizedTenant(bool forRead = false)
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        if (forRead)
        {
            SetupAuthorization(AuthorizationResult.Success());
        }
        else
        {
            SetupUpdateAuthorization(AuthorizationResult.Success());
        }
    }

    private static OwnerRootKeyInfo CreateRootKeyInfo(
        string state = OwnerRootKeyStates.Active,
        string? failure = null
    ) =>
        new(
            TenantOwner,
            "aws-kms",
            RootKeyReference,
            state,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddHours(1),
            DateTimeOffset.UnixEpoch.AddHours(2),
            failure is null ? null : DateTimeOffset.UnixEpoch.AddHours(3),
            failure
        );

    private static EncryptionRootKeyRequest CreateRootKeyRequest(
        string provider = "aws-kms",
        string keyReference = RootKeyReference,
        string? accessIdentity = null
    ) =>
        new()
        {
            Provider = provider,
            KeyReference = keyReference,
            AccessIdentity = accessIdentity,
        };

    private void SetupAccessBinding(string binding = "tenant:tenant-id") =>
        MockOwnerRootKeyManager
            .Setup(x => x.GetAccessBinding(TenantOwner))
            .Returns(binding)
            .Verifiable();

    [Fact]
    public async Task GetEncryptionKeysAsync_WhenTheTenantMayBringAKey_ReportsTheAccessBinding()
    {
        SetupAuthorizedTenant(forRead: true);
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms");
        SetupAccessBinding("tenant:acme");
        MockMasterKeyManager
            .Setup(x => x.GetStatusAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<MasterKeyStatus>(
                    new MasterKeyStatus(TenantOwner, [], RecoveryConfigured: false)
                    {
                        RootKey = CreateRootKeyInfo() with
                        {
                            AccessIdentity = "arn:aws:iam::1:role/r",
                        },
                    }
                )
            )
            .Verifiable();

        var result = await Handler.GetEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<EncryptionKeysResource>>(result);
        Assert.Equal("tenant:acme", ok.Value?.RootKeyAccessBinding);
        Assert.Equal("arn:aws:iam::1:role/r", ok.Value?.RootKey?.AccessIdentity);
    }

    [Fact]
    public async Task SetRootKeyAsync_WithAnAccessIdentity_PassesItToTheManager()
    {
        const string accessIdentity = "arn:aws:iam::111122223333:role/tenant-kms";
        SetupAuthorizedTenant();
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms");
        MockOwnerRootKeyManager
            .Setup(x =>
                x.TryValidateKeyReference("aws-kms", RootKeyReference, out It.Ref<string?>.IsAny)
            )
            .Returns(true)
            .Verifiable();
        MockOwnerRootKeyManager
            .Setup(x =>
                x.TryValidateAccessIdentity("aws-kms", accessIdentity, out It.Ref<string?>.IsAny)
            )
            .Returns(true)
            .Verifiable();
        var rootKey = CreateRootKeyInfo() with { AccessIdentity = accessIdentity };
        MockOwnerRootKeyManager
            .Setup(x =>
                x.SetAsync(
                    TenantOwner,
                    new OwnerRootKeyRequest("aws-kms", RootKeyReference)
                    {
                        AccessIdentity = accessIdentity,
                    },
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<OwnerRootKeyChange>(
                    new OwnerRootKeyChange(
                        rootKey,
                        null,
                        null,
                        new MasterKeyRewrapResult(1, 0, 0, 0)
                    )
                )
            )
            .Verifiable();

        var result = await Handler.SetRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateRootKeyRequest(accessIdentity: accessIdentity),
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<EncryptionRootKeyChangeResource>>(result);
        Assert.Equal(accessIdentity, ok.Value?.RootKey.AccessIdentity);
    }

    [Fact]
    public async Task SetRootKeyAsync_WithAnInvalidAccessIdentity_ReturnsAValidationProblemNamingIt()
    {
        SetupAuthorizedTenant();
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms");
        MockOwnerRootKeyManager
            .Setup(x =>
                x.TryValidateKeyReference("aws-kms", RootKeyReference, out It.Ref<string?>.IsAny)
            )
            .Returns(true)
            .Verifiable();
        string? failure = "The access identity must be the ARN of an IAM role.";
        MockOwnerRootKeyManager
            .Setup(x => x.TryValidateAccessIdentity("aws-kms", "nope", out failure))
            .Returns(false)
            .Verifiable();

        var result = await Handler.SetRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateRootKeyRequest(accessIdentity: "nope"),
            CancellationToken.None
        );

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal([failure], problem.ProblemDetails.Errors["accessIdentity"]);
    }

    [Fact]
    public async Task SetRootKeyAsync_WithABlankAccessIdentity_ReturnsAValidationProblemNamingIt()
    {
        SetupAuthorizedTenant();
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms");
        MockOwnerRootKeyManager
            .Setup(x =>
                x.TryValidateKeyReference("aws-kms", RootKeyReference, out It.Ref<string?>.IsAny)
            )
            .Returns(true)
            .Verifiable();

        var result = await Handler.SetRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateRootKeyRequest(accessIdentity: " "),
            CancellationToken.None
        );

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Contains("accessIdentity", problem.ProblemDetails.Errors.Keys);
    }

    [Fact]
    public async Task GetEncryptionKeysAsync_WithARootKey_ReportsItWithoutMaterial()
    {
        SetupAuthorizedTenant(forRead: true);
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms", "azure-key-vault");
        SetupAccessBinding();
        MockMasterKeyManager
            .Setup(x => x.GetStatusAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<MasterKeyStatus>(
                    new MasterKeyStatus(TenantOwner, [], RecoveryConfigured: true)
                    {
                        RootKey = CreateRootKeyInfo(
                            OwnerRootKeyStates.Unavailable,
                            "AccessDenied: revoked"
                        ),
                    }
                )
            )
            .Verifiable();

        var result = await Handler.GetEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<EncryptionKeysResource>>(result);
        var rootKey = ok.Value?.RootKey;
        Assert.Equal("aws-kms", rootKey?.Provider);
        Assert.Equal(RootKeyReference, rootKey?.KeyReference);
        Assert.Equal(EncryptionRootKeyStates.Unavailable, rootKey?.State);
        Assert.Equal("AccessDenied: revoked", rootKey?.Failure);
        Assert.Equal(["aws-kms", "azure-key-vault"], ok.Value?.RootKeyProviders);
    }

    [Fact]
    public async Task SetRootKeyAsync_StoresTheKeyAndRecordsTheRootKeyAndTheRotation()
    {
        SetupAuthorizedTenant();
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms");
        MockOwnerRootKeyManager
            .Setup(x =>
                x.TryValidateKeyReference("aws-kms", RootKeyReference, out It.Ref<string?>.IsAny)
            )
            .Returns(true)
            .Verifiable();
        var rotation = new MasterKeyRotation(
            TenantOwner,
            CreateKeyInfo(2, retired: false),
            CreateKeyInfo(1, retired: true)
        );
        var rootKey = CreateRootKeyInfo();
        MockOwnerRootKeyManager
            .Setup(x => x.SetAsync(TenantOwner, RootKeyRequest, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<OwnerRootKeyChange>(
                    new OwnerRootKeyChange(
                        rootKey,
                        RootKeyChanges.Set,
                        rotation,
                        new MasterKeyRewrapResult(2, 1, 0, 1)
                    )
                )
            )
            .Verifiable();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordRootKeyChangedAsync(
                    It.IsAny<HttpContext>(),
                    rootKey,
                    RootKeyChanges.Set,
                    RootKeyChangeReasons.Requested
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordMasterKeyRotatedAsync(
                    It.IsAny<HttpContext>(),
                    rotation,
                    MasterKeyChangeReasons.RootKeyChanged
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);

        var result = await Handler.SetRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateRootKeyRequest(),
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<EncryptionRootKeyChangeResource>>(result);
        Assert.Equal(RootKeyReference, ok.Value?.RootKey.KeyReference);
        Assert.Equal(2, ok.Value?.ActiveKey?.Version);
        Assert.Equal(1, ok.Value?.RetiredKey?.Version);
        Assert.Equal(1, ok.Value?.RewrappedKeys);
        Assert.Equal(1, ok.Value?.FailedKeys);
    }

    [Fact]
    public async Task SetRootKeyAsync_SameKeyWithNothingToChange_RecordsNothing()
    {
        SetupAuthorizedTenant();
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms");
        MockOwnerRootKeyManager
            .Setup(x =>
                x.TryValidateKeyReference("aws-kms", RootKeyReference, out It.Ref<string?>.IsAny)
            )
            .Returns(true)
            .Verifiable();
        MockOwnerRootKeyManager
            .Setup(x => x.SetAsync(TenantOwner, RootKeyRequest, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<OwnerRootKeyChange>(
                    new OwnerRootKeyChange(
                        CreateRootKeyInfo(),
                        null,
                        null,
                        new MasterKeyRewrapResult(1, 0, 0, 0)
                    )
                )
            )
            .Verifiable();

        var result = await Handler.SetRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateRootKeyRequest(),
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<EncryptionRootKeyChangeResource>>(result);
        Assert.Null(ok.Value?.ActiveKey);
    }

    [Theory]
    [InlineData("", RootKeyReference, "provider", false)]
    [InlineData("local", RootKeyReference, "provider", true)]
    [InlineData("aws-kms", "", "keyReference", true)]
    public async Task SetRootKeyAsync_InvalidField_ReturnsAValidationProblemNamingIt(
        string provider,
        string keyReference,
        string field,
        bool readsTheAllowedNames
    )
    {
        SetupAuthorizedTenant();
        SetupCanBringRootKey();
        // A blank provider is refused before the allowed names are read.
        if (readsTheAllowedNames)
        {
            SetupAllowedKeyWrappers("aws-kms");
        }

        var result = await Handler.SetRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateRootKeyRequest(provider, keyReference),
            CancellationToken.None
        );

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Contains(field, problem.ProblemDetails.Errors.Keys);
    }

    [Fact]
    public async Task SetRootKeyAsync_MalformedReference_ReturnsAValidationProblemWithTheWrapperPhrase()
    {
        SetupAuthorizedTenant();
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms");
        string? failure = "The key reference must be the ARN of a KMS key or alias.";
        MockOwnerRootKeyManager
            .Setup(x => x.TryValidateKeyReference("aws-kms", "nope", out failure))
            .Returns(false)
            .Verifiable();

        var result = await Handler.SetRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateRootKeyRequest(keyReference: "nope"),
            CancellationToken.None
        );

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal([failure], problem.ProblemDetails.Errors["keyReference"]);
    }

    [Fact]
    public async Task SetRootKeyAsync_WhenTheProbeFails_ReturnsUnprocessableAndRecordsAnUnavailableKey()
    {
        SetupAuthorizedTenant();
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms");
        MockOwnerRootKeyManager
            .Setup(x =>
                x.TryValidateKeyReference("aws-kms", RootKeyReference, out It.Ref<string?>.IsAny)
            )
            .Returns(true)
            .Verifiable();
        var unavailable = CreateRootKeyInfo(OwnerRootKeyStates.Unavailable, "AccessDenied");
        MockOwnerRootKeyManager
            .Setup(x => x.SetAsync(TenantOwner, RootKeyRequest, It.IsAny<CancellationToken>()))
            .Throws(
                new OwnerRootKeyProbeException(
                    TenantOwner,
                    "aws-kms",
                    RootKeyReference,
                    "AccessDenied"
                )
                {
                    UnavailableRootKey = unavailable,
                }
            )
            .Verifiable();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordRootKeyChangedAsync(
                    It.IsAny<HttpContext>(),
                    unavailable,
                    RootKeyChanges.Unavailable,
                    RootKeyChangeReasons.ValidationFailed
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);

        var result = await Handler.SetRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateRootKeyRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
        Assert.Equal(
            "The key could not wrap and unwrap a probe through 'aws-kms': AccessDenied",
            problem.ProblemDetails.Detail
        );
    }

    [Fact]
    public async Task SetRootKeyAsync_WhenTheLastRotationIsTooRecent_ReturnsConflictWithRetryAfter()
    {
        SetupAuthorizedTenant();
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms");
        MockOwnerRootKeyManager
            .Setup(x =>
                x.TryValidateKeyReference("aws-kms", RootKeyReference, out It.Ref<string?>.IsAny)
            )
            .Returns(true)
            .Verifiable();
        MockOwnerRootKeyManager
            .Setup(x => x.SetAsync(TenantOwner, RootKeyRequest, It.IsAny<CancellationToken>()))
            .Throws(new MasterKeyRotationTooSoonException(TenantOwner, TimeSpan.FromSeconds(90.2)))
            .Verifiable();
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.SetRootKeyAsync(
            httpContext,
            TenantId,
            CreateRootKeyRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("91", httpContext.Response.Headers.RetryAfter.ToString());
        Assert.Equal(
            91L,
            problem.ProblemDetails.Extensions[ManagementProblemMembers.RetryAfterSeconds]
        );
    }

    [Fact]
    public async Task SetRootKeyAsync_WhenAnotherWriterWonTheRace_ThrowsStoreConcurrencyExceptionForTheFilter()
    {
        SetupAuthorizedTenant();
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms");
        MockOwnerRootKeyManager
            .Setup(x =>
                x.TryValidateKeyReference("aws-kms", RootKeyReference, out It.Ref<string?>.IsAny)
            )
            .Returns(true)
            .Verifiable();
        MockOwnerRootKeyManager
            .Setup(x => x.SetAsync(TenantOwner, RootKeyRequest, It.IsAny<CancellationToken>()))
            .Throws(new StoreConcurrencyException())
            .Verifiable();

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Handler.SetRootKeyAsync(
                CreateHttpContext(authenticated: true),
                TenantId,
                CreateRootKeyRequest(),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task SetRootKeyAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
        SetupUpdateAuthorization(AuthorizationResult.Success());

        var result = await Handler.SetRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateRootKeyRequest(),
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetEncryptionKeysAsync_ForARefusedTenant_ReportsNoRootKeyAndNoProviders()
    {
        SetupAuthorizedTenant(forRead: true);
        SetupRootKeyRefused();
        // The strict owner root key manager is never asked for its allowed providers.
        MockMasterKeyManager
            .Setup(x => x.GetStatusAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<MasterKeyStatus>(
                    new MasterKeyStatus(
                        TenantOwner,
                        [CreateKeyInfo(1, retired: false)],
                        RecoveryConfigured: true
                    )
                )
            )
            .Verifiable();

        var result = await Handler.GetEncryptionKeysAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<EncryptionKeysResource>>(result);
        Assert.Null(ok.Value?.RootKey);
        Assert.Empty(ok.Value?.RootKeyProviders ?? ["unexpected"]);
        Assert.Single(ok.Value?.Keys ?? []);
    }

    [Fact]
    public async Task SetRootKeyAsync_ForARefusedTenant_ReturnsBadRequestAndNeverSetsTheKey()
    {
        SetupAuthorizedTenant();
        SetupRootKeyRefused();

        // The strict owner root key manager has no other setups and the audit recorder none: any other call fails the test.
        var result = await Handler.SetRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateRootKeyRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("The tenant cannot bring a root key.", problem.ProblemDetails.Title);
        Assert.Equal(RootKeyRefusal, problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task SetRootKeyAsync_WhenAPolicyRefusesDuringTheSet_ReturnsBadRequestWithTheReason()
    {
        SetupAuthorizedTenant();
        SetupCanBringRootKey();
        SetupAllowedKeyWrappers("aws-kms");
        MockOwnerRootKeyManager
            .Setup(x =>
                x.TryValidateKeyReference("aws-kms", RootKeyReference, out It.Ref<string?>.IsAny)
            )
            .Returns(true)
            .Verifiable();
        MockOwnerRootKeyManager
            .Setup(x => x.SetAsync(TenantOwner, RootKeyRequest, It.IsAny<CancellationToken>()))
            .Throws(new OwnerRootKeyNotAllowedException(TenantOwner, RootKeyRefusal))
            .Verifiable();

        var result = await Handler.SetRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateRootKeyRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("The tenant cannot bring a root key.", problem.ProblemDetails.Title);
        Assert.Equal(RootKeyRefusal, problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task SetRootKeyAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupUpdateAuthorization(AuthorizationResult.Failed());

        var result = await Handler.SetRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CreateRootKeyRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task RemoveRootKeyAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupUpdateAuthorization(AuthorizationResult.Failed());

        // The strict owner root key manager and audit recorder have no setups: any call fails the test.
        var result = await Handler.RemoveRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task ValidateRootKeyAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupUpdateAuthorization(AuthorizationResult.Failed());

        // The strict owner root key manager and audit recorder have no setups: any call fails the test.
        var result = await Handler.ValidateRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task RemoveRootKeyAsync_WhenEveryKeyMovedBack_ReturnsNoContentAndRecordsTheRemoval()
    {
        SetupAuthorizedTenant();
        var before = CreateRootKeyInfo();
        MockOwnerRootKeyManager
            .Setup(x => x.GetAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<OwnerRootKeyInfo?>(before))
            .Verifiable();
        MockOwnerRootKeyManager
            .Setup(x => x.RemoveAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<OwnerRootKeyRemoval>(
                    new OwnerRootKeyRemoval(null, true, new MasterKeyRewrapResult(2, 2, 0, 0))
                )
            )
            .Verifiable();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordRootKeyChangedAsync(
                    It.IsAny<HttpContext>(),
                    It.Is<OwnerRootKeyInfo>(info => info.State == OwnerRootKeyStates.Removing),
                    RootKeyChanges.Removed,
                    RootKeyChangeReasons.Requested
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);

        var result = await Handler.RemoveRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task RemoveRootKeyAsync_WhenMasterKeysRemain_ReturnsAcceptedWithTheRemovingKey()
    {
        SetupAuthorizedTenant();
        MockOwnerRootKeyManager
            .Setup(x => x.GetAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<OwnerRootKeyInfo?>(CreateRootKeyInfo()))
            .Verifiable();
        MockOwnerRootKeyManager
            .Setup(x => x.RemoveAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<OwnerRootKeyRemoval>(
                    new OwnerRootKeyRemoval(
                        CreateRootKeyInfo(OwnerRootKeyStates.Removing),
                        true,
                        new MasterKeyRewrapResult(2, 1, 0, 1)
                    )
                )
            )
            .Verifiable();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordRootKeyChangedAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<OwnerRootKeyInfo>(),
                    RootKeyChanges.Removed,
                    RootKeyChangeReasons.Requested
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);

        var result = await Handler.RemoveRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var accepted = Assert.IsType<Accepted<EncryptionRootKeyResource>>(result);
        Assert.Equal(EncryptionRootKeyStates.Removing, accepted.Value?.State);
    }

    [Fact]
    public async Task RemoveRootKeyAsync_WithoutARootKey_ReturnsNoContentAndRecordsNothing()
    {
        SetupAuthorizedTenant();
        MockOwnerRootKeyManager
            .Setup(x => x.GetAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<OwnerRootKeyInfo?>((OwnerRootKeyInfo?)null))
            .Verifiable();
        MockOwnerRootKeyManager
            .Setup(x => x.RemoveAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<OwnerRootKeyRemoval>(
                    new OwnerRootKeyRemoval(null, false, new MasterKeyRewrapResult(0, 0, 0, 0))
                )
            )
            .Verifiable();

        var result = await Handler.RemoveRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task ValidateRootKeyAsync_WhenTheKeyFails_RecordsTheUnavailableChange()
    {
        SetupAuthorizedTenant();
        var unavailable = CreateRootKeyInfo(OwnerRootKeyStates.Unavailable, "AccessDenied");
        MockOwnerRootKeyManager
            .Setup(x => x.ValidateAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<OwnerRootKeyValidation>(
                    new OwnerRootKeyValidation(
                        unavailable,
                        RootKeyChanges.Unavailable,
                        null,
                        new MasterKeyRewrapResult(0, 0, 0, 0)
                    )
                )
            )
            .Verifiable();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordRootKeyChangedAsync(
                    It.IsAny<HttpContext>(),
                    unavailable,
                    RootKeyChanges.Unavailable,
                    RootKeyChangeReasons.ValidationFailed
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);

        var result = await Handler.ValidateRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<EncryptionRootKeyResource>>(result);
        Assert.Equal(EncryptionRootKeyStates.Unavailable, ok.Value?.State);
    }

    [Fact]
    public async Task ValidateRootKeyAsync_WhenTheKeyIsRestoredAndRotated_RecordsBothChanges()
    {
        SetupAuthorizedTenant();
        var restored = CreateRootKeyInfo();
        var rotation = new MasterKeyRotation(
            TenantOwner,
            CreateKeyInfo(2, retired: false),
            CreateKeyInfo(1, retired: true)
        );
        MockOwnerRootKeyManager
            .Setup(x => x.ValidateAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<OwnerRootKeyValidation>(
                    new OwnerRootKeyValidation(
                        restored,
                        RootKeyChanges.Restored,
                        rotation,
                        new MasterKeyRewrapResult(1, 1, 0, 0)
                    )
                )
            )
            .Verifiable();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordRootKeyChangedAsync(
                    It.IsAny<HttpContext>(),
                    restored,
                    RootKeyChanges.Restored,
                    RootKeyChangeReasons.ValidationSucceeded
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordMasterKeyRotatedAsync(
                    It.IsAny<HttpContext>(),
                    rotation,
                    MasterKeyChangeReasons.RootKeyChanged
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);

        var result = await Handler.ValidateRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        Assert.IsType<Ok<EncryptionRootKeyResource>>(result);
    }

    [Fact]
    public async Task ValidateRootKeyAsync_WithoutARootKey_ReturnsNotFound()
    {
        SetupAuthorizedTenant();
        MockOwnerRootKeyManager
            .Setup(x => x.ValidateAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<OwnerRootKeyValidation>(
                    new OwnerRootKeyValidation(
                        null,
                        null,
                        null,
                        new MasterKeyRewrapResult(0, 0, 0, 0)
                    )
                )
            )
            .Verifiable();

        var result = await Handler.ValidateRootKeyAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    #endregion

    #region Admins Tests

    [Fact]
    public async Task ListAdminsAsync_WhenAuthorized_ReturnsTheTenantAdminAssignments()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant("tenant-ct"));
        MockResourceOwnershipService
            .Setup(x =>
                x.GetAssignmentsAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Tenant,
                    TenantId,
                    BuiltInRoles.TenantAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateAssignment("principal-1", BuiltInRoles.TenantAdmin)])
            .Verifiable();

        var result = await Handler.ListAdminsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<OwnerResource>>>(result);
        var admin = Assert.Single(json.Value!.Items);
        Assert.Equal(
            ("principal-1", BuiltInRoles.TenantAdmin),
            (admin.PrincipalId, admin.RoleName)
        );
    }

    [Fact]
    public async Task ListAdminsAsync_WhenTheTenantDoesNotExist_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTenant(null);

        var result = await Handler.ListAdminsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task ListAdminsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.ListAdminsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task AddAdminAsync_WhenTheTenantDoesNotExist_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTenant(null);

        var result = await Handler.AddAdminAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            new AddOwnerRequest { PrincipalId = "principal-2" },
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task AddAdminAsync_WhenAuthorized_AssignsTheTenantAdminRoleAndReturnsCreated()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant("tenant-ct"));
        SetupSave();
        SetupRoleAssignmentChangedAudit();
        MockResourceOwnershipService
            .Setup(x =>
                x.AddAssignmentAsync(
                    MockStoreManager.Object,
                    TenantId,
                    ResourceNodeTypes.Tenant,
                    TenantId,
                    "principal-2",
                    BuiltInRoles.TenantAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RoleAssignmentGrant
                {
                    Assignment = CreateAssignment("principal-2", BuiltInRoles.TenantAdmin),
                    IsAdded = true,
                }
            )
            .Verifiable();

        var result = await Handler.AddAdminAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            new AddOwnerRequest { PrincipalId = "principal-2" },
            CancellationToken.None
        );

        var created = Assert.IsType<Created<OwnerResource>>(result);
        Assert.Equal($"/tenants/{TenantId}/admins/principal-2", created.Location);
    }

    [Fact]
    public async Task AddAdminAsync_RequestsCreateOnTheTenantNode()
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
        SetupTenant(CreateTenant("tenant-ct"));
        SetupSave();
        SetupRoleAssignmentChangedAudit();
        MockResourceOwnershipService
            .Setup(x =>
                x.AddAssignmentAsync(
                    MockStoreManager.Object,
                    TenantId,
                    ResourceNodeTypes.Tenant,
                    TenantId,
                    "principal-2",
                    BuiltInRoles.TenantAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RoleAssignmentGrant
                {
                    Assignment = CreateAssignment("principal-2", BuiltInRoles.TenantAdmin),
                    IsAdded = true,
                }
            )
            .Verifiable();

        await Handler.AddAdminAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            new AddOwnerRequest { PrincipalId = "principal-2" },
            CancellationToken.None
        );

        Assert.Same(Operations.Create, Assert.Single(requirements));
        var node = Assert.IsAssignableFrom<IResourceNode>(authorizedResource);
        Assert.Equal(
            (TenantId, ResourceNodeTypes.Tenant, TenantId),
            (node.TenantId, node.ResourceType, node.ResourceId)
        );
    }

    [Fact]
    public async Task AddAdminAsync_WhenAdded_RecordsACreatedRoleAssignmentAtTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant("tenant-ct"));
        SetupSave();
        RoleAssignmentChangedAuditEvent? recorded = null;
        SetupRoleAssignmentChangedAuditCapture(auditEvent => recorded = auditEvent);
        MockResourceOwnershipService
            .Setup(x =>
                x.AddAssignmentAsync(
                    MockStoreManager.Object,
                    TenantId,
                    ResourceNodeTypes.Tenant,
                    TenantId,
                    "principal-2",
                    BuiltInRoles.TenantAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RoleAssignmentGrant
                {
                    Assignment = CreateAssignment("principal-2", BuiltInRoles.TenantAdmin),
                    IsAdded = true,
                }
            )
            .Verifiable();

        await Handler.AddAdminAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            new AddOwnerRequest { PrincipalId = "principal-2" },
            CancellationToken.None
        );

        Assert.NotNull(recorded);
        Assert.Equal(ResourceChangeTypes.Created, recorded.ChangeType);
        Assert.Equal("principal-2", recorded.SubjectId);
        Assert.Equal("assignment-principal-2", recorded.ResourceId);
        Assert.Equal((ResourceNodeTypes.Tenant, TenantId), (recorded.NodeType, recorded.NodeId));
    }

    [Fact]
    public async Task AddAdminAsync_WhenAlreadyHeld_ReturnsOkAndRecordsNothing()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant("tenant-ct"));
        SetupSave();
        MockResourceOwnershipService
            .Setup(x =>
                x.AddAssignmentAsync(
                    MockStoreManager.Object,
                    TenantId,
                    ResourceNodeTypes.Tenant,
                    TenantId,
                    "principal-2",
                    BuiltInRoles.TenantAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RoleAssignmentGrant
                {
                    Assignment = CreateAssignment("principal-2", BuiltInRoles.TenantAdmin),
                    IsAdded = false,
                }
            )
            .Verifiable();

        var result = await Handler.AddAdminAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            new AddOwnerRequest { PrincipalId = "principal-2" },
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<OwnerResource>>(result);
        Assert.Equal("principal-2", ok.Value?.PrincipalId);
    }

    [Fact]
    public async Task AddAdminAsync_WhenTheCallerIsOnlyAnOwner_ReturnsForbidden()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.AddAdminAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            new AddOwnerRequest { PrincipalId = "principal-2" },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task AddAdminAsync_WhenForbiddenForAnUnknownTenant_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no store setup, so reading the unknown tenant would fail the test.
        var result = await Handler.AddAdminAsync(
            CreateHttpContext(authenticated: true),
            "unknown-tenant",
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
            TenantId,
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
        SetupRoleAssignmentChangedAudit();
        MockResourceOwnershipService
            .Setup(x =>
                x.RemoveAssignmentAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Tenant,
                    TenantId,
                    "principal-1",
                    BuiltInRoles.TenantAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateAssignment("principal-1", BuiltInRoles.TenantAdmin)])
            .Verifiable();

        var result = await Handler.RemoveAdminAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            "principal-1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task RemoveAdminAsync_RequestsCreateOnTheTenantNode()
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
        SetupRoleAssignmentChangedAudit();
        MockResourceOwnershipService
            .Setup(x =>
                x.RemoveAssignmentAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Tenant,
                    TenantId,
                    "principal-1",
                    BuiltInRoles.TenantAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateAssignment("principal-1", BuiltInRoles.TenantAdmin)])
            .Verifiable();

        await Handler.RemoveAdminAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            "principal-1",
            CancellationToken.None
        );

        Assert.Same(Operations.Create, Assert.Single(requirements));
        var node = Assert.IsAssignableFrom<IResourceNode>(authorizedResource);
        Assert.Equal(
            (TenantId, ResourceNodeTypes.Tenant, TenantId),
            (node.TenantId, node.ResourceType, node.ResourceId)
        );
    }

    [Fact]
    public async Task RemoveAdminAsync_WhenRemoved_RecordsADeletedRoleAssignmentForEachRemoved()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStoreManagerOnly();
        SetupSave();
        var recorded = new List<RoleAssignmentChangedAuditEvent>();
        SetupRoleAssignmentChangedAuditCapture(recorded.Add);
        MockResourceOwnershipService
            .Setup(x =>
                x.RemoveAssignmentAsync(
                    MockStoreManager.Object,
                    ResourceNodeTypes.Tenant,
                    TenantId,
                    "principal-1",
                    BuiltInRoles.TenantAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateAssignment("principal-1", BuiltInRoles.TenantAdmin)])
            .Verifiable();

        await Handler.RemoveAdminAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            "principal-1",
            CancellationToken.None
        );

        var auditEvent = Assert.Single(recorded);
        Assert.Equal(ResourceChangeTypes.Deleted, auditEvent.ChangeType);
        Assert.Equal("principal-1", auditEvent.SubjectId);
        Assert.Equal("assignment-principal-1", auditEvent.ResourceId);
        Assert.Equal(
            (ResourceNodeTypes.Tenant, TenantId),
            (auditEvent.NodeType, auditEvent.NodeId)
        );
    }

    [Fact]
    public async Task RemoveAdminAsync_WhenTheCallerIsOnlyAnOwner_ReturnsForbidden()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.RemoveAdminAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
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
                    ResourceNodeTypes.Tenant,
                    TenantId,
                    "principal-1",
                    BuiltInRoles.TenantAdmin,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([])
            .Verifiable();

        var result = await Handler.RemoveAdminAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            "principal-1",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    #endregion
}
