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
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Clients;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
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
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ISecretGenerator> MockSecretGenerator { get; }
    private Mock<IClientKeyImporter> MockClientKeyImporter { get; }
    private Mock<ISecretPublicKeyConverter> MockSecretPublicKeyConverter { get; }
    private Mock<IClientValidator> MockClientValidator { get; }
    private Mock<ISecretValidator> MockSecretValidator { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<ISettingScopeCollectionProvider> MockSettingScopeCollectionProvider { get; }
    private Mock<IOpenIdServerProvider> MockServerProvider { get; }
    private Mock<ISecurityProfileCatalog> MockSecurityProfileCatalog { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IResourceOwnershipService> MockResourceOwnershipService { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private ServiceProvider ServiceProvider { get; }
    private ClientApiEndpointHandler Handler { get; }

    public ClientApiEndpointHandlerTests()
    {
        // The real key-type catalog and algorithms are the sources of truth the key rules read.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockClientStore = MockRepository.Create<IClientStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockSecretGenerator = MockRepository.Create<ISecretGenerator>();
        MockClientKeyImporter = MockRepository.Create<IClientKeyImporter>();
        MockSecretPublicKeyConverter = MockRepository.Create<ISecretPublicKeyConverter>();
        MockClientValidator = MockRepository.Create<IClientValidator>();
        MockSecretValidator = MockRepository.Create<ISecretValidator>();
        MockSecretValidator
            .Setup(x => x.ValidateCreate(It.IsAny<CreateSecretRequest>()))
            .Returns((ManagementError?)null);
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockSettingScopeCollectionProvider =
            MockRepository.Create<ISettingScopeCollectionProvider>();
        MockServerProvider = MockRepository.Create<IOpenIdServerProvider>();
        MockSecurityProfileCatalog = MockRepository.Create<ISecurityProfileCatalog>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockResourceOwnershipService = MockRepository.Create<IResourceOwnershipService>();
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
            .Returns(ValueTask.CompletedTask);

        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();
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
            .Returns(ValueTask.CompletedTask);
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
            .Returns(ValueTask.CompletedTask);

        Handler = new ClientApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockClientValidator.Object,
            MockSecretValidator.Object,
            MockSecretGenerator.Object,
            MockClientKeyImporter.Object,
            ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            MockSecretPublicKeyConverter.Object,
            TimeProvider.System,
            MockCryptoService.Object,
            MockSettingSerializer.Object,
            MockSettingScopeCollectionProvider.Object,
            MockServerProvider.Object,
            MockSecurityProfileCatalog.Object,
            MockResourceOwnershipService.Object,
            MockManagementAuditRecorder.Object,
            NullLogger<ClientApiEndpointHandler>.Instance
        );
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        MockRepository.Verify();
        await ServiceProvider.DisposeAsync();
    }

    #region Helpers

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
            .Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
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
                new SettingError("tenant_issuer", "'tenant_issuer' is not a registered setting."),
            ])
            .Verifiable();

    private static void AssertSettingsValidationProblem(IResult result)
    {
        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(["tenant_issuer"], problem.ProblemDetails.Errors.Keys);
        Assert.Contains(
            "'tenant_issuer' is not a registered setting.",
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
            SecretType = SecretTypes.Rsa,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.Spki,
            KeySizeBits = 2048,
            EncodedValue = "encoded",
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
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.SymmetricRaw,
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

    #region CreateClientAsync Tests

    [Fact]
    public async Task CreateClientAsync_UsesAmbientTenant()
    {
        SetupValidSettings();
        SetupStore();
        SetupResourceId("generated-id");

        PersistedClient? captured = null;
        MockClientValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<PersistedClient>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ClaimsPrincipal _, PersistedClient c, IStoreManager _, CancellationToken _) =>
                    captured = c
            )
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
    }

    [Fact]
    public async Task CreateClientAsync_WhenBoundToAResourceServer_PersistsAndReturnsTheBinding()
    {
        SetupValidSettings();
        SetupStore();
        SetupResourceId("generated-id");

        PersistedClient? captured = null;
        MockClientValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<PersistedClient>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ClaimsPrincipal _, PersistedClient c, IStoreManager _, CancellationToken _) =>
                    captured = c
            )
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
        SetupInvalidSettings();
        SetupStore();
        SetupResourceId("generated-id");
        MockClientValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
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
        Assert.IsType<ForbidHttpResult>(result);
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

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetClientAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
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

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
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
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenantSettings())
            .Verifiable();
        MockSecurityProfileCatalog
            .Setup(x =>
                x.Expand(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Tenant,
                    It.IsAny<JsonSerializerOptions>()
                )
            )
            .Returns(Array.Empty<Setting>())
            .Verifiable();
        MockSecurityProfileCatalog
            .Setup(x =>
                x.Expand(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Client,
                    It.IsAny<JsonSerializerOptions>()
                )
            )
            .Returns(Array.Empty<Setting>())
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
        SetupServerProvider(effective);
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

        // A setting the runtime reads only from the tenant's settings does not govern the client (ADR-0149).
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

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetEffectiveSettingsAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
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

    private void SetupServerProvider(ISettingCollection effectiveSettings)
    {
        var mockServerCollection = new Mock<IReadOnlySettingCollection>(MockBehavior.Loose);
        mockServerCollection
            .Setup(x => x.Merge(It.IsAny<IEnumerable<Setting>>()))
            .Returns(effectiveSettings);

        var mockProvider = new Mock<IReadOnlySettingCollectionProvider>(MockBehavior.Loose);
        mockProvider.Setup(x => x.Collection).Returns(mockServerCollection.Object);

        var mockServer = new Mock<OpenIdServer>(MockBehavior.Loose);
        mockServer.Setup(x => x.SettingsProvider).Returns(mockProvider.Object);

        MockServerProvider
            .Setup(x => x.GetAsync(It.IsAny<OpenIdEnvironment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockServer.Object)
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

    [Fact]
    public async Task UpdateSettingsAsync_WhenAuthorized_ReturnsNoContent()
    {
        SetupValidSettings();
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
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

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            ClientId,
            CreateAddPatch("name", "value"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenSettingsCannotBeHonored_ReturnsValidationProblem()
    {
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
            ClientId,
            CreateAddPatch("tenant_issuer", "value"),
            ifMatch: null,
            CancellationToken.None
        );

        AssertSettingsValidationProblem(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
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
        SetupValidSettings();
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
        SetupValidSettings();
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

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            ClientId,
            CreateAddPatch(OpenIdSettingNames.JwksUri, "https://client.example/jwks"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
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

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretsAsync(
            httpContext,
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    #endregion

    #region CreateSecretAsync Tests

    [Fact]
    public async Task CreateSecretAsync_WhenAuthorized_GeneratesAndReturnsCreated()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId();

        var generated = CreatePersistedSecret();
        MockSecretGenerator
            .Setup(x => x.GenerateSharedSecret(It.IsAny<GenerateSecretRequest>()))
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
    public async Task CreateSecretAsync_WhenAsymmetric_DoesNotRevealMaterial()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId();

        var generated = CreatePersistedSecret();
        MockSecretGenerator
            .Setup(x => x.GenerateSecret(It.IsAny<GenerateSecretRequest>()))
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

    [Fact]
    public async Task CreateSecretAsync_WhenClientNotFound_ReturnsNotFound()
    {
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
    public async Task CreateSecretAsync_WhenValidationFails_ReturnsBadRequest()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockSecretValidator
            .Setup(x => x.ValidateCreate(It.IsAny<CreateSecretRequest>()))
            .Returns(
                new ManagementError
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Detail = "incoherent",
                }
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
    public async Task CreateSecretAsync_WhenClientPublishesJwksUriAndKeyIsAsymmetric_ReturnsConflict()
    {
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
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct", JwksUriSettings()))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId();

        var generated = CreatePersistedSecret();
        MockSecretGenerator
            .Setup(x => x.GenerateSharedSecret(It.IsAny<GenerateSecretRequest>()))
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
        SetupAuthorization(AuthorizationResult.Success());
        MockSecretValidator
            .Setup(x => x.ValidateImport(It.IsAny<ImportSecretRequest>()))
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
    public async Task ImportSecretAsync_WhenClientPublishesJwksUri_ReturnsConflict()
    {
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
    public async Task GetPublicKeyAsync_WhenForbidden_ReturnsForbid()
    {
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateClientSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetPublicKeyAsync(
            httpContext,
            TenantId,
            ClientId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
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
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateClientSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
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
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateClientSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

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
        SetupAuthorization(AuthorizationResult.Success());
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
        SetupStore();
        MockClientStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ClientId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateClientSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
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
            ClientId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task DeleteSecretAsync_WhenNotFound_ReturnsNotFound()
    {
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
            ClientId,
            "missing",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task DeleteSecretAsync_AuthorizesAgainstTenantScopedResource()
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
            ClientId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        var scoped = Assert.IsType<TenantOwnedResource<PersistedSecret>>(capturedResource);
        Assert.Equal(TenantId, scoped.TenantId);
        Assert.Equal("secret-1", scoped.Value.SecretId);
    }

    #endregion

    #region RotateSecretAsync Tests

    [Fact]
    public async Task RotateSecretAsync_WhenAuthorized_ReplacesTheSecretAndRevealsTheReplacement()
    {
        SetupStore();
        var rotated = CreatePersistedSecret("secret-1", "rotated-ct");
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct", EmptyObject(), rotated))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceId("secret-2");

        var expiresWhen = DateTimeOffset.UnixEpoch.AddYears(3);
        var replacement = CreatePersistedSecret("secret-2", "replacement-ct");
        MockSecretGenerator
            .Setup(x =>
                x.GenerateSharedSecret(
                    It.Is<GenerateSecretRequest>(generate =>
                        generate.SecretId == "secret-2"
                        && generate.SecretType == rotated.SecretType
                        && generate.KeySizeBits == rotated.KeySizeBits
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
        MockClientStore
            .Setup(x => x.RemoveSecretAsync(ClientId, "secret-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
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
            Times.Once
        );
    }

    [Fact]
    public async Task RotateSecretAsync_WhenSecretNotFound_ReturnsNotFound()
    {
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
    public async Task RotateSecretAsync_WhenNotAuthorized_ReturnsForbidden()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct", EmptyObject(), CreatePersistedSecret()))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

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

        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task RotateSecretAsync_WhenIfMatchDiffers_ReturnsPreconditionFailed()
    {
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

    #region DeleteClientAsync Tests

    [Fact]
    public async Task DeleteClientAsync_WhenPreconditionsPass_ReturnsNoContent()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        MockClientValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<ClaimsPrincipal>(),
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

        var result = await Handler.DeleteClientAsync(httpContext, ClientId, CancellationToken.None);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task DeleteClientAsync_WhenValidatorReportsConflict_ReturnsProblem()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient("client-ct"))
            .Verifiable();
        MockClientValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<ClaimsPrincipal>(),
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

        var result = await Handler.DeleteClientAsync(httpContext, ClientId, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteClientAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteClientAsync(httpContext, ClientId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    #endregion
}
