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
    private void SetupTenantChangedAnnouncement() =>
        MockMediator
            .Setup(x =>
                x.SendAsync(It.IsAny<TenantChangedCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void VerifyTenantChanged(bool settingsChanged) =>
        MockMediator.Verify(
            x =>
                x.SendAsync(
                    new TenantChangedCommand(TenantId, settingsChanged),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

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

    private static PersistedSecret CreateRemoteSecret(string secretId = "secret-1") =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
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
        VerifyTenantChanged(settingsChanged: false);
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
    public async Task CreateRemoteSecretAsync_WhenTheSecretIdIsTaken_DestroysTheKeyAndReturnsConflict()
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
            .Throws(new InvalidOperationException("duplicate"))
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

        var result = await CreateHandler()
            .CreateRemoteSecretAsync(
                CreateHttpContext(),
                TenantId,
                CreateRemoteSecretRequest(),
                CancellationToken.None
            );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    #endregion
}
