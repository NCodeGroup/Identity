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

using System.Security.Cryptography;
using System.Text.Json;
using IdGen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Servers;

public class DefaultOpenIdServerFactoryTests : BaseTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5.0);

    private DefaultOpenIdServerFactory CreateFactory(
        ISecretSerializer secretSerializer,
        ISecretProtectorProvider secretProtectorProvider,
        ICollectionDataSourceFactory? collectionDataSourceFactory = null,
        ISecretKeyCollectionProviderFactory? secretKeyCollectionProviderFactory = null
    ) =>
        new(
            Options.Create(new OpenIdOptions()),
            CreateStrictMock<IServiceProvider>().Object,
            secretSerializer,
            secretProtectorProvider,
            CreateStrictMock<IStoreManagerFactory>().Object,
            collectionDataSourceFactory ?? CreateStrictMock<ICollectionDataSourceFactory>().Object,
            CreateStrictMock<IReadOnlySettingCollectionProviderFactory>().Object,
            secretKeyCollectionProviderFactory
                ?? CreateStrictMock<ISecretKeyCollectionProviderFactory>().Object,
            CreateStrictMock<ISecurityProfileCatalog>().Object,
            CreateStrictMock<IIdGenerator<long>>().Object
        );

    private static PersistedSecret CreateSecret(
        string secretId,
        string secretType,
        string secretEncoding,
        string encodedValue
    ) =>
        new()
        {
            SecretId = secretId,
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.MaxValue,
            SecretType = secretType,
            SecretEncoding = secretEncoding,
            KeySizeBits = 256,
            EncodedValue = encodedValue,
        };

    private static PersistedServer CreatePersistedServer(
        string serverId,
        IReadOnlyCollection<PersistedSecret> secrets
    ) =>
        new()
        {
            ServerId = serverId,
            ConcurrencyToken = "token",
            DisplayName = serverId,
            Settings = new PersistedServerSettings
            {
                ServerId = serverId,
                ConcurrencyToken = "token",
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedServerSecrets
            {
                ServerId = serverId,
                ConcurrencyToken = "token",
                Value = secrets,
            },
        };

    #region Constructor Tests

    [Fact]
    public void Constructor_DoesNotReadTheOptions()
    {
        var mockOptionsAccessor = CreateStrictMock<IOptions<OpenIdOptions>>();

        var factory = new DefaultOpenIdServerFactory(
            mockOptionsAccessor.Object,
            CreateStrictMock<IServiceProvider>().Object,
            CreateStrictMock<ISecretSerializer>().Object,
            CreateStrictMock<ISecretProtectorProvider>().Object,
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<ICollectionDataSourceFactory>().Object,
            CreateStrictMock<IReadOnlySettingCollectionProviderFactory>().Object,
            CreateStrictMock<ISecretKeyCollectionProviderFactory>().Object,
            CreateStrictMock<ISecurityProfileCatalog>().Object,
            CreateStrictMock<IIdGenerator<long>>().Object
        );

        Assert.NotNull(factory);
    }

    #endregion

    #region CreateSecretsProviderAsync Tests

    [Fact]
    public async Task CreateSecretsProviderAsync_LoadsTheServerOwnersProtectorForItsPrivateSecrets()
    {
        // The data source and provider factories are the real ones, so the returned provider owns a real
        // periodic-polling data source that disposing the provider releases.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var persistedServer = CreatePersistedServer(
            "server-a",
            [
                CreateSecret(
                    "private-secret",
                    SecretKeyTypes.Symmetric,
                    SecretEncodings.SymmetricRaw,
                    "nce1.private"
                ),
                CreateSecret(
                    "public-secret",
                    SecretKeyTypes.Rsa,
                    SecretEncodings.Spki,
                    "public-value"
                ),
            ]
        );

        var protector = CreateStrictMock<ISecretProtector>().Object;

        var mockSecretProtectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        mockSecretProtectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForServer("server-a"),
                    It.Is<IReadOnlyCollection<string>>(v =>
                        v.Count == 1 && v.Contains("nce1.private")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(protector)
            .Verifiable();

        var mockSecretSerializer = CreateStrictMock<ISecretSerializer>();
        mockSecretSerializer
            .Setup(x => x.DeserializeSecrets(protector, persistedServer.Secrets.Value))
            .Returns(Array.Empty<SecretKey>())
            .Verifiable();

        var factory = CreateFactory(
            mockSecretSerializer.Object,
            mockSecretProtectorProvider.Object,
            serviceProvider.GetRequiredService<ICollectionDataSourceFactory>(),
            serviceProvider.GetRequiredService<ISecretKeyCollectionProviderFactory>()
        );

        await using var provider = await factory.CreateSecretsProviderAsync(
            persistedServer,
            CreateStrictMock<IPropertyBag>().Object,
            CancellationToken.None
        );

        Assert.NotNull(provider);
    }

    [Fact]
    public async Task CreateSecretsProviderAsync_WhenTheMasterKeyCannotBeLoaded_Propagates()
    {
        var persistedServer = CreatePersistedServer(
            "server-a",
            [
                CreateSecret(
                    "private-secret",
                    SecretKeyTypes.Symmetric,
                    SecretEncodings.SymmetricRaw,
                    "nce1.private"
                ),
            ]
        );

        var masterKeyFailure = new CryptographicException("The master key was not found.");
        var mockSecretProtectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        mockSecretProtectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForServer("server-a"),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(masterKeyFailure)
            .Verifiable();

        // The serializer is strict with no setup: deserializing after the failure fails the test.
        var factory = CreateFactory(
            CreateStrictMock<ISecretSerializer>().Object,
            mockSecretProtectorProvider.Object
        );

        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
            await factory.CreateSecretsProviderAsync(
                persistedServer,
                CreateStrictMock<IPropertyBag>().Object,
                CancellationToken.None
            )
        );

        Assert.Same(masterKeyFailure, exception);
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
        DefaultOpenIdServerFactoryTests tests,
        IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
        IDisposableCollectionDataSource<Setting> rootSettingsDataSource,
        RefreshableSettingsCollectionDataSource serverSettingsDataSource
    )
        : DefaultOpenIdServerFactory(
            Options.Create(new OpenIdOptions()),
            tests.CreateStrictMock<IServiceProvider>().Object,
            tests.CreateStrictMock<ISecretSerializer>().Object,
            tests.CreateStrictMock<ISecretProtectorProvider>().Object,
            tests.CreateStrictMock<IStoreManagerFactory>().Object,
            tests.CreateStrictMock<ICollectionDataSourceFactory>().Object,
            settingCollectionProviderFactory,
            tests.CreateStrictMock<ISecretKeyCollectionProviderFactory>().Object,
            tests.CreateStrictMock<ISecurityProfileCatalog>().Object,
            tests.CreateStrictMock<IIdGenerator<long>>().Object
        )
    {
        protected internal override ValueTask<PersistedServer> GetPersistedServerAsync(
            string serverId,
            IPropertyBag propertyBag,
            CancellationToken cancellationToken
        ) => ValueTask.FromResult(CreateEmptyPersistedServer(serverId));

        protected internal override ValueTask<
            IDisposableCollectionDataSource<Setting>
        > CreateRootSettingsDataSourceAsync(
            IPropertyBag propertyBag,
            CancellationToken cancellationToken
        ) => ValueTask.FromResult(rootSettingsDataSource);

        protected internal override ValueTask<RefreshableSettingsCollectionDataSource> CreateServerSettingsDataSourceAsync(
            OpenIdEnvironment openIdEnvironment,
            PersistedServer persistedServer,
            IPropertyBag propertyBag,
            CancellationToken cancellationToken
        ) => ValueTask.FromResult(serverSettingsDataSource);
    }
}
