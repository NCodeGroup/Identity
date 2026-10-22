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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Grants;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Grants;

public sealed class GrantApiEndpointHandlerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string GrantId = "grant-1";
    private const string ResourceServerId = "rs-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IGrantStore> MockGrantStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private GrantApiEndpointHandler Handler { get; }

    public GrantApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockGrantStore = MockRepository.Create<IGrantStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();
        MockPersistedGrantService = MockRepository.Create<IPersistedGrantService>();

        Handler = new GrantApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockManagementAuditRecorder.Object,
            MockPersistedGrantService.Object,
            TimeProvider.System,
            MockCryptoService.Object
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
            .Setup(x => x.GetStore<IGrantStore>())
            .Returns(MockGrantStore.Object)
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

    private static PersistedGrant CreateGrant(DateTimeOffset? revokedWhen = null) =>
        new()
        {
            GrantType = "authorization_code",
            HashedKey = "hashed-key-1",
            GrantId = GrantId,
            ConcurrencyToken = "grant-ct",
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = "subject-1",
            SessionId = null,
            RefreshTokenLineageId = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddHours(1),
            RevokedWhen = revokedWhen,
            ConsumedWhen = null,
            PayloadJson = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
        };

    #endregion

    #region ListGrantsAsync Tests

    [Fact]
    public async Task ListGrantsAsync_WhenAuthorized_ReturnsPage()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockGrantStore
            .Setup(x =>
                x.GetPageAsync(
                    It.IsAny<GrantFilter>(),
                    It.IsAny<PageRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedGrant> { Items = [CreateGrant()], NextCursor = "next" }
            )
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListGrantsAsync(
            httpContext,
            TenantId,
            subjectId: null,
            clientId: null,
            sessionId: null,
            grantType: null,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<GrantResource>>>(result);
        Assert.Single(json.Value!.Items);
        Assert.Equal("next", json.Value.ContinuationToken);
        Assert.Equal(GrantId, json.Value.Items[0].GrantId);
    }

    [Fact]
    public async Task ListGrantsAsync_WhenFiltered_PassesSubjectAndClientToStore()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockGrantStore
            .Setup(x =>
                x.GetPageAsync(
                    It.Is<GrantFilter>(f =>
                        f.SubjectId == "subject-1"
                        && f.ClientId == "client-1"
                        && f.SessionId == "sid-1"
                    ),
                    It.IsAny<PageRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedGrant> { Items = [CreateGrant()], NextCursor = null }
            )
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListGrantsAsync(
            httpContext,
            TenantId,
            subjectId: "subject-1",
            clientId: "client-1",
            sessionId: "sid-1",
            grantType: null,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<GrantResource>>>(result);
        Assert.Single(json.Value!.Items);
    }

    [Fact]
    public async Task ListGrantsAsync_WhenForbidden_ReturnsForbidWithoutQueryingStore()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListGrantsAsync(
            httpContext,
            TenantId,
            subjectId: null,
            clientId: null,
            sessionId: null,
            grantType: null,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        // No store scaffold is set up, so a strict-mock failure would mean authorization ran too late.
        Assert.IsType<ForbidHttpResult>(result);
    }

    #endregion

    #region RevokeGrantsAsync Tests

    [Fact]
    public async Task RevokeGrantsAsync_WhenNoFilter_ReturnsBadRequestWithoutAuthorizing()
    {
        var httpContext = CreateHttpContext(authenticated: true);

        // No authorization or store scaffold: a strict-mock failure would mean the guard ran too late.
        var result = await Handler.RevokeGrantsAsync(
            httpContext,
            TenantId,
            subjectId: null,
            clientId: null,
            sessionId: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task RevokeGrantsAsync_WhenAuthorized_RevokesAndReturnsCount()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockGrantStore
            .Setup(x =>
                x.RevokeWhereAsync(
                    It.Is<GrantFilter>(f => f.SubjectId == "subject-1"),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(3)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordGrantsRevokedAsync(
                    It.IsAny<HttpContext>(),
                    TenantId,
                    "subject-1",
                    null,
                    null,
                    3,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RevokeGrantsAsync(
            httpContext,
            TenantId,
            subjectId: "subject-1",
            clientId: null,
            sessionId: null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<GrantRevocationResult>>(result);
        Assert.Equal(3, json.Value?.Revoked);
    }

    [Fact]
    public async Task RevokeGrantsAsync_WhenForbidden_ReturnsForbidWithoutQueryingStore()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RevokeGrantsAsync(
            httpContext,
            TenantId,
            subjectId: "subject-1",
            clientId: null,
            sessionId: null,
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
    }

    #endregion

    #region GetGrantAsync Tests

    [Fact]
    public async Task GetGrantAsync_WhenFoundAndAuthorized_ReturnsJson()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockGrantStore
            .Setup(x => x.GetOrDefaultAsync(GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGrant())
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetGrantAsync(
            httpContext,
            TenantId,
            GrantId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<GrantResource>>(result);
        Assert.Equal(GrantId, json.Value?.GrantId);
        Assert.Equal(TenantId, json.Value?.TenantId);
    }

    [Fact]
    public async Task GetGrantAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockGrantStore
            .Setup(x => x.GetOrDefaultAsync(GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedGrant?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetGrantAsync(
            httpContext,
            TenantId,
            GrantId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetGrantAsync_WhenForbidden_ReturnsForbidWithoutQueryingStore()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetGrantAsync(
            httpContext,
            TenantId,
            GrantId,
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
    }

    #endregion

    #region RevokeGrantAsync Tests

    [Fact]
    public async Task RevokeGrantAsync_WhenActive_SoftRevokesAndReturnsNoContent()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();

        var grant = CreateGrant();
        MockGrantStore
            .Setup(x => x.GetOrDefaultAsync(GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(grant)
            .Verifiable();
        MockGrantStore
            .Setup(x => x.UpdateAsync(grant, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordGrantRevokedAsync(
                    It.IsAny<HttpContext>(),
                    TenantId,
                    "subject-1",
                    "client-1",
                    "authorization_code",
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RevokeGrantAsync(
            httpContext,
            TenantId,
            GrantId,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.NotNull(grant.RevokedWhen);
    }

    [Fact]
    public async Task RevokeGrantAsync_WhenAlreadyRevoked_ReturnsNoContentWithoutUpdate()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();

        var alreadyRevoked = DateTimeOffset.UnixEpoch.AddMinutes(5);
        MockGrantStore
            .Setup(x => x.GetOrDefaultAsync(GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGrant(revokedWhen: alreadyRevoked))
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        // No UpdateAsync/SaveChangesAsync scaffold: a strict-mock failure would mean an idempotent revoke wrote.
        var result = await Handler.RevokeGrantAsync(
            httpContext,
            TenantId,
            GrantId,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task RevokeGrantAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockGrantStore
            .Setup(x => x.GetOrDefaultAsync(GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedGrant?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RevokeGrantAsync(
            httpContext,
            TenantId,
            GrantId,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task RevokeGrantAsync_WhenForbidden_ReturnsForbidWithoutQueryingStore()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RevokeGrantAsync(
            httpContext,
            TenantId,
            GrantId,
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
    }

    #endregion

    #region CreateInitialAccessTokenAsync Tests

    [Fact]
    public async Task CreateInitialAccessTokenAsync_WhenAuthorized_MintsTokenAndReturnsCreated()
    {
        const string token = "minted-token";
        const string mintedGrantId = "iat-grant-1";

        SetupAuthorization(AuthorizationResult.Success());

        MockCryptoService
            .Setup(x => x.GenerateKey(32, BinaryEncodings.Base64Url))
            .Returns(token)
            .Verifiable();

        var grantId = new PersistedGrantId
        {
            TenantId = TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.InitialAccessToken,
            GrantKey = token,
        };
        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(
                    TenantId,
                    OpenIdConstants.PersistedGrantTypes.InitialAccessToken,
                    token
                )
            )
            .Returns(grantId)
            .Verifiable();

        PersistedGrant<InitialAccessTokenGrant> capturedGrant = default;
        MockPersistedGrantService
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<OpenIdContext>(),
                    grantId,
                    It.IsAny<PersistedGrant<InitialAccessTokenGrant>>(),
                    It.IsAny<DateTimeOffset>(),
                    TimeSpan.FromDays(1),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    PersistedGrant<InitialAccessTokenGrant> grant,
                    DateTimeOffset _,
                    TimeSpan? _,
                    CancellationToken _
                ) => capturedGrant = grant
            )
            .ReturnsAsync(mintedGrantId)
            .Verifiable();

        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordResourceChangeAsync(
                    It.IsAny<HttpContext>(),
                    It.Is<InitialAccessTokenCreatedAuditEvent>(e =>
                        e.TenantId == TenantId && e.ResourceId == mintedGrantId
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateInitialAccessTokenAsync(
            httpContext,
            TenantId,
            new CreateInitialAccessTokenRequest { Description = "ci pipeline" },
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CreatedInitialAccessTokenResource>>(result);
        Assert.Equal(StatusCodes.Status201Created, json.StatusCode);
        Assert.Equal(token, json.Value?.InitialAccessToken);
        Assert.Equal(mintedGrantId, json.Value?.GrantId);
        Assert.Equal(TenantId, json.Value?.TenantId);
        Assert.Equal("ci pipeline", json.Value?.Description);
        Assert.Equal(json.Value!.CreatedWhen.AddDays(1), json.Value.ExpiresWhen);

        Assert.Equal(PersistedGrantStatus.Active, capturedGrant.Status);
        Assert.Equal(TenantId, capturedGrant.TenantId);
        Assert.Null(capturedGrant.ClientId);
        Assert.Null(capturedGrant.SubjectId);
        Assert.Equal("ci pipeline", capturedGrant.Payload.Description);
    }

    [Fact]
    public async Task CreateInitialAccessTokenAsync_WhenLifetimeSpecified_SetsExpiry()
    {
        const string token = "minted-token";
        const string mintedGrantId = "iat-grant-2";
        const long lifetimeSeconds = 3600;

        SetupAuthorization(AuthorizationResult.Success());

        MockCryptoService
            .Setup(x => x.GenerateKey(32, BinaryEncodings.Base64Url))
            .Returns(token)
            .Verifiable();

        var grantId = new PersistedGrantId
        {
            TenantId = TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.InitialAccessToken,
            GrantKey = token,
        };
        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(
                    TenantId,
                    OpenIdConstants.PersistedGrantTypes.InitialAccessToken,
                    token
                )
            )
            .Returns(grantId)
            .Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<OpenIdContext>(),
                    grantId,
                    It.IsAny<PersistedGrant<InitialAccessTokenGrant>>(),
                    It.IsAny<DateTimeOffset>(),
                    TimeSpan.FromSeconds(lifetimeSeconds),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(mintedGrantId)
            .Verifiable();

        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordResourceChangeAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<InitialAccessTokenCreatedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateInitialAccessTokenAsync(
            httpContext,
            TenantId,
            new CreateInitialAccessTokenRequest { LifetimeSeconds = lifetimeSeconds },
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CreatedInitialAccessTokenResource>>(result);
        Assert.NotNull(json.Value?.ExpiresWhen);
        Assert.Equal(json.Value!.CreatedWhen.AddSeconds(lifetimeSeconds), json.Value.ExpiresWhen);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2_592_001)]
    public async Task CreateInitialAccessTokenAsync_WhenLifetimeOutOfRange_ReturnsBadRequest(
        long lifetimeSeconds
    )
    {
        var httpContext = CreateHttpContext(authenticated: true);

        // No authorization or mint scaffold: a strict-mock failure would mean the guard ran too late.
        var result = await Handler.CreateInitialAccessTokenAsync(
            httpContext,
            TenantId,
            new CreateInitialAccessTokenRequest { LifetimeSeconds = lifetimeSeconds },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task CreateInitialAccessTokenAsync_WhenForbidden_ReturnsForbidWithoutMinting()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        // No mint scaffold: a strict-mock failure would mean authorization ran too late.
        var result = await Handler.CreateInitialAccessTokenAsync(
            httpContext,
            TenantId,
            new CreateInitialAccessTokenRequest(),
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
    }

    private void SetupResourceServer(string tenantId, bool isSystem)
    {
        var mockResourceServerStore = MockRepository.Create<IResourceServerStore>();
        mockResourceServerStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PersistedResourceServer
                {
                    TenantId = tenantId,
                    ResourceServerId = ResourceServerId,
                    Identifier = "https://api.example.com",
                    ConcurrencyToken = "rs-ct",
                    Name = "Example API",
                    IsSystem = isSystem,
                    IsDisabled = false,
                    Settings = new PersistedResourceServerSettings
                    {
                        TenantId = tenantId,
                        ResourceServerId = ResourceServerId,
                        Value = JsonSerializer.SerializeToElement(new { }),
                    },
                    Scopes = [],
                    Secrets = new PersistedResourceServerSecrets
                    {
                        TenantId = tenantId,
                        ResourceServerId = ResourceServerId,
                        Value = [],
                    },
                }
            )
            .Verifiable();

        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IResourceServerStore>())
            .Returns(mockResourceServerStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    [Fact]
    public async Task CreateInitialAccessTokenAsync_WhenResourceServerIsInAnotherTenant_ReturnsBadRequest()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceServer("tenant-2", isSystem: false);

        // No mint scaffold: a strict-mock failure would mean the resource server was not checked.
        var result = await Handler.CreateInitialAccessTokenAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            new CreateInitialAccessTokenRequest { ResourceServerId = ResourceServerId },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task CreateInitialAccessTokenAsync_WhenResourceServerIsSystem_ReturnsBadRequest()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceServer(TenantId, isSystem: true);

        var result = await Handler.CreateInitialAccessTokenAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            new CreateInitialAccessTokenRequest { ResourceServerId = ResourceServerId },
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task CreateInitialAccessTokenAsync_WhenNotAuthorizedToUpdateResourceServer_ReturnsForbid()
    {
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
        SetupResourceServer(TenantId, isSystem: false);

        var result = await Handler.CreateInitialAccessTokenAsync(
            CreateHttpContext(authenticated: true),
            TenantId,
            new CreateInitialAccessTokenRequest { ResourceServerId = ResourceServerId },
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
        MockAuthorizationService.Verify(
            x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource => resource is IResourceNode),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                ),
            Times.Once
        );
    }

    #endregion
}
