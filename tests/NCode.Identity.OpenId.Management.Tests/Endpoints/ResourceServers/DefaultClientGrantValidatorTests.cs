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
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

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

    private static JsonElement EmptyObject() => JsonSerializer.SerializeToElement(new JsonObject());

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
        new()
        {
            TenantId = TenantId,
            ResourceServerId = ResourceServerId,
            Identifier = "https://api.example.com",
            ConcurrencyToken = "rs-ct",
            Name = "Example API",
            IsSystem = false,
            IsDisabled = false,
            Settings = EmptyObject(),
            Scopes = scopeValues
                .Select(value => new PersistedScope
                {
                    Value = value,
                    Description = null,
                    IsSystem = false,
                })
                .ToList(),
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

    #region ValidateCreateAsync Tests

    [Fact]
    public async Task ValidateCreateAsync_WhenForbidden_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
            CreateGrant(KnownScope),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task ValidateCreateAsync_WhenResourceServerMissing_Returns404()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceServerStore(resourceServer: null);

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceServerStore(CreateResourceServer(KnownScope));

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceServerStore(CreateResourceServer(KnownScope));
        SetupClientGrantStore(CreateGrant(KnownScope));

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceServerStore(CreateResourceServer(KnownScope));
        SetupClientGrantStore(existing: null);

        var error = await Validator.ValidateCreateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceServerStore(CreateResourceServer(KnownScope));

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
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
        SetupAuthorization(AuthorizationResult.Success());
        SetupResourceServerStore(CreateResourceServer(KnownScope));

        var error = await Validator.ValidateUpdateAsync(
            CreateUser(authenticated: true),
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
    public async Task ValidateDeleteAsync_WhenForbidden_Returns403()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var error = await Validator.ValidateDeleteAsync(
            CreateUser(authenticated: true),
            CreateGrant(KnownScope),
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
            CreateGrant(KnownScope),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    #endregion
}
