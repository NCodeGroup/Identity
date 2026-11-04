// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Platform;

public class DefaultOpenIdPlatformFactoryTests : BaseTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5.0);

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

        var serverSettingsDataSource = new RefreshableSettingsCollectionDataSource(
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

        var factory = new TestableServerFactory(
            this,
            mockProviderFactory.Object,
            mockRootDataSource.Object,
            serverSettingsDataSource
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            factory.CreateAsync(mockEnvironment.Object, CancellationToken.None).AsTask()
        );

        Assert.Throws<ObjectDisposedException>(() => serverSettingsDataSource.GetChangeToken());
    }

    #endregion

    private sealed class TestableServerFactory(
        DefaultOpenIdPlatformFactoryTests tests,
        IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
        IDisposableCollectionDataSource<Setting> rootSettingsDataSource,
        RefreshableSettingsCollectionDataSource serverSettingsDataSource
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
            string platformId,
            IPropertyBag propertyBag,
            CancellationToken cancellationToken
        ) => ValueTask.FromResult(CreateEmptyPersistedPlatform(platformId));

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
        ) => ValueTask.FromResult(serverSettingsDataSource);
    }
}
