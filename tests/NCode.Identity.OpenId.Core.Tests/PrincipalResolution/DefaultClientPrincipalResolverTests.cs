// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.PrincipalResolution;

public sealed class DefaultClientPrincipalResolverTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";
    private const string PrincipalId = "principal-1";
    private const string OtherPrincipalId = "principal-2";

    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdTenant> MockTenant { get; }
    private DefaultClientPrincipalResolver Resolver { get; }

    public DefaultClientPrincipalResolverTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockClientStore = CreateStrictMock<IClientStore>();
        MockAmbientTenantAccessor = CreateStrictMock<IAmbientTenantAccessor>();
        MockContext = CreateStrictMock<OpenIdContext>();
        MockTenant = CreateStrictMock<OpenIdTenant>();

        MockContext
            .Setup(x => x.GetSubjectId(It.IsAny<ClaimsPrincipal>()))
            .Returns((ClaimsPrincipal user) => user.FindFirstValue("sub"))
            .Verifiable();

        Resolver = new DefaultClientPrincipalResolver(
            MockStoreManagerFactory.Object,
            MockAmbientTenantAccessor.Object
        );
    }

    #region GetActingClientOrDefaultAsync Tests

    [Fact]
    public async Task GetActingClientOrDefaultAsync_WhenTheSubjectIsTheNamedClientsPrincipal_ReturnsTheClient()
    {
        var client = CreateClient();
        SetupClientRead(client);

        var result = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            CreateUser(PrincipalId, ClientId),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Same(client, result);
    }

    [Fact]
    public async Task GetActingClientOrDefaultAsync_WhenTheSubjectIsAnotherPrincipal_ReturnsNull()
    {
        SetupClientRead(CreateClient());

        var result = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            CreateUser(OtherPrincipalId, ClientId),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActingClientOrDefaultAsync_WhenTheTokenNamesNoClient_ReturnsNull()
    {
        var result = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            CreateUser(PrincipalId, null),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActingClientOrDefaultAsync_WhenTheClientIsDisabled_ReturnsTheClient()
    {
        var client = CreateClient(isDisabled: true);
        SetupClientRead(client);

        var result = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            CreateUser(PrincipalId, ClientId),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Same(client, result);
    }

    [Fact]
    public async Task GetActingClientOrDefaultAsync_WhenTheClientDoesNotExist_ReturnsNull()
    {
        SetupClientRead(null);

        var result = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            CreateUser(PrincipalId, ClientId),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActingClientOrDefaultAsync_WithoutAStoreManager_ReadsThroughItsOwnStoreManager()
    {
        var client = CreateClient();
        SetupClientRead(client);
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        var result = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            CreateUser(PrincipalId, ClientId),
            CancellationToken.None
        );

        Assert.Same(client, result);
    }

    [Fact]
    public async Task GetActingClientOrDefaultAsync_WhenTheCallerWasResolvedInTheRequest_ReturnsTheSameClientWithoutReading()
    {
        var client = CreateClient();
        SetupClientRead(client);
        var user = CreateUser(PrincipalId, ClientId);
        var first = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            user,
            MockStoreManager.Object,
            CancellationToken.None
        );
        SetupNextClientRead(null, Times.Never());

        var second = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            user,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Same(client, first);
        Assert.Same(client, second);
    }

    [Fact]
    public async Task GetActingClientOrDefaultAsync_WhenTheCallerWasResolvedAsNoClient_ReturnsNullWithoutReading()
    {
        SetupClientRead(null);
        var user = CreateUser(PrincipalId, ClientId);
        var first = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            user,
            MockStoreManager.Object,
            CancellationToken.None
        );
        var client = CreateClient();
        SetupNextClientRead(client, Times.Never());

        var second = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            user,
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Null(first);
        Assert.Null(second);
    }

    [Fact]
    public async Task GetActingClientOrDefaultAsync_WithoutAStoreManagerWhenTheCallerWasResolved_CreatesNoStoreManager()
    {
        var client = CreateClient();
        SetupClientRead(client);
        var user = CreateUser(PrincipalId, ClientId);
        var first = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            user,
            MockStoreManager.Object,
            CancellationToken.None
        );

        var second = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            user,
            CancellationToken.None
        );

        Assert.Same(client, first);
        Assert.Same(client, second);
    }

    [Fact]
    public async Task GetActingClientOrDefaultAsync_ForAnotherSubject_ReadsItsClient()
    {
        var client = CreateClient();
        SetupClientRead(client);
        var first = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            CreateUser(PrincipalId, ClientId),
            MockStoreManager.Object,
            CancellationToken.None
        );
        var otherClient = CreateClient(principalId: OtherPrincipalId);
        SetupNextClientRead(otherClient, Times.Once());

        var second = await Resolver.GetActingClientOrDefaultAsync(
            MockContext.Object,
            CreateUser(OtherPrincipalId, ClientId),
            MockStoreManager.Object,
            CancellationToken.None
        );

        Assert.Same(client, first);
        Assert.Same(otherClient, second);
    }

    #endregion

    #region Scaffolding

    private DefaultHttpContext HttpContext { get; } = new();

    private static ClaimsPrincipal CreateUser(string subject, string? clientId)
    {
        List<Claim> claims = [new Claim("sub", subject)];
        if (clientId is not null)
        {
            claims.Add(new Claim("client_id", clientId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static PersistedClient CreateClient(
        bool isDisabled = false,
        string principalId = PrincipalId
    ) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            PrincipalId = principalId,
            ConcurrencyToken = "ct",
            IsDisabled = isDisabled,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = "ct",
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = "ct",
                Value = [],
            },
        };

    // Expects the client to be read under the context's tenant scope, which ends after the read.
    private void SetupClientRead(PersistedClient? client)
    {
        MockContext.Setup(x => x.Tenant).Returns(MockTenant.Object).Verifiable();
        MockContext.Setup(x => x.Http).Returns(HttpContext).Verifiable();
        MockTenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(MockClientStore.Object)
            .Verifiable();

        var mockScope = CreateStrictMock<IDisposable>();
        mockScope.Setup(x => x.Dispose()).Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(mockScope.Object)
            .Verifiable();

        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client)
            .Verifiable();
    }

    // Replaces the client read of SetupClientRead, so the test states how many more reads it expects.
    private void SetupNextClientRead(PersistedClient? client, Times times) =>
        MockClientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client)
            .Verifiable(times);

    #endregion
}
