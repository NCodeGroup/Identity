// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

public sealed class DefaultTeamValidatorTests : IDisposable
{
    private const string ControlPlaneTenantId = "root";
    private const string TeamId = "team-1";

    private MockRepository MockRepository { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITeamStore> MockTeamStore { get; }
    private DefaultTeamValidator Validator { get; }

    public DefaultTeamValidatorTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTeamStore = MockRepository.Create<ITeamStore>();

        Validator = new DefaultTeamValidator(
            MockAuthorizationService.Object,
            Options.Create(new TenantResolutionOptions())
        );
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

    private static PersistedTeam CreateTeam(
        string teamId = TeamId,
        string concurrencyToken = "team-ct",
        string displayName = "Team One"
    ) =>
        new()
        {
            TeamId = teamId,
            ConcurrencyToken = concurrencyToken,
            IsDisabled = false,
            DisplayName = displayName,
            Settings = new PersistedTeamSettings
            {
                TeamId = teamId,
                ConcurrencyToken = "settings-ct",
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
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

    private void SetupNodeAuthorization(
        string resourceType,
        string resourceId,
        AuthorizationResult result,
        IAuthorizationRequirement? operation = null
    )
    {
        var expectedOperation = operation ?? Operations.Update;
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.Is<object?>(resource =>
                        resource is IResourceNode
                        && ((IResourceNode)resource).TenantId == ControlPlaneTenantId
                        && ((IResourceNode)resource).ResourceType == resourceType
                        && ((IResourceNode)resource).ResourceId == resourceId
                    ),
                    It.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                        requirements.Contains(expectedOperation)
                    )
                )
            )
            .ReturnsAsync(result)
            .Verifiable();
    }

    private void SetupTeamStore()
    {
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable();
    }

    private void SetupExisting(string teamId, PersistedTeam? existing)
    {
        MockTeamStore
            .Setup(x => x.GetOrDefaultAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing)
            .Verifiable();
    }

    private ValueTask<ManagementError?> Create(bool authenticated, PersistedTeam team) =>
        Validator.ValidateCreateAsync(
            CreateUser(authenticated),
            ControlPlaneTenantId,
            team,
            MockStoreManager.Object,
            CancellationToken.None
        );

    private ValueTask<ManagementError?> Update(
        PersistedTeam team,
        UpdateTeamRequest model,
        string? ifMatch = null,
        bool authenticated = true
    ) =>
        Validator.ValidateUpdateAsync(
            CreateUser(authenticated),
            ControlPlaneTenantId,
            team,
            model,
            ifMatch,
            MockStoreManager.Object,
            CancellationToken.None
        );

    private ValueTask<ManagementError?> Delete(PersistedTeam team, bool authenticated = true) =>
        Validator.ValidateDeleteAsync(
            CreateUser(authenticated),
            ControlPlaneTenantId,
            team,
            MockStoreManager.Object,
            CancellationToken.None
        );

    #endregion

    #region ValidateCreateAsync Tests

    [Fact]
    public async Task ValidateCreateAsync_WhenForbiddenAndAuthenticated_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Create(authenticated: true, CreateTeam());

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenForbiddenAndAnonymous_Returns401()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Create(authenticated: false, CreateTeam());

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTeamIdIsInvalid_Returns400()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Create(authenticated: true, CreateTeam(teamId: "-Bad_Id"));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateCreateAsync_WhenTheDisplayNameIsBlank_ReturnsBadRequest(
        string displayName
    )
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Create(authenticated: true, CreateTeam(displayName: displayName));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal("The display name is required.", error.Detail);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTeamExists_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTeamStore();
        SetupExisting(TeamId, CreateTeam());

        var error = await Create(authenticated: true, CreateTeam());

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("A team with the specified identifier already exists.", error.Detail);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTeamIdIsRetired_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTeamStore();
        SetupExisting(TeamId, null);
        MockTeamStore
            .Setup(x => x.IsTeamIdRetiredAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var error = await Create(authenticated: true, CreateTeam());

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(
            "The identifier belonged to a deleted team and cannot be reused.",
            error.Detail
        );
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenValid_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTeamStore();
        SetupExisting(TeamId, null);
        MockTeamStore
            .Setup(x => x.IsTeamIdRetiredAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

        var error = await Create(authenticated: true, CreateTeam());

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateCreateAsync_AuthorizesCreateOnThePlatformNode()
    {
        SetupNodeAuthorization(
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId,
            AuthorizationResult.Success(),
            Operations.Create
        );
        SetupTeamStore();
        SetupExisting(TeamId, null);
        MockTeamStore
            .Setup(x => x.IsTeamIdRetiredAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

        var error = await Create(authenticated: true, CreateTeam());

        Assert.Null(error);
    }

    #endregion

    #region ValidateUpdateAsync Tests

    [Fact]
    public async Task ValidateUpdateAsync_WhenForbiddenAndAuthenticated_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Update(CreateTeam(), new UpdateTeamRequest { DisplayName = "New" });

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenForbiddenAndAnonymous_Returns401()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Update(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = "New" },
            authenticated: false
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIfMatchMismatch_Returns412()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Update(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = "New" },
            ifMatch: "stale-token"
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenDisplayNameBlank_Returns400()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Update(CreateTeam(), new UpdateTeamRequest { DisplayName = " " });

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal("The display name is required.", error.Detail);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenDisablingThePlatformTeam_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Update(
            CreateTeam(teamId: RootTenantOptions.DefaultTeamId),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = true }
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("The platform's own team cannot be disabled.", error.Detail);
    }

    [Fact]
    public async Task ValidateUpdateAsync_AuthorizesUpdateOnTheTeamNode()
    {
        SetupNodeAuthorization(
            ResourceNodeTypes.Team,
            TeamId,
            AuthorizationResult.Success(),
            Operations.Update
        );

        var error = await Update(CreateTeam(), new UpdateTeamRequest { DisplayName = "New" });

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIsDisabledChangesAndTheCallerHasNoPlatformAuthority_ReturnsForbidden()
    {
        SetupNodeAuthorization(ResourceNodeTypes.Team, TeamId, AuthorizationResult.Success());
        SetupNodeAuthorization(
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId,
            AuthorizationResult.Failed()
        );

        var error = await Update(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = true }
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
        Assert.Equal("The caller is not authorized to disable or enable this team.", error.Detail);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIsDisabledChangesAndTheCallerHasPlatformAuthority_ReturnsNull()
    {
        SetupNodeAuthorization(ResourceNodeTypes.Team, TeamId, AuthorizationResult.Success());
        SetupNodeAuthorization(
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId,
            AuthorizationResult.Success()
        );

        var error = await Update(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = true }
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIsDisabledIsUnchanged_DoesNotAuthorizeThePlatformNode()
    {
        SetupNodeAuthorization(ResourceNodeTypes.Team, TeamId, AuthorizationResult.Success());

        var error = await Update(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = false }
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenValid_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Update(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = true },
            ifMatch: "team-ct"
        );

        Assert.Null(error);
    }

    #endregion

    #region ValidateDeleteAsync Tests

    [Fact]
    public async Task ValidateDeleteAsync_WhenForbiddenAndAuthenticated_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Delete(CreateTeam());

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenForbiddenAndAnonymous_Returns401()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Delete(CreateTeam(), authenticated: false);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenPlatformTeam_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Delete(CreateTeam(teamId: RootTenantOptions.DefaultTeamId));

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("The platform's own team cannot be deleted.", error.Detail);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenTeamHasTenants_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTeamStore();
        MockTeamStore
            .Setup(x => x.HasDependentsAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var error = await Delete(CreateTeam());

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("The team cannot be deleted while it still has tenants.", error.Detail);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenValid_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTeamStore();
        MockTeamStore
            .Setup(x => x.HasDependentsAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

        var error = await Delete(CreateTeam());

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateDeleteAsync_AuthorizesDeleteOnThePlatformNode()
    {
        SetupNodeAuthorization(
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId,
            AuthorizationResult.Success(),
            Operations.Delete
        );
        SetupTeamStore();
        MockTeamStore
            .Setup(x => x.HasDependentsAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

        var error = await Delete(CreateTeam());

        Assert.Null(error);
    }

    #endregion
}
