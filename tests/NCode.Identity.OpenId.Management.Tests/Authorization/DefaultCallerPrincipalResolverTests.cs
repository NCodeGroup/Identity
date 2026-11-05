// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Authorization;

public class DefaultCallerPrincipalResolverTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string Subject = "subject-1";
    private const string SubjectPrincipalId = "subject-principal-1";
    private const string ClientId = "client-1";
    private const string ClientPrincipalId = "client-principal-1";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    private static ClaimsPrincipal CreateUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    // The caller's subject is derived through the tenant-aware context seam, as the principal resolver derives it.
    private OpenIdContext CreateOpenIdContext()
    {
        var context = MockRepository.Create<OpenIdContext>();
        context
            .Setup(x => x.GetSubjectId(It.IsAny<ClaimsPrincipal>()))
            .Returns((ClaimsPrincipal user) => user.FindFirstValue("sub"))
            .Verifiable();
        return context.Object;
    }

    private static PersistedClient CreateClient(bool isDisabled = false) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = isDisabled,
            PrincipalId = ClientPrincipalId,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = ClientId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    // The store manager hands out the client store, which answers the one client lookup the resolver makes.
    private Mock<IStoreManager> CreateStoreManagerWithClient(PersistedClient? client)
    {
        var manager = MockRepository.Create<IStoreManager>();
        var clientStore = MockRepository.Create<IClientStore>();

        manager.Setup(x => x.GetStore<IClientStore>()).Returns(clientStore.Object).Verifiable();
        clientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client)
            .Verifiable();

        return manager;
    }

    #region ResolveCallerPrincipalIdOrDefaultAsync Tests

    [Fact]
    public async Task ResolveCallerPrincipalIdOrDefaultAsync_WhenTheCallerNamesASubject_DelegatesToThePrincipalResolver()
    {
        var manager = MockRepository.Create<IStoreManager>();
        var principalResolver = MockRepository.Create<IPrincipalResolver>();
        var openIdContext = CreateOpenIdContext();
        var caller = CreateUser(new Claim("sub", Subject), new Claim("client_id", ClientId));

        principalResolver
            .Setup(x =>
                x.ResolvePrincipalIdOrDefaultAsync(
                    openIdContext,
                    caller,
                    manager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(SubjectPrincipalId)
            .Verifiable();

        var resolver = new DefaultCallerPrincipalResolver(principalResolver.Object);

        var result = await resolver.ResolveCallerPrincipalIdOrDefaultAsync(
            openIdContext,
            caller,
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(SubjectPrincipalId, result);
    }

    [Fact]
    public async Task ResolveCallerPrincipalIdOrDefaultAsync_WhenAClientActsForItself_ReturnsTheClientsPrincipalId()
    {
        var manager = CreateStoreManagerWithClient(CreateClient());
        var resolver = new DefaultCallerPrincipalResolver(
            MockRepository.Create<IPrincipalResolver>().Object
        );

        var result = await resolver.ResolveCallerPrincipalIdOrDefaultAsync(
            CreateOpenIdContext(),
            CreateUser(new Claim("client_id", ClientId)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(ClientPrincipalId, result);
    }

    [Fact]
    public async Task ResolveCallerPrincipalIdOrDefaultAsync_WhenTheClientIsDisabled_ReturnsNull()
    {
        var manager = CreateStoreManagerWithClient(CreateClient(isDisabled: true));
        var resolver = new DefaultCallerPrincipalResolver(
            MockRepository.Create<IPrincipalResolver>().Object
        );

        var result = await resolver.ResolveCallerPrincipalIdOrDefaultAsync(
            CreateOpenIdContext(),
            CreateUser(new Claim("client_id", ClientId)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveCallerPrincipalIdOrDefaultAsync_WhenTheClientIsUnknown_ReturnsNull()
    {
        var manager = CreateStoreManagerWithClient(null);
        var resolver = new DefaultCallerPrincipalResolver(
            MockRepository.Create<IPrincipalResolver>().Object
        );

        var result = await resolver.ResolveCallerPrincipalIdOrDefaultAsync(
            CreateOpenIdContext(),
            CreateUser(new Claim("client_id", ClientId)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveCallerPrincipalIdOrDefaultAsync_WhenTheCallerNamesNeitherSubjectNorClient_ReturnsNull()
    {
        var resolver = new DefaultCallerPrincipalResolver(
            MockRepository.Create<IPrincipalResolver>().Object
        );

        var result = await resolver.ResolveCallerPrincipalIdOrDefaultAsync(
            CreateOpenIdContext(),
            CreateUser(),
            MockRepository.Create<IStoreManager>().Object,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    #endregion

    #region ResolveCallerPrincipalIdAsync Tests

    [Fact]
    public async Task ResolveCallerPrincipalIdAsync_WhenTheCallerNamesASubject_DelegatesToThePrincipalResolver()
    {
        var manager = MockRepository.Create<IStoreManager>();
        var principalResolver = MockRepository.Create<IPrincipalResolver>();
        var openIdContext = CreateOpenIdContext();
        var caller = CreateUser(new Claim("sub", Subject), new Claim("client_id", ClientId));

        principalResolver
            .Setup(x =>
                x.ResolvePrincipalIdAsync(
                    openIdContext,
                    caller,
                    manager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(SubjectPrincipalId)
            .Verifiable();

        var resolver = new DefaultCallerPrincipalResolver(principalResolver.Object);

        var result = await resolver.ResolveCallerPrincipalIdAsync(
            openIdContext,
            caller,
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(SubjectPrincipalId, result);
    }

    [Fact]
    public async Task ResolveCallerPrincipalIdAsync_WhenAClientActsForItself_ReturnsItsPrincipalWithoutProvisioning()
    {
        var manager = CreateStoreManagerWithClient(CreateClient());
        var resolver = new DefaultCallerPrincipalResolver(
            MockRepository.Create<IPrincipalResolver>().Object
        );

        var result = await resolver.ResolveCallerPrincipalIdAsync(
            CreateOpenIdContext(),
            CreateUser(new Claim("client_id", ClientId)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(ClientPrincipalId, result);
    }

    [Fact]
    public async Task ResolveCallerPrincipalIdAsync_WhenTheClientIsDisabled_ReturnsEmpty()
    {
        var manager = CreateStoreManagerWithClient(CreateClient(isDisabled: true));
        var resolver = new DefaultCallerPrincipalResolver(
            MockRepository.Create<IPrincipalResolver>().Object
        );

        var result = await resolver.ResolveCallerPrincipalIdAsync(
            CreateOpenIdContext(),
            CreateUser(new Claim("client_id", ClientId)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ResolveCallerPrincipalIdAsync_WhenTheCallerNamesNeitherSubjectNorClient_ReturnsEmpty()
    {
        var resolver = new DefaultCallerPrincipalResolver(
            MockRepository.Create<IPrincipalResolver>().Object
        );

        var result = await resolver.ResolveCallerPrincipalIdAsync(
            CreateOpenIdContext(),
            CreateUser(),
            MockRepository.Create<IStoreManager>().Object,
            CancellationToken.None
        );

        Assert.Equal(string.Empty, result);
    }

    #endregion
}
