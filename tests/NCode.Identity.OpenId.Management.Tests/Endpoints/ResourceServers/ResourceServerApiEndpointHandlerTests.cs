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
using NCode.Identity.Cryptography;
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
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using SystemTextJsonPatch;

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
    private Mock<ISecretImporter> MockSecretImporter { get; }
    private Mock<ISecretPublicKeyConverter> MockSecretPublicKeyConverter { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<IOpenIdServerProvider> MockServerProvider { get; }
    private Mock<ISecurityProfileCatalog> MockSecurityProfileCatalog { get; }
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
        MockSecretImporter = MockRepository.Create<ISecretImporter>();
        MockSecretPublicKeyConverter = MockRepository.Create<ISecretPublicKeyConverter>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockServerProvider = MockRepository.Create<IOpenIdServerProvider>();
        MockSecurityProfileCatalog = MockRepository.Create<ISecurityProfileCatalog>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
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
                x.RecordResourceServerChangedAsync(
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
            .Returns(ValueTask.CompletedTask);
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
            .Returns(ValueTask.CompletedTask);

        Handler = new ResourceServerApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockValidator.Object,
            MockSecretValidator.Object,
            MockSecretGenerator.Object,
            MockSecretImporter.Object,
            MockSecretPublicKeyConverter.Object,
            TimeProvider.System,
            MockCryptoService.Object,
            MockSettingSerializer.Object,
            MockServerProvider.Object,
            MockSecurityProfileCatalog.Object,
            MockResourceOwnershipService.Object,
            MockManagementAuditRecorder.Object,
            NullLogger<ResourceServerApiEndpointHandler>.Instance
        );
    }

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
                x.ValidateSettings(It.IsAny<JsonElement>(), It.IsAny<JsonSerializerOptions>())
            )
            .Returns([])
            .Verifiable();

    private void SetupInvalidSettings() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettings(It.IsAny<JsonElement>(), It.IsAny<JsonSerializerOptions>())
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
        SetupValidSettings();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
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
    public async Task CreateAsync_WhenSettingsCannotBeHonored_ReturnsValidationProblem()
    {
        SetupInvalidSettings();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
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
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PersistedTenantSettings
                {
                    TenantId = TenantId,
                    ConcurrencyToken = "tenant-ct",
                    Value = JsonSerializer.SerializeToElement(
                        new Dictionary<string, object> { ["scope"] = "tenant" }
                    ),
                }
            )
            .Verifiable();
        var descriptor = new SettingDescriptor<bool> { Name = "require_pkce" };
        IReadOnlyCollection<Setting> tenantScopeSettings = [descriptor.Create(true)];
        IReadOnlyCollection<Setting> resourceServerScopeSettings = [];
        MockSecurityProfileCatalog
            .Setup(x =>
                x.Expand(
                    It.Is<JsonElement>(element => IsTenantDocument(element)),
                    It.IsAny<JsonSerializerOptions>()
                )
            )
            .Returns(tenantScopeSettings)
            .Verifiable();
        MockSettingSerializer
            .Setup(x =>
                x.DeserializeSettings(
                    It.Is<JsonElement>(element => !IsTenantDocument(element)),
                    It.IsAny<JsonSerializerOptions>()
                )
            )
            .Returns(resourceServerScopeSettings)
            .Verifiable();
        SetupServerProvider(tenantScopeSettings, resourceServerScopeSettings);

        var result = await Handler.GetEffectiveSettingsAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<ResourceServerEffectiveSettingsResource>>(result);
        Assert.True(json.Value!.Settings.GetProperty("require_pkce").GetBoolean());
    }

    private static bool IsTenantDocument(JsonElement element) =>
        element.TryGetProperty("scope", out _);

    private void SetupServerProvider(
        IReadOnlyCollection<Setting> tenantScopeSettings,
        IReadOnlyCollection<Setting> resourceServerScopeSettings
    )
    {
        var effective = new Mock<ISettingCollection>(MockBehavior.Loose);
        effective.Setup(x => x.GetEnumerator()).Returns(() => tenantScopeSettings.GetEnumerator());

        var tenantEffective = new Mock<ISettingCollection>(MockBehavior.Loose);
        tenantEffective.Setup(x => x.Merge(resourceServerScopeSettings)).Returns(effective.Object);

        var serverCollection = new Mock<IReadOnlySettingCollection>(MockBehavior.Loose);
        serverCollection.Setup(x => x.Merge(tenantScopeSettings)).Returns(tenantEffective.Object);

        var provider = new Mock<IReadOnlySettingCollectionProvider>(MockBehavior.Loose);
        provider.Setup(x => x.Collection).Returns(serverCollection.Object);

        var server = new Mock<OpenIdServer>(MockBehavior.Loose);
        server.Setup(x => x.SettingsProvider).Returns(provider.Object);

        MockServerProvider
            .Setup(x => x.GetAsync(It.IsAny<OpenIdEnvironment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(server.Object)
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
            SecretType = "rsa",
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.Pkcs8,
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

    [Fact]
    public async Task UpdateSettingsAsync_WhenAuthorized_ReturnsNoContent()
    {
        SetupValidSettings();
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();
        MockSecretValidator
            .Setup(x => x.ValidateCreate(It.IsAny<CreateSecretRequest>()))
            .Returns((ManagementError?)null)
            .Verifiable();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
            .Returns("secret-1")
            .Verifiable();

        var generated = CreatePersistedSecret();
        MockSecretGenerator
            .Setup(x => x.GenerateSecret(It.IsAny<GenerateSecretRequest>()))
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
    public async Task DeleteSecretAsync_WhenAuthorized_ReturnsNoContent()
    {
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
}
