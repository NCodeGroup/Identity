// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

public sealed class DefaultTenantValidatorTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string TeamId = "platform";
    private const string ControlPlaneTenantId = "platform-root";
    private const string Issuer = "https://example.test";

    private MockRepository MockRepository { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ITenantResolver> MockTenantResolver { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private DefaultTenantValidator Validator { get; }

    public DefaultTenantValidatorTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockTenantResolver = MockRepository.Create<ITenantResolver>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTenantStore = MockRepository.Create<ITenantStore>();

        Validator = new DefaultTenantValidator(
            MockAuthorizationService.Object,
            MockTenantResolver.Object
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

    private static PersistedTenant CreateTenant(
        string tenantId = TenantId,
        string concurrencyToken = "tenant-ct",
        string? issuer = Issuer,
        string displayName = "Tenant One"
    ) =>
        new PersistedTenantBuilder
        {
            TenantId = tenantId,
            TeamId = TeamId,
            ConcurrencyToken = concurrencyToken,
            Issuer = issuer,
            DisplayName = displayName,
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

    private void SetupTenantStore()
    {
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
    }

    private void SetupTenantValidation(string? issuer, params TenantValidationFailure[] failures)
    {
        MockTenantResolver
            .Setup(x =>
                x.ValidateTenantAsync(
                    TenantId,
                    issuer,
                    null,
                    MockStoreManager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(failures)
            .Verifiable();
    }

    private static TenantValidationFailure Invalid(string detail) => new() { Detail = detail };

    private static TenantValidationFailure Conflict(string detail) =>
        new() { Detail = detail, IsConflict = true };

    #endregion

    #region AuthorizeCreateAsync Tests

    [Fact]
    public async Task AuthorizeCreateAsync_WhenAuthorized_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.AuthorizeCreateAsync(
            CreateUser(authenticated: true),
            ControlPlaneTenantId,
            TeamId,
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
            ControlPlaneTenantId,
            TeamId,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
        Assert.Equal("The caller is not authorized to create this tenant.", error.Detail);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_WhenForbiddenAndAnonymous_Returns401()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.AuthorizeCreateAsync(
            CreateUser(authenticated: false),
            ControlPlaneTenantId,
            TeamId,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
        Assert.Equal("Authentication is required to create this tenant.", error.Detail);
    }

    [Fact]
    public async Task AuthorizeCreateAsync_AuthorizesCreateOnTheTeamNode()
    {
        var authorizedResource = SetupAuthorizationCapturingTheNode(Operations.Create);

        await Validator.AuthorizeCreateAsync(
            CreateUser(authenticated: true),
            ControlPlaneTenantId,
            TeamId,
            CancellationToken.None
        );

        var node = Assert.IsType<ResourceNode>(authorizedResource());
        Assert.Equal(ControlPlaneTenantId, node.TenantId);
        Assert.Equal(ResourceNodeTypes.Team, node.ResourceType);
        Assert.Equal(TeamId, node.ResourceId);
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
        Assert.Equal("The caller is not authorized to update this tenant.", error.Detail);
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
        Assert.Equal("Authentication is required to update this tenant.", error.Detail);
    }

    [Fact]
    public async Task AuthorizeUpdateAsync_AuthorizesUpdateOnTheTenantNode()
    {
        var authorizedResource = SetupAuthorizationCapturingTheNode(Operations.Update);

        await Validator.AuthorizeUpdateAsync(
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
        Assert.Equal("The caller is not authorized to delete this tenant.", error.Detail);
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
        Assert.Equal("Authentication is required to delete this tenant.", error.Detail);
    }

    [Fact]
    public async Task AuthorizeDeleteAsync_AuthorizesDeleteOnTheTenantNode()
    {
        var authorizedResource = SetupAuthorizationCapturingTheNode(Operations.Delete);

        await Validator.AuthorizeDeleteAsync(
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

    #region ValidateCreateAsync Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateCreateAsync_WhenTheDisplayNameIsBlank_ReturnsBadRequest(
        string displayName
    )
    {
        var error = await Validator.ValidateCreateAsync(
            CreateTenant(displayName: displayName),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal("The 'displayName' is required.", error.Detail);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenAddressIsInvalid_Returns400WithEveryReason()
    {
        SetupTenantValidation(Issuer, Invalid("Bad identifier."), Invalid("Bad issuer."));

        var error = await Validator.ValidateCreateAsync(
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal("Bad identifier. Bad issuer.", error.Detail);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenAddressIsInvalidAndConflicts_Returns400()
    {
        SetupTenantValidation(Issuer, Conflict("Issuer taken."), Invalid("Bad route."));

        var error = await Validator.ValidateCreateAsync(
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal("Bad route.", error.Detail);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenTenantIdExists_Returns409()
    {
        SetupTenantValidation(Issuer);
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant())
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("A tenant with the specified identifier already exists.", error.Detail);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenAddressConflicts_Returns409()
    {
        SetupTenantValidation(Issuer, Conflict("Issuer taken."));
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("Issuer taken.", error.Detail);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenValid_ReturnsNull()
    {
        SetupTenantValidation(Issuer);
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion

    #region ValidateUpdateAsync Tests

    [Fact]
    public async Task ValidateUpdateAsync_WhenIfMatchMismatch_Returns412()
    {
        var error = await Validator.ValidateUpdateAsync(
            CreateTenant(concurrencyToken: "tenant-ct"),
            new UpdateTenantRequest { DisplayName = "New Name" },
            ifMatch: "stale-token",
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenTheDisplayNameIsWhitespace_ReturnsBadRequest()
    {
        var error = await Validator.ValidateUpdateAsync(
            CreateTenant(),
            new UpdateTenantRequest { DisplayName = "   " },
            ifMatch: null,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal("The 'displayName' is required.", error.Detail);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenDisplayNameMissing_Returns400()
    {
        var error = await Validator.ValidateUpdateAsync(
            CreateTenant(),
            new UpdateTenantRequest { DisplayName = null },
            ifMatch: null,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIssuerChangedAndAddressIsInvalid_Returns400()
    {
        SetupTenantValidation("https://new.test", Invalid("Bad route."));

        var error = await Validator.ValidateUpdateAsync(
            CreateTenant(),
            new UpdateTenantRequest { DisplayName = "New Name", Issuer = "https://new.test" },
            ifMatch: null,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
        Assert.Equal("Bad route.", error.Detail);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIssuerChangedAndAddressConflicts_Returns409()
    {
        SetupTenantValidation("https://new.test", Conflict("Issuer taken."));

        var error = await Validator.ValidateUpdateAsync(
            CreateTenant(),
            new UpdateTenantRequest { DisplayName = "New Name", Issuer = "https://new.test" },
            ifMatch: null,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal("Issuer taken.", error.Detail);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIssuerChangedAndValid_ReturnsNull()
    {
        SetupTenantValidation("https://new.test");

        var error = await Validator.ValidateUpdateAsync(
            CreateTenant(),
            new UpdateTenantRequest { DisplayName = "New Name", Issuer = "https://new.test" },
            ifMatch: null,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIssuerCleared_ValidatesTheTenantWithoutAnIssuer()
    {
        SetupTenantValidation(issuer: null, Invalid("The tenant requires an issuer."));

        var error = await Validator.ValidateUpdateAsync(
            CreateTenant(issuer: Issuer),
            new UpdateTenantRequest { DisplayName = "New Name", Issuer = null },
            ifMatch: null,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status400BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenIssuerUnchanged_SkipsAddressValidationAndReturnsNull()
    {
        var error = await Validator.ValidateUpdateAsync(
            CreateTenant(issuer: Issuer),
            new UpdateTenantRequest { DisplayName = "New Name", Issuer = Issuer },
            ifMatch: null,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion

    #region ValidateDeleteAsync Tests

    [Fact]
    public async Task ValidateDeleteAsync_WhenRequiredTenant_Returns409WithoutCheckingDependents()
    {
        MockTenantResolver.Setup(x => x.IsRequiredTenant(TenantId)).Returns(true).Verifiable();

        var error = await Validator.ValidateDeleteAsync(
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Contains("required by the deployment", error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenHasDependents_Returns409()
    {
        MockTenantResolver.Setup(x => x.IsRequiredTenant(TenantId)).Returns(false).Verifiable();
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.HasDependentsAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var error = await Validator.ValidateDeleteAsync(
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Contains("clients or resource servers", error.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenValid_ReturnsNull()
    {
        MockTenantResolver.Setup(x => x.IsRequiredTenant(TenantId)).Returns(false).Verifiable();
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.HasDependentsAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

        var error = await Validator.ValidateDeleteAsync(
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion
}
