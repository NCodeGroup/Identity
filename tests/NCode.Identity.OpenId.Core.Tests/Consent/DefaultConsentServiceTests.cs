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

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Consent;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Consent;

public sealed class DefaultConsentServiceTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";
    private const string SubjectId = "subject-1";

    private MockRepository MockRepository { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IConsentStore> MockConsentStore { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<IOpenIdClientResolver> MockClientResolver { get; }
    private Mock<IScopeDescriptionService> MockScopeDescriptionService { get; }
    private DefaultConsentService Service { get; }

    public DefaultConsentServiceTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockConsentStore = MockRepository.Create<IConsentStore>();
        MockPersistedGrantService = MockRepository.Create<IPersistedGrantService>();
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockClientResolver = MockRepository.Create<IOpenIdClientResolver>();
        MockScopeDescriptionService = MockRepository.Create<IScopeDescriptionService>();

        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(Mock.Of<IDisposable>());

        Service = new DefaultConsentService(
            TimeProvider.System,
            MockStoreManagerFactory.Object,
            MockPersistedGrantService.Object,
            MockAmbientTenantAccessor.Object,
            MockClientResolver.Object,
            MockScopeDescriptionService.Object
        );
    }

    public void Dispose() => MockRepository.Verify();

    private void SetupStore()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IConsentStore>())
            .Returns(MockConsentStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private static PersistedConsent Consent(
        IReadOnlyCollection<string> requested,
        IReadOnlyCollection<string> granted
    ) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            SubjectId = SubjectId,
            RequestedScopes = requested,
            GrantedScopes = granted,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            UpdatedWhen = DateTimeOffset.UnixEpoch,
            ConcurrencyToken = "ct",
        };

    private Mock<OpenIdContext> CreateContext()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        return mockContext;
    }

    #region ResolveConsentAsync

    [Fact]
    public async Task ResolveConsentAsync_WhenNoRequestedScopes_ReturnsDecidedWithoutStore()
    {
        var context = CreateContext();

        var result = await Service.ResolveConsentAsync(
            context.Object,
            ClientId,
            SubjectId,
            [],
            CancellationToken.None
        );

        Assert.True(result.IsDecided);
        Assert.Empty(result.GrantedScopes);
    }

    [Fact]
    public async Task ResolveConsentAsync_WhenNoRecord_ReturnsNotDecidedEmpty()
    {
        var context = CreateContext();
        SetupStore();
        MockConsentStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedConsent?)null)
            .Verifiable();

        var result = await Service.ResolveConsentAsync(
            context.Object,
            ClientId,
            SubjectId,
            ["openid"],
            CancellationToken.None
        );

        Assert.False(result.IsDecided);
        Assert.Empty(result.GrantedScopes);
    }

    [Fact]
    public async Task ResolveConsentAsync_WhenDecidedCoversRequested_ReturnsGrantedSubsetInRequestedOrder()
    {
        var context = CreateContext();
        SetupStore();
        // Decided covers {openid, profile, email}; the subject denied email.
        MockConsentStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Consent(["openid", "profile", "email"], ["openid", "profile"]))
            .Verifiable();

        var result = await Service.ResolveConsentAsync(
            context.Object,
            ClientId,
            SubjectId,
            ["profile", "openid", "email"],
            CancellationToken.None
        );

        Assert.True(result.IsDecided);
        // Down-scoped to the granted subset, preserving the requested order.
        Assert.Equal(["profile", "openid"], result.GrantedScopes);
    }

    [Fact]
    public async Task ResolveConsentAsync_WhenRequestIntroducesUndecidedScope_ReturnsNotDecided()
    {
        var context = CreateContext();
        SetupStore();
        MockConsentStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Consent(["openid"], ["openid"]))
            .Verifiable();

        var result = await Service.ResolveConsentAsync(
            context.Object,
            ClientId,
            SubjectId,
            ["openid", "profile"],
            CancellationToken.None
        );

        Assert.False(result.IsDecided);
        Assert.Equal(["openid"], result.GrantedScopes);
    }

    #endregion

    #region GrantConsentAsync

    [Fact]
    public async Task GrantConsentAsync_WhenNew_PersistsDecidedAndGranted()
    {
        var context = CreateContext();
        SetupStore();
        MockConsentStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedConsent?)null)
            .Verifiable();

        PersistedConsent? saved = null;
        MockConsentStore
            .Setup(x => x.SaveAsync(It.IsAny<PersistedConsent>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedConsent consent, CancellationToken _) => saved = consent)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Service.GrantConsentAsync(
            context.Object,
            ClientId,
            SubjectId,
            ["openid", "profile", "email"],
            ["openid", "profile"],
            CancellationToken.None
        );

        Assert.NotNull(saved);
        Assert.Equal(["email", "openid", "profile"], saved!.RequestedScopes.OrderBy(x => x));
        Assert.Equal(["openid", "profile"], saved.GrantedScopes.OrderBy(x => x));
    }

    [Fact]
    public async Task GrantConsentAsync_WhenExisting_GrowsDecidedAndReplacesGrantedWithinRequested()
    {
        var context = CreateContext();
        SetupStore();
        // Previously decided {openid, email, address}, all granted; now re-consent to {openid, email}
        // revoking email, while the prior {address} grant outside this request is preserved.
        MockConsentStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Consent(["openid", "email", "address"], ["openid", "email", "address"]))
            .Verifiable();

        PersistedConsent? saved = null;
        MockConsentStore
            .Setup(x => x.SaveAsync(It.IsAny<PersistedConsent>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedConsent consent, CancellationToken _) => saved = consent)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Service.GrantConsentAsync(
            context.Object,
            ClientId,
            SubjectId,
            ["openid", "email"],
            ["openid"],
            CancellationToken.None
        );

        Assert.NotNull(saved);
        Assert.Equal(["address", "email", "openid"], saved!.RequestedScopes.OrderBy(x => x));
        // email revoked within the requested set; address preserved (outside this request).
        Assert.Equal(["address", "openid"], saved.GrantedScopes.OrderBy(x => x));
    }

    [Fact]
    public async Task GrantConsentAsync_IgnoresGrantedScopesOutsideRequested()
    {
        var context = CreateContext();
        SetupStore();
        MockConsentStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedConsent?)null)
            .Verifiable();

        PersistedConsent? saved = null;
        MockConsentStore
            .Setup(x => x.SaveAsync(It.IsAny<PersistedConsent>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedConsent consent, CancellationToken _) => saved = consent)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Service.GrantConsentAsync(
            context.Object,
            ClientId,
            SubjectId,
            ["openid"],
            ["openid", "profile"],
            CancellationToken.None
        );

        Assert.NotNull(saved);
        Assert.Equal(["openid"], saved!.GrantedScopes);
    }

    #endregion

    #region RevokeConsentAsync

    [Fact]
    public async Task RevokeConsentAsync_RemovesAndSaves()
    {
        var context = CreateContext();
        SetupStore();
        MockConsentStore
            .Setup(x => x.RemoveAsync(ClientId, SubjectId, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Service.RevokeConsentAsync(
            context.Object,
            ClientId,
            SubjectId,
            CancellationToken.None
        );
    }

    #endregion

    #region GetAuthorizedApplicationsAsync

    [Fact]
    public async Task GetAuthorizedApplicationsAsync_ReturnsAppsWithGrantedScopesAndDescriptions()
    {
        var context = CreateContext();
        SetupStore();
        MockConsentStore
            .Setup(x => x.GetBySubjectAsync(SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Consent(["openid", "profile"], ["openid", "profile"])])
            .Verifiable();

        MockClientResolver
            .Setup(x =>
                x.ResolveClientOrDefaultAsync(
                    context.Object,
                    ClientId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((OpenIdClient?)null)
            .Verifiable();

        MockScopeDescriptionService
            .Setup(x =>
                x.DescribeScopesAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<string, string> { ["openid"] = "Sign you in." })
            .Verifiable();

        var result = await Service.GetAuthorizedApplicationsAsync(
            context.Object,
            SubjectId,
            CancellationToken.None
        );

        var application = Assert.Single(result);
        Assert.Equal(ClientId, application.ClientId);
        Assert.Null(application.ClientName);
        Assert.Collection(
            application.Scopes,
            scope =>
            {
                Assert.Equal("openid", scope.Value);
                Assert.Equal("Sign you in.", scope.Description);
                Assert.True(scope.IsGranted);
            },
            scope =>
            {
                Assert.Equal("profile", scope.Value);
                Assert.Null(scope.Description);
            }
        );
    }

    [Fact]
    public async Task GetAuthorizedApplicationsAsync_SkipsConsentsWithNoGrantedScopes()
    {
        var context = CreateContext();
        SetupStore();
        MockConsentStore
            .Setup(x => x.GetBySubjectAsync(SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Consent(["openid"], [])])
            .Verifiable();

        var result = await Service.GetAuthorizedApplicationsAsync(
            context.Object,
            SubjectId,
            CancellationToken.None
        );

        Assert.Empty(result);
    }

    #endregion
}
