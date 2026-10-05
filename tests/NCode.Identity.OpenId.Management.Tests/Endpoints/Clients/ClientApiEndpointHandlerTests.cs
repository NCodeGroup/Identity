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
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Clients;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Operations;
using Xunit;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

public sealed class ClientApiEndpointHandlerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ISecretGenerator> MockSecretGenerator { get; }
    private Mock<IClientValidator> MockClientValidator { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<IOpenIdServerProvider> MockServerProvider { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IResourceOwnershipService> MockResourceOwnershipService { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private ClientApiEndpointHandler Handler { get; }

    public ClientApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockClientStore = MockRepository.Create<IClientStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockSecretGenerator = MockRepository.Create<ISecretGenerator>();
        MockClientValidator = MockRepository.Create<IClientValidator>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockServerProvider = MockRepository.Create<IOpenIdServerProvider>();
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
            MockSecretGenerator.Object,
            TimeProvider.System,
            MockCryptoService.Object,
            MockSettingSerializer.Object,
            MockServerProvider.Object,
            MockResourceOwnershipService.Object,
            MockManagementAuditRecorder.Object,
            NullLogger<ClientApiEndpointHandler>.Instance
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
                )
            )
        );
        return httpContext;
    }

    private static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new JsonObject());

    private static PersistedClientSettings CreateSettings(string concurrencyToken) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = concurrencyToken,
            Value = EmptyObject(),
        };

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
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = concurrencyToken,
            IsDisabled = false,
            Settings = CreateSettings("settings-ct"),
            Secrets = CreateSecretsCollection("secrets-ct"),
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
    }

    #endregion

    #region ListClientsAsync Tests

    [Fact]
    public async Task ListClientsAsync_WhenAuthorized_ReturnsPage()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockClientStore
            .Setup(x => x.GetPageAsync(null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
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
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<ClientResource>>>(result);
        Assert.Single(json.Value!.Items);
        Assert.Equal("next", json.Value.ContinuationToken);
    }

    [Fact]
    public async Task ListClientsAsync_WhenForbidden_ReturnsForbidWithoutQueryingStore()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListClientsAsync(
            httpContext,
            TenantId,
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
        MockSettingSerializer
            .Setup(x =>
                x.DeserializeSettings(It.IsAny<JsonElement>(), It.IsAny<JsonSerializerOptions>())
            )
            .Returns(Array.Empty<Setting>())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var descriptor = new SettingDescriptor<bool> { Name = "claims_parameter_supported" };
        var effective = CreateSettingCollection(descriptor.Create(true));
        SetupServerProvider(effective);
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
            .Setup(x => x.GenerateSecret(It.IsAny<GenerateSecretRequest>()))
            .Returns(generated)
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

        var created = Assert.IsType<Created<SecretResource>>(result);
        Assert.Equal("secret-1", created.Value?.SecretId);
        Assert.Equal("secret-ct", httpContext.Response.Headers.ETag);
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
