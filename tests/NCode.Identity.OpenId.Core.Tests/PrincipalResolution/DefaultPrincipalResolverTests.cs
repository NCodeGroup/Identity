// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.PrincipalResolution;

public sealed class DefaultPrincipalResolverTests : BaseTests
{
    private const string PrincipalId = "principal-1";
    private const string TenantId = "tenant-1";
    private const string Issuer = "https://issuer.example";
    private const string Subject = "upstream-subject-1";
    private const string GeneratedId = "generated-id";
    private const string ClientId = "client-1";
    private const string ClientPrincipalId = "client-principal-1";

    // A subject that is not a principal id is read as an upstream identity through the tenant's issuer claim, and only a
    // resolution that provisions reads the tenant id.
    private OpenIdContext CreateOpenIdContext(
        bool readsUpstreamClaims = false,
        bool provisions = false
    )
    {
        var context = CreateStrictMock<OpenIdContext>();
        if (readsUpstreamClaims || provisions)
        {
            var tenant = CreateStrictMock<OpenIdTenant>();
            context.Setup(x => x.Tenant).Returns(tenant.Object).Verifiable();
            if (readsUpstreamClaims)
            {
                var settings = CreateStrictMock<IReadOnlySettingCollection>();
                settings
                    .Setup(x => x.GetValue(OpenIdSettingKeys.PrincipalIssuerClaim))
                    .Returns("iss")
                    .Verifiable();
                var provider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
                provider.Setup(x => x.Collection).Returns(settings.Object).Verifiable();
                tenant.Setup(x => x.SettingsProvider).Returns(provider.Object).Verifiable();
            }

            if (provisions)
            {
                tenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
            }
        }

        // The resolver derives the subject through the tenant-aware context seam (ADR-1053).
        context
            .Setup(x => x.GetSubjectId(It.IsAny<ClaimsPrincipal>()))
            .Returns((ClaimsPrincipal user) => user.FindFirstValue("sub"))
            .Verifiable();
        return context.Object;
    }

    private static ClaimsPrincipal CreateUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    private static PersistedFederatedPrincipal Principal(string principalId) =>
        new()
        {
            TenantId = TenantId,
            PrincipalId = principalId,
            ConcurrencyToken = string.Empty,
        };

    private static PersistedFederatedIdentity Identity(string principalId) =>
        new()
        {
            TenantId = TenantId,
            FederatedIdentityId = "identity-1",
            PrincipalId = principalId,
            Issuer = Issuer,
            Subject = Subject,
            JoinKey = null,
            ConcurrencyToken = string.Empty,
        };

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

    [Fact]
    public async Task ResolvePrincipalIdOrDefaultAsync_WhenSubjectIsPrincipalId_ReturnsIt()
    {
        var manager = CreateStrictMock<IStoreManager>();
        var principalStore = CreateStrictMock<IFederatedPrincipalStore>();

        manager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(principalStore.Object)
            .Verifiable();
        principalStore
            .Setup(x => x.GetOrDefaultAsync(PrincipalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Principal(PrincipalId))
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICryptoService>().Object,
            CreateStrictMock<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdOrDefaultAsync(
            CreateOpenIdContext(),
            CreateUser(new Claim("sub", PrincipalId)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(PrincipalId, result);
    }

    [Fact]
    public async Task ResolvePrincipalIdOrDefaultAsync_WhenUpstreamIdentity_ReturnsOwningPrincipal()
    {
        var manager = CreateStrictMock<IStoreManager>();
        var principalStore = CreateStrictMock<IFederatedPrincipalStore>();
        var identityStore = CreateStrictMock<IFederatedIdentityStore>();

        manager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(principalStore.Object)
            .Verifiable();
        manager
            .Setup(x => x.GetStore<IFederatedIdentityStore>())
            .Returns(identityStore.Object)
            .Verifiable();
        principalStore
            .Setup(x => x.GetOrDefaultAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedPrincipal?)null)
            .Verifiable();
        identityStore
            .Setup(x => x.GetByIssuerSubjectAsync(Issuer, Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Identity(PrincipalId))
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICryptoService>().Object,
            CreateStrictMock<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdOrDefaultAsync(
            CreateOpenIdContext(readsUpstreamClaims: true),
            CreateUser(new Claim("sub", Subject), new Claim("iss", Issuer)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(PrincipalId, result);
    }

    [Fact]
    public async Task ResolvePrincipalIdOrDefaultAsync_WhenNoSubjectClaim_ReturnsNull()
    {
        var manager = CreateStrictMock<IStoreManager>();

        var resolver = new DefaultPrincipalResolver(
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICryptoService>().Object,
            CreateStrictMock<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdOrDefaultAsync(
            CreateOpenIdContext(),
            CreateUser(),
            manager.Object,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolvePrincipalIdOrDefaultAsync_WhenUpstreamWithoutIssuer_ReturnsNull()
    {
        var manager = CreateStrictMock<IStoreManager>();
        var principalStore = CreateStrictMock<IFederatedPrincipalStore>();

        manager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(principalStore.Object)
            .Verifiable();
        principalStore
            .Setup(x => x.GetOrDefaultAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedPrincipal?)null)
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICryptoService>().Object,
            CreateStrictMock<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdOrDefaultAsync(
            CreateOpenIdContext(readsUpstreamClaims: true),
            CreateUser(new Claim("sub", Subject)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolvePrincipalIdAsync_WhenAlreadyProvisioned_DoesNotProvision()
    {
        var manager = CreateStrictMock<IStoreManager>();
        var principalStore = CreateStrictMock<IFederatedPrincipalStore>();

        manager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(principalStore.Object)
            .Verifiable();
        principalStore
            .Setup(x => x.GetOrDefaultAsync(PrincipalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Principal(PrincipalId))
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICryptoService>().Object,
            CreateStrictMock<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdAsync(
            CreateOpenIdContext(),
            CreateUser(new Claim("sub", PrincipalId)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(PrincipalId, result);
    }

    [Fact]
    public async Task ResolvePrincipalIdAsync_WhenLinked_AttachesIdentityToLinkedPrincipal()
    {
        var manager = CreateStrictMock<IStoreManager>();
        var principalStore = CreateStrictMock<IFederatedPrincipalStore>();
        var identityStore = CreateStrictMock<IFederatedIdentityStore>();
        var crypto = CreateStrictMock<ICryptoService>();
        var linkingPolicy = CreateStrictMock<IFederatedIdentityLinkingPolicy>();

        manager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(principalStore.Object)
            .Verifiable();
        manager
            .Setup(x => x.GetStore<IFederatedIdentityStore>())
            .Returns(identityStore.Object)
            .Verifiable();
        principalStore
            .Setup(x => x.GetOrDefaultAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedPrincipal?)null)
            .Verifiable();
        identityStore
            .Setup(x => x.GetByIssuerSubjectAsync(Issuer, Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedIdentity?)null)
            .Verifiable();
        linkingPolicy
            .Setup(x =>
                x.ResolveLinkAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    manager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new FederatedIdentityLinkDecision
                {
                    LinkedPrincipalId = PrincipalId,
                    JoinKey = "user@example.com",
                }
            )
            .Verifiable();
        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns(GeneratedId)
            .Verifiable();

        PersistedFederatedIdentity? captured = null;
        identityStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedFederatedIdentity>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedFederatedIdentity identity, CancellationToken _) => captured = identity
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            CreateStrictMock<IStoreManagerFactory>().Object,
            crypto.Object,
            linkingPolicy.Object
        );

        var result = await resolver.ResolvePrincipalIdAsync(
            CreateOpenIdContext(readsUpstreamClaims: true, provisions: true),
            CreateUser(new Claim("sub", Subject), new Claim("iss", Issuer)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(PrincipalId, result);
        Assert.NotNull(captured);
        Assert.Equal(PrincipalId, captured.PrincipalId);
        Assert.Equal("user@example.com", captured.JoinKey);
        // A linked identity never provisions a new principal.
        principalStore.Verify(
            x => x.AddAsync(It.IsAny<PersistedFederatedPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ResolvePrincipalIdAsync_WhenNotLinked_ProvisionsNewPrincipalAndIdentity()
    {
        var manager = CreateStrictMock<IStoreManager>();
        var principalStore = CreateStrictMock<IFederatedPrincipalStore>();
        var identityStore = CreateStrictMock<IFederatedIdentityStore>();
        var crypto = CreateStrictMock<ICryptoService>();
        var linkingPolicy = CreateStrictMock<IFederatedIdentityLinkingPolicy>();

        manager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(principalStore.Object)
            .Verifiable();
        manager
            .Setup(x => x.GetStore<IFederatedIdentityStore>())
            .Returns(identityStore.Object)
            .Verifiable();
        principalStore
            .Setup(x => x.GetOrDefaultAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedPrincipal?)null)
            .Verifiable();
        identityStore
            .Setup(x => x.GetByIssuerSubjectAsync(Issuer, Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedIdentity?)null)
            .Verifiable();
        linkingPolicy
            .Setup(x =>
                x.ResolveLinkAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    manager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new FederatedIdentityLinkDecision { JoinKey = null })
            .Verifiable();
        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns(GeneratedId)
            .Verifiable();

        PersistedFederatedPrincipal? capturedPrincipal = null;
        principalStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedFederatedPrincipal>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedFederatedPrincipal principal, CancellationToken _) =>
                    capturedPrincipal = principal
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        PersistedFederatedIdentity? capturedIdentity = null;
        identityStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedFederatedIdentity>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedFederatedIdentity identity, CancellationToken _) =>
                    capturedIdentity = identity
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            CreateStrictMock<IStoreManagerFactory>().Object,
            crypto.Object,
            linkingPolicy.Object
        );

        var result = await resolver.ResolvePrincipalIdAsync(
            CreateOpenIdContext(readsUpstreamClaims: true, provisions: true),
            CreateUser(new Claim("sub", Subject), new Claim("iss", Issuer)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(GeneratedId, result);
        Assert.NotNull(capturedPrincipal);
        Assert.Equal(GeneratedId, capturedPrincipal.PrincipalId);
        Assert.Equal(TenantId, capturedPrincipal.TenantId);
        Assert.NotNull(capturedIdentity);
        Assert.Equal(GeneratedId, capturedIdentity.PrincipalId);
        Assert.Equal(TenantId, capturedIdentity.TenantId);
        Assert.Equal(Issuer, capturedIdentity.Issuer);
        Assert.Equal(Subject, capturedIdentity.Subject);
    }

    [Fact]
    public async Task ResolvePrincipalIdAsync_WhenUpstreamClaimsMissing_FallsBackToRawSubject()
    {
        var manager = CreateStrictMock<IStoreManager>();
        var principalStore = CreateStrictMock<IFederatedPrincipalStore>();

        manager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(principalStore.Object)
            .Verifiable();
        principalStore
            .Setup(x => x.GetOrDefaultAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedPrincipal?)null)
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICryptoService>().Object,
            CreateStrictMock<IFederatedIdentityLinkingPolicy>().Object
        );

        // Subject present but no issuer: cannot form a durable identity, so the subject value is used as-is.
        var result = await resolver.ResolvePrincipalIdAsync(
            CreateOpenIdContext(readsUpstreamClaims: true),
            CreateUser(new Claim("sub", Subject)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(Subject, result);
    }

    [Fact]
    public async Task ResolvePrincipalIdOrDefaultAsync_WhenAClientActsForItself_ReturnsTheClientsPrincipalId()
    {
        var manager = CreateStrictMock<IStoreManager>();
        var clientStore = CreateStrictMock<IClientStore>();

        manager.Setup(x => x.GetStore<IClientStore>()).Returns(clientStore.Object).Verifiable();
        clientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient())
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICryptoService>().Object,
            CreateStrictMock<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdOrDefaultAsync(
            CreateOpenIdContext(),
            CreateUser(new Claim("client_id", ClientId)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(ClientPrincipalId, result);
    }

    [Fact]
    public async Task ResolvePrincipalIdOrDefaultAsync_WhenTheClientIsDisabled_ReturnsNull()
    {
        var manager = CreateStrictMock<IStoreManager>();
        var clientStore = CreateStrictMock<IClientStore>();

        manager.Setup(x => x.GetStore<IClientStore>()).Returns(clientStore.Object).Verifiable();
        clientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient(isDisabled: true))
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICryptoService>().Object,
            CreateStrictMock<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdOrDefaultAsync(
            CreateOpenIdContext(),
            CreateUser(new Claim("client_id", ClientId)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolvePrincipalIdOrDefaultAsync_WhenTheClientIsUnknown_ReturnsNull()
    {
        var manager = CreateStrictMock<IStoreManager>();
        var clientStore = CreateStrictMock<IClientStore>();

        manager.Setup(x => x.GetStore<IClientStore>()).Returns(clientStore.Object).Verifiable();
        clientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedClient?)null)
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICryptoService>().Object,
            CreateStrictMock<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdOrDefaultAsync(
            CreateOpenIdContext(),
            CreateUser(new Claim("client_id", ClientId)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolvePrincipalIdAsync_WhenAClientActsForItself_ReturnsItsPrincipalWithoutProvisioning()
    {
        var manager = CreateStrictMock<IStoreManager>();
        var clientStore = CreateStrictMock<IClientStore>();

        manager.Setup(x => x.GetStore<IClientStore>()).Returns(clientStore.Object).Verifiable();
        clientStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClient())
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICryptoService>().Object,
            CreateStrictMock<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdAsync(
            CreateOpenIdContext(),
            CreateUser(new Claim("client_id", ClientId)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(ClientPrincipalId, result);
    }
}
