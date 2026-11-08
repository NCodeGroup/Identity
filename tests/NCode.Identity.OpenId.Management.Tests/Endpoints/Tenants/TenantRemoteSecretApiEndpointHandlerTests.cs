// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

public sealed class TenantRemoteSecretApiEndpointHandlerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ControlPlaneTenantId = "platform-root";
    private const string Provider = "aws-kms";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ISecretValidator> MockSecretValidator { get; }
    private Mock<ISecretGenerator> MockSecretGenerator { get; }
    private Mock<IRemoteSecretImporter> MockRemoteSecretImporter { get; }
    private Mock<IRemoteSecretDestroyer> MockRemoteSecretDestroyer { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private Mock<IMediator> MockMediator { get; }
    private Mock<ITenantProvisioner> MockTenantProvisioner { get; }

    public TenantRemoteSecretApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockSecretValidator = MockRepository.Create<ISecretValidator>();
        MockSecretGenerator = MockRepository.Create<ISecretGenerator>();
        MockRemoteSecretImporter = MockRepository.Create<IRemoteSecretImporter>();
        MockRemoteSecretDestroyer = MockRepository.Create<IRemoteSecretDestroyer>();
        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();

        // A write announces the change after it commits; the tests that write verify the announcement.
        MockMediator = MockRepository.Create<IMediator>();
        MockTenantProvisioner = MockRepository.Create<ITenantProvisioner>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private TenantRemoteSecretApiEndpointHandler CreateHandler() =>
        new(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockCryptoService.Object,
            MockSecretValidator.Object,
            MockSecretGenerator.Object,
            MockRemoteSecretImporter.Object,
            MockRemoteSecretDestroyer.Object,
            MockManagementAuditRecorder.Object,
            TimeProvider.System,
            NullLogger<TenantRemoteSecretApiEndpointHandler>.Instance
        );

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

    private void SetupTenant(PersistedTenant? tenant) =>
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant)
            .Verifiable();

    private void SetupSave() =>
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupAuthorization(
        IAuthorizationRequirement operation,
        AuthorizationResult result
    ) =>
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                        requirements.Contains(operation)
                    )
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    private void SetupResourceId(string id = "generated-id")
    {
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns(id)
            .Verifiable();
    }

    // Only a request that is coherent resolves the parameters the key is generated with.
    private void SetupCreateRemoteValidation(SecretCreateValidation validation) =>
        MockSecretValidator
            .Setup(x =>
                x.ValidateCreateRemote(
                    It.IsAny<CreateRemoteSecretRequest>(),
                    It.IsAny<DateTimeOffset>()
                )
            )
            .Returns(validation)
            .Verifiable();

    private void SetupValidCreateRemoteValidation() =>
        SetupCreateRemoteValidation(
            SecretCreateValidation.Valid(
                new SecretKeyGenerationParameters { KeySizeBits = 256, Curve = "P-256" }
            )
        );

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

    // Only a request that changes the tenant announces the change to every replica.
    private void SetupTenantChangedAnnouncement()
    {
        var command = new TenantChangedCommand(TenantId, false);
        MockMediator
            .Setup(x => x.SendAsync(command, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    private HttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
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

    private static PersistedTenant CreateTenant() =>
        new PersistedTenantBuilder
        {
            TenantId = TenantId,
            ConcurrencyToken = "tenant-ct",
            DisplayName = "Tenant One",
            SettingsConcurrencyToken = "settings-ct",
            SecretsConcurrencyToken = "secrets-ct",
        }.Build();

    private static PersistedSecret CreateRemoteSecret(
        string secretId = "secret-1",
        string? keyId = null
    ) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            KeyId = keyId,
            ConcurrencyToken = "secret-ct",
            Use = "sig",
            Algorithm = "ES256",
            SecretType = SecretTypes.Ecc,
            SecretEncoding = SecretEncodings.Remote,
            EncodedValue = $"{Provider}:key-1",
        }.Build();

    private static CreateRemoteSecretRequest CreateRemoteSecretRequest() =>
        new()
        {
            RemoteKeyProvider = Provider,
            SecretType = SecretTypes.Ecc,
            KeySizeBits = 256,
            Algorithm = "ES256",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    private static ImportRemoteSecretRequest CreateImportRemoteSecretRequest() =>
        new()
        {
            RemoteKeyProvider = Provider,
            RemoteKeyId = "key-1",
            SecretType = SecretTypes.Ecc,
            Format = "jwk",
            KeyMaterial = """{"kty":"EC","crv":"P-256","x":"AQAB","y":"AQAB"}""",
            Algorithm = "ES256",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    private void SetupImportRemoteValidation(ManagementError? error = null) =>
        MockSecretValidator
            .Setup(x =>
                x.ValidateImportRemote(
                    It.IsAny<ImportRemoteSecretRequest>(),
                    It.IsAny<DateTimeOffset>()
                )
            )
            .Returns(error)
            .Verifiable();

    // Moq cannot capture beside out arguments, so the import request is matched by what the handler builds from the request.
    private void SetupTryImport(PersistedSecret imported)
    {
        var importedSecret = (PersistedSecret?)imported;
        string? error = null;
        MockRemoteSecretImporter
            .Setup(x =>
                x.TryImport(
                    It.Is<RemoteSecretImportRequest>(request =>
                        request.Key.SecretId == "generated-id"
                        && request.Key.SecretType == SecretTypes.Ecc
                        && request.Key.Use == SecretKeyUses.Signature
                        && request.Key.Algorithm == "ES256"
                        && request.Reference == new RemoteKeyReference(Provider, "key-1")
                    ),
                    out importedSecret,
                    out error
                )
            )
            .Returns(true)
            .Verifiable();
    }

    private void SetupVerifyBinding(PersistedSecret secret, ManagementError? error) =>
        MockRemoteSecretImporter
            .Setup(x => x.VerifyBindingAsync(secret, It.IsAny<CancellationToken>()))
            .ReturnsAsync(error)
            .Verifiable();

    #endregion

    #region CreateRemoteSecretAsync Tests

    [Fact]
    public async Task CreateRemoteSecretAsync_WhenAuthorized_CreatesTheKeyAndReturnsCreated()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant());
        SetupValidCreateRemoteValidation();
        SetupResourceId();

        var secret = CreateRemoteSecret();
        GenerateRemoteSecretRequest? generateRequest = null;
        MockSecretGenerator
            .Setup(x =>
                x.GenerateRemoteSecretAsync(
                    It.IsAny<GenerateRemoteSecretRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (GenerateRemoteSecretRequest request, CancellationToken _) =>
                    generateRequest = request
            )
            .ReturnsAsync(secret)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.AddSecretAsync(TenantId, secret, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupSave();
        SetupTenantSecretChangedAudit();
        SetupTenantChangedAnnouncement();

        var httpContext = CreateHttpContext();

        var result = await CreateHandler()
            .CreateRemoteSecretAsync(
                httpContext,
                TenantId,
                CreateRemoteSecretRequest(),
                CancellationToken.None
            );

        var created = Assert.IsType<Created<SecretResource>>(result);
        Assert.Equal("secret-1", created.Value?.SecretId);
        Assert.Equal(Provider, created.Value?.RemoteKeyProvider);
        Assert.Equal("secret-ct", httpContext.Response.Headers.ETag);
        Assert.NotNull(generateRequest);
        Assert.Equal("generated-id", generateRequest.SecretId);
        Assert.Equal(SecretOwner.ForTenant(TenantId), generateRequest.Owner);
        Assert.Equal(Provider, generateRequest.Provider);
        Assert.Equal(SecretTypes.Ecc, generateRequest.SecretType);
        Assert.Equal(SecretKeyUses.Signature, generateRequest.Use);
        Assert.Equal("ES256", generateRequest.Algorithm);
        Assert.Equal(256, generateRequest.KeySizeBits);
        Assert.Equal("P-256", generateRequest.Curve);
        Assert.Null(generateRequest.ManagedBy);
    }

    [Fact]
    public async Task CreateRemoteSecretAsync_WhenForbidden_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Failed());

        var result = await CreateHandler()
            .CreateRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task CreateRemoteSecretAsync_WhenTheTenantIsMissing_ReturnsNotFound()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(null);

        var result = await CreateHandler()
            .CreateRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateRemoteSecretRequest(),
                CancellationToken.None
            );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task CreateRemoteSecretAsync_WhenTheRequestIsInvalid_ReturnsBadRequestWithoutCallingTheProvider()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant());
        SetupCreateRemoteValidation(
            SecretCreateValidation.Invalid(
                new ManagementError
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Detail = "The remote key provider 'aws-kms' is not registered.",
                }
            )
        );

        var result = await CreateHandler()
            .CreateRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task CreateRemoteSecretAsync_WhenTheGeneratorRefuses_ReturnsBadRequest()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant());
        SetupValidCreateRemoteValidation();
        SetupResourceId();
        MockSecretGenerator
            .Setup(x =>
                x.GenerateRemoteSecretAsync(
                    It.IsAny<GenerateRemoteSecretRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(new InvalidOperationException("refused"))
            .Verifiable();

        var result = await CreateHandler()
            .CreateRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task CreateRemoteSecretAsync_WhenTheProviderFails_ReturnsBadGateway()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant());
        SetupValidCreateRemoteValidation();
        SetupResourceId();
        MockSecretGenerator
            .Setup(x =>
                x.GenerateRemoteSecretAsync(
                    It.IsAny<GenerateRemoteSecretRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(new IOException("unreachable"))
            .Verifiable();

        var result = await CreateHandler()
            .CreateRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, problem.StatusCode);
    }

    [Fact]
    public async Task CreateRemoteSecretAsync_WhenTheSecretIdIsTaken_ReturnsConflictWithoutCallingTheProvider()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(
            new PersistedTenantBuilder
            {
                TenantId = TenantId,
                ConcurrencyToken = "tenant-ct",
                DisplayName = "Tenant One",
                SettingsConcurrencyToken = "settings-ct",
                SecretsConcurrencyToken = "secrets-ct",
                Secrets = [CreateRemoteSecret("generated-id")],
            }.Build()
        );
        SetupValidCreateRemoteValidation();
        SetupResourceId();

        var result = await CreateHandler()
            .CreateRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal(
            "A secret with the specified identifier already exists.",
            problem.ProblemDetails.Detail
        );
    }

    [Fact]
    public async Task CreateRemoteSecretAsync_WhenTheStoreRefusesTheRecord_DestroysTheKeyAndRethrows()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant());
        SetupValidCreateRemoteValidation();
        SetupResourceId();

        var secret = CreateRemoteSecret();
        MockSecretGenerator
            .Setup(x =>
                x.GenerateRemoteSecretAsync(
                    It.IsAny<GenerateRemoteSecretRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(secret)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.AddSecretAsync(TenantId, secret, It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("refused"))
            .Verifiable();
        MockRemoteSecretDestroyer
            .Setup(x =>
                x.DestroyAsync(
                    It.Is<IReadOnlyCollection<PersistedSecret>>(removed =>
                        removed.Count == 1 && ReferenceEquals(removed.Single(), secret)
                    )
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateHandler()
                .CreateRemoteSecretAsync(
                    CreateHttpContext(),
                    TenantId,
                    CreateRemoteSecretRequest(),
                    CancellationToken.None
                )
        );
    }

    [Fact]
    public async Task CreateRemoteSecretAsync_WhenTheCommitFails_DestroysTheKeyAndRethrows()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant());
        SetupValidCreateRemoteValidation();
        SetupResourceId();

        var secret = CreateRemoteSecret();
        MockSecretGenerator
            .Setup(x =>
                x.GenerateRemoteSecretAsync(
                    It.IsAny<GenerateRemoteSecretRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(secret)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.AddSecretAsync(TenantId, secret, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Throws(new IOException("commit failed"))
            .Verifiable();
        MockRemoteSecretDestroyer
            .Setup(x =>
                x.DestroyAsync(
                    It.Is<IReadOnlyCollection<PersistedSecret>>(removed =>
                        removed.Count == 1 && ReferenceEquals(removed.Single(), secret)
                    )
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Assert.ThrowsAsync<IOException>(async () =>
            await CreateHandler()
                .CreateRemoteSecretAsync(
                    CreateHttpContext(),
                    TenantId,
                    CreateRemoteSecretRequest(),
                    CancellationToken.None
                )
        );
    }

    #endregion

    #region ImportRemoteSecretAsync Tests

    [Fact]
    public async Task ImportRemoteSecretAsync_WhenAuthorized_BindsTheKeyAndReturnsCreated()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupAuthorization(Operations.Update, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant());
        SetupImportRemoteValidation();
        SetupResourceId();

        var secret = CreateRemoteSecret();
        SetupTryImport(secret);
        SetupVerifyBinding(secret, null);
        MockTenantStore
            .Setup(x => x.AddSecretAsync(TenantId, secret, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupSave();
        SetupTenantSecretChangedAudit();
        SetupTenantChangedAnnouncement();

        var httpContext = CreateHttpContext();

        var result = await CreateHandler()
            .ImportRemoteSecretAsync(
                httpContext,
                TenantId,
                CreateImportRemoteSecretRequest(),
                CancellationToken.None
            );

        var created = Assert.IsType<Created<SecretResource>>(result);
        Assert.Equal("secret-1", created.Value?.SecretId);
        Assert.Equal(Provider, created.Value?.RemoteKeyProvider);
        Assert.Equal("secret-ct", httpContext.Response.Headers.ETag);
    }

    [Fact]
    public async Task ImportRemoteSecretAsync_WhenTheTenantIsMissing_ReturnsNotFound()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupAuthorization(Operations.Update, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(null);

        var result = await CreateHandler()
            .ImportRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateImportRemoteSecretRequest(),
                CancellationToken.None
            );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task ImportRemoteSecretAsync_WhenTheRequestIsInvalid_ReturnsBadRequestWithoutImporting()
    {
        const string detail = "The key management service's key identifier must not be empty.";
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupAuthorization(Operations.Update, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant());
        SetupImportRemoteValidation(
            new ManagementError { StatusCode = StatusCodes.Status400BadRequest, Detail = detail }
        );

        var result = await CreateHandler()
            .ImportRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateImportRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal(detail, problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task ImportRemoteSecretAsync_WhenTheCallerMayNotUpdateThePlatform_ReturnsForbiddenWithoutReadingTheTenant()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupAuthorization(Operations.Update, AuthorizationResult.Failed());

        var result = await CreateHandler()
            .ImportRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateImportRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task ImportRemoteSecretAsync_WhenForbiddenAtTheTenant_ReturnsForbiddenWithoutAuthorizingAtThePlatform()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Failed());

        var result = await CreateHandler()
            .ImportRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateImportRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task ImportRemoteSecretAsync_WhenTheImportIsRefused_ReturnsBadRequest()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupAuthorization(Operations.Update, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant());
        SetupImportRemoteValidation();
        SetupResourceId();

        PersistedSecret? refusedSecret = null;
        var refusal = "The key is not a public key.";
        MockRemoteSecretImporter
            .Setup(x =>
                x.TryImport(It.IsAny<RemoteSecretImportRequest>(), out refusedSecret, out refusal)
            )
            .Returns(false)
            .Verifiable();

        var result = await CreateHandler()
            .ImportRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateImportRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task ImportRemoteSecretAsync_WhenTheSecretIdIsTaken_ReturnsConflictWithoutImporting()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupAuthorization(Operations.Update, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(
            new PersistedTenantBuilder
            {
                TenantId = TenantId,
                ConcurrencyToken = "tenant-ct",
                DisplayName = "Tenant One",
                SettingsConcurrencyToken = "settings-ct",
                SecretsConcurrencyToken = "secrets-ct",
                Secrets = [CreateRemoteSecret("generated-id")],
            }.Build()
        );
        SetupImportRemoteValidation();
        SetupResourceId();

        var result = await CreateHandler()
            .ImportRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateImportRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal(
            "A secret with the specified identifier already exists.",
            problem.ProblemDetails.Detail
        );
    }

    [Fact]
    public async Task ImportRemoteSecretAsync_WhenTheKidIsTaken_ReturnsConflictWithoutVerifyingTheBinding()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupAuthorization(Operations.Update, AuthorizationResult.Success());
        SetupStore();
        var existing = CreateRemoteSecret("secret-0", keyId: "kid-1");
        SetupTenant(
            new PersistedTenantBuilder
            {
                TenantId = TenantId,
                ConcurrencyToken = "tenant-ct",
                DisplayName = "Tenant One",
                SettingsConcurrencyToken = "settings-ct",
                SecretsConcurrencyToken = "secrets-ct",
                Secrets = [existing],
            }.Build()
        );
        SetupImportRemoteValidation();
        SetupResourceId();
        SetupTryImport(CreateRemoteSecret("secret-1", keyId: "kid-1"));

        var result = await CreateHandler()
            .ImportRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateImportRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task ImportRemoteSecretAsync_WhenTheBindingIsNotProven_ReturnsItsErrorAndStoresNothing()
    {
        SetupAuthorization(Operations.Create, AuthorizationResult.Success());
        SetupAuthorization(Operations.Update, AuthorizationResult.Success());
        SetupStore();
        SetupTenant(CreateTenant());
        SetupImportRemoteValidation();
        SetupResourceId();

        var secret = CreateRemoteSecret();
        SetupTryImport(secret);
        SetupVerifyBinding(
            secret,
            new ManagementError
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Detail = "The key does not sign with the supplied public key.",
            }
        );

        var result = await CreateHandler()
            .ImportRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateImportRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    #endregion
}
