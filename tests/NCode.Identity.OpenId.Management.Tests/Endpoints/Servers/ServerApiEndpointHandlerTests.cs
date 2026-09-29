#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Management.Endpoints.Servers;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Mediator;
using NCode.Persistence.Stores;
using SystemTextJsonPatch;
using SystemTextJsonPatch.Operations;
using Xunit;

namespace NCode.Identity.OpenId.Management.Endpoints.Servers;

public sealed class ServerApiEndpointHandlerTests : IDisposable
{
    private const string ServerId = "server-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IServerStore> MockServerStore { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ISecretGenerator> MockSecretGenerator { get; }
    private Mock<IMediator> MockMediator { get; }
    private ServerApiEndpointHandler Handler { get; }

    public ServerApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockServerStore = MockRepository.Create<IServerStore>();
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockSecretGenerator = MockRepository.Create<ISecretGenerator>();
        MockMediator = MockRepository.Create<IMediator>();

        Handler = new ServerApiEndpointHandler(
            MockStoreManagerFactory.Object,
            MockAuthorizationService.Object,
            MockSecretGenerator.Object,
            TimeProvider.System,
            NullLogger<ServerApiEndpointHandler>.Instance
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private void SetupStore()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();

        MockStoreManager
            .Setup(x => x.GetStore<IServerStore>())
            .Returns(MockServerStore.Object)
            .Verifiable();

        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

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

    private static HttpContext CreateHttpContext(bool authenticated, string? ifNoneMatch = null)
    {
        var identity = authenticated
            ? new ClaimsIdentity(authenticationType: "test")
            : new ClaimsIdentity();
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        if (ifNoneMatch is not null)
        {
            httpContext.Request.Headers.IfNoneMatch = ifNoneMatch;
        }

        return httpContext;
    }

    private static JsonElement EmptyObject()
    {
        return JsonSerializer.SerializeToElement(new JsonObject());
    }

    private static PersistedServerSettings CreateSettings(
        string concurrencyToken,
        JsonElement value
    )
    {
        return new PersistedServerSettings
        {
            ServerId = ServerId,
            ConcurrencyToken = concurrencyToken,
            Value = value,
        };
    }

    private static PersistedServerSecrets CreateSecrets(string concurrencyToken)
    {
        return new PersistedServerSecrets
        {
            ServerId = ServerId,
            ConcurrencyToken = concurrencyToken,
            Value = [],
        };
    }

    private static PersistedServer CreateServer(string concurrencyToken)
    {
        return new PersistedServer
        {
            ServerId = ServerId,
            ConcurrencyToken = concurrencyToken,
            Settings = CreateSettings("settings-ct", EmptyObject()),
            Secrets = CreateSecrets("secrets-ct"),
        };
    }

    #endregion

    #region GetServerAsync Tests

    [Fact]
    public async Task GetServerAsync_WhenFoundAndAuthorized_ReturnsJsonWithETag()
    {
        SetupStore();
        var server = CreateServer("server-ct");
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(server)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetServerAsync(httpContext, ServerId, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<ServerResource>>(result);
        Assert.Equal(ServerId, json.Value?.ServerId);
        Assert.Equal("server-ct", json.Value?.ConcurrencyToken);
        Assert.Equal("server-ct", httpContext.Response.Headers.ETag);
    }

    [Fact]
    public async Task GetServerAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedServer?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetServerAsync(httpContext, ServerId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task GetServerAsync_WhenForbidden_ReturnsForbid()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateServer("server-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetServerAsync(httpContext, ServerId, CancellationToken.None);

        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task GetServerAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateServer("server-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: false);

        var result = await Handler.GetServerAsync(httpContext, ServerId, CancellationToken.None);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task GetServerAsync_WhenIfNoneMatchMatches_ReturnsNotModified()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateServer("server-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true, ifNoneMatch: "server-ct");

        var result = await Handler.GetServerAsync(httpContext, ServerId, CancellationToken.None);

        var statusCode = Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, statusCode.StatusCode);
    }

    #endregion

    #region GetSettingsAsync Tests

    [Fact]
    public async Task GetSettingsAsync_WhenFoundAndAuthorized_ReturnsJson()
    {
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockServerStore
            .Setup(x => x.GetSettingsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSettingsAsync(httpContext, ServerId, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<ServerSettingsResource>>(result);
        Assert.Equal(ServerId, json.Value?.ServerId);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetSettingsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedServerSettings?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSettingsAsync(httpContext, ServerId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    #endregion

    #region GetSecretsAsync Tests

    [Fact]
    public async Task GetSecretsAsync_WhenFoundAndAuthorized_ReturnsJson()
    {
        SetupStore();
        var secrets = CreateSecrets("secrets-ct");
        MockServerStore
            .Setup(x => x.GetSecretsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secrets)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretsAsync(httpContext, ServerId, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<ServerSecretsResource>>(result);
        Assert.Equal(ServerId, json.Value?.ServerId);
        Assert.Empty(json.Value?.Secrets ?? []);
    }

    [Fact]
    public async Task GetSecretsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetSecretsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedServerSecrets?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretsAsync(httpContext, ServerId, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    #endregion

    #region UpdateSettingsAsync Tests

    private static JsonPatchDocument<JsonObject> CreateAddPatch(string name, string value)
    {
        var patch = new JsonPatchDocument<JsonObject>();
        patch.Operations.Add(
            new Operation<JsonObject>("add", $"/{name}", from: null, value: value)
        );
        return patch;
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenAuthorizedAndNoIfMatch_AppliesPatchAndReturnsNoContent()
    {
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockServerStore
            .Setup(x => x.GetSettingsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockServerStore
            .Setup(x => x.UpdateSettingsAsync(settings, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);
        var patch = CreateAddPatch("foo", "bar");

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            ServerId,
            patch,
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
        Assert.Equal("settings-ct", httpContext.Response.Headers.ETag);
        Assert.Equal("bar", settings.Value.GetProperty("foo").GetString());
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenIfMatchMatches_AppliesPatch()
    {
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockServerStore
            .Setup(x => x.GetSettingsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockServerStore
            .Setup(x => x.UpdateSettingsAsync(settings, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            ServerId,
            CreateAddPatch("foo", "bar"),
            ifMatch: "settings-ct",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenIfMatchMismatch_ReturnsPreconditionFailed()
    {
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockServerStore
            .Setup(x => x.GetSettingsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            ServerId,
            CreateAddPatch("foo", "bar"),
            ifMatch: "stale-token",
            CancellationToken.None
        );

        var statusCode = Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, statusCode.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenPatchInvalid_ReturnsBadRequest()
    {
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockServerStore
            .Setup(x => x.GetSettingsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        // Replace on a path that does not exist throws a JsonPatchException.
        var patch = new JsonPatchDocument<JsonObject>();
        patch.Operations.Add(
            new Operation<JsonObject>("replace", "/missing", from: null, value: "value")
        );

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            ServerId,
            patch,
            ifMatch: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetSettingsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedServerSettings?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            ServerId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenForbidden_ReturnsForbid()
    {
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockServerStore
            .Setup(x => x.GetSettingsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            ServerId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenUnauthenticated_ReturnsUnauthorized()
    {
        SetupStore();
        var settings = CreateSettings("settings-ct", EmptyObject());
        MockServerStore
            .Setup(x => x.GetSettingsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: false);

        var result = await Handler.UpdateSettingsAsync(
            httpContext,
            ServerId,
            CreateAddPatch("foo", "bar"),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    #endregion

    #region Secret Helpers

    private static PersistedSecret CreatePersistedSecret(
        string secretId = "secret-1",
        string concurrencyToken = "secret-ct"
    ) =>
        new()
        {
            SecretId = secretId,
            ConcurrencyToken = concurrencyToken,
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretTypes.Symmetric,
            KeySizeBits = 256,
            EncodedValue = "encoded",
        };

    private static CreateSecretRequest CreateSecretRequest() =>
        new()
        {
            SecretId = "secret-1",
            SecretType = SecretTypes.Symmetric,
            KeySizeBits = 256,
            Use = "sig",
            Algorithm = "RS256",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
        };

    #endregion

    #region CreateSecretAsync Tests

    [Fact]
    public async Task CreateSecretAsync_WhenAuthorized_GeneratesAndReturnsCreated()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateServer("server-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var generated = CreatePersistedSecret();
        MockSecretGenerator
            .Setup(x => x.GenerateSecret(It.IsAny<GenerateSecretRequest>()))
            .Returns(generated)
            .Verifiable();
        MockServerStore
            .Setup(x => x.AddSecretAsync(ServerId, generated, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            ServerId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var created = Assert.IsType<Created<SecretResource>>(result);
        Assert.Equal("secret-1", created.Value?.SecretId);
        Assert.Equal("secret-ct", httpContext.Response.Headers.ETag);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenServerNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedServer?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            ServerId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenForbidden_ReturnsForbid()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateServer("server-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Failed());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            ServerId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenGeneratorRejectsInput_ReturnsBadRequest()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateServer("server-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockSecretGenerator
            .Setup(x => x.GenerateSecret(It.IsAny<GenerateSecretRequest>()))
            .Throws(new ArgumentException("bad size"))
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            ServerId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task CreateSecretAsync_WhenDuplicate_ReturnsConflict()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateServer("server-ct"))
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var generated = CreatePersistedSecret();
        MockSecretGenerator
            .Setup(x => x.GenerateSecret(It.IsAny<GenerateSecretRequest>()))
            .Returns(generated)
            .Verifiable();
        MockServerStore
            .Setup(x => x.AddSecretAsync(ServerId, generated, It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("duplicate"))
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.CreateSecretAsync(
            httpContext,
            ServerId,
            CreateSecretRequest(),
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    #endregion

    #region GetSecretAsync Tests

    [Fact]
    public async Task GetSecretAsync_WhenFoundAndAuthorized_ReturnsJson()
    {
        SetupStore();
        MockServerStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ServerId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretAsync(
            httpContext,
            ServerId,
            "secret-1",
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<SecretResource>>(result);
        Assert.Equal("secret-1", json.Value?.SecretId);
    }

    [Fact]
    public async Task GetSecretAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockServerStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ServerId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedSecret?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.GetSecretAsync(
            httpContext,
            ServerId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    #endregion

    #region UpdateSecretAsync Tests

    private static UpdateSecretRequest UpdateSecretRequest() =>
        new()
        {
            Use = "enc",
            Algorithm = "RS512",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(2),
        };

    [Fact]
    public async Task UpdateSecretAsync_WhenAuthorized_ReturnsNoContent()
    {
        SetupStore();
        MockServerStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ServerId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockServerStore
            .Setup(x =>
                x.UpdateSecretAsync(
                    ServerId,
                    It.IsAny<PersistedSecret>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSecretAsync(
            httpContext,
            ServerId,
            "secret-1",
            UpdateSecretRequest(),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockServerStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ServerId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedSecret?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSecretAsync(
            httpContext,
            ServerId,
            "secret-1",
            UpdateSecretRequest(),
            ifMatch: null,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task UpdateSecretAsync_WhenIfMatchMismatch_ReturnsPreconditionFailed()
    {
        SetupStore();
        MockServerStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ServerId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.UpdateSecretAsync(
            httpContext,
            ServerId,
            "secret-1",
            UpdateSecretRequest(),
            ifMatch: "stale-token",
            CancellationToken.None
        );

        var statusCode = Assert.IsType<StatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, statusCode.StatusCode);
    }

    #endregion

    #region DeleteSecretAsync Tests

    [Fact]
    public async Task DeleteSecretAsync_WhenAuthorized_ReturnsNoContent()
    {
        SetupStore();
        MockServerStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ServerId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedSecret())
            .Verifiable();
        SetupAuthorization(AuthorizationResult.Success());
        MockServerStore
            .Setup(x => x.RemoveSecretAsync(ServerId, "secret-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteSecretAsync(
            httpContext,
            ServerId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task DeleteSecretAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockServerStore
            .Setup(x =>
                x.GetSecretOrDefaultAsync(ServerId, "secret-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedSecret?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteSecretAsync(
            httpContext,
            ServerId,
            "secret-1",
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    #endregion

    #region DeleteServerAsync Tests

    [Fact]
    public async Task DeleteServerAsync_WhenPreconditionsPass_ReturnsNoContent()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateServer("server-ct"))
            .Verifiable();
        MockMediator
            .Setup(x =>
                x.SendAsync(It.IsAny<ValidateDeleteServerCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockServerStore
            .Setup(x => x.RemoveAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteServerAsync(
            httpContext,
            ServerId,
            MockMediator.Object,
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task DeleteServerAsync_WhenPipelineReportsConflict_ReturnsProblem()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateServer("server-ct"))
            .Verifiable();
        MockMediator
            .Setup(x =>
                x.SendAsync(It.IsAny<ValidateDeleteServerCommand>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (ValidateDeleteServerCommand command, CancellationToken _) =>
                    command.Disposition.Error = new ManagementError
                    {
                        StatusCode = StatusCodes.Status409Conflict,
                        Detail = "conflict",
                    }
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteServerAsync(
            httpContext,
            ServerId,
            MockMediator.Object,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteServerAsync_WhenNotFound_ReturnsNotFound()
    {
        SetupStore();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedServer?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.DeleteServerAsync(
            httpContext,
            ServerId,
            MockMediator.Object,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    #endregion
}
