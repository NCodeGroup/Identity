// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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

    private Func<object?> SetupAuthorizationCapturingTheNode(IAuthorizationRequirement requirement)
    {
        object? authorizedResource = null;
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.Is<IEnumerable<IAuthorizationRequirement>>(r => r.Contains(requirement))
                )
            )
            .Callback(
                (ClaimsPrincipal _, object? resource, IEnumerable<IAuthorizationRequirement> _) =>
                    authorizedResource = resource
            )
            .ReturnsAsync(AuthorizationResult.Failed())
            .Verifiable();
        return () => authorizedResource;
    }

    private static bool IsNodeOfType(object? resource, string resourceType) =>
        resource is ResourceNode node && node.ResourceType == resourceType;

    private void SetupAuthorizationForResourceType(
        string resourceType,
        IAuthorizationRequirement requirement,
        AuthorizationResult result
    )
    {
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource => IsNodeOfType(resource, resourceType)),
                    It.Is<IEnumerable<IAuthorizationRequirement>>(r => r.Contains(requirement))
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

    #region AuthorizeCreateAsync Tests

    [Fact]
    public async Task AuthorizeCreateAsync_WhenAuthorized_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.AuthorizeCreateAsync(
            CreateUser(authenticated: true),
            TenantId,
            resourceServerId: null,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_WhenForbiddenAndAuthenticated_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.AuthorizeCreateAsync(
            CreateUser(authenticated: true),
            TenantId,
            resourceServerId: null,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_WhenForbiddenAndAnonymous_Returns401()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.AuthorizeCreateAsync(
            CreateUser(authenticated: false),
            TenantId,
            resourceServerId: null,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_AuthorizesCreateOnTheTenantNode()
    {
        var authorizedResource = SetupAuthorizationCapturingTheNode(Operations.Create);

        await Validator.AuthorizeCreateAsync(
            CreateUser(authenticated: true),
            TenantId,
            resourceServerId: null,
            CancellationToken.None
        );

        var node = Assert.IsType<ResourceNode>(authorizedResource());
        Assert.Equal(TenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.Tenant, node.ResourceType);
        Assert.Equal(TenantId, node.ResourceId);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_WhenTheCallerCannotCreateInTheTenant_DoesNotAuthorizeTheResourceServer()
    {
        SetupAuthorizationForResourceType(
            ResourceNodeTypes.Tenant,
            Operations.Create,
            AuthorizationResult.Failed()
        );

        var error = await Validator.AuthorizeCreateAsync(
            CreateUser(authenticated: true),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_WhenTheCallerCannotUpdateTheNamedResourceServer_Returns403()
    {
        SetupAuthorizationForResourceType(
            ResourceNodeTypes.Tenant,
            Operations.Create,
            AuthorizationResult.Success()
        );
        SetupAuthorizationForResourceType(
            ResourceNodeTypes.ResourceServer,
            Operations.Update,
            AuthorizationResult.Failed()
        );

        var error = await Validator.AuthorizeCreateAsync(
            CreateUser(authenticated: true),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_WhenTheCallerCanUpdateTheNamedResourceServer_ReturnsNull()
    {
        SetupAuthorizationForResourceType(
            ResourceNodeTypes.Tenant,
            Operations.Create,
            AuthorizationResult.Success()
        );
        SetupAuthorizationForResourceType(
            ResourceNodeTypes.ResourceServer,
            Operations.Update,
            AuthorizationResult.Success()
        );

        var error = await Validator.AuthorizeCreateAsync(
            CreateUser(authenticated: true),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion

    #region AuthorizeUpdateAsync Tests

    [Fact]
    public async Task AuthorizeUpdateAsync_WhenAuthorized_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.AuthorizeUpdateAsync(
            CreateUser(authenticated: true),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task AuthorizeUpdateAsync_WhenForbiddenAndAuthenticated_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.AuthorizeUpdateAsync(
            CreateUser(authenticated: true),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeUpdateAsync_WhenForbiddenAndAnonymous_Returns401()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.AuthorizeUpdateAsync(
            CreateUser(authenticated: false),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeUpdateAsync_AuthorizesUpdateOnTheClientNode()
    {
        var authorizedResource = SetupAuthorizationCapturingTheNode(Operations.Update);

        await Validator.AuthorizeUpdateAsync(
            CreateUser(authenticated: true),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        var node = Assert.IsType<ResourceNode>(authorizedResource());
        Assert.Equal(TenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.Client, node.ResourceType);
        Assert.Equal(ClientId, node.ResourceId);
    }

    #endregion

    #region AuthorizeDeleteAsync Tests

    [Fact]
    public async Task AuthorizeDeleteAsync_WhenAuthorized_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.AuthorizeDeleteAsync(
            CreateUser(authenticated: true),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task AuthorizeDeleteAsync_WhenForbiddenAndAuthenticated_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.AuthorizeDeleteAsync(
            CreateUser(authenticated: true),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeDeleteAsync_WhenForbiddenAndAnonymous_Returns401()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.AuthorizeDeleteAsync(
            CreateUser(authenticated: false),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeDeleteAsync_AuthorizesDeleteOnTheClientNode()
    {
        var authorizedResource = SetupAuthorizationCapturingTheNode(Operations.Delete);

        await Validator.AuthorizeDeleteAsync(
            CreateUser(authenticated: true),
            TenantId,
            ClientId,
            CancellationToken.None
        );

        var node = Assert.IsType<ResourceNode>(authorizedResource());
        Assert.Equal(TenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.Client, node.ResourceType);
        Assert.Equal(ClientId, node.ResourceId);
    }

    #endregion

    #region ValidateCreateAsync Tests

    [Fact]
    public async Task ValidateCreateAsync_WhenClientExists_Returns409()
    {
        SetupClientStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient())
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
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
        SetupClientStore();
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
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

    // The client create is judged against the stored rows only; the caller's authority is asserted before any row is read.
    private void SetupBoundClientCreate(PersistedResourceServer? resourceServer)
    {
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
    }

    private async Task<ManagementError?> ValidateBoundCreateAsync() =>
        await Validator.ValidateCreateAsync(
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
    public async Task ValidateCreateAsync_WhenTheBoundResourceServerIsASystemResourceServer_Returns400()
    {
        SetupBoundClientCreate(CreateResourceServer(isSystem: true));

        var error = await ValidateBoundCreateAsync();

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTheBindingIsValid_ReturnsNull()
    {
        SetupBoundClientCreate(CreateResourceServer());

        var error = await ValidateBoundCreateAsync();

        Assert.Null(error);
    }

    #endregion

    #region ValidateUpdateAsync Tests

    [Fact]
    public async Task ValidateUpdateAsync_WhenIfMatchMismatch_Returns412()
    {
        var error = await Validator.ValidateUpdateAsync(
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
        var error = await Validator.ValidateUpdateAsync(
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
        var error = await Validator.ValidateUpdateAsync(
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
    public async Task ValidateDeleteAsync_ReturnsNull()
    {
        var error = await Validator.ValidateDeleteAsync(
            CreateClient(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion
}
