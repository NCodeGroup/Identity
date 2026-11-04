// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Collections.Providers;
using NCode.Collections.Providers.PeriodicPolling;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Teams;

public sealed class DefaultOpenIdTeamFactoryTests
{
    #region CreateTeamAsync Tests

    [Fact]
    public async Task CreateTeamAsync_LayersThePlatformProviderUnderTheTeamDocument()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var parentCollection = mocks.Create<IReadOnlySettingCollection>();
        var platform = CreatePlatform(mocks, parentCollection.Object);
        var resolver = mocks.Create<IEffectiveSettingsResolver>();
        resolver
            .Setup(x =>
                x.ReadScopeSettings(
                    parentCollection.Object,
                    It.IsAny<string>(),
                    It.IsAny<JsonElement>()
                )
            )
            .Returns([])
            .Verifiable();
        var fixture = CreateFixture(mocks, resolver.Object);

        var lease = await fixture.Factory.CreateTeamAsync(
            Mock.Of<OpenIdEnvironment>(),
            platform.Object,
            CreatePersistedTeam(),
            Mock.Of<IPropertyBag>(),
            CancellationToken.None
        );

        Assert.Equal(2, fixture.DataSources.Count);
        Assert.Same(parentCollection.Object, fixture.DataSources[0].Collection);
        Assert.Equal("platform", lease.Value.TeamId);
        await lease.DisposeAsync();
        mocks.Verify();
    }

    [Fact]
    public async Task CreateTeamAsync_ReadsTheTeamDocumentAtTheTeamScope()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var persistedTeam = CreatePersistedTeam();
        var teamDocument = persistedTeam.Settings.Value;
        var parentCollection = mocks.Create<IReadOnlySettingCollection>();
        var platform = CreatePlatform(mocks, parentCollection.Object);
        var resolver = mocks.Create<IEffectiveSettingsResolver>();
        resolver
            .Setup(x =>
                x.ReadScopeSettings(
                    It.IsAny<IReadOnlySettingCollection>(),
                    OpenIdSettingScopes.Team,
                    It.Is<JsonElement>(document => JsonElement.DeepEquals(document, teamDocument))
                )
            )
            .Returns([])
            .Verifiable();
        var fixture = CreateFixture(mocks, resolver.Object);

        var lease = await fixture.Factory.CreateTeamAsync(
            Mock.Of<OpenIdEnvironment>(),
            platform.Object,
            persistedTeam,
            Mock.Of<IPropertyBag>(),
            CancellationToken.None
        );

        await lease.DisposeAsync();
        mocks.Verify();
    }

    #endregion

    #region RefreshSettingsAsync Tests

    [Fact]
    public async Task RefreshSettingsAsync_WhenTheTokenIsUnchanged_ReturnsUnchanged()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var parentCollection = mocks.Create<IReadOnlySettingCollection>();
        var platform = CreatePlatform(mocks, parentCollection.Object);
        var resolver = mocks.Create<IEffectiveSettingsResolver>();
        resolver
            .Setup(x =>
                x.ReadScopeSettings(
                    It.IsAny<IReadOnlySettingCollection>(),
                    It.IsAny<string>(),
                    It.IsAny<JsonElement>()
                )
            )
            .Returns([])
            .Verifiable();
        var persistedTeam = CreatePersistedTeam();

        var teamStore = mocks.Create<ITeamStore>();
        teamStore
            .Setup(x => x.GetSettingsAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PersistedTeamSettings
                {
                    TeamId = "platform",
                    ConcurrencyToken = persistedTeam.Settings.ConcurrencyToken,
                    Value = persistedTeam.Settings.Value,
                }
            )
            .Verifiable();
        var storeManager = mocks.Create<IStoreManager>();
        storeManager.Setup(x => x.GetStore<ITeamStore>()).Returns(teamStore.Object).Verifiable();
        storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();
        storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(storeManager.Object)
            .Verifiable();

        var fixture = CreateFixture(mocks, resolver.Object, storeManagerFactory.Object);

        var lease = await fixture.Factory.CreateTeamAsync(
            Mock.Of<OpenIdEnvironment>(),
            platform.Object,
            persistedTeam,
            Mock.Of<IPropertyBag>(),
            CancellationToken.None
        );

        var refresh = Assert.IsAssignableFrom<Delegate>(fixture.CapturedRefresh);
        var pending = refresh.DynamicInvoke(
            fixture.CapturedState,
            new List<Setting>(),
            CancellationToken.None
        );
        var result = await Assert.IsType<ValueTask<RefreshCollectionResult<Setting>>>(pending);

        Assert.True(result.IsUnchanged);
        await lease.DisposeAsync();
        mocks.Verify();
    }

    #endregion

    private static PersistedTeam CreatePersistedTeam() =>
        new()
        {
            TeamId = "platform",
            IsDisabled = false,
            DisplayName = "Platform",
            Settings = new PersistedTeamSettings
            {
                TeamId = "platform",
                ConcurrencyToken = "token",
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
        };

    private static Mock<OpenIdPlatform> CreatePlatform(
        MockRepository mocks,
        IReadOnlySettingCollection parentCollection
    )
    {
        var parentProvider = mocks.Create<IReadOnlySettingCollectionProvider>();
        parentProvider.Setup(x => x.Collection).Returns(parentCollection).Verifiable();

        var platform = mocks.Create<OpenIdPlatform>();
        platform.Setup(x => x.SettingsProvider).Returns(parentProvider.Object).Verifiable();
        return platform;
    }

    private static Fixture CreateFixture(
        MockRepository mocks,
        IEffectiveSettingsResolver resolver,
        IStoreManagerFactory? storeManagerFactory = null
    )
    {
        var fixture = new Fixture();

        var pollingSource = mocks.Create<IAsyncDisposableCollectionDataSource<Setting>>();
        var dataSourceFactory = mocks.Create<ICollectionDataSourceFactory>();
        dataSourceFactory
            .Setup(x =>
                x.CreatePeriodicPolling<Setting, It.IsAnyType>(
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<IReadOnlyCollection<Setting>>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<RefreshCollectionAsyncDelegate<Setting, It.IsAnyType>>(),
                    It.IsAny<HandleExceptionAsyncDelegate?>()
                )
            )
            .Callback(
                new InvocationAction(invocation =>
                {
                    fixture.CapturedState = invocation.Arguments[0];
                    fixture.CapturedRefresh = invocation.Arguments[3];
                })
            )
            .Returns(pollingSource.Object)
            .Verifiable();

        var provider = mocks.Create<IReadOnlySettingCollectionProvider>();
        provider.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        var providerFactory = mocks.Create<IReadOnlySettingCollectionProviderFactory>();
        providerFactory
            .Setup(x => x.Create(It.IsAny<IEnumerable<ICollectionDataSource<Setting>>>(), true))
            .Callback(
                (IEnumerable<ICollectionDataSource<Setting>> dataSources, bool _) =>
                    fixture.DataSources.AddRange(dataSources)
            )
            .Returns(provider.Object)
            .Verifiable();

        fixture.Factory = new DefaultOpenIdTeamFactory(
            Options.Create(new OpenIdOptions()),
            storeManagerFactory ?? mocks.Create<IStoreManagerFactory>().Object,
            providerFactory.Object,
            dataSourceFactory.Object,
            resolver
        );
        return fixture;
    }

    private sealed class Fixture
    {
        public DefaultOpenIdTeamFactory Factory { get; set; } = null!;
        public List<ICollectionDataSource<Setting>> DataSources { get; } = [];
        public object? CapturedState { get; set; }
        public object? CapturedRefresh { get; set; }
    }
}
