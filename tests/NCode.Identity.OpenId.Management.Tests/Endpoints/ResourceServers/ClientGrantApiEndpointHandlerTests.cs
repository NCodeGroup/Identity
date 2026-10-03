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
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.ResourceServers;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;
using Xunit;

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

    #endregion

    [Fact]
    public async Task ListAsync_WhenAuthorized_ReturnsPage()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockStore
            .Setup(x =>
                x.GetPageAsync(ClientId, null, It.IsAny<int>(), It.IsAny<CancellationToken>())
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

    [Fact]
    public async Task CreateAsync_WhenValid_ReturnsCreated()
    {
        SetupStore();
        MockValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
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
        SetupStore();
        MockValidator
            .Setup(x =>
                x.ValidateCreateAsync(
                    It.IsAny<ClaimsPrincipal>(),
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
                    It.IsAny<ClaimsPrincipal>(),
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
            ClientId,
            ResourceServerId,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }
}
