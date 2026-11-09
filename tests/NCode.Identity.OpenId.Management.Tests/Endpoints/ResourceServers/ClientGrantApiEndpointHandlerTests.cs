// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.ResourceServers;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

public sealed class ClientGrantApiEndpointHandlerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";
    private const string ResourceServerId = "rs-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientGrantStore> MockStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<IClientGrantValidator> MockValidator { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private ClientGrantApiEndpointHandler Handler { get; }

    public ClientGrantApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockStore = MockRepository.Create<IClientGrantStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockValidator = MockRepository.Create<IClientGrantValidator>();
        MockCryptoService = MockRepository.Create<ICryptoService>();

        Handler = new ClientGrantApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockValidator.Object,
            MockCryptoService.Object
        );
    }

    public void Dispose() => MockRepository.Verify();

    #region Helpers

    private void SetupStore()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IClientGrantStore>())
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
                )
            )
        );
        return httpContext;
    }

    private static PersistedClientGrant CreateGrant() =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ResourceServerId = ResourceServerId,
            ConcurrencyToken = "grant-ct",
            Scopes = ["read:messages"],
        };

    private void SetupAuthorizeCreate(ManagementError? error)
    {
        MockValidator
            .Setup(x =>
                x.AuthorizeCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();
    }

    private void SetupAuthorizeUpdate(ManagementError? error)
    {
        MockValidator
            .Setup(x =>
                x.AuthorizeUpdateAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();
    }

    private void SetupAuthorizeDelete(ManagementError? error)
    {
        MockValidator
            .Setup(x =>
                x.AuthorizeDeleteAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(error)
            .Verifiable();
    }

    private static ManagementError CreateForbiddenError() =>
        new()
        {
            StatusCode = StatusCodes.Status403Forbidden,
            Detail = "The caller is not authorized.",
        };

    #endregion

    [Fact]
    public async Task ListAsync_WhenAuthorized_ReturnsPage()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x =>
                x.GetPageAsync(ClientId, It.IsAny<PageRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new PagedResult<PersistedClientGrant> { Items = [CreateGrant()], NextCursor = null }
            )
            .Verifiable();

        var result = await Handler.ListAsync(
            CreateHttpContext(),
            TenantId,
            ClientId,
            null,
            null,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<ClientGrantResource>>>(result);
        Assert.Single(json.Value!.Items);
    }

    private void SetupTenantAuthorization(AuthorizationResult result)
    {
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is TenantScopeResource
                        && ((TenantScopeResource)resource).TenantId == TenantId
                    ),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();
    }

    [Fact]
    public async Task GetAsync_WhenForbidden_ReturnsForbiddenWithoutReading()
    {
        SetupTenantAuthorization(AuthorizationResult.Failed());

        // No store is set up, so a strict-mock failure would mean the grant was read before authorization.
        var result = await Handler.GetAsync(
            CreateHttpContext(),
            TenantId,
            ClientId,
            ResourceServerId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task GetAsync_WhenAuthorizedAndMissing_ReturnsNotFound()
    {
        SetupTenantAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x =>
                x.GetOrDefaultAsync(ClientId, ResourceServerId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedClientGrant?)null)
            .Verifiable();

        var result = await Handler.GetAsync(
            CreateHttpContext(),
            TenantId,
            ClientId,
            ResourceServerId,
            CancellationToken.None
        );

        var notFound = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task GetAsync_WhenAuthorized_ReturnsTheGrant()
    {
        SetupTenantAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x =>
                x.GetOrDefaultAsync(ClientId, ResourceServerId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateGrant())
            .Verifiable();

        var result = await Handler.GetAsync(
            CreateHttpContext(),
            TenantId,
            ClientId,
            ResourceServerId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<ClientGrantResource>>(result);
        Assert.Equal(ResourceServerId, json.Value?.ResourceServerId);
        Assert.Equal(["read:messages"], json.Value?.Scopes);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_ReturnsCreated()
    {
        SetupAuthorizeCreate(error: null);
        SetupStore();
        MockValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<PersistedClientGrant>(),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        MockStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedClientGrant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var request = new CreateClientGrantRequest
        {
            ResourceServerId = ResourceServerId,
            Scopes = ["read:messages"],
        };

        var result = await Handler.CreateAsync(
            CreateHttpContext(),
            TenantId,
            ClientId,
            request,
            CancellationToken.None
        );

        var created = Assert.IsType<Created<ClientGrantResource>>(result);
        Assert.Equal(ResourceServerId, created.Value?.ResourceServerId);
    }

    [Fact]
    public async Task CreateAsync_WhenScopeUnknown_ReturnsUnprocessableEntity()
    {
        SetupAuthorizeCreate(error: null);
        SetupStore();
        MockValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<PersistedClientGrant>(),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ManagementError
                {
                    StatusCode = StatusCodes.Status422UnprocessableEntity,
                    Detail = "The following scopes are not defined on the resource server: x.",
                }
            )
            .Verifiable();

        var request = new CreateClientGrantRequest
        {
            ResourceServerId = ResourceServerId,
            Scopes = ["x"],
        };

        var result = await Handler.CreateAsync(
            CreateHttpContext(),
            TenantId,
            ClientId,
            request,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteAsync_WhenValid_ReturnsNoContent()
    {
        SetupAuthorizeDelete(error: null);
        SetupStore();
        MockStore
            .Setup(x =>
                x.GetOrDefaultAsync(ClientId, ResourceServerId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateGrant())
            .Verifiable();
        MockValidator
            .Setup(x =>
                x.ValidateDeleteAsync(
                    It.IsAny<PersistedClientGrant>(),
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ManagementError?)null)
            .Verifiable();
        MockStore
            .Setup(x => x.RemoveAsync(ClientId, ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.DeleteAsync(
            CreateHttpContext(),
            TenantId,
            ClientId,
            ResourceServerId,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task CreateAsync_WhenUnauthorized_ReturnsTheErrorWithoutOpeningTheStore()
    {
        SetupAuthorizeCreate(CreateForbiddenError());

        var request = new CreateClientGrantRequest
        {
            ResourceServerId = ResourceServerId,
            Scopes = ["read:messages"],
        };

        var result = await Handler.CreateAsync(
            CreateHttpContext(),
            TenantId,
            ClientId,
            request,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheGrant()
    {
        SetupAuthorizeUpdate(CreateForbiddenError());

        var request = new UpdateClientGrantRequest { Scopes = ["read:messages"] };

        var result = await Handler.UpdateAsync(
            CreateHttpContext(),
            TenantId,
            ClientId,
            ResourceServerId,
            request,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteAsync_WhenUnauthorized_ReturnsTheErrorWithoutReadingTheGrant()
    {
        SetupAuthorizeDelete(CreateForbiddenError());

        var result = await Handler.DeleteAsync(
            CreateHttpContext(),
            TenantId,
            ClientId,
            ResourceServerId,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #region Map Tests

    [Theory]
    [InlineData("GET", "/grants", new[] { 400, 401, 403 })]
    [InlineData("POST", "/grants", new[] { 400, 401, 403, 404, 409, 412, 415, 422 })]
    [InlineData("GET", "/grants/{resourceServerId}", new[] { 401, 403, 404 })]
    [InlineData("PUT", "/grants/{resourceServerId}", new[] { 400, 401, 403, 404, 412, 415, 422 })]
    [InlineData("DELETE", "/grants/{resourceServerId}", new[] { 401, 403, 404, 412 })]
    public async Task Map_Always_DeclaresAndDescribesEveryProblemARouteAnswers(
        string httpMethod,
        string routePattern,
        int[] statusCodes
    )
    {
        Assert.Equal(
            statusCodes,
            await EndpointProblemMetadata.ReadStatusCodesAsync(Handler, httpMethod, routePattern)
        );
        Assert.Empty(
            await EndpointProblemMetadata.ReadUndescribedStatusCodesAsync(
                Handler,
                httpMethod,
                routePattern
            )
        );
    }

    #endregion
}
