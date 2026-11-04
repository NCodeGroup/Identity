// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Management.Contracts.Platform;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Platform;

public sealed class DefaultPlatformValidatorTests : IDisposable
{
    private const string PlatformId = "server-1";

    private MockRepository MockRepository { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IPlatformStore> MockServerStore { get; }
    private DefaultPlatformValidator Validator { get; }

    public DefaultPlatformValidatorTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockServerStore = MockRepository.Create<IPlatformStore>();

        Validator = new DefaultPlatformValidator(MockAuthorizationService.Object);
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

    private static PersistedPlatform CreateServer(string concurrencyToken = "server-ct") =>
        new()
        {
            PlatformId = PlatformId,
            ConcurrencyToken = concurrencyToken,
            DisplayName = "Test Server",
            Settings = new PersistedPlatformSettings
            {
                PlatformId = PlatformId,
                ConcurrencyToken = "settings-ct",
                Value = EmptyObject(),
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

    private void SetupServerStore()
    {
        MockStoreManager
            .Setup(x => x.GetStore<IPlatformStore>())
            .Returns(MockServerStore.Object)
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
            CreateServer(),
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
            CreateServer(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenServerExists_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupServerStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(PlatformId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateServer())
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
            CreateServer(),
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
        SetupServerStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(PlatformId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatform?)null)
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
            CreateServer(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion

    #region ValidateUpdateAsync Tests

    private static UpdatePlatformRequest CreateUpdateModel(
        string? displayName = "Renamed Server"
    ) => new() { DisplayName = displayName };

    [Fact]
    public async Task ValidateUpdateAsync_WhenForbiddenAndAuthenticated_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
            CreateServer(),
            CreateUpdateModel(),
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
            CreateServer("server-ct"),
            CreateUpdateModel(),
            ifMatch: "stale-token",
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenDisplayNameMissing_Returns400()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
            CreateServer(),
            CreateUpdateModel(displayName: null),
            ifMatch: null,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenValid_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
            CreateServer("server-ct"),
            CreateUpdateModel(),
            ifMatch: "server-ct",
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
            CreateServer(),
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
            CreateServer(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion
}
