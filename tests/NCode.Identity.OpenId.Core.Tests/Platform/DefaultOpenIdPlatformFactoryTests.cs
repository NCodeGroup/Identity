// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Platform;

public class DefaultOpenIdPlatformFactoryTests : BaseTests
{
    private static TimeSpan Interval { get; } = TimeSpan.FromMinutes(5.0);

    #region Constructor Tests

    [Fact]
    public void Constructor_DoesNotReadTheOptions()
    {
        var mockOptionsAccessor = CreateStrictMock<IOptions<OpenIdOptions>>();

        var factory = new DefaultOpenIdPlatformFactory(
            mockOptionsAccessor.Object,
            CreateStrictMock<IServiceProvider>().Object,
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICollectionDataSourceFactory>().Object,
            CreateStrictMock<IReadOnlySettingCollectionProviderFactory>().Object,
            CreateStrictMock<ISecurityProfileCatalog>().Object
        );

        Assert.NotNull(factory);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WhenTheMergedProviderCannotBeCreated_DisposesBothSettingsDataSources()
    {
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        mockEnvironment.Setup(x => x.PropertyBag).Returns(PropertyBagFactory.Create()).Verifiable();

        var mockRootDataSource = CreateStrictMock<IDisposableCollectionDataSource<Setting>>();
        mockRootDataSource.Setup(x => x.Dispose()).Verifiable();

        var mockTimer = CreateStrictMock<ITimer>();
        mockTimer.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        var mockTimeProvider = CreateStrictMock<TimeProvider>();
        mockTimeProvider
            .Setup(x =>
                x.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object?>(), Interval, Interval)
            )
            .Returns(mockTimer.Object)
            .Verifiable();

        var platformSettingsDataSource = new RefreshableSettingsCollectionDataSource(
            [],
            _ => ValueTask.FromResult<IReadOnlyCollection<Setting>?>(null),
            Interval,
            mockTimeProvider.Object,
            CreateLooseMock<ILogger<RefreshableSettingsCollectionDataSource>>().Object
        );

        var mockProviderFactory = CreateStrictMock<IReadOnlySettingCollectionProviderFactory>();
        mockProviderFactory
            .Setup(x => x.Create(It.IsAny<IEnumerable<ICollectionDataSource<Setting>>>(), true))
            .Throws(new InvalidOperationException("merge"))
            .Verifiable();

        var factory = new TestablePlatformFactory(
            this,
            mockProviderFactory.Object,
            mockRootDataSource.Object,
            platformSettingsDataSource
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            factory.CreateAsync(mockEnvironment.Object, CancellationToken.None).AsTask()
        );

        Assert.Throws<ObjectDisposedException>(() => platformSettingsDataSource.GetChangeToken());
    }

    #endregion

    #region GetPersistedPlatformAsync Tests

    private (
        DefaultOpenIdPlatformFactory Factory,
        Mock<IStoreManagerFactory> MockFactory
    ) CreateStoreFactory()
    {
        var mockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        var factory = new DefaultOpenIdPlatformFactory(
            Options.Create(new OpenIdOptions()),
            CreateStrictMock<IServiceProvider>().Object,
            mockStoreManagerFactory.Object,
            CreateStrictMock<ICollectionDataSourceFactory>().Object,
            CreateStrictMock<IReadOnlySettingCollectionProviderFactory>().Object,
            CreateStrictMock<ISecurityProfileCatalog>().Object
        );
        return (factory, mockStoreManagerFactory);
    }

    private (Mock<IStoreManager> StoreManager, Mock<IPlatformStore> Store) CreateStoreManager()
    {
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockStore = CreateStrictMock<IPlatformStore>();
        mockStoreManager
            .Setup(x => x.GetStore<IPlatformStore>())
            .Returns(mockStore.Object)
            .Verifiable();
        mockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        return (mockStoreManager, mockStore);
    }

    private static PersistedPlatform CreatePlatform() =>
        new()
        {
            ConcurrencyToken = "token",
            Settings = new PersistedPlatformSettings
            {
                ConcurrencyToken = "settings-token",
                Value = JsonSerializer.SerializeToElement(null, typeof(object)),
            },
        };

    private static void SetupStoreManagers(
        Mock<IStoreManagerFactory> mockStoreManagerFactory,
        params Mock<IStoreManager>[] storeManagers
    )
    {
        var calls = 0;
        mockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storeManagers[calls++].Object)
            .Verifiable(Times.Exactly(storeManagers.Length));
    }

    [Fact]
    public async Task GetPersistedPlatformAsync_WhenARowExists_ReturnsItWithoutWriting()
    {
        var (factory, mockFactory) = CreateStoreFactory();
        var (mockStoreManager, mockStore) = CreateStoreManager();
        var existing = CreatePlatform();
        mockStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing)
            .Verifiable();
        SetupStoreManagers(mockFactory, mockStoreManager);

        var result = await factory.GetPersistedPlatformAsync(
            PropertyBagFactory.Create(),
            CancellationToken.None
        );

        Assert.Same(existing, result);
    }

    [Fact]
    public async Task GetPersistedPlatformAsync_WhenNoRowExists_AddsAndSaves()
    {
        var (factory, mockFactory) = CreateStoreFactory();
        var (mockStoreManager, mockStore) = CreateStoreManager();
        PersistedPlatform? added = null;
        mockStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatform?)null)
            .Verifiable();
        mockStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedPlatform>(), It.IsAny<CancellationToken>()))
            .Callback((PersistedPlatform item, CancellationToken _) => added = item)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        mockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupStoreManagers(mockFactory, mockStoreManager);

        var result = await factory.GetPersistedPlatformAsync(
            PropertyBagFactory.Create(),
            CancellationToken.None
        );

        Assert.NotNull(added);
        Assert.Same(added, result);
    }

    [Fact]
    public async Task GetPersistedPlatformAsync_WhenSaveFailsAndAnotherReplicaInserted_ReturnsTheirRow()
    {
        var (factory, mockFactory) = CreateStoreFactory();
        var (firstManager, firstStore) = CreateStoreManager();
        var (secondManager, secondStore) = CreateStoreManager();
        var theirs = CreatePlatform();
        firstStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatform?)null)
            .Verifiable();
        firstStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedPlatform>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        firstManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromException(new InvalidOperationException("duplicate key")))
            .Verifiable();
        secondStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(theirs)
            .Verifiable();
        SetupStoreManagers(mockFactory, firstManager, secondManager);

        var result = await factory.GetPersistedPlatformAsync(
            PropertyBagFactory.Create(),
            CancellationToken.None
        );

        Assert.Same(theirs, result);
    }

    [Fact]
    public async Task GetPersistedPlatformAsync_WhenSaveFailsAndNoRowExists_Rethrows()
    {
        var (factory, mockFactory) = CreateStoreFactory();
        var (firstManager, firstStore) = CreateStoreManager();
        var (secondManager, secondStore) = CreateStoreManager();
        var failure = new InvalidOperationException("disk full");
        firstStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatform?)null)
            .Verifiable();
        firstStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedPlatform>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        firstManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromException(failure))
            .Verifiable();
        secondStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatform?)null)
            .Verifiable();
        SetupStoreManagers(mockFactory, firstManager, secondManager);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.GetPersistedPlatformAsync(
                PropertyBagFactory.Create(),
                CancellationToken.None
            )
        );

        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task GetPersistedPlatformAsync_WhenSaveIsCancelled_Rethrows()
    {
        var (factory, mockFactory) = CreateStoreFactory();
        var (mockStoreManager, mockStore) = CreateStoreManager();
        mockStore
            .Setup(x => x.GetOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatform?)null)
            .Verifiable();
        mockStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedPlatform>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        mockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromException(new OperationCanceledException()))
            .Verifiable();
        SetupStoreManagers(mockFactory, mockStoreManager);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await factory.GetPersistedPlatformAsync(
                PropertyBagFactory.Create(),
                CancellationToken.None
            )
        );
    }

    #endregion

    private sealed class TestablePlatformFactory(
        DefaultOpenIdPlatformFactoryTests tests,
        IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
        IDisposableCollectionDataSource<Setting> rootSettingsDataSource,
        RefreshableSettingsCollectionDataSource platformSettingsDataSource
    )
        : DefaultOpenIdPlatformFactory(
            Options.Create(new OpenIdOptions()),
            tests.CreateStrictMock<IServiceProvider>().Object,
            tests.CreateStrictMock<IStoreManagerFactory>().Object,
            tests.CreateStrictMock<ICollectionDataSourceFactory>().Object,
            settingCollectionProviderFactory,
            tests.CreateStrictMock<ISecurityProfileCatalog>().Object
        )
    {
        protected internal override ValueTask<PersistedPlatform> GetPersistedPlatformAsync(
            IPropertyBag propertyBag,
            CancellationToken cancellationToken
        ) => ValueTask.FromResult(CreateEmptyPersistedPlatform());

        protected internal override ValueTask<
            IDisposableCollectionDataSource<Setting>
        > CreateRootSettingsDataSourceAsync(
            IPropertyBag propertyBag,
            CancellationToken cancellationToken
        ) => ValueTask.FromResult(rootSettingsDataSource);

        protected internal override ValueTask<RefreshableSettingsCollectionDataSource> CreatePlatformSettingsDataSourceAsync(
            OpenIdEnvironment openIdEnvironment,
            PersistedPlatform persistedPlatform,
            IPropertyBag propertyBag,
            CancellationToken cancellationToken
        ) => ValueTask.FromResult(platformSettingsDataSource);
    }
}
