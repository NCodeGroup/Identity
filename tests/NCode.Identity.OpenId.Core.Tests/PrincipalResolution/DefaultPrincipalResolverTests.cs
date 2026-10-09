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
using Moq;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.PrincipalResolution;

public sealed class DefaultPrincipalResolverTests
{
    private const string PrincipalId = "principal-1";
    private const string TenantId = "tenant-1";
    private const string Issuer = "https://issuer.example";
    private const string Subject = "upstream-subject-1";
    private const string GeneratedId = "generated-id";

    private static OpenIdContext CreateOpenIdContext(MockRepository mocks)
    {
        var settings = mocks.Create<IReadOnlySettingCollection>();
        settings.Setup(x => x.GetValue(OpenIdSettingKeys.PrincipalSourceClaim)).Returns("sub");
        settings.Setup(x => x.GetValue(OpenIdSettingKeys.PrincipalIssuerClaim)).Returns("iss");

        var provider = mocks.Create<IReadOnlySettingCollectionProvider>();
        provider.Setup(x => x.Collection).Returns(settings.Object);

        var tenant = mocks.Create<OpenIdTenant>();
        tenant.Setup(x => x.SettingsProvider).Returns(provider.Object);
        tenant.Setup(x => x.TenantId).Returns(TenantId);

        var context = mocks.Create<OpenIdContext>();
        context.Setup(x => x.Tenant).Returns(tenant.Object);
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

    [Fact]
    public async Task ResolvePrincipalIdOrDefaultAsync_WhenSubjectIsPrincipalId_ReturnsIt()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();
        var principalStore = mocks.Create<IFederatedPrincipalStore>();

        manager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(principalStore.Object)
            .Verifiable();
        principalStore
            .Setup(x => x.GetOrDefaultAsync(PrincipalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Principal(PrincipalId))
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            mocks.Create<ICryptoService>().Object,
            mocks.Create<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdOrDefaultAsync(
            CreateOpenIdContext(mocks),
            CreateUser(new Claim("sub", PrincipalId)),
            manager.Object,
            CancellationToken.None
        );

        mocks.Verify();
        Assert.Equal(PrincipalId, result);
    }

    [Fact]
    public async Task ResolvePrincipalIdOrDefaultAsync_WhenUpstreamIdentity_ReturnsOwningPrincipal()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();
        var principalStore = mocks.Create<IFederatedPrincipalStore>();
        var identityStore = mocks.Create<IFederatedIdentityStore>();

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
            mocks.Create<ICryptoService>().Object,
            mocks.Create<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdOrDefaultAsync(
            CreateOpenIdContext(mocks),
            CreateUser(new Claim("sub", Subject), new Claim("iss", Issuer)),
            manager.Object,
            CancellationToken.None
        );

        mocks.Verify();
        Assert.Equal(PrincipalId, result);
    }

    [Fact]
    public async Task ResolvePrincipalIdOrDefaultAsync_WhenNoSubjectClaim_ReturnsNull()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();

        var resolver = new DefaultPrincipalResolver(
            mocks.Create<ICryptoService>().Object,
            mocks.Create<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdOrDefaultAsync(
            CreateOpenIdContext(mocks),
            CreateUser(),
            manager.Object,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolvePrincipalIdOrDefaultAsync_WhenUpstreamWithoutIssuer_ReturnsNull()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();
        var principalStore = mocks.Create<IFederatedPrincipalStore>();

        manager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(principalStore.Object)
            .Verifiable();
        principalStore
            .Setup(x => x.GetOrDefaultAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedPrincipal?)null)
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            mocks.Create<ICryptoService>().Object,
            mocks.Create<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdOrDefaultAsync(
            CreateOpenIdContext(mocks),
            CreateUser(new Claim("sub", Subject)),
            manager.Object,
            CancellationToken.None
        );

        mocks.Verify();
        Assert.Null(result);
    }

    [Fact]
    public async Task ResolvePrincipalIdAsync_WhenAlreadyProvisioned_DoesNotProvision()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();
        var principalStore = mocks.Create<IFederatedPrincipalStore>();

        manager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(principalStore.Object)
            .Verifiable();
        principalStore
            .Setup(x => x.GetOrDefaultAsync(PrincipalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Principal(PrincipalId))
            .Verifiable();

        var resolver = new DefaultPrincipalResolver(
            mocks.Create<ICryptoService>().Object,
            mocks.Create<IFederatedIdentityLinkingPolicy>().Object
        );

        var result = await resolver.ResolvePrincipalIdAsync(
            CreateOpenIdContext(mocks),
            CreateUser(new Claim("sub", PrincipalId)),
            manager.Object,
            CancellationToken.None
        );

        mocks.Verify();
        Assert.Equal(PrincipalId, result);
    }

    [Fact]
    public async Task ResolvePrincipalIdAsync_WhenLinked_AttachesIdentityToLinkedPrincipal()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();
        var principalStore = mocks.Create<IFederatedPrincipalStore>();
        var identityStore = mocks.Create<IFederatedIdentityStore>();
        var crypto = mocks.Create<ICryptoService>();
        var linkingPolicy = mocks.Create<IFederatedIdentityLinkingPolicy>();

        manager.Setup(x => x.GetStore<IFederatedPrincipalStore>()).Returns(principalStore.Object);
        manager.Setup(x => x.GetStore<IFederatedIdentityStore>()).Returns(identityStore.Object);
        principalStore
            .Setup(x => x.GetOrDefaultAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedPrincipal?)null);
        identityStore
            .Setup(x => x.GetByIssuerSubjectAsync(Issuer, Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedIdentity?)null);
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
            .Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
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

        var resolver = new DefaultPrincipalResolver(crypto.Object, linkingPolicy.Object);

        var result = await resolver.ResolvePrincipalIdAsync(
            CreateOpenIdContext(mocks),
            CreateUser(new Claim("sub", Subject), new Claim("iss", Issuer)),
            manager.Object,
            CancellationToken.None
        );

        linkingPolicy.Verify();
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
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();
        var principalStore = mocks.Create<IFederatedPrincipalStore>();
        var identityStore = mocks.Create<IFederatedIdentityStore>();
        var crypto = mocks.Create<ICryptoService>();
        var linkingPolicy = mocks.Create<IFederatedIdentityLinkingPolicy>();

        manager.Setup(x => x.GetStore<IFederatedPrincipalStore>()).Returns(principalStore.Object);
        manager.Setup(x => x.GetStore<IFederatedIdentityStore>()).Returns(identityStore.Object);
        principalStore
            .Setup(x => x.GetOrDefaultAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedPrincipal?)null);
        identityStore
            .Setup(x => x.GetByIssuerSubjectAsync(Issuer, Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedIdentity?)null);
        linkingPolicy
            .Setup(x =>
                x.ResolveLinkAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    manager.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new FederatedIdentityLinkDecision { JoinKey = null });
        crypto.Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url)).Returns(GeneratedId);

        PersistedFederatedPrincipal? capturedPrincipal = null;
        principalStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedFederatedPrincipal>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedFederatedPrincipal principal, CancellationToken _) =>
                    capturedPrincipal = principal
            )
            .Returns(ValueTask.CompletedTask);
        PersistedFederatedIdentity? capturedIdentity = null;
        identityStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedFederatedIdentity>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedFederatedIdentity identity, CancellationToken _) =>
                    capturedIdentity = identity
            )
            .Returns(ValueTask.CompletedTask);

        var resolver = new DefaultPrincipalResolver(crypto.Object, linkingPolicy.Object);

        var result = await resolver.ResolvePrincipalIdAsync(
            CreateOpenIdContext(mocks),
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
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();
        var principalStore = mocks.Create<IFederatedPrincipalStore>();

        manager.Setup(x => x.GetStore<IFederatedPrincipalStore>()).Returns(principalStore.Object);
        principalStore
            .Setup(x => x.GetOrDefaultAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedFederatedPrincipal?)null);

        var resolver = new DefaultPrincipalResolver(
            mocks.Create<ICryptoService>().Object,
            mocks.Create<IFederatedIdentityLinkingPolicy>().Object
        );

        // Subject present but no issuer: cannot form a durable identity, so the subject value is used as-is.
        var result = await resolver.ResolvePrincipalIdAsync(
            CreateOpenIdContext(mocks),
            CreateUser(new Claim("sub", Subject)),
            manager.Object,
            CancellationToken.None
        );

        Assert.Equal(Subject, result);
    }
}
