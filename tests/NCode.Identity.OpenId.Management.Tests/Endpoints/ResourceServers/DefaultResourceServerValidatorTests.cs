// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

public sealed class DefaultResourceServerValidatorTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ResourceServerId = "rs-1";
    private const string Identifier = "https://api.example.com";

    private MockRepository MockRepository { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IResourceServerStore> MockResourceServerStore { get; }
    private DefaultResourceServerValidator Validator { get; }

    public DefaultResourceServerValidatorTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockResourceServerStore = MockRepository.Create<IResourceServerStore>();

        Validator = new DefaultResourceServerValidator(MockAuthorizationService.Object);
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

    private static PersistedResourceServer CreateResourceServer(bool isSystem = false) =>
        new PersistedResourceServerBuilder
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
            Identifier = Identifier,
            IsSystem = isSystem,
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

    private void SetupResourceServerStore()
    {
        MockStoreManager
            .Setup(x => x.GetStore<IResourceServerStore>())
            .Returns(MockResourceServerStore.Object)
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
    public async Task AuthorizeCreateAsync_AuthorizesCreateOnTheTenantNode()
    {
        var authorizedResource = SetupAuthorizationCapturingTheNode(Operations.Create);

        await Validator.AuthorizeCreateAsync(
            CreateUser(authenticated: true),
            TenantId,
            CancellationToken.None
        );

        var node = Assert.IsType<ResourceNode>(authorizedResource());
        Assert.Equal(TenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.Tenant, node.ResourceType);
        Assert.Equal(TenantId, node.ResourceId);
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
            ResourceServerId,
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
            ResourceServerId,
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
            ResourceServerId,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeUpdateAsync_AuthorizesUpdateOnTheResourceServerNode()
    {
        var authorizedResource = SetupAuthorizationCapturingTheNode(Operations.Update);

        await Validator.AuthorizeUpdateAsync(
            CreateUser(authenticated: true),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        var node = Assert.IsType<ResourceNode>(authorizedResource());
        Assert.Equal(TenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.ResourceServer, node.ResourceType);
        Assert.Equal(ResourceServerId, node.ResourceId);
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
            ResourceServerId,
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
            ResourceServerId,
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
            ResourceServerId,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task AuthorizeDeleteAsync_AuthorizesDeleteOnTheResourceServerNode()
    {
        var authorizedResource = SetupAuthorizationCapturingTheNode(Operations.Delete);

        await Validator.AuthorizeDeleteAsync(
            CreateUser(authenticated: true),
            TenantId,
            ResourceServerId,
            CancellationToken.None
        );

        var node = Assert.IsType<ResourceNode>(authorizedResource());
        Assert.Equal(TenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.ResourceServer, node.ResourceType);
        Assert.Equal(ResourceServerId, node.ResourceId);
    }

    #endregion

    #region ValidateCreateAsync Tests

    [Fact]
    public async Task ValidateCreateAsync_WhenIdentifierExists_Returns409()
    {
        SetupResourceServerStore();
        MockResourceServerStore
            .Setup(x => x.GetByIdentifierOrDefaultAsync(Identifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateResourceServer())
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateResourceServer(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenValid_ReturnsNull()
    {
        SetupResourceServerStore();
        MockResourceServerStore
            .Setup(x => x.GetByIdentifierOrDefaultAsync(Identifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedResourceServer?)null)
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateResourceServer(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion

    #region ValidateUpdateAsync Tests

    [Fact]
    public async Task ValidateUpdateAsync_WhenCalled_ReturnsNull()
    {
        var error = await Validator.ValidateUpdateAsync(
            CreateResourceServer(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion

    #region ValidateDeleteAsync Tests

    [Fact]
    public async Task ValidateDeleteAsync_WhenSystem_Returns409()
    {
        var error = await Validator.ValidateDeleteAsync(
            CreateResourceServer(isSystem: true),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenHasDependents_Returns409()
    {
        SetupResourceServerStore();
        MockResourceServerStore
            .Setup(x => x.HasDependentsAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var error = await Validator.ValidateDeleteAsync(
            CreateResourceServer(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenValid_ReturnsNull()
    {
        SetupResourceServerStore();
        MockResourceServerStore
            .Setup(x => x.HasDependentsAsync(ResourceServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

        var error = await Validator.ValidateDeleteAsync(
            CreateResourceServer(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion
}
