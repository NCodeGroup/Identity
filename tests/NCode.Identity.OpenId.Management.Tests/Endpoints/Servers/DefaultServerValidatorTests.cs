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

namespace NCode.Identity.OpenId.Management.Endpoints.Servers;

public sealed class DefaultServerValidatorTests : IDisposable
{
    private const string ServerId = "server-1";

    private MockRepository MockRepository { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IServerStore> MockServerStore { get; }
    private DefaultServerValidator Validator { get; }

    public DefaultServerValidatorTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockServerStore = MockRepository.Create<IServerStore>();

        Validator = new DefaultServerValidator(MockAuthorizationService.Object);
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

    private static PersistedServer CreateServer(string concurrencyToken = "server-ct") =>
        new()
        {
            ServerId = ServerId,
            ConcurrencyToken = concurrencyToken,
            Settings = new PersistedServerSettings
            {
                ServerId = ServerId,
                ConcurrencyToken = "settings-ct",
                Value = EmptyObject(),
            },
            Secrets = new PersistedServerSecrets
            {
                ServerId = ServerId,
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

    private void SetupServerStore()
    {
        MockStoreManager
            .Setup(x => x.GetStore<IServerStore>())
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
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
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
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedServer?)null)
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
    public async Task ValidateDeleteAsync_WhenHasDependents_Returns409()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupServerStore();
        MockServerStore
            .Setup(x => x.HasDependentsAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var error = await Validator.ValidateDeleteAsync(
            CreateUser(authenticated: true),
            CreateServer(),
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
        SetupServerStore();
        MockServerStore
            .Setup(x => x.HasDependentsAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

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
