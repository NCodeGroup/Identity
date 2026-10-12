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
using NCode.Identity.OpenId.Authentication.Consent;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
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
    private DefaultConsentService Service { get; }

    public DefaultConsentServiceTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockConsentStore = MockRepository.Create<IConsentStore>();
        MockPersistedGrantService = MockRepository.Create<IPersistedGrantService>();

        Service = new DefaultConsentService(
            TimeProvider.System,
            MockStoreManagerFactory.Object,
            MockPersistedGrantService.Object
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

    private static PersistedConsent Consent(params string[] scopes) =>
        new()
        {
            TenantId = TenantId,
            ClientId = ClientId,
            SubjectId = SubjectId,
            GrantedScopes = scopes,
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

    [Fact]
    public async Task HasConsentAsync_WhenNoRequestedScopes_ReturnsTrueWithoutStore()
    {
        var context = CreateContext();

        var result = await Service.HasConsentAsync(
            context.Object,
            ClientId,
            SubjectId,
            [],
            CancellationToken.None
        );

        Assert.True(result);
    }

    [Fact]
    public async Task HasConsentAsync_WhenRecordCoversRequested_ReturnsTrue()
    {
        var context = CreateContext();
        SetupStore();
        MockConsentStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Consent("openid", "profile", "email"))
            .Verifiable();

        var result = await Service.HasConsentAsync(
            context.Object,
            ClientId,
            SubjectId,
            ["openid", "profile"],
            CancellationToken.None
        );

        Assert.True(result);
    }

    [Fact]
    public async Task HasConsentAsync_WhenRecordMissingScope_ReturnsFalse()
    {
        var context = CreateContext();
        SetupStore();
        MockConsentStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Consent("openid"))
            .Verifiable();

        var result = await Service.HasConsentAsync(
            context.Object,
            ClientId,
            SubjectId,
            ["openid", "profile"],
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task HasConsentAsync_WhenNoRecord_ReturnsFalse()
    {
        var context = CreateContext();
        SetupStore();
        MockConsentStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedConsent?)null)
            .Verifiable();

        var result = await Service.HasConsentAsync(
            context.Object,
            ClientId,
            SubjectId,
            ["openid"],
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task GrantConsentAsync_WhenExisting_UnionsScopesAndSaves()
    {
        var context = CreateContext();
        SetupStore();
        MockConsentStore
            .Setup(x => x.GetOrDefaultAsync(ClientId, SubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Consent("email"))
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
            CancellationToken.None
        );

        Assert.NotNull(saved);
        Assert.Equal(["email", "openid"], saved!.GrantedScopes.OrderBy(x => x));
    }
}
