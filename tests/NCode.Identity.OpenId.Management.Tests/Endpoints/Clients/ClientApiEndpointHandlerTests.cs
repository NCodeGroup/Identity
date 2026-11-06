// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Clients;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Clients;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Operations;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

public sealed class ClientApiEndpointHandlerTests : IAsyncLifetime
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private Mock<IRoleAssignmentStore> MockRoleAssignmentStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ISecretGenerator> MockSecretGenerator { get; }
    private Mock<IClientKeyImporter> MockClientKeyImporter { get; }
    private Mock<ISecretPublicKeyConverter> MockSecretPublicKeyConverter { get; }
    private Mock<ISecretProtectorProvider> MockSecretProtectorProvider { get; }
    private Mock<IClientValidator> MockClientValidator { get; }
    private Mock<ISecretValidator> MockSecretValidator { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<ISettingScopeCollectionProvider> MockSettingScopeCollectionProvider { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IClientCoherenceValidator> MockClientCoherenceValidator { get; }
    private Mock<IClientCoherenceReporter> MockClientCoherenceReporter { get; }
    private Mock<IClientSecretIssuer> MockClientSecretIssuer { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IResourceOwnershipService> MockResourceOwnershipService { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private Mock<IHostManagedClientPolicy> MockHostManagedClientPolicy { get; }
    private ServiceProvider ServiceProvider { get; }
    private ClientApiEndpointHandler Handler { get; }

    public ClientApiEndpointHandlerTests()
    {
        // The real key-type catalog and algorithms are the sources of truth the key rules read.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        var secrets = serviceCollection.AddSecretsLibrary();
        secrets.AddPersistenceLibrary();
        secrets.AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockClientStore = MockRepository.Create<IClientStore>();
        MockRoleAssignmentStore = MockRepository.Create<IRoleAssignmentStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockSecretGenerator = MockRepository.Create<ISecretGenerator>();
        MockClientKeyImporter = MockRepository.Create<IClientKeyImporter>();
        MockSecretPublicKeyConverter = MockRepository.Create<ISecretPublicKeyConverter>();
        MockSecretProtectorProvider = MockRepository.Create<ISecretProtectorProvider>();
        MockClientValidator = MockRepository.Create<IClientValidator>();
        MockSecretValidator = MockRepository.Create<ISecretValidator>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockSettingScopeCollectionProvider =
            MockRepository.Create<ISettingScopeCollectionProvider>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockClientCoherenceValidator = MockRepository.Create<IClientCoherenceValidator>();
        MockClientCoherenceReporter = MockRepository.Create<IClientCoherenceReporter>();
        MockClientSecretIssuer = MockRepository.Create<IClientSecretIssuer>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockResourceOwnershipService = MockRepository.Create<IResourceOwnershipService>();

        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();
        MockHostManagedClientPolicy = MockRepository.Create<IHostManagedClientPolicy>();

        Handler = CreateHandler();
    }

    // The default handler has no host-managed client policy, so no client is host-managed.
    private ClientApiEndpointHandler CreateHandler(
        params IHostManagedClientPolicy[] hostManagedClientPolicies
    ) =>
        new(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockClientValidator.Object,
            MockSecretValidator.Object,
            MockSecretGenerator.Object,
            MockClientKeyImporter.Object,
            MockSecretPublicKeyConverter.Object,
            MockSecretProtectorProvider.Object,
            TimeProvider.System,
            MockCryptoService.Object,
            MockSettingSerializer.Object,
            MockSettingScopeCollectionProvider.Object,
            MockEffectiveSettingsResolver.Object,
            MockClientCoherenceValidator.Object,
            MockClientCoherenceReporter.Object,
            MockClientSecretIssuer.Object,
            MockResourceOwnershipService.Object,
            MockManagementAuditRecorder.Object,
            new DefaultHostManagedClientChecker(hostManagedClientPolicies),
            new DefaultClientKeySourceChecker(
                ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>()
            ),
            NullLogger<ClientApiEndpointHandler>.Instance
        );

    // Only a request that updates a secret validates the update.
    private void SetupSecretUpdateValidation()
    {
        // Update metadata is checked by the real validator against the real key-type catalog and algorithms.
        var secretValidator = new DefaultSecretValidator(
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
            ServiceProvider.GetRequiredService<ISecretSerializer>()
        );
        MockSecretValidator
            .Setup(x =>
                x.ValidateUpdate(It.IsAny<PersistedSecret>(), It.IsAny<UpdateSecretRequest>())
            )
            .Returns(
                (PersistedSecret secret, UpdateSecretRequest request) =>
                    secretValidator.ValidateUpdate(secret, request)
            )
            .Verifiable();
    }

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
    private void SetupCreatorAssignment() =>
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
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    // Only a request that changes a secret records it.
    private void SetupClientSecretChangedAudit() =>
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordClientSecretChangedAsync(
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
    private void SetupClientChangedAudit() =>
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordClientChangedAsync(
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

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        MockRepository.Verify();
        await ServiceProvider.DisposeAsync();
    }

    #region Helpers

    // The caller's authority, which the validator asserts from the route and the request before any row is read.
    private void SetupAuthorizeCreate(string? resourceServerId, ManagementError? error) =>
        MockClientValidator
            .Setup(x =>
                x.AuthorizeCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    TenantId,
                    resourceServerId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    private void SetupAuthorizeUpdate(ManagementError? error) =>
        MockClientValidator
            .Setup(x =>
                x.AuthorizeUpdateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    TenantId,
                    ClientId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    private void SetupAuthorizeDelete(ManagementError? error) =>
        MockClientValidator
            .Setup(x =>
                x.AuthorizeDeleteAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    TenantId,
                    ClientId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();

    private static ManagementError Forbidden() =>
        new() { StatusCode = StatusCodes.Status403Forbidden, Detail = "forbidden" };

    private void SetupStore()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();

        MockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(MockClientStore.Object)
            .Verifiable();

        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    // Only a request that changes a client's credentials, settings or owners looks up the roles the client holds.
    private void SetupClientRoleAssignments(params PersistedRoleAssignment[] assignments)
    {
        MockStoreManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(MockRoleAssignmentStore.Object)
            .Verifiable();

        MockRoleAssignmentStore
            .Setup(x => x.GetByPrincipalAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignments)
            .Verifiable();
    }

    // Only a request that changes one of a client's secrets, which the store reads by secret, reads the client too.
    private void SetupClientLookup() =>
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();

    // The authorization of the client itself, which the caller's own authority over the client decides.
    private void SetupClientAuthorization(AuthorizationResult result) =>
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource => resource is PersistedClient),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    // The authorization of the client's node, which the caller's own authority to update the client decides.
    private void SetupClientNodeAuthorization(AuthorizationResult result) =>
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).TenantId == TenantId
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.Client
                        && ((ResourceNode)resource).ResourceId == ClientId
                    ),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    // The authorization of a node the client's principal holds a role at, which the caller's authority to grant decides.
    private void SetupRoleNodeAuthorization(AuthorizationResult result) =>
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.Team
                        && ((ResourceNode)resource).ResourceId == "team-1"
                    ),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    private static PersistedRoleAssignment CreateTeamAssignment() =>
        new()
        {
            TenantId = TenantId,
            AssignmentId = "assignment-1",
            PrincipalId = "client-principal",
            RoleName = "TeamAdmin",
            ResourceType = ResourceNodeTypes.Team,
            ResourceId = "team-1",
            ConcurrencyToken = "assignment-ct",
        };

    // The host configures the client, so a handler built with the policy refuses every change to it.
    private ClientApiEndpointHandler CreateHostManagedHandler()
    {
        MockHostManagedClientPolicy
            .Setup(x => x.IsHostManaged(TenantId, ClientId))
            .Returns(true)
            .Verifiable();
        return CreateHandler(MockHostManagedClientPolicy.Object);
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

    private void SetupValidSettings() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Client,
                    It.IsAny<JsonSerializerOptions>()
                )
            )
            .Returns([])
            .Verifiable();

    private void SetupNoChangedLimits(string name, string value) =>
        SetupChangedLimits([], name, value);

    private void SetupChangedLimits(IReadOnlyList<string> limits, string name, string value)
    {
        var proposed = JsonSerializer.SerializeToElement(new JsonObject { [name] = value });
        MockSettingSerializer
            .Setup(x =>
                x.GetChangedLimits(
                    It.Is<JsonElement>(element => JsonElement.DeepEquals(element, EmptyObject())),
                    It.Is<JsonElement>(element => JsonElement.DeepEquals(element, proposed))
                )
            )
            .Returns(limits)
            .Verifiable();
    }

    private void SetupInvalidSettings() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Client,
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

    private static HttpContext CreateHttpContext(bool authenticated)
    {
        var identity = authenticated
            ? new ClaimsIdentity(authenticationType: "test")
            : new ClaimsIdentity();
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
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

    private static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new JsonObject());

    private static PersistedTenantSettings CreateTenantSettings() =>
        new()
        {
            TenantId = TenantId,
            ConcurrencyToken = "tenant-settings-ct",
            Value = EmptyObject(),
        };

    private static PersistedTenant CreateTenant() =>
        new()
        {
            TenantId = TenantId,
            TeamId = "platform",
            ConcurrencyToken = "tenant-ct",
            Issuer = null,
            IsDisabled = false,
            DisplayName = TenantId,
            Settings = CreateTenantSettings(),
            Secrets = null!,
        };

    private static PersistedClientSecrets CreateSecretsCollection(
        string concurrencyToken,
        params PersistedSecret[] secrets
    ) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = concurrencyToken,
            Value = secrets,
        };

    private static PersistedClientSecret CreateClientSecret(string secretId = "secret-1") =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            Value = CreatePersistedSecret(secretId),
        };

    private static PersistedClient CreateClient(string concurrencyToken) =>
        CreateClient(concurrencyToken, EmptyObject());

    private static PersistedClient CreateClient(
        string concurrencyToken,
        JsonElement settings,
        params PersistedSecret[] secrets
    ) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = concurrencyToken,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = "settings-ct",
                Value = settings,
            },
            Secrets = CreateSecretsCollection("secrets-ct", secrets),
        };

    private static JsonElement JwksUriSettings() =>
        JsonSerializer.SerializeToElement(
            new JsonObject { [OpenIdSettingNames.JwksUri] = "https://client.example/jwks" }
        );

    // A real public key, because an update reads the stored key to check its algorithm against it.
    private static string RsaSubjectPublicKeyInfo { get; } = CreateRsaSubjectPublicKeyInfo();

    private static string CreateRsaSubjectPublicKeyInfo()
    {
        using var rsa = RSA.Create(2048);
        return Base64Url.EncodeToString(rsa.ExportSubjectPublicKeyInfo());
    }

    private static PersistedSecret CreatePublicKeySecret(string secretId, string? keyId) =>
        new()
        {
            SecretId = secretId,
            KeyId = keyId,
            ConcurrencyToken = "secret-ct",
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            ActivatesWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretTypes.Rsa,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.Spki,
            Origin = SecretOrigins.Generated,
            KeySizeBits = 2048,
            EncodedValue = RsaSubjectPublicKeyInfo,
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

    private static CreateSecretRequest CreateAsymmetricSecretRequest() =>
        new()
        {
            SecretType = SecretTypes.Rsa,
            KeySizeBits = 2048,
            Use = "sig",
            Algorithm = "RS256",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    private static UpdateSecretRequest UpdateSecretRequest() =>
        new()
        {
            Use = "sig",
            Algorithm = "HS256",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(2),
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

    #region ToClientResource Tests

    [Fact]
    public void ToClientResource_MapsTheClientsPrincipalId()
    {
        var client = CreateClient("ct-1");
        client.PrincipalId = "principal-1";

        var resource = Handler.ToClientResource(client);

        Assert.Equal("principal-1", resource.PrincipalId);
        Assert.Equal(ClientId, resource.ClientId);
    }

    #endregion

    #region CreateClientAsync Tests

    [Fact]
    public async Task CreateClientAsync_UsesAmbientTenant()
    {
        SetupAuthorizeCreate(resourceServerId: null, error: null);
        SetupCreatorAssignment();
        SetupClientChangedAudit();
        SetupValidSettings();
        SetupStore();
        SetupResourceId("generated-id");

        PersistedClient? captured = null;
        MockClientValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<PersistedClient>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback((PersistedClient c, IStoreManager _, CancellationToken _) => captured = c)
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedClient>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        SetupEffectiveSettings();
        SetupCreatedClientAuthenticationMethod();

        var request = new CreateClientRequest { IsDisabled = false, Settings = EmptyObject() };
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateClientAsync(
            httpContext,
            TenantId,
            request,
            CancellationToken.None
        );

        Assert.IsType<Created<ClientResource>>(result);
        Assert.NotNull(captured);
        Assert.Equal(TenantId, captured.TenantId);
        Assert.Equal(TenantId, captured.Settings.TenantId);
        Assert.Equal(TenantId, captured.Secrets.TenantId);
        Assert.Null(captured.ResourceServerId);

        // The request names no method, so the stored document names the tenant's. See ADR-1391.
        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            captured
                .Settings.Value.GetProperty(OpenIdSettingNames.TokenEndpointAuthMethod)
                .GetString()
        );
    }

    // The issuer resolves the created client's method from the tenant's stored settings.
    private void SetupCreatedClientAuthenticationMethod() =>
        MockClientSecretIssuer
            .Setup(x =>
                x.GetTokenEndpointAuthMethodAsync(
                    "platform",
                    TenantId,
                    It.Is<JsonElement?>(settings => settings.HasValue),
                    "generated-id",
                    It.IsAny<JsonElement>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic)
            .Verifiable();

    [Fact]
    public async Task CreateClientAsync_WhenBoundToAResourceServer_PersistsAndReturnsTheBinding()
    {
        SetupAuthorizeCreate("rs-1", error: null);
        SetupCreatorAssignment();
        SetupClientChangedAudit();
        SetupValidSettings();
        SetupStore();
        SetupResourceId("generated-id");

        PersistedClient? captured = null;
        MockClientValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<PersistedClient>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback((PersistedClient c, IStoreManager _, CancellationToken _) => captured = c)
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedClient>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        SetupEffectiveSettings();
        SetupCreatedClientAuthenticationMethod();

        var request = new CreateClientRequest
        {
            IsDisabled = false,
            ResourceServerId = "rs-1",
            Settings = EmptyObject(),
        };
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateClientAsync(
            httpContext,
            TenantId,
            request,
            CancellationToken.None
        );

        var created = Assert.IsType<Created<ClientResource>>(result);
        Assert.Equal("rs-1", captured?.ResourceServerId);
        Assert.Equal("rs-1", created.Value?.ResourceServerId);
    }

    [Fact]
    public async Task CreateClientAsync_WhenSettingsCannotBeHonored_ReturnsValidationProblem()
    {
        SetupAuthorizeCreate(resourceServerId: null, error: null);
        SetupInvalidSettings();
        SetupStore();
        SetupResourceId("generated-id");
        MockClientValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<PersistedClient>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();

        var request = new CreateClientRequest { IsDisabled = false, Settings = EmptyObject() };
        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no AddAsync / SaveChangesAsync setup, so persisting the client would fail the test.
        var result = await Handler.CreateClientAsync(
            httpContext,
            TenantId,
            request,
            CancellationToken.None
        );

        AssertSettingsValidationProblem(result);
    }

    [Fact]
    public async Task CreateClientAsync_WhenARegisteredAlgorithmIsNotPermitted_ReturnsValidationProblem()
    {
        SetupAuthorizeCreate(resourceServerId: null, error: null);
        SetupValidSettings();
        SetupStore();
        SetupResourceId("generated-id");
        MockClientValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<PersistedClient>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        SetupEffectiveSettings(
            new ClientCoherenceViolation(
                OpenIdSettingNames.IdTokenSignedResponseAlg,
                "RS256",
                OpenIdSettingNames.IdTokenSigningAlgValuesSupported
            )
        );

        var request = new CreateClientRequest { IsDisabled = false, Settings = EmptyObject() };
        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no AddAsync / SaveChangesAsync setup, so persisting the client would fail the test.
        var result = await Handler.CreateClientAsync(
            httpContext,
            TenantId,
            request,
            CancellationToken.None
        );

        AssertAlgorithmNotPermitted(result, OpenIdSettingNames.IdTokenSignedResponseAlg, "RS256");
    }

    [Fact]
    public async Task CreateClientAsync_WhenUnauthorized_ReturnsTheErrorWithoutOpeningTheStore()
    {
        SetupAuthorizeCreate("rs-1", Forbidden());

        var request = new CreateClientRequest
        {
            IsDisabled = false,
            ResourceServerId = "rs-1",
            Settings = EmptyObject(),
        };

        // Strict mocks: no IStoreManagerFactory.CreateAsync setup, so opening the store would fail the test.
        var result = await Handler.CreateClientAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            request,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region UpdateClientAsync Tests

    [Fact]
    public async Task UpdateClientAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheClient()
    {
        SetupAuthorizeUpdate(Forbidden());

        // Strict mocks: no IStoreManagerFactory.CreateAsync setup, so reading the client would fail the test.
        var result = await Handler.UpdateClientAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            new JsonPatchDocument<UpdateClientRequest>(),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region ListClientsAsync Tests

    [Fact]
    public async Task ListClientsAsync_WhenAuthorized_ReturnsPage()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockClientStore
            .Setup(x => x.GetPageAsync(It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PagedResult<PersistedClient>
                {
                    Items = [CreateClient("client-ct")],
                    NextCursor = "next",
                }
            )
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListClientsAsync(
            httpContext,
            TenantId,
            resourceServerId: null,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<ClientResource>>>(result);
        Assert.Single(json.Value!.Items);
        Assert.Equal("next", json.Value.ContinuationToken);
    }

    [Fact]
    public async Task ListClientsAsync_WhenFilteredByResourceServer_ListsOnlyItsClients()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetPageByResourceServerAsync(
                    "rs-1",
                    It.IsAny<PageRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedClient>
                {
                    Items = [CreateClient("client-ct")],
                    NextCursor = null,
                }
            )
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListClientsAsync(
            httpContext,
            TenantId,
            resourceServerId: "rs-1",
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<ClientResource>>>(result);
        Assert.Single(json.Value!.Items);
    }

    [Fact]
    public async Task ListClientsAsync_WhenForbidden_ReturnsForbidWithoutQueryingStore()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListClientsAsync(
            httpContext,
            TenantId,
            resourceServerId: null,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        // No store scaffold is set up, so a strict-mock failure would mean authorization ran too late.
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region ListIncoherentClientsAsync Tests

    [Fact]
    public async Task ListIncoherentClientsAsync_WhenAuthorized_ReturnsTheIncoherentClients()
    {
        var violation = new ClientCoherenceViolation(
            OpenIdSettingNames.RedirectUris,
            "http://x",
            OpenIdSettingNames.RedirectUriKindsSupported
        );
        SetupAuthorization(AuthorizationResult.Success());
        var tenantSettings = SetupIncoherentClientsStore(CreateTenant());
        Assert.NotNull(tenantSettings);
        MockClientCoherenceReporter
            .Setup(x =>
                x.FindIncoherentClientsAsync(
                    MockStoreManager.Object,
                    TenantId,
                    tenantSettings,
                    It.Is<PageRequest>(page => page.Cursor == "c1" && page.Limit == 10),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<IncoherentClient>
                {
                    Items =
                    [
                        new IncoherentClient { ClientId = ClientId, Violations = [violation] },
                    ],
                    NextCursor = "next",
                }
            )
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListIncoherentClientsAsync(
            httpContext,
            TenantId,
            cursor: "c1",
            limit: 10,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<IncoherentClientResource>>>(
            result
        );
        var item = Assert.Single(json.Value!.Items);
        Assert.Equal(TenantId, item.TenantId);
        Assert.Equal(ClientId, item.ClientId);
        Assert.Null(item.DocumentError);
        var violationResource = Assert.Single(item.Violations);
        Assert.Equal(violation.SettingName, violationResource.SettingName);
        Assert.Equal(violation.Value, violationResource.Value);
        Assert.Equal(violation.GoverningSettingName, violationResource.GoverningSettingName);
        Assert.Equal(violation.Description, violationResource.Description);
        Assert.Equal("next", json.Value.ContinuationToken);
    }

    [Fact]
    public async Task ListIncoherentClientsAsync_WhenTheDocumentCannotBeHonored_ReturnsTheReason()
    {
        const string reason = "the document cannot be honored";
        SetupAuthorization(AuthorizationResult.Success());
        var tenantSettings = SetupIncoherentClientsStore(CreateTenant());
        Assert.NotNull(tenantSettings);
        MockClientCoherenceReporter
            .Setup(x =>
                x.FindIncoherentClientsAsync(
                    MockStoreManager.Object,
                    TenantId,
                    tenantSettings,
                    It.Is<PageRequest>(page => page.Cursor == null && page.Limit == 50),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<IncoherentClient>
                {
                    Items =
                    [
                        new IncoherentClient
                        {
                            ClientId = ClientId,
                            Violations = [],
                            DocumentError = reason,
                        },
                    ],
                    NextCursor = null,
                }
            )
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListIncoherentClientsAsync(
            httpContext,
            TenantId,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<IncoherentClientResource>>>(
            result
        );
        var item = Assert.Single(json.Value!.Items);
        Assert.Equal(reason, item.DocumentError);
        Assert.Empty(item.Violations);
        Assert.Null(json.Value.ContinuationToken);
    }

    [Fact]
    public async Task ListIncoherentClientsAsync_WhenTheTenantIsMissing_ReturnsAnEmptyPage()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupIncoherentClientsStore(null);

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListIncoherentClientsAsync(
            httpContext,
            TenantId,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<IncoherentClientResource>>>(
            result
        );
        Assert.Empty(json.Value!.Items);
        Assert.Null(json.Value.ContinuationToken);
    }

    [Fact]
    public async Task ListIncoherentClientsAsync_WhenForbidden_ReturnsForbidWithoutQueryingStore()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListIncoherentClientsAsync(
            httpContext,
            TenantId,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        // No store scaffold is set up, so a strict-mock failure would mean authorization ran too late.
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    // Returns the tenant's effective settings that the reporter receives, or null when the tenant has no settings.
    private IReadOnlySettingCollection? SetupIncoherentClientsStore(PersistedTenant? tenant)
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant)
            .Verifiable();

        return tenant is null ? null : SetupTenantEffectiveSettings();
    }

    #endregion

    #region GetClientAsync Tests

    [Fact]
    public async Task GetClientAsync_WhenAuthorized_ReturnsClient()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetClientAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<ClientResource>>(result);
        Assert.Equal(ClientId, json.Value?.ClientId);
        Assert.Equal(TenantId, json.Value?.TenantId);
    }

    [Fact]
    public async Task GetClientAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetClientAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetClientAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheClient()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetClientAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task GetClientAsync_WhenTheClientIsHostManaged_ReturnsIsHostManaged()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        var handler = CreateHostManagedHandler();

        var result = await handler.GetClientAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<ClientResource>>(result);
        Assert.True(json.Value?.IsHostManaged);
    }

    #endregion

    #region GetSettingsAsync Tests

    [Fact]
    public async Task GetSettingsAsync_WhenAuthorized_ReturnsSettings()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<ClientSettingsResource>>(result);
        Assert.Equal(ClientId, json.Value?.ClientId);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheClient()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
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
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var descriptor = new SettingDescriptor<bool>
        {
            Name = "claims_parameter_supported",
            AppliesTo = ["tenant", "client"],
        };
        var tenantOnlyDescriptor = new SettingDescriptor<bool>
        {
            Name = "session_management_supported",
            AppliesTo = ["tenant"],
        };
        var effective = CreateSettingCollection(
            descriptor.Create(true),
            tenantOnlyDescriptor.Create(true)
        );
        SetupEffectiveSettingsResolver(effective);
        SetupSettingScopes(
            OpenIdSettingScopes.Client,
            (descriptor, true),
            (tenantOnlyDescriptor, false)
        );
        var httpContext = CreateHttpContextWithEnvironment(authenticated: true);

        var result = await Handler.GetEffectiveSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<ClientEffectiveSettingsResource>>(result);
        Assert.Equal(ClientId, json.Value?.ClientId);
        Assert.Equal(TenantId, json.Value?.TenantId);
        Assert.True(json.Value!.Settings.GetProperty("claims_parameter_supported").GetBoolean());

        // A setting the runtime reads only from the tenant's settings does not govern the client (ADR-1149).
        Assert.False(json.Value.Settings.TryGetProperty("session_management_supported", out _));
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetEffectiveSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheClient()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetEffectiveSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        // No store or resolver is set up, so a strict-mock failure would mean the client was read or the settings were
        // resolved before authorization.
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    private static ISettingCollection CreateSettingCollection(params Setting[] settings)
    {
        var list = settings.ToList();
        var mock = new Mock<ISettingCollection>(MockBehavior.Loose);
        mock.Setup(x => x.GetEnumerator()).Returns(() => list.GetEnumerator());
        mock.Setup(x => x.Merge(It.IsAny<IEnumerable<Setting>>())).Returns(mock.Object);
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

    // The client's effective token_endpoint_auth_method, which the client-secret issuer is handed to decide the form its
    // shared secret is issued in. See ADR-1391.
    private void SetupRegisteredAuthenticationMethod(bool usesClientSecretAsKey) =>
        SetupRegisteredAuthenticationMethod(
            usesClientSecretAsKey
                ? OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt
                : OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );

    // The issuer resolves the method from the tenant's stored settings and the client's settings document.
    private void SetupRegisteredAuthenticationMethod(string method)
    {
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant())
            .Verifiable();
        MockClientSecretIssuer
            .Setup(x =>
                x.GetTokenEndpointAuthMethodAsync(
                    "platform",
                    TenantId,
                    It.Is<JsonElement?>(settings => settings.HasValue),
                    ClientId,
                    It.IsAny<JsonElement>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(method)
            .Verifiable();
    }

    private void SetupEffectiveSettingsResolver(ISettingCollection effectiveSettings)
    {
        var tenantSettings = SetupTenantEffectiveSettings();

        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    tenantSettings,
                    OpenIdSettingScopes.Client,
                    ClientId,
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(effectiveSettings)
            .Verifiable();
    }

    private IReadOnlySettingCollection SetupTenantEffectiveSettings()
    {
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

        return tenantSettings;
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

    [Fact]
    public async Task UpdateSettingsAsync_WhenAuthorized_ReturnsNoContent()
    {
        SetupClientRoleAssignments();
        SetupClientChangedAudit();
        SetupValidSettings();
        SetupNoChangedLimits("name", "value");
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        JsonElement? stored = null;
        MockClientStore
            .Setup(x =>
                x.UpdateSettingsAsync(
                    It.IsAny<PersistedClientSettings>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (PersistedClientSettings settings, CancellationToken _) => stored = settings.Value
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        SetupEffectiveSettings();
        // The settings leave the method as it is, so the secrets are left as they are: no reconciliation is set up.
        SetupResolvedAuthenticationMethods(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateAddPatch("name", "value"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);

        // The settings name no method, so the stored document names the tenant's, which a later change to the tenant's
        // settings cannot move. See ADR-1391.
        Assert.NotNull(stored);
        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            stored.Value.GetProperty(OpenIdSettingNames.TokenEndpointAuthMethod).GetString()
        );
        Assert.Equal("value", stored.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenSettingsCannotBeHonored_ReturnsValidationProblem()
    {
        SetupClientRoleAssignments();
        SetupInvalidSettings();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no UpdateSettingsAsync / SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateAddPatch("unregistered_setting", "value"),
            ifMatch: null,
            CancellationToken.None
        );

        AssertSettingsValidationProblem(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenARegisteredAlgorithmIsNotPermitted_ReturnsValidationProblem()
    {
        SetupClientRoleAssignments();
        SetupValidSettings();
        SetupNoChangedLimits(OpenIdSettingNames.IdTokenSignedResponseAlg, "RS256");
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupEffectiveSettings(
            new ClientCoherenceViolation(
                OpenIdSettingNames.IdTokenSignedResponseAlg,
                "RS256",
                OpenIdSettingNames.IdTokenSigningAlgValuesSupported
            )
        );

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no UpdateSettingsAsync / SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateAddPatch(OpenIdSettingNames.IdTokenSignedResponseAlg, "RS256"),
            ifMatch: null,
            CancellationToken.None
        );

        AssertAlgorithmNotPermitted(result, OpenIdSettingNames.IdTokenSignedResponseAlg, "RS256");
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateAddPatch("name", "value"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenJwksUriIsAddedToAClientWithPublicKeys_ReturnsConflict()
    {
        SetupClientRoleAssignments();
        SetupValidSettings();
        SetupNoChangedLimits(OpenIdSettingNames.JwksUri, "https://client.example/jwks");
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateClient("client-ct", EmptyObject(), CreatePublicKeySecret("key-1", "kid-1"))
            )
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no UpdateSettingsAsync / SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateAddPatch(OpenIdSettingNames.JwksUri, "https://client.example/jwks"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenJwksUriIsAddedToAClientWithOnlySharedSecrets_ReturnsNoContent()
    {
        SetupClientRoleAssignments();
        SetupClientChangedAudit();
        SetupValidSettings();
        SetupNoChangedLimits(OpenIdSettingNames.JwksUri, "https://client.example/jwks");
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct", EmptyObject(), CreatePersistedSecret()))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockClientStore
            .Setup(x =>
                x.UpdateSettingsAsync(
                    It.IsAny<PersistedClientSettings>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        SetupEffectiveSettings();
        // The settings leave the method as it is, so the secrets are left as they are: no reconciliation is set up.
        SetupResolvedAuthenticationMethods(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateAddPatch(OpenIdSettingNames.JwksUri, "https://client.example/jwks"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    // The issuer resolves the stored settings to the stored method and the patched settings to the method they
    // register, and brings the client's secrets into that method's form.
    private ISecretProtector SetupReconciliation(
        ClientSecretReconciliation reconciliation,
        string storedMethod,
        string method
    )
    {
        SetupResolvedAuthenticationMethods(storedMethod);
        var protector = SetupGenerateSecretProtector();
        MockClientSecretIssuer
            .Setup(x =>
                x.ReconcileClientSecrets(
                    protector,
                    method,
                    It.IsAny<IReadOnlyCollection<PersistedSecret>>()
                )
            )
            .Returns(reconciliation)
            .Verifiable();
        return protector;
    }

    // A settings document that names a method resolves to it, and one that names none to the tenant's method.
    private void SetupResolvedAuthenticationMethods(string tenantMethod)
    {
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant())
            .Verifiable();
        MockClientSecretIssuer
            .Setup(x =>
                x.GetTokenEndpointAuthMethodAsync(
                    "platform",
                    TenantId,
                    It.Is<JsonElement?>(settings => settings.HasValue),
                    ClientId,
                    It.IsAny<JsonElement>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    string _,
                    string _,
                    JsonElement? _,
                    string _,
                    JsonElement settings,
                    CancellationToken _
                ) =>
                    settings.TryGetProperty(
                        OpenIdSettingNames.TokenEndpointAuthMethod,
                        out var method
                    )
                        ? method.GetString()
                        : tenantMethod
            )
            .Verifiable();
    }

    private void SetupSettingsWrite()
    {
        MockClientStore
            .Setup(x =>
                x.UpdateSettingsAsync(
                    It.IsAny<PersistedClientSettings>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenNoSharedSecretCanServeTheMethod_RevealsTheIssuedSecretOnce()
    {
        SetupClientRoleAssignments();
        // The settings register a method none of the client's secrets can serve, so the issuer issues one, which this
        // response reveals once, as dynamic client registration does. See ADR-1391.
        var verifier = CreatePersistedSecret("verifier-1");
        var issued = CreatePersistedSecret("issued-1", "issued-ct");
        SetupClientChangedAudit();
        SetupClientSecretChangedAudit();
        SetupValidSettings();
        SetupNoChangedLimits(OpenIdSettingNames.TokenEndpointAuthMethod, "client_secret_post");
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct", EmptyObject(), verifier))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupEffectiveSettings();
        SetupReconciliation(
            new ClientSecretReconciliation
            {
                Secrets = [issued],
                RemovedSecrets = [verifier],
                AddedSecrets = [issued],
                IssuedSecret = new GeneratedSecret
                {
                    Secret = issued,
                    SecretMaterial = "issued-material",
                },
            },
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );
        SetupSettingsWrite();
        MockClientStore
            .Setup(x => x.RemoveSecretAsync(ClientId, "verifier-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddSecretAsync(ClientId, issued, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateAddPatch(OpenIdSettingNames.TokenEndpointAuthMethod, "client_secret_post"),
            ifMatch: null,
            CancellationToken.None
        );

        var ok = Assert.IsType<Ok<CreatedSecretResource>>(result);
        Assert.Equal("issued-1", ok.Value?.SecretId);
        Assert.Equal("issued-material", ok.Value?.SecretMaterial);
        Assert.Equal("settings-ct", httpContext.Response.Headers.ETag);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheIssuerConvertsASecret_StoresTheConversionAndRevealsNothing()
    {
        SetupClientRoleAssignments();
        var reversible = CreatePersistedSecret("reversible-1");
        var converted = CreatePersistedSecret("converted-1");
        SetupClientChangedAudit();
        SetupClientSecretChangedAudit();
        SetupValidSettings();
        SetupNoChangedLimits(OpenIdSettingNames.TokenEndpointAuthMethod, "client_secret_post");
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct", EmptyObject(), reversible))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupEffectiveSettings();
        SetupReconciliation(
            new ClientSecretReconciliation
            {
                Secrets = [converted],
                RemovedSecrets = [reversible],
                AddedSecrets = [converted],
            },
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );
        SetupSettingsWrite();
        MockClientStore
            .Setup(x =>
                x.RemoveSecretAsync(ClientId, "reversible-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true)
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddSecretAsync(ClientId, converted, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateAddPatch(OpenIdSettingNames.TokenEndpointAuthMethod, "client_secret_post"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheCallerMayNotCreateTheIssuedSecret_ReturnsForbiddenWithoutWriting()
    {
        SetupClientRoleAssignments();
        // Issuing a secret is creating one, which needs the authority creating a secret needs.
        var issued = CreatePersistedSecret("issued-1");
        SetupValidSettings();
        SetupNoChangedLimits(OpenIdSettingNames.TokenEndpointAuthMethod, "client_secret_post");
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        MockAuthorizationService
            .SetupSequence(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .ReturnsAsync(AuthorizationResult.Failed());
        SetupEffectiveSettings();
        SetupReconciliation(
            new ClientSecretReconciliation
            {
                Secrets = [issued],
                RemovedSecrets = [],
                AddedSecrets = [issued],
                IssuedSecret = new GeneratedSecret
                {
                    Secret = issued,
                    SecretMaterial = "issued-material",
                },
            },
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );

        // Strict mocks: no UpdateSettingsAsync / AddSecretAsync / SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateAddPatch(OpenIdSettingNames.TokenEndpointAuthMethod, "client_secret_post"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        MockAuthorizationService.Verify(
            x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheClientHoldsATeamRoleTheCallerCannotGrant_ReturnsForbiddenWithoutWriting()
    {
        SetupClientRoleAssignments(CreateTeamAssignment());
        SetupClientNodeAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupClientLookup();
        SetupRoleNodeAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no UpdateSettingsAsync / SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateAddPatch("name", "value"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenALimitChangesAndTheTenantRefuses_ReturnsForbiddenWithoutWriting()
    {
        SetupValidSettings();
        SetupChangedLimits(
            [OpenIdSettingNames.RateLimitClientPerMinute],
            OpenIdSettingNames.RateLimitClientPerMinute,
            "10"
        );
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupClientRoleAssignments();
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

        // Strict mocks: no effective-settings or store-write setup, so continuing past the refusal would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateAddPatch(OpenIdSettingNames.RateLimitClientPerMinute, "10"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Contains("'rate_limit_client_per_minute'", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheClientIsHostManaged_ReturnsConflictWithoutWriting()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        var handler = CreateHostManagedHandler();

        // Strict mocks: no validation or store-write setup, so continuing past the refusal would fail the test.
        var result = await handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateAddPatch("name", "value"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenTheCallerIsUnauthorizedForAHostManagedClient_ReturnsForbidden()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        // Strict mocks: the store and the policy have no setup, so reading the client or consulting the policy before
        // the authorization would fail the test.
        var handler = CreateHandler(MockHostManagedClientPolicy.Object);

        var result = await handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateAddPatch("name", "value"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheClient()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no store setup, so opening the store or reading the client would fail the test.
        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateAddPatch("name", "value"),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_RequestsUpdateOnTheClientNode()
    {
        SetupClientRoleAssignments();
        SetupStore();
        SetupClientLookup();
        object? authorizedResource = null;
        SetupAuthorizationCapture(resource => authorizedResource ??= resource);

        // The stale If-Match ends the route right after its own authorization.
        var result = await Handler.UpdateSettingsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateAddPatch("name", "value"),
            ifMatch: "stale-token",
            CancellationToken.None
        );

        Assert.IsType<StatusCodeHttpResult>(result);
        var node = Assert.IsType<ResourceNode>(authorizedResource);
        Assert.Equal(ResourceNodeTypes.Client, node.ResourceType);
        Assert.Equal(ClientId, node.ResourceId);
        Assert.Equal(TenantId, node.TenantId);
    }

    #endregion

    #region NameTokenEndpointAuthMethod Tests

    [Fact]
    public void NameTokenEndpointAuthMethod_WhenTheSettingsNameNone_NamesTheResolvedMethod()
    {
        var settings = JsonSerializer.SerializeToElement(
            new JsonObject { [OpenIdSettingNames.ClientName] = "client" }
        );

        var named = Handler.NameTokenEndpointAuthMethod(
            settings,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt
        );

        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
            named.GetProperty(OpenIdSettingNames.TokenEndpointAuthMethod).GetString()
        );
        Assert.Equal("client", named.GetProperty(OpenIdSettingNames.ClientName).GetString());
    }

    [Fact]
    public void NameTokenEndpointAuthMethod_WhenTheSettingsNameOne_KeepsIt()
    {
        var settings = JsonSerializer.SerializeToElement(
            new JsonObject
            {
                [OpenIdSettingNames.TokenEndpointAuthMethod] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .PrivateKeyJwt,
            }
        );

        var named = Handler.NameTokenEndpointAuthMethod(
            settings,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        );

        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
            named.GetProperty(OpenIdSettingNames.TokenEndpointAuthMethod).GetString()
        );
    }

    [Fact]
    public void NameTokenEndpointAuthMethod_WhenTheMethodCannotBeResolved_KeepsTheSettings()
    {
        var settings = EmptyObject();

        var named = Handler.NameTokenEndpointAuthMethod(settings, null);

        Assert.False(named.TryGetProperty(OpenIdSettingNames.TokenEndpointAuthMethod, out _));
    }

    #endregion

    #region ValidateEffectiveSettingsAsync Tests

    [Fact]
    public async Task ValidateEffectiveSettingsAsync_WhenARuleFindsAViolation_KeysTheErrorByTheRegisteredSetting()
    {
        const string redirectUri = "myapp://callback";
        SetupEffectiveSettings(
            new ClientCoherenceViolation(
                OpenIdSettingNames.RedirectUris,
                redirectUri,
                OpenIdSettingNames.RedirectUriKindsSupported
            )
        );

        var errors = await Handler.ValidateEffectiveSettingsAsync(
            TenantId,
            EmptyObject(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        var error = Assert.Single(errors);
        Assert.Equal(OpenIdSettingNames.RedirectUris, error.SettingName);
        Assert.Contains(redirectUri, error.Message, StringComparison.Ordinal);
    }

    private void SetupEffectiveSettings(params ClientCoherenceViolation[] violations)
    {
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant())
            .Verifiable();
        var tenantSettings = SetupTenantEffectiveSettings();
        MockClientCoherenceValidator
            .Setup(x => x.Validate(tenantSettings, It.IsAny<JsonElement>()))
            .Returns(violations)
            .Verifiable();
    }

    private static void AssertAlgorithmNotPermitted(
        IResult result,
        string settingName,
        string registered
    )
    {
        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal([settingName], problem.ProblemDetails.Errors.Keys);
        Assert.Contains($"'{registered}'", problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    #endregion

    #region GetSecretsAsync Tests

    [Fact]
    public async Task GetSecretsAsync_WhenAuthorized_ReturnsSecrets()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetSecretsOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSecretsCollection("secrets-ct", CreatePersistedSecret()))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretsAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<ClientSecretsResource>>(result);
        Assert.Equal(ClientId, json.Value?.ClientId);
        Assert.Single(json.Value!.Secrets);
    }

    [Fact]
    public async Task GetSecretsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetSecretsOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClientSecrets?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretsAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetSecretsAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheClient()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetSecretsAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region CreateSecretAsync Tests

    private ISecretProtector SetupGenerateSecretProtector()
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
        SetupClientRoleAssignments();
        SetupSecretCreateValidation();
        SetupClientSecretChangedAudit();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId();
        SetupRegisteredAuthenticationMethod(usesClientSecretAsKey: false);

        var protector = SetupGenerateSecretProtector();
        var generated = CreatePersistedSecret();
        MockClientSecretIssuer
            .Setup(x =>
                x.GenerateClientSecret(
                    protector,
                    OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
                    It.IsAny<SharedSecretRequest>()
                )
            )
            .Returns(
                new GeneratedSecret { Secret = generated, SecretMaterial = "revealed-material" }
            )
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddSecretAsync(ClientId, generated, It.IsAny<CancellationToken>()))
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
            ClientId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var created = Assert.IsType<Created<CreatedSecretResource>>(result);
        Assert.Equal("secret-1", created.Value?.SecretId);
        Assert.Equal("revealed-material", created.Value?.SecretMaterial);
        Assert.Equal("secret-ct", httpContext.Response.Headers.ETag);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenTheClientHoldsATeamRoleTheCallerCannotGrant_ReturnsForbiddenWithoutWriting()
    {
        SetupClientRoleAssignments(CreateTeamAssignment());
        SetupStore();
        SetupClientLookup();
        SetupClientNodeAuthorization(AuthorizationResult.Success());
        SetupRoleNodeAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no validation, generation, AddSecretAsync or SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.CreateSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenTheCallerCanGrantEveryRoleTheClientHolds_CreatesTheSecret()
    {
        SetupClientRoleAssignments(CreateTeamAssignment());
        SetupSecretCreateValidation();
        SetupClientSecretChangedAudit();
        SetupStore();
        SetupClientLookup();
        SetupClientNodeAuthorization(AuthorizationResult.Success());
        SetupRoleNodeAuthorization(AuthorizationResult.Success());
        SetupResourceId();
        SetupRegisteredAuthenticationMethod(usesClientSecretAsKey: false);

        var protector = SetupGenerateSecretProtector();
        var generated = CreatePersistedSecret();
        MockClientSecretIssuer
            .Setup(x =>
                x.GenerateClientSecret(
                    protector,
                    OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
                    It.IsAny<SharedSecretRequest>()
                )
            )
            .Returns(
                new GeneratedSecret { Secret = generated, SecretMaterial = "revealed-material" }
            )
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddSecretAsync(ClientId, generated, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.CreateSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        Assert.IsType<Created<CreatedSecretResource>>(result);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenTheClientUsesItsSecretAsAKey_IssuesTheSecretForItsMethod()
    {
        SetupClientRoleAssignments();
        // The secret is issued for the client's effective method, client_secret_jwt here, which the client-secret
        // issuer stores so the server can read it back. See ADR-1391.
        SetupSecretCreateValidation();
        SetupClientSecretChangedAudit();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId();
        SetupRegisteredAuthenticationMethod(usesClientSecretAsKey: true);

        var protector = SetupGenerateSecretProtector();
        var generated = CreatePersistedSecret();
        MockClientSecretIssuer
            .Setup(x =>
                x.GenerateClientSecret(
                    protector,
                    OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
                    It.IsAny<SharedSecretRequest>()
                )
            )
            .Returns(new GeneratedSecret { Secret = generated, SecretMaterial = "key-material" })
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddSecretAsync(ClientId, generated, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.CreateSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var created = Assert.IsType<Created<CreatedSecretResource>>(result);
        Assert.Equal("key-material", created.Value?.SecretMaterial);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenAsymmetric_DoesNotRevealMaterial()
    {
        SetupClientRoleAssignments();
        SetupSecretCreateValidation();
        SetupClientSecretChangedAudit();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId();

        var protector = SetupGenerateSecretProtector();
        var generated = CreatePersistedSecret();
        MockSecretGenerator
            .Setup(x => x.GenerateSecret(protector, It.IsAny<GenerateSecretRequest>()))
            .Returns(new GeneratedSecret { Secret = generated, SecretMaterial = null })
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddSecretAsync(ClientId, generated, It.IsAny<CancellationToken>()))
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
            ClientId,
            CreateAsymmetricSecretRequest(),
            CancellationToken.None
        );

        var created = Assert.IsType<Created<CreatedSecretResource>>(result);
        Assert.Null(created.Value?.SecretMaterial);
    }

    // The key is generated with the parameters the validator resolves, such as the curve of an algorithm bound to one,
    // not with the request's own.
    [Fact]
    public async Task CreateSecretAsync_WhenValidationResolvesTheCurve_GeneratesTheKeyOnIt()
    {
        SetupClientRoleAssignments();
        SetupClientSecretChangedAudit();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId();
        MockSecretValidator
            .Setup(x =>
                x.ValidateCreate(It.IsAny<CreateSecretRequest>(), It.IsAny<DateTimeOffset>())
            )
            .Returns(
                SecretCreateValidation.Valid(
                    new SecretKeyGenerationParameters { KeySizeBits = 256, Curve = "secp256k1" }
                )
            )
            .Verifiable();

        var protector = SetupGenerateSecretProtector();
        var generated = CreatePersistedSecret();
        MockSecretGenerator
            .Setup(x =>
                x.GenerateSecret(
                    protector,
                    It.Is<GenerateSecretRequest>(generate =>
                        generate.GenerationParameters.KeySizeBits == 256
                        && generate.GenerationParameters.Curve == "secp256k1"
                    )
                )
            )
            .Returns(new GeneratedSecret { Secret = generated, SecretMaterial = null })
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddSecretAsync(ClientId, generated, It.IsAny<CancellationToken>()))
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
            ClientId,
            new CreateSecretRequest
            {
                SecretType = SecretTypes.Ecc,
                KeySizeBits = 256,
                Use = "sig",
                Algorithm = "ES256K",
                ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            },
            CancellationToken.None
        );

        Assert.IsType<Created<CreatedSecretResource>>(result);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenClientNotFound_ReturnsNotFound()
    {
        SetupClientNodeAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheClient()
    {
        SetupClientNodeAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no store setup, so opening the store or reading the client would fail the test.
        var result = await Handler.CreateSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenValidationFails_ReturnsBadRequest()
    {
        SetupClientRoleAssignments();
        SetupSecretCreateValidation();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
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
            ClientId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenASharedSecretNamesAnActivationOrARetirement_ReturnsBadRequest()
    {
        SetupClientRoleAssignments();
        // A shared secret is active for its whole validity, so an instant it cannot honor is refused rather than
        // dropped: the issuer is strict with no setup, so nothing is generated.
        SetupSecretCreateValidation();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            new CreateSecretRequest
            {
                SecretType = SecretTypes.Symmetric,
                KeySizeBits = 256,
                Use = "sig",
                Algorithm = "HS256",
                ActivatesWhen = DateTimeOffset.UnixEpoch.AddDays(1),
                ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Contains("shared secret", problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenSharedSecretIsBelowTheMinimumEntropy_ReturnsBadRequest()
    {
        SetupClientRoleAssignments();
        SetupSecretCreateValidation();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
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

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            new CreateSecretRequest
            {
                SecretType = SecretTypes.Symmetric,
                KeySizeBits = 128,
                Use = "sig",
                Algorithm = "RS256",
                ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenClientPublishesJwksUriAndKeyIsAsymmetric_ReturnsConflict()
    {
        SetupClientRoleAssignments();
        SetupSecretCreateValidation();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct", JwksUriSettings()))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateAsymmetricSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenClientPublishesJwksUriAndSecretIsShared_ReturnsCreated()
    {
        SetupClientRoleAssignments();
        SetupSecretCreateValidation();
        SetupClientSecretChangedAudit();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct", JwksUriSettings()))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId();
        SetupRegisteredAuthenticationMethod(usesClientSecretAsKey: false);

        var protector = SetupGenerateSecretProtector();
        var generated = CreatePersistedSecret();
        MockClientSecretIssuer
            .Setup(x =>
                x.GenerateClientSecret(
                    protector,
                    OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
                    It.IsAny<SharedSecretRequest>()
                )
            )
            .Returns(new GeneratedSecret { Secret = generated, SecretMaterial = "material" })
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddSecretAsync(ClientId, generated, It.IsAny<CancellationToken>()))
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
            ClientId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        Assert.IsType<Created<CreatedSecretResource>>(result);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenTheClientIsHostManaged_ReturnsConflictWithoutWriting()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        var handler = CreateHostManagedHandler();

        // Strict mocks: no validation or generation setup, so continuing past the refusal would fail the test.
        var result = await handler.CreateSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    #endregion

    #region ImportSecretAsync Tests

    private static ImportSecretRequest CreateImportSecretRequest() =>
        new()
        {
            SecretType = SecretTypes.Rsa,
            Format = "jwk",
            KeyMaterial = """{"kty":"RSA","kid":"client-kid","n":"AQAB","e":"AQAB"}""",
            Use = null,
            Algorithm = null,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    private void SetupImportClient(PersistedClient client)
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client)
            .Verifiable();
        SetupClientNodeAuthorization(AuthorizationResult.Success());
        MockSecretValidator
            .Setup(x =>
                x.ValidateImport(It.IsAny<ImportSecretRequest>(), It.IsAny<DateTimeOffset>())
            )
            .Returns((ManagementError?)null)
            .Verifiable();
    }

    private void SetupImportedKey(PersistedSecret imported)
    {
        SetupResourceId("secret-2");
        var importedSecret = (PersistedSecret?)imported;
        string? error = null;
        MockClientKeyImporter
            .Setup(x =>
                x.TryImportKey(
                    It.Is<SecretKeyImportRequest>(request =>
                        request.SecretId == "secret-2" && request.KeyId == null
                    ),
                    out importedSecret,
                    out error
                )
            )
            .Returns(true)
            .Verifiable();
    }

    [Fact]
    public async Task ImportSecretAsync_WhenKeyIsImported_ReturnsCreated()
    {
        SetupClientRoleAssignments();
        SetupClientSecretChangedAudit();
        SetupImportClient(CreateClient("client-ct"));
        var imported = CreatePublicKeySecret("secret-2", "client-kid");
        SetupImportedKey(imported);
        MockClientStore
            .Setup(x => x.AddSecretAsync(ClientId, imported, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ImportSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateImportSecretRequest(),
            CancellationToken.None
        );

        var created = Assert.IsType<Created<SecretResource>>(result);
        Assert.Equal("secret-2", created.Value?.SecretId);
    }

    [Fact]
    public async Task ImportSecretAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheClient()
    {
        SetupClientNodeAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no store setup, so opening the store or reading the client would fail the test.
        var result = await Handler.ImportSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CreateImportSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task ImportSecretAsync_WhenClientPublishesJwksUri_ReturnsConflict()
    {
        SetupClientRoleAssignments();
        SetupImportClient(CreateClient("client-ct", JwksUriSettings()));

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ImportSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateImportSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task ImportSecretAsync_WhenKeyIsRefused_ReturnsBadRequestWithTheReason()
    {
        SetupClientRoleAssignments();
        SetupImportClient(CreateClient("client-ct"));
        SetupResourceId("secret-2");
        PersistedSecret? importedSecret = null;
        var error = (string?)"The key is not public.";
        MockClientKeyImporter
            .Setup(x =>
                x.TryImportKey(It.IsAny<SecretKeyImportRequest>(), out importedSecret, out error)
            )
            .Returns(false)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ImportSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateImportSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("The key is not public.", problem.ProblemDetails.Detail);
    }

    [Theory]
    [InlineData("client-kid")]
    [InlineData(null)]
    public async Task ImportSecretAsync_WhenKidIsAlreadyRegisteredForTheClient_ReturnsConflict(
        string? registeredKeyId
    )
    {
        SetupClientRoleAssignments();
        // A registered key without its own kid is selected by its secret identifier.
        var registered = CreatePublicKeySecret(registeredKeyId ?? "client-kid", registeredKeyId);
        SetupImportClient(CreateClient("client-ct", EmptyObject(), registered));
        SetupImportedKey(CreatePublicKeySecret("secret-2", "client-kid"));

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no AddSecretAsync / SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.ImportSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            CreateImportSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
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
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateClientSecret())
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
            ClientId,
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
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateClientSecret())
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
            ClientId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetPublicKeyAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "missing", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedClientSecret?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetPublicKeyAsync(
            httpContext,
            TenantId,
            ClientId,
            "missing",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetPublicKeyAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheClient()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetPublicKeyAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region GetSecretAsync / UpdateSecretAsync / DeleteSecretAsync Tests

    [Fact]
    public async Task GetSecretAsync_WhenAuthorized_ReturnsSecret()
    {
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateClientSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<SecretResource>>(result);
        Assert.Equal("secret-1", json.Value?.SecretId);
        Assert.False(json.Value?.IsVerifier);
    }

    [Fact]
    public async Task GetSecretAsync_WhenTheSecretIsAVerifier_SaysSo()
    {
        // The operator can tell a secret that client_secret_jwt cannot use from one it can. See ADR-1391.
        SetupStore();
        var stored = CreatePersistedSecret();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new PersistedClientSecret
                {
                    TenantId = TenantId,
                    ClientId = ClientId,
                    Value = new PersistedSecret
                    {
                        SecretId = stored.SecretId,
                        ConcurrencyToken = stored.ConcurrencyToken,
                        Use = stored.Use,
                        Algorithm = stored.Algorithm,
                        CreatedWhen = stored.CreatedWhen,
                        ExpiresWhen = stored.ExpiresWhen,
                        ActivatesWhen = stored.CreatedWhen,
                        RetiresWhen = stored.ExpiresWhen,
                        SecretType = stored.SecretType,
                        SecretEncoding = NCode.Identity.Secrets.SecretEncodings.Verifier,
                        Origin = SecretOrigins.Generated,
                        KeySizeBits = stored.KeySizeBits,
                        EncodedValue = stored.EncodedValue,
                    },
                }
            )
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.GetSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            "secret-1",
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<SecretResource>>(result);
        Assert.True(json.Value?.IsVerifier);
    }

    [Fact]
    public async Task GetSecretAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "missing", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedClientSecret?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "missing",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetSecretAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheClient()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            "secret-1",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task GetSecretAsync_AuthorizesAgainstTheClientNode()
    {
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateClientSecret())
            .Verifiable();
        object? capturedResource = null;
        SetupAuthorizationCapture(resource => capturedResource = resource);

        var httpContext = CreateHttpContext(authenticated: true);

        await Handler.GetSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            CancellationToken.None
        );

        var node = Assert.IsType<ResourceNode>(capturedResource);
        Assert.Equal(TenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.Client, node.ResourceType);
        Assert.Equal(ClientId, node.ResourceId);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenAuthorized_ReturnsNoContent()
    {
        SetupClientLookup();
        SetupClientRoleAssignments();
        SetupSecretUpdateValidation();
        SetupClientSecretChangedAudit();
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateClientSecret())
            .Verifiable();
        SetupClientNodeAuthorization(AuthorizationResult.Success());
        MockClientStore
            .Setup(x =>
                x.UpdateSecretAsync(
                    ClientId,
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
            ClientId,
            "secret-1",
            UpdateSecretRequest(),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Theory]
    [InlineData("sig", "RS256")]
    [InlineData("enc", "HS256")]
    [InlineData("both", null)]
    public async Task UpdateSecretAsync_WhenMetadataDoesNotFitTheKey_ReturnsBadRequest(
        string use,
        string? algorithm
    )
    {
        SetupClientLookup();
        SetupClientRoleAssignments();
        SetupSecretUpdateValidation();
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateClientSecret())
            .Verifiable();
        SetupClientNodeAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);
        var request = new UpdateSecretRequest
        {
            Use = use,
            Algorithm = algorithm,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(2),
        };

        // Strict mocks: no UpdateSecretAsync / SaveChangesAsync setup, so a write would fail the test.
        var result = await Handler.UpdateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            request,
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenKeyHasAKid_KeepsIt()
    {
        SetupClientLookup();
        SetupClientRoleAssignments();
        SetupSecretUpdateValidation();
        SetupClientSecretChangedAudit();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetSecretOrDefaultAsync(ClientId, "key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PersistedClientSecret
                {
                    TenantId = TenantId,
                    ClientId = ClientId,
                    Value = CreatePublicKeySecret("key-1", "client-kid"),
                }
            )
            .Verifiable();
        SetupClientNodeAuthorization(AuthorizationResult.Success());
        PersistedSecret? updated = null;
        MockClientStore
            .Setup(x =>
                x.UpdateSecretAsync(
                    ClientId,
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

        var httpContext = CreateHttpContext(authenticated: true);
        var request = new UpdateSecretRequest
        {
            Use = "sig",
            Algorithm = "PS256",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(2),
        };

        var result = await Handler.UpdateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "key-1",
            request,
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.NotNull(updated);
        Assert.Equal("client-kid", updated.KeyId);
        Assert.Equal("PS256", updated.Algorithm);
    }

    [Fact]
    public async Task DeleteSecretAsync_WhenAuthorized_ReturnsNoContent()
    {
        SetupClientLookup();
        SetupClientRoleAssignments();
        SetupClientSecretChangedAudit();
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateClientSecret())
            .Verifiable();
        SetupClientNodeAuthorization(AuthorizationResult.Success());
        MockClientStore
            .Setup(x => x.RemoveSecretAsync(ClientId, "secret-1", It.IsAny<CancellationToken>()))
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
            ClientId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task DeleteSecretAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupClientNodeAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "missing", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedClientSecret?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "missing",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task DeleteSecretAsync_AuthorizesAgainstTheClientNode()
    {
        SetupClientLookup();
        SetupClientRoleAssignments();
        SetupClientSecretChangedAudit();
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateClientSecret())
            .Verifiable();
        object? capturedResource = null;
        SetupAuthorizationCapture(resource => capturedResource = resource);
        MockClientStore
            .Setup(x => x.RemoveSecretAsync(ClientId, "secret-1", It.IsAny<CancellationToken>()))
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
            ClientId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        var node = Assert.IsType<ResourceNode>(capturedResource);
        Assert.Equal(TenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.Client, node.ResourceType);
        Assert.Equal(ClientId, node.ResourceId);
    }

    [Fact]
    public async Task DeleteSecretAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheClient()
    {
        SetupClientNodeAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no store setup, so opening the store or reading the client would fail the test.
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheClient()
    {
        SetupClientNodeAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no store setup, so opening the store or reading the client would fail the test.
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            UpdateSecretRequest(),
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region RotateSecretAsync Tests

    [Fact]
    public async Task RotateSecretAsync_WhenAuthorized_AddsTheReplacementAndKeepsTheRotatedSecret()
    {
        SetupClientRoleAssignments();
        SetupSecretCreateValidation();
        SetupClientSecretChangedAudit();
        SetupStore();
        var rotated = CreatePersistedSecret("secret-1", "rotated-ct");
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct", EmptyObject(), rotated))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId("secret-2");
        SetupRegisteredAuthenticationMethod(usesClientSecretAsKey: false);

        var protector = SetupGenerateSecretProtector();
        var expiresWhen = DateTimeOffset.UnixEpoch.AddYears(3);
        var replacement = CreatePersistedSecret("secret-2", "replacement-ct");
        MockClientSecretIssuer
            .Setup(x =>
                x.GenerateClientSecret(
                    protector,
                    OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
                    It.Is<SharedSecretRequest>(generate =>
                        generate.SecretId == "secret-2"
                        && generate.EntropyBits == 256
                        && generate.Use == rotated.Use
                        && generate.Algorithm == rotated.Algorithm
                        && generate.ExpiresWhen == expiresWhen
                    )
                )
            )
            .Returns(
                new GeneratedSecret { Secret = replacement, SecretMaterial = "revealed-material" }
            )
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddSecretAsync(ClientId, replacement, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RotateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            new RotateSecretRequest { ExpiresWhen = expiresWhen },
            "rotated-ct",
            CancellationToken.None
        );

        var created = Assert.IsType<Created<CreatedSecretResource>>(result);
        Assert.Equal("secret-2", created.Value?.SecretId);
        Assert.Equal("revealed-material", created.Value?.SecretMaterial);
        Assert.Equal($"/tenants/{TenantId}/clients/{ClientId}/secrets/secret-2", created.Location);
        Assert.Equal("replacement-ct", httpContext.Response.Headers.ETag);
        MockManagementAuditRecorder.Verify(
            x =>
                x.RecordClientSecretChangedAsync(
                    httpContext,
                    TenantId,
                    ClientId,
                    "secret-2",
                    ResourceChangeTypes.Created,
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        MockManagementAuditRecorder.Verify(
            x =>
                x.RecordClientSecretChangedAsync(
                    httpContext,
                    TenantId,
                    ClientId,
                    "secret-1",
                    ResourceChangeTypes.Deleted,
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        MockManagementAuditRecorder.Verify(
            x =>
                x.RecordClientSecretChangedAsync(
                    httpContext,
                    TenantId,
                    ClientId,
                    "secret-1",
                    ResourceChangeTypes.Updated,
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        MockAuthorizationService.Verify(
            x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is ResourceNode
                        && ((ResourceNode)resource).TenantId == TenantId
                        && ((ResourceNode)resource).ResourceType == ResourceNodeTypes.Client
                        && ((ResourceNode)resource).ResourceId == ClientId
                    ),
                    It.Is<IEnumerable<IAuthorizationRequirement>>(r =>
                        r.Contains(Operations.Update)
                    )
                ),
            Times.Once
        );
        MockAuthorizationService.Verify(
            x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.Is<IEnumerable<IAuthorizationRequirement>>(r =>
                        r.Contains(Operations.Delete)
                    )
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RotateSecretAsync_WhenTheClientUsesItsSecretAsAKey_IssuesTheReplacementForItsMethod()
    {
        SetupClientRoleAssignments();
        // The replacement's form follows the method the client is registered for when it is rotated. See ADR-1391.
        SetupSecretCreateValidation();
        SetupClientSecretChangedAudit();
        SetupStore();
        var rotated = CreatePersistedSecret("secret-1", "rotated-ct");
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct", EmptyObject(), rotated))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId("secret-2");
        SetupRegisteredAuthenticationMethod(usesClientSecretAsKey: true);

        var protector = SetupGenerateSecretProtector();
        var replacement = CreatePersistedSecret("secret-2", "replacement-ct");
        MockClientSecretIssuer
            .Setup(x =>
                x.GenerateClientSecret(
                    protector,
                    OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
                    It.Is<SharedSecretRequest>(generate => generate.SecretId == "secret-2")
                )
            )
            .Returns(new GeneratedSecret { Secret = replacement, SecretMaterial = "key-material" })
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddSecretAsync(ClientId, replacement, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.RotateSecretAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            "secret-1",
            new RotateSecretRequest { ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(3) },
            "rotated-ct",
            CancellationToken.None
        );

        var created = Assert.IsType<Created<CreatedSecretResource>>(result);
        Assert.Equal("key-material", created.Value?.SecretMaterial);
    }

    [Fact]
    public async Task RotateSecretAsync_WhenRotatedExpiresWhenIsEarlier_ShortensTheRotatedSecretsExpiry()
    {
        SetupClientRoleAssignments();
        SetupSecretCreateValidation();
        SetupClientSecretChangedAudit();
        var grace = DateTimeOffset.UnixEpoch.AddMonths(1);
        var httpContext = await RotateWithGraceAsync(grace);

        MockClientStore.Verify(
            x =>
                x.UpdateSecretAsync(
                    ClientId,
                    It.Is<PersistedSecret>(s =>
                        s.SecretId == "secret-1"
                        && s.ExpiresWhen == grace
                        && s.EncodedValue == "encoded"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        MockManagementAuditRecorder.Verify(
            x =>
                x.RecordClientSecretChangedAsync(
                    httpContext,
                    TenantId,
                    ClientId,
                    "secret-1",
                    ResourceChangeTypes.Updated,
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RotateSecretAsync_WhenRotatedExpiresWhenPrecedesTheSecretsCreation_StopsItAtItsCreation()
    {
        SetupClientRoleAssignments();
        // A value in the past stops the rotated secret at once; its creation is the earliest instant its lifecycle
        // can hold, and its active window collapses with its expiry so the instants stay in order.
        SetupSecretCreateValidation();
        SetupClientSecretChangedAudit();
        await RotateWithGraceAsync(DateTimeOffset.UnixEpoch.AddDays(-1));

        MockClientStore.Verify(
            x =>
                x.UpdateSecretAsync(
                    ClientId,
                    It.Is<PersistedSecret>(s =>
                        s.SecretId == "secret-1"
                        && s.CreatedWhen == DateTimeOffset.UnixEpoch
                        && s.ActivatesWhen == DateTimeOffset.UnixEpoch
                        && s.RetiresWhen == DateTimeOffset.UnixEpoch
                        && s.ExpiresWhen == DateTimeOffset.UnixEpoch
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task RotateSecretAsync_WhenRotatedExpiresWhenIsLater_KeepsTheRotatedSecretsExpiry()
    {
        SetupClientRoleAssignments();
        SetupSecretCreateValidation();
        SetupClientSecretChangedAudit();
        var httpContext = await RotateWithGraceAsync(DateTimeOffset.UnixEpoch.AddYears(5));

        MockClientStore.Verify(
            x =>
                x.UpdateSecretAsync(
                    It.IsAny<string>(),
                    It.IsAny<PersistedSecret>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        MockManagementAuditRecorder.Verify(
            x =>
                x.RecordClientSecretChangedAsync(
                    httpContext,
                    TenantId,
                    ClientId,
                    "secret-1",
                    ResourceChangeTypes.Updated,
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Theory]
    [InlineData(64, 256)]
    [InlineData(256, 256)]
    [InlineData(344, 256)]
    [InlineData(688, 512)]
    public void GetReplacementEntropyBits_ReturnsTheStoredEntropyButAtLeastTheMinimum(
        int storedKeySizeBits,
        int expected
    )
    {
        Assert.Equal(
            expected,
            ClientApiEndpointHandler.GetReplacementEntropyBits(storedKeySizeBits)
        );
    }

    // Rotates "secret-1" (expiring a year after the epoch) with the given grace expiry for the rotated secret.
    private async Task<HttpContext> RotateWithGraceAsync(DateTimeOffset rotatedExpiresWhen)
    {
        SetupStore();
        var rotated = CreatePersistedSecret("secret-1", "rotated-ct");
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct", EmptyObject(), rotated))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId("secret-2");
        SetupRegisteredAuthenticationMethod(usesClientSecretAsKey: false);

        var protector = SetupGenerateSecretProtector();
        var expiresWhen = DateTimeOffset.UnixEpoch.AddYears(3);
        var replacement = CreatePersistedSecret("secret-2", "replacement-ct");
        MockClientSecretIssuer
            .Setup(x =>
                x.GenerateClientSecret(
                    protector,
                    OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
                    It.Is<SharedSecretRequest>(generate => generate.SecretId == "secret-2")
                )
            )
            .Returns(
                new GeneratedSecret { Secret = replacement, SecretMaterial = "revealed-material" }
            )
            .Verifiable();
        MockClientStore
            .Setup(x => x.AddSecretAsync(ClientId, replacement, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        if (rotatedExpiresWhen < rotated.ExpiresWhen)
        {
            MockClientStore
                .Setup(x =>
                    x.UpdateSecretAsync(
                        ClientId,
                        It.IsAny<PersistedSecret>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(ValueTask.CompletedTask)
                .Verifiable();
        }
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RotateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            new RotateSecretRequest
            {
                ExpiresWhen = expiresWhen,
                RotatedExpiresWhen = rotatedExpiresWhen,
            },
            "rotated-ct",
            CancellationToken.None
        );

        Assert.IsType<Created<CreatedSecretResource>>(result);
        return httpContext;
    }

    [Fact]
    public async Task RotateSecretAsync_WhenSecretNotFound_ReturnsNotFound()
    {
        SetupClientNodeAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RotateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            new RotateSecretRequest { ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(3) },
            null,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task RotateSecretAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheClient()
    {
        SetupClientNodeAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no store setup, so opening the store or reading the client would fail the test.
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RotateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            new RotateSecretRequest { ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(3) },
            null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task RotateSecretAsync_WhenUpdateOfTheRotatedSecretIsDenied_ReturnsForbidden()
    {
        SetupRotateAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupRotateAuthorization(Operations.Update, AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RotateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            new RotateSecretRequest { ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(3) },
            null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task RotateSecretAsync_WhenCreateIsDenied_ReturnsForbidden()
    {
        SetupRotateAuthorization(Operations.Create, AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RotateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            new RotateSecretRequest { ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(3) },
            null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    // Rotation is authorized as the create and the update it performs, so each requirement is set up on its own, and
    // only by the tests that reach it.
    private void SetupRotateAuthorization(
        IAuthorizationRequirement requirement,
        AuthorizationResult result
    ) =>
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.Is<IEnumerable<IAuthorizationRequirement>>(r => r.Contains(requirement))
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    [Fact]
    public async Task RotateSecretAsync_WhenIfMatchDiffers_ReturnsPreconditionFailed()
    {
        SetupClientRoleAssignments();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct", EmptyObject(), CreatePersistedSecret()))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RotateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            new RotateSecretRequest { ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(3) },
            "stale-ct",
            CancellationToken.None
        );

        var status = Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, status.StatusCode);
    }

    [Fact]
    public async Task RotateSecretAsync_WhenSecretIsAKey_ReturnsBadRequest()
    {
        SetupClientRoleAssignments();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateClient("client-ct", EmptyObject(), CreatePublicKeySecret("secret-1", "kid-1"))
            )
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RotateSecretAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            new RotateSecretRequest { ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(3) },
            null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    #endregion

    #region AddOwnerAsync Tests

    [Fact]
    public async Task AddOwnerAsync_WhenTheClientHoldsATeamRoleTheCallerCannotGrant_ReturnsForbiddenWithoutAddingTheOwner()
    {
        SetupClientRoleAssignments(CreateTeamAssignment());
        SetupStore();
        SetupClientLookup();
        SetupClientNodeAuthorization(AuthorizationResult.Success());
        SetupRoleNodeAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no ResourceOwnershipService setup, so an owner change would fail the test.
        var result = await Handler.AddOwnerAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            new AddOwnerRequest { PrincipalId = "principal-1" },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task AddOwnerAsync_WhenTheCallerIsNotAuthorizedAtTheClient_ReturnsForbiddenBeforeReadingTheClient()
    {
        SetupClientNodeAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no store manager factory setup, so reading the client would fail the test.
        var result = await Handler.AddOwnerAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            new AddOwnerRequest { PrincipalId = "principal-1" },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task AddOwnerAsync_WhenTheClientIsHostManaged_ReturnsConflictWithoutWriting()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        SetupClientLookup();
        var handler = CreateHostManagedHandler();

        // Strict mocks: no ownership setup, so continuing past the refusal would fail the test.
        var result = await handler.AddOwnerAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            new AddOwnerRequest { PrincipalId = "principal-1" },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    #endregion

    #region RemoveOwnerAsync Tests

    [Fact]
    public async Task RemoveOwnerAsync_WhenTheClientHoldsATeamRoleTheCallerCannotGrant_ReturnsForbiddenWithoutRemovingTheOwner()
    {
        SetupClientRoleAssignments(CreateTeamAssignment());
        SetupStore();
        SetupClientLookup();
        SetupClientNodeAuthorization(AuthorizationResult.Success());
        SetupRoleNodeAuthorization(AuthorizationResult.Failed());

        // Strict mocks: no ResourceOwnershipService setup, so an owner change would fail the test.
        var result = await Handler.RemoveOwnerAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            "principal-1",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion

    #region DeleteClientAsync Tests

    [Fact]
    public async Task DeleteClientAsync_WhenPreconditionsPass_ReturnsNoContent()
    {
        SetupAuthorizeDelete(error: null);
        SetupClientRoleAssignments();
        SetupClientChangedAudit();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        MockClientValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<PersistedClient>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        MockClientStore
            .Setup(x => x.RemoveAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteClientAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task DeleteClientAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheClient()
    {
        SetupAuthorizeDelete(Forbidden());

        // Strict mocks: no IStoreManagerFactory.CreateAsync setup, so reading the client would fail the test.
        var result = await Handler.DeleteClientAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteClientAsync_WhenValidatorReportsConflict_ReturnsProblem()
    {
        SetupAuthorizeDelete(error: null);
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        MockClientValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<PersistedClient>(),
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

        var result = await Handler.DeleteClientAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteClientAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupAuthorizeDelete(error: null);
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteClientAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task DeleteClientAsync_WhenTheClientIsHostManaged_ReturnsConflictWithoutWriting()
    {
        SetupAuthorizeDelete(error: null);
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        MockClientValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<PersistedClient>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        var handler = CreateHostManagedHandler();

        // Strict mocks: no store-write or audit setup, so continuing past the refusal would fail the test.
        var result = await handler.DeleteClientAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    #endregion
}
