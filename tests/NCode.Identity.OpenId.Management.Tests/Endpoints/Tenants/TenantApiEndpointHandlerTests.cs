#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
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

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ISecretGenerator> MockSecretGenerator { get; }
    private Mock<IClientKeyImporter> MockKeyImporter { get; }
    private Mock<ISecretPublicKeyConverter> MockSecretPublicKeyConverter { get; }
    private Mock<ISecretProtectorProvider> MockSecretProtectorProvider { get; }
    private Mock<ITenantValidator> MockTenantValidator { get; }
    private Mock<ITenantResolver> MockTenantResolver { get; }
    private Mock<ITenantProvisioner> MockTenantProvisioner { get; }
    private Mock<ISecretValidator> MockSecretValidator { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<ISettingScopeCollectionProvider> MockSettingScopeCollectionProvider { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
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
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockSecretGenerator = MockRepository.Create<ISecretGenerator>();
        MockKeyImporter = MockRepository.Create<IClientKeyImporter>();
        MockSecretPublicKeyConverter = MockRepository.Create<ISecretPublicKeyConverter>();
        MockSecretProtectorProvider = MockRepository.Create<ISecretProtectorProvider>();
        MockTenantValidator = MockRepository.Create<ITenantValidator>();
        MockTenantResolver = MockRepository.Create<ITenantResolver>();
        MockTenantProvisioner = MockRepository.Create<ITenantProvisioner>();
        MockSecretValidator = MockRepository.Create<ISecretValidator>();
        MockSecretValidator
            .Setup(x => x.ValidateCreate(It.IsAny<CreateSecretRequest>()))
            .Returns((ManagementError?)null);
        MockSecretValidator
            .Setup(x =>
                x.ValidateUpdate(It.IsAny<PersistedSecret>(), It.IsAny<UpdateSecretRequest>())
            )
            .Returns((ManagementError?)null);
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockSettingSerializer = MockRepository.Create<ISettingSerializer>();
        MockSettingScopeCollectionProvider =
            MockRepository.Create<ISettingScopeCollectionProvider>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
        MockTenantClientCoherenceChecker = MockRepository.Create<ITenantClientCoherenceChecker>();
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
                x.RecordTenantChangedAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask);
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordTenantSecretChangedAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask);

        // A write announces the change after it commits; the tests that write verify the announcement.
        MockMediator = MockRepository.Create<IMediator>();
        MockMediator
            .Setup(x =>
                x.SendAsync(It.IsAny<TenantChangedCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask);

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
            TimeProvider.System,
            MockCryptoService.Object,
            MockSettingSerializer.Object,
            MockSettingScopeCollectionProvider.Object,
            MockEffectiveSettingsResolver.Object,
            MockTenantClientCoherenceChecker.Object,
            MockResourceOwnershipService.Object,
            MockManagementAuditRecorder.Object,
            NullLogger<TenantApiEndpointHandler>.Instance
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
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
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
                    OpenIdSettingScopes.Tenant,
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
                    OpenIdSettingScopes.Tenant,
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

    private static PersistedTenant CreateTenant(string concurrencyToken, string? issuer = null) =>
        new()
        {
            TenantId = TenantId,
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

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetTenantAsync(httpContext, TenantId, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<TenantResource>>(result);
        Assert.Equal(TenantId, json.Value?.TenantId);
        Assert.Equal("Tenant One", json.Value?.DisplayName);
        Assert.Equal("tenant-ct", httpContext.Response.Headers.ETag);
    }

    [Fact]
    public async Task GetTenantAsync_WhenNotFound_ReturnsNotFound()
    {
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
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: false);

        var result = await Handler.GetTenantAsync(httpContext, TenantId, CancellationToken.None);

        Assert.IsType<UnauthorizedHttpResult>(result);
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

        Assert.IsType<UnauthorizedHttpResult>(result);
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
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenantSettings?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSettingsAsync(httpContext, TenantId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    #endregion

    #region GetEffectiveSettingsAsync Tests

    [Fact]
    public async Task GetEffectiveSettingsAsync_WhenFoundAndAuthorized_ReturnsFlatSettings()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct"))
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
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenantSettings?)null)
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
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContextWithEnvironment(authenticated: false);

        var result = await Handler.GetEffectiveSettingsAsync(
            httpContext,
            TenantId,
            CancellationToken.None
        );

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    private void SetupEffectiveSettingsResolver(ISettingCollection effectiveSettings)
    {
        var serverSettings = MockRepository.Create<IReadOnlySettingCollection>();

        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverSettings.Object)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    serverSettings.Object,
                    OpenIdSettingScopes.Tenant,
                    TenantId,
                    It.IsAny<JsonElement>()
                )
            )
            .Returns(effectiveSettings)
            .Verifiable();
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
        var serverSettings = MockRepository.Create<IReadOnlySettingCollection>();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverSettings.Object)
            .Verifiable();

        var currentEffective = CreateSettingCollection();
        var newEffective = CreateSettingCollection();

        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    serverSettings.Object,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => !HasProperty(e, "foo"))
                )
            )
            .Returns(currentEffective)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    serverSettings.Object,
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
        SetupValidSettings();
        SetupStore();
        var settings = CreateSettings("settings-ct");
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
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
    public async Task UpdateSettingsAsync_WhenTheReplacedDocumentCannotBeHonored_WritesWithoutCheckingTheClients()
    {
        SetupValidSettings();
        SetupStore();
        var settings = CreateSettings("settings-ct");
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var serverSettings = MockRepository.Create<IReadOnlySettingCollection>();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverSettings.Object)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    serverSettings.Object,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => HasProperty(e, "foo"))
                )
            )
            .Returns(CreateSettingCollection())
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    serverSettings.Object,
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
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct"))
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
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetSettingsOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSettings("settings-ct"))
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
            CreateAddPatch("tenant_issuer", "bar"),
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

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretsAsync(httpContext, TenantId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
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
    public async Task CreateSecretAsync_WhenTenantNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

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
    public async Task CreateSecretAsync_WhenDuplicate_ReturnsConflict()
    {
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
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
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

    private ISecretProtector SetupSecretProtector()
    {
        var protector = MockRepository.Create<ISecretProtector>().Object;
        MockSecretProtectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForTenant(TenantId),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(protector)
            .Verifiable();
        return protector;
    }

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
        var protector = SetupSecretProtector();
        SetupAuthorization(AuthorizationResult.Success());
        MockSecretPublicKeyConverter
            .Setup(x => x.Convert(protector, It.IsAny<PersistedSecret>()))
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
        var protector = SetupSecretProtector();
        SetupAuthorization(AuthorizationResult.Success());
        MockSecretPublicKeyConverter
            .Setup(x => x.Convert(protector, It.IsAny<PersistedSecret>()))
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
    public async Task GetPublicKeyAsync_WhenForbidden_ReturnsForbidWithoutProtector()
    {
        SetupStore();
        MockTenantStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(TenantId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetPublicKeyAsync(
            httpContext,
            TenantId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
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
    public async Task DeleteSecretAsync_WhenAuthorized_ReturnsNoContent()
    {
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
    public async Task DeleteSecretAsync_AuthorizesAgainstTenantScopedResource()
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
        var scoped = Assert.IsType<TenantOwnedResource<PersistedSecret>>(capturedResource);
        Assert.Equal(TenantId, scoped.TenantId);
        Assert.Equal("secret-1", scoped.Value.SecretId);
    }

    #endregion

    #region DeleteTenantAsync Tests

    [Fact]
    public async Task DeleteTenantAsync_WhenPreconditionsPass_ReturnsNoContent()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        MockTenantValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<ClaimsPrincipal>(),
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

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteTenantAsync(httpContext, TenantId, CancellationToken.None);

        Assert.IsType<NoContent>(result);
        VerifyTenantChanged(settingsChanged: false);
    }

    [Fact]
    public async Task DeleteTenantAsync_WhenValidatorReportsConflict_ReturnsProblem()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant("tenant-ct"))
            .Verifiable();
        MockTenantValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<ClaimsPrincipal>(),
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
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteTenantAsync(httpContext, TenantId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
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
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<PersistedTenant>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ClaimsPrincipal _, PersistedTenant tenant, IStoreManager _, CancellationToken _) =>
                    validated = tenant
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        return assert => assert(validated);
    }

    private void SetupCreatePersistence(PersistedTenant reloaded)
    {
        MockTenantStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedTenant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTenantProvisioner
            .Setup(x => x.SeedAsync("acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reloaded)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync("acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reloaded)
            .Verifiable();
    }

    private static PersistedTenant CreateSeededTenant(string issuer) =>
        new()
        {
            TenantId = "acme",
            ConcurrencyToken = "seeded-ct",
            Issuer = issuer,
            IsDisabled = false,
            DisplayName = "Tenant One",
            Settings = CreateSettings("settings-ct"),
            Secrets = CreateSecretsCollection("secrets-ct"),
        };

    [Fact]
    public async Task CreateTenantAsync_WhenIssuerIsOmitted_CreatesTheTenantAtItsDerivedIssuer()
    {
        SetupDefaultAlias("acme", alias: null);
        SetupValidSettings();
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        var assertValidated = SetupCreateValidation();
        SetupCreatePersistence(CreateSeededTenant(DerivedIssuer));

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateTenantAsync(
            httpContext,
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
        const string derivedAlias = "https://acme.mtls.example";

        SetupValidSettings();
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        SetupDefaultAlias("acme", derivedAlias);
        var assertValidated = SetupCreateValidation();
        SetupCreatePersistence(CreateSeededTenant(DerivedIssuer));

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateTenantAsync(
            httpContext,
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
        SetupDefaultAlias("acme", alias: null);
        const string issuer = "https://acme.example";

        SetupValidSettings();
        SetupStore();
        var assertValidated = SetupCreateValidation();
        SetupCreatePersistence(CreateSeededTenant(issuer));

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: no GetDefaultIssuerAsync setup, so deriving an issuer would fail the test.
        var result = await Handler.CreateTenantAsync(
            httpContext,
            CreateTenantRequest(issuer),
            CancellationToken.None
        );

        Assert.IsType<Created<TenantResource>>(result);
        assertValidated(validated => Assert.Equal(issuer, validated?.Issuer));
    }

    [Fact]
    public async Task CreateTenantAsync_WhenCreated_SeedsTheTenantAndReturnsItAsSeeded()
    {
        SetupDefaultAlias("acme", alias: null);
        SetupValidSettings();
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        SetupCreateValidation();
        SetupCreatePersistence(CreateSeededTenant(DerivedIssuer));

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateTenantAsync(
            httpContext,
            CreateTenantRequest(),
            CancellationToken.None
        );

        // A seed contributor may replace the row's concurrency token, so the response carries the reloaded one.
        var created = Assert.IsType<Created<TenantResource>>(result);
        Assert.NotNull(created.Value);
        Assert.Equal("seeded-ct", created.Value.ConcurrencyToken);
        Assert.Equal("seeded-ct", httpContext.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task CreateTenantAsync_WhenSettingsCannotBeHonored_ReturnsValidationProblem()
    {
        SetupDefaultAlias("acme", alias: null);
        SetupInvalidSettings();
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        MockTenantValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
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
            CreateTenantRequest(),
            CancellationToken.None
        );

        AssertSettingsValidationProblem(result);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenValidatorReportsConflict_ReturnsProblem()
    {
        SetupDefaultAlias("acme", alias: null);
        SetupStore();
        SetupDefaultIssuer("acme", DerivedIssuer);
        MockTenantValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
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
            CreateTenantRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    private PersistedTenant SetupUpdate(PersistedTenant tenant, Action<UpdateTenantRequest> capture)
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant)
            .Verifiable();
        MockTenantValidator
            .Setup(x =>
                x.ValidateUpdateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<PersistedTenant>(),
                    It.IsAny<UpdateTenantRequest>(),
                    It.IsAny<string?>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    ClaimsPrincipal _,
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

    [Fact]
    public async Task GetAddressAsync_WhenTenantDoesNotExist_PreviewsItsDerivedIssuerUnderCreateAuthority()
    {
        SetupDefaultAlias("acme", alias: null);
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync("acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
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
                (ClaimsPrincipal _, object? _, IEnumerable<IAuthorizationRequirement> value) =>
                    requirements = value
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();
        SetupDefaultIssuer("acme", DerivedIssuer);
        SetupAddressValidation("acme", DerivedIssuer);

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetAddressAsync(
            httpContext,
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
        Assert.Same(Operations.Create, Assert.Single(requirements!));
    }

    [Theory]
    [InlineData(null, "https://acme.mtls.example", true)]
    [InlineData("https://other.mtls.example", "https://other.mtls.example", false)]
    public async Task GetAddressAsync_WhenTenantDoesNotExist_PreviewsTheProposedOrDerivedAlias(
        string? proposedAlias,
        string expectedAlias,
        bool expectedDerived
    )
    {
        if (proposedAlias is null)
            SetupDefaultAlias("acme", "https://acme.mtls.example");
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync("acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
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

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetAddressAsync(
            httpContext,
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
    public async Task GetAddressAsync_WhenNotAuthorized_ReturnsForbidden()
    {
        SetupStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync("acme", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        // Strict mocks: neither the derived issuer nor the address rules are consulted for an unauthorized caller.
        var result = await Handler.GetAddressAsync(
            httpContext,
            "acme",
            issuer: null,
            mtlsEndpointAliasAuthority: null,
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
    }

    #endregion
}
