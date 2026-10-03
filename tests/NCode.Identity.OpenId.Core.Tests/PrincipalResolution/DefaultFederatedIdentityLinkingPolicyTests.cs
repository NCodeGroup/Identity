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
using Microsoft.AspNetCore.Http;
using Moq;
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

public sealed class DefaultFederatedIdentityLinkingPolicyTests
{
    private const string JoinKey = "user@example.com";
    private const string MatchedPrincipalId = "principal-9";

    private static IHttpContextAccessor CreateHttpContextAccessor(
        MockRepository mocks,
        bool explicitOnly = false,
        bool requireVerified = true
    )
    {
        var settings = mocks.Create<IReadOnlySettingCollection>();
        settings
            .Setup(x => x.GetValue(OpenIdSettingKeys.FederatedIdentityJoinClaim))
            .Returns("email");
        settings
            .Setup(x => x.GetValue(OpenIdSettingKeys.FederatedIdentityVerifiedClaim))
            .Returns("email_verified");
        settings
            .Setup(x => x.GetValue(OpenIdSettingKeys.FederatedIdentityRequireVerified))
            .Returns(requireVerified);
        settings
            .Setup(x => x.GetValue(OpenIdSettingKeys.FederatedIdentityExplicitOnly))
            .Returns(explicitOnly);

        var provider = mocks.Create<IReadOnlySettingCollectionProvider>();
        provider.Setup(x => x.Collection).Returns(settings.Object);

        var tenant = mocks.Create<OpenIdTenant>();
        tenant.Setup(x => x.SettingsProvider).Returns(provider.Object);

        var context = mocks.Create<OpenIdContext>();
        context.Setup(x => x.Tenant).Returns(tenant.Object);

        var feature = mocks.Create<IOpenIdContextFeature>();
        feature.Setup(x => x.OpenIdContext).Returns(context.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set(feature.Object);

        var accessor = mocks.Create<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(httpContext);
        return accessor.Object;
    }

    private static ClaimsPrincipal CreateUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    private static ClaimsPrincipal VerifiedUser(string email = JoinKey) =>
        CreateUser(new Claim("email", email), new Claim("email_verified", "true"));

    private static PersistedFederatedIdentity Identity(string principalId) =>
        new()
        {
            FederatedIdentityId = "identity-1",
            PrincipalId = principalId,
            Issuer = "https://issuer.example",
            Subject = "subject-1",
            JoinKey = JoinKey,
            ConcurrencyToken = string.Empty,
        };

    [Fact]
    public async Task ResolveLinkAsync_WhenNoJoinClaim_ReturnsEmptyDecision()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();

        var policy = new DefaultFederatedIdentityLinkingPolicy(CreateHttpContextAccessor(mocks));

        var decision = await policy.ResolveLinkAsync(
            CreateUser(),
            manager.Object,
            CancellationToken.None
        );

        Assert.Null(decision.LinkedPrincipalId);
        Assert.Null(decision.JoinKey);
    }

    [Fact]
    public async Task ResolveLinkAsync_WhenUnverifiedAndVerificationRequired_ReturnsEmptyDecision()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();

        var policy = new DefaultFederatedIdentityLinkingPolicy(CreateHttpContextAccessor(mocks));

        var decision = await policy.ResolveLinkAsync(
            CreateUser(new Claim("email", JoinKey), new Claim("email_verified", "false")),
            manager.Object,
            CancellationToken.None
        );

        // An unverified join key is neither recorded nor matchable.
        Assert.Null(decision.LinkedPrincipalId);
        Assert.Null(decision.JoinKey);
    }

    [Fact]
    public async Task ResolveLinkAsync_WhenExplicitOnly_RecordsJoinKeyButDoesNotLink()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();

        var policy = new DefaultFederatedIdentityLinkingPolicy(
            CreateHttpContextAccessor(mocks, explicitOnly: true)
        );

        var decision = await policy.ResolveLinkAsync(
            VerifiedUser(),
            manager.Object,
            CancellationToken.None
        );

        Assert.Null(decision.LinkedPrincipalId);
        Assert.Equal(JoinKey, decision.JoinKey);
    }

    [Fact]
    public async Task ResolveLinkAsync_WhenVerifiedMatch_AttachesToExistingPrincipal()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();
        var identityStore = mocks.Create<IFederatedIdentityStore>();

        manager
            .Setup(x => x.GetStore<IFederatedIdentityStore>())
            .Returns(identityStore.Object)
            .Verifiable();
        identityStore
            .Setup(x => x.GetByJoinKeyAsync(JoinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Identity(MatchedPrincipalId)])
            .Verifiable();

        var policy = new DefaultFederatedIdentityLinkingPolicy(CreateHttpContextAccessor(mocks));

        var decision = await policy.ResolveLinkAsync(
            VerifiedUser(),
            manager.Object,
            CancellationToken.None
        );

        mocks.Verify();
        Assert.Equal(MatchedPrincipalId, decision.LinkedPrincipalId);
        Assert.Equal(JoinKey, decision.JoinKey);
    }

    [Fact]
    public async Task ResolveLinkAsync_WhenVerifiedNoMatch_RecordsJoinKeyWithoutLink()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var manager = mocks.Create<IStoreManager>();
        var identityStore = mocks.Create<IFederatedIdentityStore>();

        manager
            .Setup(x => x.GetStore<IFederatedIdentityStore>())
            .Returns(identityStore.Object)
            .Verifiable();
        identityStore
            .Setup(x => x.GetByJoinKeyAsync(JoinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .Verifiable();

        var policy = new DefaultFederatedIdentityLinkingPolicy(CreateHttpContextAccessor(mocks));

        var decision = await policy.ResolveLinkAsync(
            VerifiedUser(),
            manager.Object,
            CancellationToken.None
        );

        mocks.Verify();
        Assert.Null(decision.LinkedPrincipalId);
        Assert.Equal(JoinKey, decision.JoinKey);
    }
}
