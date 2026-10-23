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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts.Principals;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Principals;

public sealed class PrincipalApiEndpointHandlerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string PrincipalId = "principal-1";
    private const string ConcurrencyToken = "token-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IFederatedPrincipalStore> MockFederatedPrincipalStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ISessionService> MockSessionService { get; }
    private Mock<IOpenIdContextFactory> MockOpenIdContextFactory { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private PrincipalApiEndpointHandler Handler { get; }

    public PrincipalApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockFederatedPrincipalStore = MockRepository.Create<IFederatedPrincipalStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockSessionService = MockRepository.Create<ISessionService>();
        MockOpenIdContextFactory = MockRepository.Create<IOpenIdContextFactory>();
        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();

        Handler = new PrincipalApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockCryptoService.Object,
            MockSessionService.Object,
            MockOpenIdContextFactory.Object,
            MockManagementAuditRecorder.Object
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

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

    // Mocks the store manager and reads the principal; the store manager is only created once authorization succeeded.
    private void SetupPrincipal(PersistedFederatedPrincipal? principal)
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(MockFederatedPrincipalStore.Object)
            .Verifiable();
        MockFederatedPrincipalStore
            .Setup(x => x.GetOrDefaultAsync(PrincipalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(principal)
            .Verifiable();
    }

    // Only the paths that change the block state write it.
    private void SetupBlockStateWritten(bool isBlocked)
    {
        MockFederatedPrincipalStore
            .Setup(x =>
                x.UpdateAsync(
                    It.Is<PersistedFederatedPrincipal>(p => p.IsBlocked == isBlocked),
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

    // Mocks the addressed-tenant context the block path materializes; a loose context suffices as it is only passed
    // through to the (strict) session service and disposed.
    private void SetupTenantContext()
    {
        MockOpenIdContextFactory
            .Setup(x =>
                x.CreateForTenantAsync(
                    It.IsAny<HttpContext>(),
                    TenantId,
                    It.IsAny<IMediator>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Mock.Of<OpenIdContext>())
            .Verifiable();
    }

    private void SetupSessionsRevoked(int count)
    {
        MockSessionService
            .Setup(x =>
                x.RevokeAllAsync(
                    It.IsAny<OpenIdContext>(),
                    PrincipalId,
                    SessionTerminationReasons.UserBlocked,
                    It.Is<CancellationToken>(token => !token.CanBeCanceled)
                )
            )
            .ReturnsAsync(count)
            .Verifiable();
    }

    private void SetupAudit()
    {
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordResourceChangeAsync(
                    It.IsAny<HttpContext>(),
                    It.Is<ResourceChangeAuditEvent>(e =>
                        e.ResourceType == ManagementResourceTypes.Principal
                        && e.ResourceId == PrincipalId
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    private static PersistedFederatedPrincipal CreatePrincipal(bool isBlocked) =>
        new()
        {
            TenantId = TenantId,
            PrincipalId = PrincipalId,
            IsBlocked = isBlocked,
            ConcurrencyToken = ConcurrencyToken,
        };

    private static HttpContext CreateHttpContext(bool authenticated)
    {
        var identity = authenticated
            ? new ClaimsIdentity(authenticationType: "test")
            : new ClaimsIdentity();
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IMediator>());
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = services.BuildServiceProvider(),
        };
    }

    #endregion

    #region GetPrincipalAsync Tests

    [Fact]
    public async Task GetPrincipalAsync_WhenNotAuthorized_ReturnsForbid()
    {
        SetupAuthorization(AuthorizationResult.Failed());
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetPrincipalAsync(
            httpContext,
            TenantId,
            PrincipalId,
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task GetPrincipalAsync_WhenMissing_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupPrincipal(principal: null);
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetPrincipalAsync(
            httpContext,
            TenantId,
            PrincipalId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetPrincipalAsync_WhenFound_ReturnsItsBlockState()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupPrincipal(CreatePrincipal(isBlocked: true));
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetPrincipalAsync(
            httpContext,
            TenantId,
            PrincipalId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<PrincipalResource>>(result);
        Assert.NotNull(json.Value);
        Assert.Equal(PrincipalId, json.Value.PrincipalId);
        Assert.True(json.Value.IsBlocked);
        Assert.Equal(ConcurrencyToken, httpContext.Response.Headers.ETag.ToString());
    }

    #endregion

    #region BlockPrincipalAsync Tests

    [Fact]
    public async Task BlockPrincipalAsync_WhenMissing_ReturnsNotFoundWithoutEndingSessions()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupPrincipal(principal: null);
        var httpContext = CreateHttpContext(authenticated: true);

        // No tenant context, session or audit scaffold: a strict-mock failure would mean a missing principal was acted on.
        var result = await Handler.BlockPrincipalAsync(
            httpContext,
            TenantId,
            PrincipalId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task BlockPrincipalAsync_WhenFound_BlocksEndsItsSessionsAndAudits()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupPrincipal(CreatePrincipal(isBlocked: false));
        SetupBlockStateWritten(isBlocked: true);
        SetupTenantContext();
        SetupSessionsRevoked(count: 1);
        SetupAudit();
        var httpContext = CreateHttpContext(authenticated: true);
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await Handler.BlockPrincipalAsync(
            httpContext,
            TenantId,
            PrincipalId,
            cancellationTokenSource.Token
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task BlockPrincipalAsync_WhenAlreadyBlocked_EndsItsSessionsWithoutWriting()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupPrincipal(CreatePrincipal(isBlocked: true));
        SetupTenantContext();
        SetupSessionsRevoked(count: 0);
        SetupAudit();
        var httpContext = CreateHttpContext(authenticated: true);
        using var cancellationTokenSource = new CancellationTokenSource();

        // No UpdateAsync/SaveChangesAsync scaffold: a strict-mock failure would mean an unchanged block state was written.
        var result = await Handler.BlockPrincipalAsync(
            httpContext,
            TenantId,
            PrincipalId,
            cancellationTokenSource.Token
        );

        Assert.IsType<NoContent>(result);
    }

    #endregion

    #region UnblockPrincipalAsync Tests

    [Fact]
    public async Task UnblockPrincipalAsync_WhenBlocked_UnblocksWithoutEndingSessions()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupPrincipal(CreatePrincipal(isBlocked: true));
        SetupBlockStateWritten(isBlocked: false);
        SetupAudit();
        var httpContext = CreateHttpContext(authenticated: true);

        // No tenant context or session scaffold: a strict-mock failure would mean an unblock ended sessions.
        var result = await Handler.UnblockPrincipalAsync(
            httpContext,
            TenantId,
            PrincipalId,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    #endregion
}
