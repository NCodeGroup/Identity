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
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

public sealed class DefaultTenantValidatorTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string DomainName = "example.test";

    private MockRepository MockRepository { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private DefaultTenantValidator Validator { get; }

    public DefaultTenantValidatorTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTenantStore = MockRepository.Create<ITenantStore>();

        Validator = new DefaultTenantValidator(MockAuthorizationService.Object);
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
        string? domainName = DomainName
    ) =>
        new()
        {
            TenantId = tenantId,
            ConcurrencyToken = concurrencyToken,
            DomainName = domainName,
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
    public async Task ValidateCreateAsync_WhenTenantIdExists_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());
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
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenDomainNameExists_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultByDomainNameAsync(DomainName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant(tenantId: "other-tenant"))
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
            CreateTenant(),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenDomainNameNull_SkipsDomainCheckAndReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
            CreateTenant(domainName: null),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenValid_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultByDomainNameAsync(DomainName, It.IsAny<CancellationToken>()))
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
    public async Task ValidateUpdateAsync_WhenDomainNameChangedAndTakenByOther_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultByDomainNameAsync("new.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant(tenantId: "other-tenant"))
            .Verifiable();

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
            CreateTenant(),
            new UpdateTenantRequest { DisplayName = "New Name", DomainName = "new.test" },
            ifMatch: null,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenDomainNameChangedButOwnedBySameTenant_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantStore();
        MockTenantStore
            .Setup(x => x.GetOrDefaultByDomainNameAsync("new.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTenant(tenantId: TenantId))
            .Verifiable();

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
            CreateTenant(),
            new UpdateTenantRequest { DisplayName = "New Name", DomainName = "new.test" },
            ifMatch: null,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateUpdateAsync_WhenDomainNameUnchanged_SkipsDomainCheckAndReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
            CreateTenant(domainName: DomainName),
            new UpdateTenantRequest { DisplayName = "New Name", DomainName = DomainName },
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
