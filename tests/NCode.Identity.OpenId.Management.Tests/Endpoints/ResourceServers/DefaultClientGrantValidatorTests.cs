// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

public sealed class DefaultClientGrantValidatorTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";
    private const string ResourceServerId = "rs-1";
    private const string KnownScope = "read:messages";
    private const string UnknownScope = "write:messages";

    private MockRepository MockRepository { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IResourceServerStore> MockResourceServerStore { get; }
    private Mock<IClientGrantStore> MockClientGrantStore { get; }
    private DefaultClientGrantValidator Validator { get; }

    public DefaultClientGrantValidatorTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockResourceServerStore = MockRepository.Create<IResourceServerStore>();
        MockClientGrantStore = MockRepository.Create<IClientGrantStore>();

        Validator = new DefaultClientGrantValidator(MockAuthorizationService.Object);
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

    private static PersistedClientGrant CreateGrant(params string[] scopes) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ResourceServerId = ResourceServerId,
            ConcurrencyToken = "grant-ct",
            Scopes = scopes,
        };

    private static PersistedResourceServer CreateResourceServer(params string[] scopeValues) =>
        new PersistedResourceServerBuilder
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
            Scopes = scopeValues
                .Select(value => new PersistedScope
                {
                    Value = value,
                    Description = null,
                    IsSystem = false,
                })
                .ToList(),
        }.Build();

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

    private Func<object?> SetupAuthorizationCapturingTheResource(
        IAuthorizationRequirement requirement
    )
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

    private void SetupResourceServerStore(PersistedResourceServer? resourceServer)
    {
        MockStoreManager
            .Setup(x => x.GetStore<IResourceServerStore>())
            .Returns(MockResourceServerStore.Object)
            .Verifiable();
        MockResourceServerStore
            .Setup(x => x.GetOrDefaultAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resourceServer)
            .Verifiable();
    }

    private void SetupClientGrantStore(PersistedClientGrant? existing)
    {
        MockStoreManager
            .Setup(x => x.GetStore<IClientGrantStore>())
            .Returns(MockClientGrantStore.Object)
            .Verifiable();
        MockClientGrantStore
            .Setup(x =>
                x.GetOrDefaultAsync(ClientId, ResourceServerId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(existing)
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
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_AuthorizesCreateOnTheTenantScope()
    {
        var authorizedResource = SetupAuthorizationCapturingTheResource(Operations.Create);

        await Validator.AuthorizeCreateAsync(
            CreateUser(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var scope = Assert.IsType<TenantScopeResource>(authorizedResource());
        Assert.Equal(TenantId, scope.TenantId);
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
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeUpdateAsync_AuthorizesUpdateOnTheTenantScope()
    {
        var authorizedResource = SetupAuthorizationCapturingTheResource(Operations.Update);

        await Validator.AuthorizeUpdateAsync(
            CreateUser(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var scope = Assert.IsType<TenantScopeResource>(authorizedResource());
        Assert.Equal(TenantId, scope.TenantId);
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
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeDeleteAsync_AuthorizesDeleteOnTheTenantScope()
    {
        var authorizedResource = SetupAuthorizationCapturingTheResource(Operations.Delete);

        await Validator.AuthorizeDeleteAsync(
            CreateUser(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var scope = Assert.IsType<TenantScopeResource>(authorizedResource());
        Assert.Equal(TenantId, scope.TenantId);
    }

    #endregion

    #region ValidateCreateAsync Tests

    [Fact]
    public async Task ValidateCreateAsync_WhenResourceServerMissing_Returns404()
    {
        SetupResourceServerStore(resourceServer: null);

        var error = await Validator.ValidateCreateAsync(
            CreateGrant(KnownScope),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status404NotFound, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenScopeUnknown_Returns422()
    {
        SetupResourceServerStore(CreateResourceServer(KnownScope));

        var error = await Validator.ValidateCreateAsync(
            CreateGrant(UnknownScope),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenGrantExists_Returns409()
    {
        SetupResourceServerStore(CreateResourceServer(KnownScope));
        SetupClientGrantStore(CreateGrant(KnownScope));

        var error = await Validator.ValidateCreateAsync(
            CreateGrant(KnownScope),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenValid_ReturnsNull()
    {
        SetupResourceServerStore(CreateResourceServer(KnownScope));
        SetupClientGrantStore(existing: null);

        var error = await Validator.ValidateCreateAsync(
            CreateGrant(KnownScope),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion

    #region ValidateUpdateAsync Tests

    [Fact]
    public async Task ValidateUpdateAsync_WhenScopeUnknown_Returns422()
    {
        SetupResourceServerStore(CreateResourceServer(KnownScope));

        var error = await Validator.ValidateUpdateAsync(
            CreateGrant(KnownScope),
            [UnknownScope],
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenValid_ReturnsNull()
    {
        SetupResourceServerStore(CreateResourceServer(KnownScope));

        var error = await Validator.ValidateUpdateAsync(
            CreateGrant(KnownScope),
            [KnownScope],
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
            CreateGrant(KnownScope),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion
}
