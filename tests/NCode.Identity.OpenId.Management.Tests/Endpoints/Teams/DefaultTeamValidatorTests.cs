// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
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
        string displayName = "Team One",
        string? parentTeamId = null
    ) =>
        new PersistedTeamBuilder
        {
            TeamId = teamId,
            ParentTeamId = parentTeamId,
            ConcurrencyToken = concurrencyToken,
            DisplayName = displayName,
            SettingsConcurrencyToken = "settings-ct",
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

    private ValueTask<ManagementError?> AuthorizeCreateAsync(
        bool authenticated,
        string? parentTeamId,
        CancellationToken cancellationToken
    ) =>
        Validator.AuthorizeCreateAsync(
            CreateUser(authenticated),
            ControlPlaneTenantId,
            parentTeamId,
            cancellationToken
        );

    private ValueTask<ManagementError?> AuthorizeUpdateAsync(
        bool authenticated,
        CancellationToken cancellationToken
    ) =>
        Validator.AuthorizeUpdateAsync(
            CreateUser(authenticated),
            ControlPlaneTenantId,
            TeamId,
            cancellationToken
        );

    private ValueTask<ManagementError?> AuthorizeDeleteAsync(
        bool authenticated,
        CancellationToken cancellationToken
    ) =>
        Validator.AuthorizeDeleteAsync(
            CreateUser(authenticated),
            ControlPlaneTenantId,
            TeamId,
            cancellationToken
        );

    private ValueTask<ManagementError?> CreateAsync(
        PersistedTeam team,
        CancellationToken cancellationToken
    ) => Validator.ValidateCreateAsync(team, MockStoreManager.Object, cancellationToken);

    private ValueTask<ManagementError?> UpdateAsync(
        PersistedTeam team,
        UpdateTeamRequest model,
        string? ifMatch,
        bool authenticated,
        CancellationToken cancellationToken
    ) =>
        Validator.ValidateUpdateAsync(
            CreateUser(authenticated),
            ControlPlaneTenantId,
            team,
            model,
            ifMatch,
            MockStoreManager.Object,
            cancellationToken
        );

    private ValueTask<ManagementError?> DeleteAsync(
        PersistedTeam team,
        bool authenticated,
        CancellationToken cancellationToken
    ) =>
        Validator.ValidateDeleteAsync(
            CreateUser(authenticated),
            ControlPlaneTenantId,
            team,
            MockStoreManager.Object,
            cancellationToken
        );

    #endregion

    #region AuthorizeCreateAsync Tests

    [Fact]
    public async Task AuthorizeCreateAsync_WhenAuthorized_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await AuthorizeCreateAsync(
            authenticated: true,
            parentTeamId: null,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_WhenForbiddenAndAuthenticated_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await AuthorizeCreateAsync(
            authenticated: true,
            parentTeamId: null,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
        Assert.Equal("The caller is not authorized to create this team.", error.Detail);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_WhenForbiddenAndAnonymous_Returns401()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await AuthorizeCreateAsync(
            authenticated: false,
            parentTeamId: null,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
        Assert.Equal("Authentication is required to create this team.", error.Detail);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_AuthorizesCreateOnThePlatformNode()
    {
        SetupNodeAuthorization(
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId,
            AuthorizationResult.Success(),
            Operations.Create
        );

        var error = await AuthorizeCreateAsync(
            authenticated: true,
            parentTeamId: null,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_WithAParent_AuthorizesCreateOnTheParentTeamNode()
    {
        SetupNodeAuthorization(
            ResourceNodeTypes.Team,
            "acme",
            AuthorizationResult.Success(),
            Operations.Create
        );

        var error = await AuthorizeCreateAsync(
            authenticated: true,
            parentTeamId: "acme",
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

        var error = await AuthorizeUpdateAsync(authenticated: true, CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task AuthorizeUpdateAsync_WhenForbiddenAndAuthenticated_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await AuthorizeUpdateAsync(authenticated: true, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
        Assert.Equal("The caller is not authorized to update this team.", error.Detail);
    }

    [Fact]
    public async Task AuthorizeUpdateAsync_WhenForbiddenAndAnonymous_Returns401()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await AuthorizeUpdateAsync(authenticated: false, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
        Assert.Equal("Authentication is required to update this team.", error.Detail);
    }

    [Fact]
    public async Task AuthorizeUpdateAsync_AuthorizesUpdateOnTheTeamNode()
    {
        SetupNodeAuthorization(
            ResourceNodeTypes.Team,
            TeamId,
            AuthorizationResult.Success(),
            Operations.Update
        );

        var error = await AuthorizeUpdateAsync(authenticated: true, CancellationToken.None);

        Assert.Null(error);
    }

    #endregion

    #region AuthorizeDeleteAsync Tests

    [Fact]
    public async Task AuthorizeDeleteAsync_WhenAuthorized_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await AuthorizeDeleteAsync(authenticated: true, CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task AuthorizeDeleteAsync_WhenForbiddenAndAuthenticated_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await AuthorizeDeleteAsync(authenticated: true, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
        Assert.Equal("The caller is not authorized to delete this team.", error.Detail);
    }

    [Fact]
    public async Task AuthorizeDeleteAsync_WhenForbiddenAndAnonymous_Returns401()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await AuthorizeDeleteAsync(authenticated: false, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
        Assert.Equal("Authentication is required to delete this team.", error.Detail);
    }

    [Fact]
    public async Task AuthorizeDeleteAsync_AuthorizesDeleteOnTheTeamNode()
    {
        SetupNodeAuthorization(
            ResourceNodeTypes.Team,
            TeamId,
            AuthorizationResult.Success(),
            Operations.Delete
        );

        var error = await AuthorizeDeleteAsync(authenticated: true, CancellationToken.None);

        Assert.Null(error);
    }

    #endregion

    #region ValidateCreateAsync Tests

    [Fact]
    public async Task ValidateCreateAsync_WhenTeamIdIsInvalid_Returns400()
    {
        var error = await CreateAsync(CreateTeam(teamId: "-Bad_Id"), CancellationToken.None);

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
        var error = await CreateAsync(CreateTeam(displayName: displayName), CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal("The display name is required.", error.Detail);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTeamExists_Returns409()
    {
        SetupTeamStore();
        SetupExisting(TeamId, CreateTeam());

        var error = await CreateAsync(CreateTeam(), CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("A team with the specified identifier already exists.", error.Detail);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTeamIdIsRetired_Returns409()
    {
        SetupTeamStore();
        SetupExisting(TeamId, null);
        MockTeamStore
            .Setup(x => x.IsTeamIdRetiredAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var error = await CreateAsync(CreateTeam(), CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(
            "The identifier belonged to a deleted team and cannot be reused.",
            error.Detail
        );
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTheParentIsThePlatformsTeam_Returns409()
    {
        SetupTeamStore();

        var error = await CreateAsync(
            CreateTeam(parentTeamId: RootTenantOptions.DefaultTeamId),
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("The platform's own team cannot hold teams.", error.Detail);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTheParentIsThePlatformsTeamInAnotherCase_Returns409()
    {
        SetupTeamStore();

        var error = await CreateAsync(
            CreateTeam(parentTeamId: RootTenantOptions.DefaultTeamId.ToUpperInvariant()),
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("The platform's own team cannot hold teams.", error.Detail);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTheParentIsMissing_Returns404()
    {
        SetupTeamStore();
        SetupExisting("acme", null);

        var error = await CreateAsync(CreateTeam(parentTeamId: "acme"), CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status404NotFound, error.StatusCode);
        Assert.Equal("The parent team does not exist.", error.Detail);
    }

    [Fact]
    public async Task ValidateCreateAsync_WithAParent_ReturnsNull()
    {
        SetupTeamStore();
        SetupExisting("acme", CreateTeam(teamId: "acme"));
        SetupExisting(TeamId, null);
        MockTeamStore
            .Setup(x => x.IsTeamIdRetiredAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

        var error = await CreateAsync(CreateTeam(parentTeamId: "acme"), CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenValid_ReturnsNull()
    {
        SetupTeamStore();
        SetupExisting(TeamId, null);
        MockTeamStore
            .Setup(x => x.IsTeamIdRetiredAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

        var error = await CreateAsync(CreateTeam(), CancellationToken.None);

        Assert.Null(error);
    }

    #endregion

    #region ValidateUpdateAsync Tests

    [Fact]
    public async Task ValidateUpdateAsync_WhenIfMatchMismatch_Returns412()
    {
        var error = await UpdateAsync(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = "New" },
            ifMatch: "stale-token",
            authenticated: true,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenDisplayNameBlank_Returns400()
    {
        var error = await UpdateAsync(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = " " },
            ifMatch: null,
            authenticated: true,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal("The display name is required.", error.Detail);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenDisablingThePlatformTeam_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await UpdateAsync(
            CreateTeam(teamId: RootTenantOptions.DefaultTeamId),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = true },
            ifMatch: null,
            authenticated: true,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("The platform's own team cannot be disabled.", error.Detail);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenDisablingThePlatformsTeamInAnotherCase_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await UpdateAsync(
            CreateTeam(teamId: RootTenantOptions.DefaultTeamId.ToUpperInvariant()),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = true },
            ifMatch: null,
            authenticated: true,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("The platform's own team cannot be disabled.", error.Detail);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIsDisabledChangesAndTheCallerHasNoPlatformAuthority_ReturnsForbidden()
    {
        SetupNodeAuthorization(
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId,
            AuthorizationResult.Failed()
        );

        var error = await UpdateAsync(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = true },
            ifMatch: null,
            authenticated: true,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
        Assert.Equal("The caller is not authorized to disable or enable this team.", error.Detail);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIsDisabledChangesAndTheCallerIsAnonymous_ReturnsUnauthorized()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await UpdateAsync(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = true },
            ifMatch: null,
            authenticated: false,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIsDisabledChangesAndTheCallerHasPlatformAuthority_ReturnsNull()
    {
        SetupNodeAuthorization(
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId,
            AuthorizationResult.Success()
        );

        var error = await UpdateAsync(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = true },
            ifMatch: null,
            authenticated: true,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIsDisabledChangesOnAChildTeam_AuthorizesUpdateOnTheParentTeamNode()
    {
        SetupNodeAuthorization(
            ResourceNodeTypes.Team,
            "acme",
            AuthorizationResult.Success(),
            Operations.Update
        );

        var error = await UpdateAsync(
            CreateTeam(parentTeamId: "acme"),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = true },
            ifMatch: null,
            authenticated: true,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIsDisabledIsUnchanged_DoesNotAuthorizeAnyNode()
    {
        var error = await UpdateAsync(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = false },
            ifMatch: null,
            authenticated: true,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenValid_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await UpdateAsync(
            CreateTeam(),
            new UpdateTeamRequest { DisplayName = "New", IsDisabled = true },
            ifMatch: "team-ct",
            authenticated: true,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion

    #region ValidateDeleteAsync Tests

    [Fact]
    public async Task ValidateDeleteAsync_WhenTheCallerHasNoAuthorityAboveTheTeam_ReturnsForbidden()
    {
        SetupNodeAuthorization(
            ResourceNodeTypes.Platform,
            OpenIdConstants.PlatformResourceId,
            AuthorizationResult.Failed(),
            Operations.Delete
        );

        var error = await DeleteAsync(CreateTeam(), authenticated: true, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
        Assert.Equal("The caller is not authorized to delete this team.", error.Detail);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenAnonymous_ReturnsUnauthorized()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await DeleteAsync(CreateTeam(), authenticated: false, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
        Assert.Equal("Authentication is required to delete this team.", error.Detail);
    }

    [Fact]
    public async Task ValidateDeleteAsync_ForAChildTeam_AuthorizesDeleteOnTheParentTeamNode()
    {
        SetupNodeAuthorization(
            ResourceNodeTypes.Team,
            "acme",
            AuthorizationResult.Success(),
            Operations.Delete
        );
        SetupTeamStore();
        MockTeamStore
            .Setup(x => x.HasDependentsAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

        var error = await DeleteAsync(
            CreateTeam(parentTeamId: "acme"),
            authenticated: true,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateDeleteAsync_ForATopLevelTeam_AuthorizesDeleteOnThePlatformNode()
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

        var error = await DeleteAsync(CreateTeam(), authenticated: true, CancellationToken.None);

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenPlatformTeam_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await DeleteAsync(
            CreateTeam(teamId: RootTenantOptions.DefaultTeamId),
            authenticated: true,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("The platform's own team cannot be deleted.", error.Detail);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenThePlatformsTeamIsNamedInAnotherCase_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await DeleteAsync(
            CreateTeam(teamId: RootTenantOptions.DefaultTeamId.ToUpperInvariant()),
            authenticated: true,
            CancellationToken.None
        );

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

        var error = await DeleteAsync(CreateTeam(), authenticated: true, CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(
            "The team cannot be deleted while it still has tenants, teams or machine clients.",
            error.Detail
        );
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

        var error = await DeleteAsync(CreateTeam(), authenticated: true, CancellationToken.None);

        Assert.Null(error);
    }

    #endregion
}
