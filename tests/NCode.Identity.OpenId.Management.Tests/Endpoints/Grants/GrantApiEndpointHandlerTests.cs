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
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Grants;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Management.Endpoints.Grants;

public sealed class GrantApiEndpointHandlerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string GrantId = "grant-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IGrantStore> MockGrantStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private GrantApiEndpointHandler Handler { get; }

    public GrantApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockGrantStore = MockRepository.Create<IGrantStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockCryptoService = MockRepository.Create<ICryptoService>();

        Handler = new GrantApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockAmbientTenantAccessor.Object,
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
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
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
        MockAmbientTenantAccessor.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockGrantStore
            .Setup(x => x.GetPageAsync(null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PagedResult<PersistedGrant> { Items = [CreateGrant()], NextCursor = "next" }
            )
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListGrantsAsync(
            httpContext,
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
    public async Task ListGrantsAsync_WhenForbidden_ReturnsForbidWithoutQueryingStore()
    {
        MockAmbientTenantAccessor.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListGrantsAsync(
            httpContext,
            cursor: null,
            limit: null,
            CancellationToken.None
        );

        // No store scaffold is set up, so a strict-mock failure would mean authorization ran too late.
        Assert.IsType<ForbidHttpResult>(result);
    }

    #endregion

    #region GetGrantAsync Tests

    [Fact]
    public async Task GetGrantAsync_WhenFoundAndAuthorized_ReturnsJson()
    {
        MockAmbientTenantAccessor.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockGrantStore
            .Setup(x => x.GetOrDefaultAsync(GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGrant())
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetGrantAsync(httpContext, GrantId, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<GrantResource>>(result);
        Assert.Equal(GrantId, json.Value?.GrantId);
        Assert.Equal(TenantId, json.Value?.TenantId);
    }

    [Fact]
    public async Task GetGrantAsync_WhenNotFound_ReturnsNotFound()
    {
        MockAmbientTenantAccessor.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockGrantStore
            .Setup(x => x.GetOrDefaultAsync(GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedGrant?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetGrantAsync(httpContext, GrantId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetGrantAsync_WhenForbidden_ReturnsForbidWithoutQueryingStore()
    {
        MockAmbientTenantAccessor.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetGrantAsync(httpContext, GrantId, CancellationToken.None);

        Assert.IsType<ForbidHttpResult>(result);
    }

    #endregion

    #region RevokeGrantAsync Tests

    [Fact]
    public async Task RevokeGrantAsync_WhenActive_SoftRevokesAndReturnsNoContent()
    {
        MockAmbientTenantAccessor.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
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

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RevokeGrantAsync(httpContext, GrantId, CancellationToken.None);

        Assert.IsType<NoContent>(result);
        Assert.NotNull(grant.RevokedWhen);
    }

    [Fact]
    public async Task RevokeGrantAsync_WhenAlreadyRevoked_ReturnsNoContentWithoutUpdate()
    {
        MockAmbientTenantAccessor.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();

        var alreadyRevoked = DateTimeOffset.UnixEpoch.AddMinutes(5);
        MockGrantStore
            .Setup(x => x.GetOrDefaultAsync(GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGrant(revokedWhen: alreadyRevoked))
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        // No UpdateAsync/SaveChangesAsync scaffold: a strict-mock failure would mean an idempotent revoke wrote.
        var result = await Handler.RevokeGrantAsync(httpContext, GrantId, CancellationToken.None);

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task RevokeGrantAsync_WhenNotFound_ReturnsNotFound()
    {
        MockAmbientTenantAccessor.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        SetupStore();
        MockGrantStore
            .Setup(x => x.GetOrDefaultAsync(GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedGrant?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RevokeGrantAsync(httpContext, GrantId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task RevokeGrantAsync_WhenForbidden_ReturnsForbidWithoutQueryingStore()
    {
        MockAmbientTenantAccessor.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RevokeGrantAsync(httpContext, GrantId, CancellationToken.None);

        Assert.IsType<ForbidHttpResult>(result);
    }

    #endregion
}
