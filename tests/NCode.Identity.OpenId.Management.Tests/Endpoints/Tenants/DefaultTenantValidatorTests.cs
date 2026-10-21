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
using NCode.Identity.OpenId.Management.Contracts.Tenants;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

public sealed class DefaultTenantValidatorTests : IDisposable
{
    private const string TenantId = "tenant-1";
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

    private static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new JsonObject());

    private static PersistedTenant CreateTenant(
        string tenantId = TenantId,
        string concurrencyToken = "tenant-ct",
        string? issuer = Issuer
    ) =>
        new()
        {
            TenantId = tenantId,
            ConcurrencyToken = concurrencyToken,
            Issuer = issuer,
            IsDisabled = false,
            DisplayName = "Tenant One",
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                ConcurrencyToken = "settings-ct",
                Value = EmptyObject(),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
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

    #region ValidateCreateAsync Tests

    [Fact]
    public async Task ValidateCreateAsync_WhenForbiddenAndAuthenticated_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
            CreateTenant(),
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
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status401Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenAddressIsInvalid_Returns400WithEveryReason()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantValidation(Issuer, Invalid("Bad identifier."), Invalid("Bad issuer."));

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantValidation(Issuer, Conflict("Issuer taken."), Invalid("Bad route."));

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantValidation(Issuer);
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant())
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantValidation(Issuer, Conflict("Issuer taken."));
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantValidation(Issuer);
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

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
            CreateTenant(),
            new UpdateTenantRequest { DisplayName = "New Name" },
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
    public async Task ValidateUpdateAsync_WhenDisplayNameMissing_Returns400()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantValidation("https://new.test", Invalid("Bad route."));

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantValidation("https://new.test", Conflict("Issuer taken."));

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantValidation("https://new.test");

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantValidation(issuer: null, Invalid("The tenant requires an issuer."));

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
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
    public async Task ValidateDeleteAsync_WhenForbidden_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.ValidateDeleteAsync(
            CreateUser(authenticated: true),
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenHasDependents_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.HasDependentsAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var error = await Validator.ValidateDeleteAsync(
            CreateUser(authenticated: true),
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
    }

    [Fact]
    public async Task ValidateDeleteAsync_WhenValid_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.HasDependentsAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

        var error = await Validator.ValidateDeleteAsync(
            CreateUser(authenticated: true),
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion
}
