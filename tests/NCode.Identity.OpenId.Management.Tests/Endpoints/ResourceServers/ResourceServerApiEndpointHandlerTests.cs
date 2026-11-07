// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.ResourceServers;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Operations;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

public sealed class ResourceServerApiEndpointHandlerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ResourceServerId = "rs-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IResourceServerStore> MockStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<IResourceServerValidator> MockValidator { get; }
    private Mock<ISecretValidator> MockSecretValidator { get; }
    private Mock<ISecretGenerator> MockSecretGenerator { get; }
    private Mock<IClientKeyImporter> MockKeyImporter { get; }
    private Mock<ISecretPublicKeyConverter> MockSecretPublicKeyConverter { get; }
    private Mock<ISecretProtectorProvider> MockSecretProtectorProvider { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<ISettingScopeCollectionProvider> MockSettingScopeCollectionProvider { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<IResourceOwnershipService> MockResourceOwnershipService { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private ResourceServerApiEndpointHandler Handler { get; }

    public ResourceServerApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockStore = MockRepository.Create<IResourceServerStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockValidator = MockRepository.Create<IResourceServerValidator>();
        MockSecretValidator = MockRepository.Create<ISecretValidator>();
        MockSecretGenerator = MockRepository.Create<ISecretGenerator>();
        MockKeyImporter = MockRepository.Create<IClientKeyImporter>();
        MockSecretPublicKeyConverter = MockRepository.Create<ISecretPublicKeyConverter>();
        MockSecretProtectorProvider = MockRepository.Create<ISecretProtectorProvider>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockSettingScopeCollectionProvider =
            MockRepository.Create<ISettingScopeCollectionProvider>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockResourceOwnershipService = MockRepository.Create<IResourceOwnershipService>();

        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();

        Handler = new ResourceServerApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockValidator.Object,
            MockSecretValidator.Object,
            MockSecretGenerator.Object,
            MockKeyImporter.Object,
            MockSecretPublicKeyConverter.Object,
            MockSecretProtectorProvider.Object,
            TimeProvider.System,
            MockCryptoService.Object,
            MockSettingSerializer.Object,
            MockSettingScopeCollectionProvider.Object,
            MockEffectiveSettingsResolver.Object,
            MockResourceOwnershipService.Object,
            MockManagementAuditRecorder.Object,
            NullLogger<ResourceServerApiEndpointHandler>.Instance
        );
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
    private void SetupRoleAssignmentChangedAuditCapture(
        Action<RoleAssignmentChangedAuditEvent> capture
    ) =>
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordResourceChangeAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<RoleAssignmentChangedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (HttpContext _, ResourceChangeAuditEvent auditEvent, CancellationToken _) =>
                    capture((RoleAssignmentChangedAuditEvent)auditEvent)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private static PersistedRoleAssignment CreateCreatorAssignment() =>
        new()
        {
            TenantId = TenantId,
            AssignmentId = "assignment-principal-1",
            PrincipalId = "principal-1",
            RoleName = BuiltInRoles.Owner,
            ResourceType = ResourceNodeTypes.ResourceServer,
            ResourceId = "generated-rs-id",
            ConcurrencyToken = "assignment-ct",
        };

    // Only a request that changes a secret records it.
    private void SetupResourceServerSecretChangedAudit() =>
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordResourceServerSecretChangedAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    // Only a request that changes a scope records it.
    private void SetupResourceServerScopeChangedAudit() =>
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordResourceServerScopeChangedAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    // Only a request that changes the resource records it.
    private void SetupResourceServerChangedAudit() =>
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordResourceServerChangedAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    public void Dispose() => MockRepository.Verify();

    private void SetupStore()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IResourceServerStore>())
            .Returns(MockStore.Object)
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

    private void SetupValidSettings() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.ResourceServer,
                    It.IsAny<JsonSerializerOptions>()
                )
            )
            .Returns([])
            .Verifiable();

    private void SetupNoChangedLimits() => SetupChangedLimits([], new JsonObject());

    private void SetupChangedLimits(IReadOnlyList<string> limits, JsonObject proposed) =>
        MockSettingSerializer
            .Setup(x =>
                x.GetChangedLimits(
                    It.Is<JsonElement>(element =>
                        JsonElement.DeepEquals(
                            element,
                            JsonSerializer.SerializeToElement(new JsonObject())
                        )
                    ),
                    It.Is<JsonElement>(element =>
                        JsonElement.DeepEquals(element, JsonSerializer.SerializeToElement(proposed))
                    )
                )
            )
            .Returns(limits)
            .Verifiable();

    private void SetupInvalidSettings() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.ResourceServer,
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

    private static HttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
        };
        // The shared environment filter publishes the context in the pipeline; emulate it for direct handler calls.
        httpContext.Features.Set<IOpenIdContextFeature>(
            Mock.Of<IOpenIdContextFeature>(feature =>
                feature.OpenIdContext
                == Mock.Of<OpenIdContext>(context =>
                    context.Tenant == Mock.Of<OpenIdTenant>(tenant => tenant.TenantId == TenantId)
                    && context.Environment
                        == Mock.Of<OpenIdEnvironment>(environment =>
                            environment.JsonSerializerOptions == JsonSerializerOptions.Default
                        )
                )
            )
        );
        return httpContext;
    }

    private static PersistedResourceServer CreateResourceServer(
        bool isSystem = false,
        params PersistedScope[] scopes
    ) =>
        new()
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
            Identifier = "https://api.example.com",
            ConcurrencyToken = "rs-ct",
            Name = "Example API",
            IsSystem = isSystem,
            IsDisabled = false,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = TenantId,
                ResourceServerId = ResourceServerId,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Scopes = scopes,
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = TenantId,
                ResourceServerId = ResourceServerId,
                Value = [],
            },
        };

    [Fact]
    public async Task ListAsync_WhenAuthorized_ReturnsPage()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetPageAsync(It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PagedResult<PersistedResourceServer>
                {
                    Items = [CreateResourceServer()],
                    NextCursor = null,
                }
            )
            .Verifiable();

        var result = await Handler.ListAsync(
            CreateHttpContext(),
            TenantId,
            null,
            null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<ResourceServerResource>>>(
            result
        );
        Assert.Single(json.Value!.Items);
    }

    [Fact]
    public async Task CreateAsync_WhenAuthorized_ReturnsCreated()
    {
        SetupCreatorAssignment();
        SetupResourceServerChangedAudit();
        SetupValidSettings();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("generated-rs-id")
            .Verifiable();
        SetupStore();
        MockValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<PersistedResourceServer>(),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        MockStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedResourceServer>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var request = new CreateResourceServerRequest
        {
            Identifier = "https://api.example.com",
            Name = "Example API",
            IsDisabled = false,
            Settings = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
        };

        var result = await Handler.CreateAsync(
            CreateHttpContext(),
            TenantId,
            request,
            CancellationToken.None
        );

        var created = Assert.IsType<Created<ResourceServerResource>>(result);
        Assert.Equal("generated-rs-id", created.Value?.ResourceServerId);
    }

    [Fact]
    public async Task CreateAsync_WhenTheCreatorIsAssignedOwner_RecordsTheCreatorsAssignment()
    {
        SetupCreatorAssignment(CreateCreatorAssignment());
        SetupResourceServerChangedAudit();
        var recorded = new List<RoleAssignmentChangedAuditEvent>();
        SetupRoleAssignmentChangedAuditCapture(recorded.Add);

        await CreateResourceServerAsync();

        var auditEvent = Assert.Single(recorded);
        Assert.Equal(ResourceChangeTypes.Created, auditEvent.ChangeType);
        Assert.Equal(ResourceNodeTypes.ResourceServer, auditEvent.NodeType);
        Assert.Equal("generated-rs-id", auditEvent.NodeId);
        Assert.Equal("principal-1", auditEvent.SubjectId);
        Assert.Equal("assignment-principal-1", auditEvent.ResourceId);
    }

    [Fact]
    public async Task CreateAsync_WhenTheCallerHasNoPrincipal_RecordsNoRoleAssignment()
    {
        // Strict mocks: no RoleAssignmentChangedAuditEvent setup, so recording one would fail the test.
        SetupCreatorAssignment();
        SetupResourceServerChangedAudit();

        var result = await CreateResourceServerAsync();

        Assert.IsType<Created<ResourceServerResource>>(result);
    }

    private async Task<IResult> CreateResourceServerAsync()
    {
        SetupValidSettings();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("generated-rs-id")
            .Verifiable();
        SetupStore();
        MockValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<PersistedResourceServer>(),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        MockStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedResourceServer>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        return await Handler.CreateAsync(
            CreateHttpContext(),
            TenantId,
            new CreateResourceServerRequest
            {
                Identifier = "https://api.example.com",
                Name = "Example API",
                IsDisabled = false,
                Settings = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            CancellationToken.None
        );
    }

    [Fact]
    public async Task CreateAsync_WhenSettingsCannotBeHonored_ReturnsValidationProblem()
    {
        SetupInvalidSettings();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("generated-rs-id")
            .Verifiable();
        SetupStore();
        MockValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<PersistedResourceServer>(),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();

        var request = new CreateResourceServerRequest
        {
            Identifier = "https://api.example.com",
            Name = "Example API",
            IsDisabled = false,
            Settings = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
        };

        // Strict mocks: no AddAsync / SaveChangesAsync setup, so persisting the API would fail the test.
        var result = await Handler.CreateAsync(
            CreateHttpContext(),
            TenantId,
            request,
            CancellationToken.None
        );

        AssertSettingsValidationProblem(result);
    }

    [Fact]
    public async Task DeleteAsync_WhenSystem_ReturnsConflict()
    {
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer(isSystem: true))
            .Verifiable();
        MockValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<PersistedResourceServer>(),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ManagementError
                {
                    StatusCode = StatusCodes.Status409Conflict,
                    Detail = "A reserved system resource server cannot be deleted.",
                }
            )
            .Verifiable();

        var result = await Handler.DeleteAsync(
            CreateHttpContext(),
            ResourceServerId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task CreateScopeAsync_WhenAuthorized_ReturnsCreated()
    {
        SetupResourceServerScopeChangedAudit();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();
        MockStore
            .Setup(x =>
                x.AddScopeAsync(
                    ResourceServerId,
                    It.IsAny<PersistedScope>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.CreateScopeAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            new CreateScopeRequest { Value = "read:x", Description = "Read." },
            CancellationToken.None
        );

        var created = Assert.IsType<Created<ScopeResource>>(result);
        Assert.Equal("read:x", created.Value?.Value);
    }

    private void SetupResourceServerNodeAuthorization(AuthorizationResult result) =>
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).TenantId == TenantId
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.ResourceServer
                        && ((ResourceNode)resource).ResourceId == ResourceServerId
                    ),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    [Fact]
    public async Task ListScopesAsync_WhenForbidden_ReturnsForbiddenWithoutReading()
    {
        SetupResourceServerNodeAuthorization(AuthorizationResult.Failed());

        // No store factory is set up, so a strict-mock failure would mean the resource server was read first.
        var result = await Handler.ListScopesAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task ListScopesAsync_WhenAuthorized_ReturnsTheScopes()
    {
        SetupResourceServerNodeAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateResourceServer(
                    false,
                    new PersistedScope
                    {
                        Value = "read:x",
                        Description = "Read.",
                        IsSystem = false,
                    }
                )
            )
            .Verifiable();

        var result = await Handler.ListScopesAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<IReadOnlyList<ScopeResource>>>(result);
        var scope = Assert.Single(json.Value!);
        Assert.Equal("read:x", scope.Value);
    }

    [Fact]
    public async Task GetScopeAsync_WhenForbidden_ReturnsForbiddenWithoutReading()
    {
        SetupResourceServerNodeAuthorization(AuthorizationResult.Failed());

        // No store factory is set up, so a strict-mock failure would mean the resource server was read first.
        var result = await Handler.GetScopeAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            "read:x",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task GetScopeAsync_WhenAuthorizedAndTheScopeIsMissing_ReturnsNotFound()
    {
        SetupResourceServerNodeAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();

        var result = await Handler.GetScopeAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            "read:x",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetScopeAsync_WhenAuthorized_ReturnsTheScope()
    {
        SetupResourceServerNodeAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateResourceServer(
                    false,
                    new PersistedScope
                    {
                        Value = "read:x",
                        Description = "Read.",
                        IsSystem = false,
                    }
                )
            )
            .Verifiable();

        var result = await Handler.GetScopeAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            "READ:X",
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<ScopeResource>>(result);
        Assert.Equal("read:x", json.Value?.Value);
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_ExpandsTheTenantProfilesOntoTheServerBaseline()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();
        var mockTenantStore = MockRepository.Create<ITenantStore>();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(mockTenantStore.Object)
            .Verifiable();
        mockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PersistedTenant
                {
                    TenantId = TenantId,
                    TeamId = "platform",
                    ConcurrencyToken = "tenant-ct",
                    Issuer = null,
                    IsDisabled = false,
                    DisplayName = TenantId,
                    Settings = new PersistedTenantSettings
                    {
                        TenantId = TenantId,
                        ConcurrencyToken = "tenant-ct",
                        Value = JsonSerializer.SerializeToElement(
                            new Dictionary<string, object> { ["scope"] = "tenant" }
                        ),
                    },
                    Secrets = null!,
                }
            )
            .Verifiable();
        var descriptor = new SettingDescriptor<bool>
        {
            Name = "access_token_encryption_required",
            AppliesTo = ["client", "resource_server"],
        };
        var clientOnlyDescriptor = new SettingDescriptor<bool>
        {
            Name = "require_pkce",
            AppliesTo = ["client"],
        };
        IReadOnlyCollection<Setting> effectiveScopeSettings =
        [
            descriptor.Create(true),
            clientOnlyDescriptor.Create(true),
        ];
        SetupEffectiveSettingsResolver(effectiveScopeSettings);
        SetupSettingScopes(
            OpenIdSettingScopes.ResourceServer,
            (descriptor, true),
            (clientOnlyDescriptor, false)
        );

        var result = await Handler.GetEffectiveSettingsAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<ResourceServerEffectiveSettingsResource>>(result);
        Assert.True(
            json.Value!.Settings.GetProperty("access_token_encryption_required").GetBoolean()
        );

        // A client setting does not govern a resource server, so its preview omits it (ADR-1149).
        Assert.False(json.Value.Settings.TryGetProperty("require_pkce", out _));
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenForbidden_ReturnsForbidWithoutReadingTheResourceServer()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetEffectiveSettingsAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        // No store factory, tenant store or resolver is set up, so a strict-mock failure would mean the resource server
        // was read before authorization.
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
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

    private void SetupEffectiveSettingsResolver(IReadOnlyCollection<Setting> effectiveScopeSettings)
    {
        var effectiveSettings = new Mock<ISettingCollection>(MockBehavior.Loose);
        effectiveSettings
            .Setup(x => x.GetEnumerator())
            .Returns(() => effectiveScopeSettings.GetEnumerator());

        var teamSettings = MockRepository.Create<IReadOnlySettingCollection>().Object;
        var tenantSettings = MockRepository.Create<IReadOnlySettingCollection>().Object;

        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamSettings)
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
            .Returns(tenantSettings)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    tenantSettings,
                    OpenIdSettingScopes.ResourceServer,
                    ResourceServerId,
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(effectiveSettings.Object)
            .Verifiable();
    }

    private static PersistedSecret CreatePersistedSecret() =>
        new()
        {
            SecretId = "secret-1",
            ConcurrencyToken = "secret-ct",
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UtcNow,
            ExpiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            ActivatesWhen = DateTimeOffset.UtcNow,
            RetiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            SecretType = "rsa",
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.Pkcs8,
            Origin = SecretOrigins.Generated,
            KeySizeBits = 2048,
            EncodedValue = "encoded",
        };

    [Fact]
    public async Task GetSettingsAsync_WhenAuthorized_ReturnsSettings()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();

        var result = await Handler.GetSettingsAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<ResourceServerSettingsResource>>(result);
        Assert.Equal(ResourceServerId, json.Value?.ResourceServerId);
    }

    #region GetSettingLimitsAsync Tests

    private void SetupResourceServerLimits(params string[] limits) =>
        MockSettingSerializer
            .Setup(x => x.GetLimits(OpenIdSettingScopes.ResourceServer))
            .Returns(limits)
            .Verifiable();

    [Fact]
    public async Task GetSettingLimitsAsync_WhenTheCallerMayUpdateTheTenant_ReportsCanChange()
    {
        SetupResourceServerLimits(OpenIdSettingNames.RateLimitClientPerMinute);
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();

        var httpContext = CreateHttpContext();

        var result = await Handler.GetSettingLimitsAsync(
            httpContext,
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<SettingLimitsResource>>(result);
        Assert.Equal([OpenIdSettingNames.RateLimitClientPerMinute], json.Value?.Names);
        Assert.True(json.Value?.CanChange);
        Assert.False(httpContext.Response.Headers.ContainsKey("ETag"));
    }

    [Fact]
    public async Task GetSettingLimitsAsync_WhenTheCallerMayNotUpdateTheTenant_ReportsCannotChange()
    {
        SetupResourceServerLimits(OpenIdSettingNames.RateLimitClientPerMinute);
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).TenantId == TenantId
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.Tenant
                        && ((ResourceNode)resource).ResourceId == TenantId
                    ),
                    It.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                        requirements.Single() == Operations.Update
                    )
                )
            )
            .ReturnsAsync(AuthorizationResult.Failed())
            .Verifiable();

        var result = await Handler.GetSettingLimitsAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<SettingLimitsResource>>(result);
        Assert.Equal([OpenIdSettingNames.RateLimitClientPerMinute], json.Value?.Names);
        Assert.False(json.Value?.CanChange);
    }

    [Fact]
    public async Task GetSettingLimitsAsync_WhenTheResourceServerIsMissing_ReturnsNotFound()
    {
        SetupResourceServerLimits(OpenIdSettingNames.RateLimitClientPerMinute);
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedResourceServer?)null)
            .Verifiable();

        var result = await Handler.GetSettingLimitsAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    #endregion

    [Fact]
    public async Task UpdateSettingsAsync_WhenAuthorized_ReturnsNoContent()
    {
        SetupResourceServerChangedAudit();
        SetupValidSettings();
        SetupNoChangedLimits();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();
        MockStore
            .Setup(x =>
                x.UpdateSettingsAsync(
                    It.IsAny<PersistedResourceServerSettings>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(),
            ResourceServerId,
            new JsonPatchDocument<JsonObject>(),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenSettingsCannotBeHonored_ReturnsValidationProblem()
    {
        SetupInvalidSettings();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();

        // Strict mocks: no UpdateSettingsAsync / SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(),
            ResourceServerId,
            new JsonPatchDocument<JsonObject>(),
            ifMatch: null,
            CancellationToken.None
        );

        AssertSettingsValidationProblem(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenALimitChangesAndTheTenantRefuses_ReturnsForbiddenWithoutWriting()
    {
        SetupValidSettings();
        SetupChangedLimits(["some_limit"], new JsonObject { ["some_limit"] = "10" });
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.Tenant
                    ),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Failed())
            .Verifiable();

        // Strict mocks: no store-write setup, so continuing past the refusal would fail the test.
        var patch = new JsonPatchDocument<JsonObject>();
        patch.Operations.Add(
            new Operation<JsonObject>("add", "/some_limit", from: null, value: "10")
        );
        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(),
            ResourceServerId,
            patch,
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Contains("'some_limit'", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task GetSecretsAsync_WhenAuthorized_ReturnsSecrets()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetSecretsOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PersistedResourceServerSecrets
                {
                    TenantId = TenantId,
                    ResourceServerId = ResourceServerId,
                    ConcurrencyToken = "sec-ct",
                    Value = [CreatePersistedSecret()],
                }
            )
            .Verifiable();

        var result = await Handler.GetSecretsAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<ResourceServerSecretsResource>>(result);
        Assert.Single(json.Value?.Secrets ?? []);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenAuthorized_ReturnsCreatedWithMaterial()
    {
        SetupResourceServerSecretChangedAudit();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();
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
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("secret-1")
            .Verifiable();

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
        var generated = CreatePersistedSecret();
        MockSecretGenerator
            .Setup(x => x.GenerateSecret(protector, It.IsAny<GenerateSecretRequest>()))
            .Returns(new GeneratedSecret { Secret = generated, SecretMaterial = "revealed" })
            .Verifiable();
        MockStore
            .Setup(x =>
                x.AddSecretAsync(ResourceServerId, generated, It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.CreateSecretAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            new CreateSecretRequest
            {
                SecretType = "rsa",
                KeySizeBits = 2048,
                Use = null,
                Algorithm = null,
                ExpiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            },
            CancellationToken.None
        );

        var created = Assert.IsType<Created<CreatedSecretResource>>(result);
        Assert.Equal("secret-1", created.Value?.SecretId);
        Assert.Equal("revealed", created.Value?.SecretMaterial);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenTheRequestNamesALifecycle_GeneratesTheKeyWithIt()
    {
        SetupResourceServerSecretChangedAudit();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();
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
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("secret-1")
            .Verifiable();
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
        var generated = CreatePersistedSecret();
        GenerateSecretRequest? generateRequest = null;
        MockSecretGenerator
            .Setup(x => x.GenerateSecret(protector, It.IsAny<GenerateSecretRequest>()))
            .Callback(
                (ISecretProtector _, GenerateSecretRequest request) => generateRequest = request
            )
            .Returns(new GeneratedSecret { Secret = generated, SecretMaterial = null })
            .Verifiable();
        MockStore
            .Setup(x =>
                x.AddSecretAsync(ResourceServerId, generated, It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.CreateSecretAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            new CreateSecretRequest
            {
                SecretType = "rsa",
                KeySizeBits = 2048,
                Use = "sig",
                Algorithm = "RS256",
                ActivatesWhen = DateTimeOffset.UnixEpoch.AddDays(2),
                RetiresWhen = DateTimeOffset.UnixEpoch.AddDays(92),
                ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
            },
            CancellationToken.None
        );

        Assert.IsType<Created<CreatedSecretResource>>(result);
        Assert.NotNull(generateRequest);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(2), generateRequest.ActivatesWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(92), generateRequest.RetiresWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(99), generateRequest.ExpiresWhen);
    }

    [Fact]
    public async Task ImportSecretAsync_WhenTheRequestNamesALifecycle_ImportsTheKeyWithIt()
    {
        SetupResourceServerSecretChangedAudit();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();
        MockSecretValidator
            .Setup(x =>
                x.ValidateImport(It.IsAny<ImportSecretRequest>(), It.IsAny<DateTimeOffset>())
            )
            .Returns((ManagementError?)null)
            .Verifiable();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("secret-2")
            .Verifiable();
        var imported = CreatePersistedSecret();
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
        MockStore
            .Setup(x => x.AddSecretAsync(ResourceServerId, imported, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.ImportSecretAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            new ImportSecretRequest
            {
                SecretType = "rsa",
                Format = "jwk",
                KeyMaterial = """{"kty":"RSA","n":"AQAB","e":"AQAB"}""",
                Use = "sig",
                Algorithm = "RS256",
                ActivatesWhen = DateTimeOffset.UnixEpoch.AddDays(2),
                RetiresWhen = DateTimeOffset.UnixEpoch.AddDays(92),
                ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
            },
            CancellationToken.None
        );

        Assert.IsType<Created<SecretResource>>(result);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenTheRequestOmitsTheInstants_KeepsTheStoredOnesBoundedByTheExpiry()
    {
        // A retired key stays retired when only its expiry is extended; the retirement follows a shortened expiry.
        SetupResourceServerSecretChangedAudit();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        var stored = CreatePersistedSecret() with
        {
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ActivatesWhen = DateTimeOffset.UnixEpoch.AddDays(2),
            RetiresWhen = DateTimeOffset.UnixEpoch.AddDays(92),
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
            ManagedBy = "rotation",
        };
        MockStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(
                    ResourceServerId,
                    "secret-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PersistedResourceServerSecret
                {
                    TenantId = TenantId,
                    ResourceServerId = ResourceServerId,
                    Value = stored,
                }
            )
            .Verifiable();
        MockSecretValidator
            .Setup(x =>
                x.ValidateUpdate(It.IsAny<PersistedSecret>(), It.IsAny<UpdateSecretRequest>())
            )
            .Returns((ManagementError?)null)
            .Verifiable();
        PersistedSecret? updated = null;
        MockStore
            .Setup(x =>
                x.UpdateSecretAsync(
                    ResourceServerId,
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

        var result = await Handler.UpdateSecretAsync(
            CreateHttpContext(),
            ResourceServerId,
            "secret-1",
            new UpdateSecretRequest
            {
                Use = "enc",
                Algorithm = "RSA-OAEP",
                ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(50),
            },
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.NotNull(updated);
        Assert.Equal("enc", updated.Use);
        Assert.Equal("RSA-OAEP", updated.Algorithm);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(2), updated.ActivatesWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(50), updated.RetiresWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(50), updated.ExpiresWhen);
        Assert.Equal("rotation", updated.ManagedBy);
        Assert.Equal(stored.EncodedValue, updated.EncodedValue);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenTheResourceServerIsASystemResourceServer_Returns409WithoutGenerating()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer(isSystem: true))
            .Verifiable();

        var result = await Handler.CreateSecretAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            new CreateSecretRequest
            {
                SecretType = "rsa",
                KeySizeBits = 2048,
                Use = null,
                Algorithm = null,
                ExpiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task ImportSecretAsync_WhenTheResourceServerIsASystemResourceServer_Returns409WithoutImporting()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer(isSystem: true))
            .Verifiable();

        var result = await Handler.ImportSecretAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            new ImportSecretRequest
            {
                SecretType = "rsa",
                Format = "jwk",
                KeyMaterial = "{}",
                Use = null,
                Algorithm = null,
                ExpiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteSecretAsync_WhenAuthorized_ReturnsNoContent()
    {
        SetupResourceServerSecretChangedAudit();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(
                    ResourceServerId,
                    "secret-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PersistedResourceServerSecret
                {
                    TenantId = TenantId,
                    ResourceServerId = ResourceServerId,
                    Value = CreatePersistedSecret(),
                }
            )
            .Verifiable();
        MockStore
            .Setup(x =>
                x.RemoveSecretAsync(ResourceServerId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.DeleteSecretAsync(
            CreateHttpContext(),
            ResourceServerId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    private static PersistedResourceServerSecret CreateResourceServerSecret() =>
        new()
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
            Value = CreatePersistedSecret(),
        };

    private static PublicKeyResource CreatePublicKeyResource() =>
        new()
        {
            SecretId = "secret-1",
            SecretType = SecretTypes.Rsa,
            Jwk = JsonSerializer.SerializeToElement(new JsonObject()),
            Pem = "-----BEGIN PUBLIC KEY-----\nMII=\n-----END PUBLIC KEY-----",
        };

    [Fact]
    public async Task GetPublicKeyAsync_WhenAsymmetricAndAuthorized_ReturnsJson()
    {
        SetupStore();
        MockStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(
                    ResourceServerId,
                    "secret-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateResourceServerSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockSecretPublicKeyConverter
            .Setup(x => x.Convert(It.IsAny<PersistedSecret>()))
            .Returns(CreatePublicKeyResource())
            .Verifiable();

        var result = await Handler.GetPublicKeyAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
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
        MockStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(
                    ResourceServerId,
                    "secret-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateResourceServerSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockSecretPublicKeyConverter
            .Setup(x => x.Convert(It.IsAny<PersistedSecret>()))
            .Returns((PublicKeyResource?)null)
            .Verifiable();

        var result = await Handler.GetPublicKeyAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetPublicKeyAsync_WhenNotFound_ReturnsNotFoundWithoutProtector()
    {
        SetupStore();
        MockStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(
                    ResourceServerId,
                    "missing",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PersistedResourceServerSecret?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        // Strict mocks: no GetProtectorAsync setup, so asking for a protector would fail the test.
        var result = await Handler.GetPublicKeyAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            "missing",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetPublicKeyAsync_WhenForbidden_ReturnsForbidWithoutReadingTheSecret()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetPublicKeyAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            "secret-1",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }
}
