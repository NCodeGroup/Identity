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
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts.Clients;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

public sealed class DefaultClientValidatorTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private MockRepository MockRepository { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private DefaultClientValidator Validator { get; }

    public DefaultClientValidatorTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockClientStore = MockRepository.Create<IClientStore>();

        Validator = new DefaultClientValidator(MockAuthorizationService.Object);
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private static ClaimsPrincipal CreateUser(bool authenticated)
    {
        var identity = authenticated
            ? new ClaimsIdentity(authenticationType: "test")
            : new ClaimsIdentity();
        return new ClaimsPrincipal(identity);
    }

    private static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new JsonObject());

    private static PersistedClient CreateClient(
        string concurrencyToken = "client-ct",
        string? resourceServerId = null
    ) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = concurrencyToken,
            IsDisabled = false,
            ResourceServerId = resourceServerId,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = "settings-ct",
                Value = EmptyObject(),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = "secrets-ct",
                Value = [],
            },
        };

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

    private void SetupClientStore()
    {
        MockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(MockClientStore.Object)
            .Verifiable();
    }

    #endregion

    #region ValidateCreateAsync Tests

    [Fact]
    public async Task ValidateCreateAsync_WhenForbiddenAndAuthenticated_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
            CreateClient(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenForbiddenAndAnonymous_Returns401()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: false),
            CreateClient(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenClientExists_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupClientStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient())
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
            CreateClient(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenValid_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupClientStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
            CreateClient(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    private const string ResourceServerId = "rs-1";

    private static PersistedResourceServer CreateResourceServer(
        string tenantId = TenantId,
        bool isSystem = false
    ) =>
        new()
        {
            TenantId = tenantId,
            ResourceServerId = ResourceServerId,
            Identifier = "https://api.example.com",
            ConcurrencyToken = "rs-ct",
            Name = "API",
            IsSystem = isSystem,
            IsDisabled = false,
            Settings = new PersistedResourceServerSettings
            {
                TenantId = tenantId,
                ResourceServerId = ResourceServerId,
                Value = EmptyObject(),
            },
            Scopes = [],
            Secrets = new PersistedResourceServerSecrets
            {
                TenantId = tenantId,
                ResourceServerId = ResourceServerId,
                Value = [],
            },
        };

    // The client create is authorized; the binding then asks for the right to update the resource server.
    private void SetupBoundClientCreate(
        PersistedResourceServer? resourceServer,
        AuthorizationResult? resourceServerAuthorization = null
    )
    {
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource => resource is PersistedClient),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(AuthorizationResult.Success())
            .Verifiable();
        SetupClientStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var mockResourceServerStore = MockRepository.Create<IResourceServerStore>();
        MockStoreManager
            .Setup(x => x.GetStore<IResourceServerStore>())
            .Returns(mockResourceServerStore.Object)
            .Verifiable();
        mockResourceServerStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resourceServer)
            .Verifiable();

        if (resourceServerAuthorization is not null)
        {
            MockAuthorizationService
                .Setup(x =>
                    x.AuthorizeAsync(
                        It.IsAny<ClaimsPrincipal>(),
                        It.Is<object?>(resource => resource is ResourceNode),
                        It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                    )
                )
                .ReturnsAsync(resourceServerAuthorization)
                .Verifiable();
        }
    }

    private async Task<ManagementError?> ValidateBoundCreateAsync() =>
        await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
            CreateClient(resourceServerId: ResourceServerId),
            MockStoreManager.Object,
            CancellationToken.None
        );

    [Fact]
    public async Task ValidateCreateAsync_WhenTheBoundResourceServerIsUnknown_Returns400()
    {
        SetupBoundClientCreate(resourceServer: null);

        var error = await ValidateBoundCreateAsync();

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTheBoundResourceServerBelongsToAnotherTenant_Returns400()
    {
        SetupBoundClientCreate(CreateResourceServer(tenantId: "tenant-2"));

        var error = await ValidateBoundCreateAsync();

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTheCallerCannotUpdateTheBoundResourceServer_Returns403()
    {
        SetupBoundClientCreate(CreateResourceServer(), AuthorizationResult.Failed());

        var error = await ValidateBoundCreateAsync();

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTheBoundResourceServerIsASystemResourceServer_Returns400()
    {
        SetupBoundClientCreate(CreateResourceServer(isSystem: true), AuthorizationResult.Success());

        var error = await ValidateBoundCreateAsync();

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTheBindingIsValid_ReturnsNull()
    {
        SetupBoundClientCreate(CreateResourceServer(), AuthorizationResult.Success());

        var error = await ValidateBoundCreateAsync();

        Assert.Null(error);
    }

    #endregion

    #region ValidateUpdateAsync Tests

    [Fact]
    public async Task ValidateUpdateAsync_WhenForbidden_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
            CreateClient(),
            new UpdateClientRequest(),
            ifMatch: null,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIfMatchMismatch_Returns412()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
            CreateClient("client-ct"),
            new UpdateClientRequest(),
            ifMatch: "stale-token",
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIfMatchMatches_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
            CreateClient("client-ct"),
            new UpdateClientRequest(),
            ifMatch: "client-ct",
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIfMatchNull_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
            CreateClient(),
            new UpdateClientRequest(),
            ifMatch: null,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion

    #region ValidateDeleteAsync Tests

    [Fact]
    public async Task ValidateDeleteAsync_WhenForbidden_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.ValidateDeleteAsync(
            CreateUser(authenticated: true),
            CreateClient(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenValid_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.ValidateDeleteAsync(
            CreateUser(authenticated: true),
            CreateClient(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion
}
