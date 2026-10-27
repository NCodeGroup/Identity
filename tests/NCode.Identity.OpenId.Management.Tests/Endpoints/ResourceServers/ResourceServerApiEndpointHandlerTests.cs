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
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
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
                new SettingError("tenant_issuer", "'tenant_issuer' is not a registered setting."),
            ])
            .Verifiable();

    private void SetupValidSettingsUpdate() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettingsUpdate(
                    It.IsAny<JsonElement>(),
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.ResourceServer,
                    It.IsAny<JsonSerializerOptions>()
                )
            )
            .Returns([])
            .Verifiable();

    private void SetupInvalidSettingsUpdate() =>
        MockSettingSerializer
            .Setup(x =>
                x.ValidateSettingsUpdate(
                    It.IsAny<JsonElement>(),
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.ResourceServer,
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

        // A client setting does not govern a resource server, so its preview omits it (ADR-0149).
        Assert.False(json.Value.Settings.TryGetProperty("require_pkce", out _));
    }

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenForbidden_ReturnsForbidWithoutResolvingTheSettings()
    {
        SetupStore();
        MockStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetEffectiveSettingsAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        // No tenant store or resolver is set up, so a strict-mock failure would mean the settings were resolved before
        // authorization.
        Assert.IsType<ForbidHttpResult>(result);
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

        var serverSettings = MockRepository.Create<IReadOnlySettingCollection>().Object;
        var tenantSettings = MockRepository.Create<IReadOnlySettingCollection>().Object;

        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverSettings)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    serverSettings,
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
        SetupValidSettingsUpdate();
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
        SetupInvalidSettingsUpdate();
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
    public async Task GetPublicKeyAsync_WhenForbidden_ReturnsForbidWithoutProtector()
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
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.GetPublicKeyAsync(
            CreateHttpContext(),
            TenantId,
            ResourceServerId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
    }
}
