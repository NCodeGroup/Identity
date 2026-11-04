// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using NCode.Collections.Providers;
using NCode.Collections.Providers.PeriodicPolling;
using NCode.Disposables;
using NCode.Identity.Exceptions;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class DefaultOpenIdTenantFactoryTests
{
    private static DefaultOpenIdTenantFactory CreateFactory(
        MockRepository mocks,
        ITenantResolver tenantResolver,
        IOpenIdTenantCache tenantCache,
        IStoreManagerFactory? storeManagerFactory = null,
        IOpenIdTeamProvider? teamProvider = null
    ) =>
        new(
            tenantResolver,
            Options.Create(new OpenIdOptions()),
            storeManagerFactory ?? mocks.Create<IStoreManagerFactory>().Object,
            tenantCache,
            mocks.Create<IReadOnlySettingCollectionProviderFactory>().Object,
            mocks.Create<ISettingSerializer>().Object,
            mocks.Create<ISecretSerializer>().Object,
            mocks.Create<ISecretProtectorProvider>().Object,
            mocks.Create<ISecretKeyCollectionProviderFactory>().Object,
            mocks.Create<ICollectionDataSourceFactory>().Object,
            mocks.Create<IEffectiveSettingsResolver>().Object,
            teamProvider ?? mocks.Create<IOpenIdTeamProvider>().Object
        );

    private static Mock<IOpenIdTeamProvider> CreateTeamProvider(
        MockRepository mocks,
        bool isDisabled,
        Action? onLookup = null
    )
    {
        var team = mocks.Create<OpenIdTeam>();
        team.Setup(x => x.IsDisabled).Returns(isDisabled);
        team.Protected().Setup<ValueTask>("DisposeAsyncCore").Returns(ValueTask.CompletedTask);

        var teamProvider = mocks.Create<IOpenIdTeamProvider>();
        teamProvider
            .Setup(x =>
                x.GetTeamAsync(
                    It.IsAny<OpenIdEnvironment>(),
                    It.IsAny<OpenIdPlatform>(),
                    "platform",
                    It.IsAny<IPropertyBag>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => onLookup?.Invoke())
            .ReturnsAsync(team.Object.AsSharedReference())
            .Verifiable();
        return teamProvider;
    }

    #region CreateTenantForRequestAsync Tests

    [Fact]
    public async Task CreateTenantForRequestAsync_WhenIdentityProbeHitsCache_DoesNotReadStore()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        var tenantResolver = mocks.Create<ITenantResolver>();
        var tenantCache = mocks.Create<IOpenIdTenantCache>();

        var tenantId = "tenant-1";
        tenantResolver
            .Setup(x => x.TryGetTenantId(It.IsAny<HttpContext>(), out tenantId))
            .Returns(true)
            .Verifiable();

        var cachedTenant = Mock.Of<OpenIdTenant>(tenant => tenant.TenantId == "tenant-1");
        await using var cachedLease = cachedTenant.AsSharedReference();

        tenantCache
            .Setup(x =>
                x.TryGetAsync("tenant-1", It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(cachedLease.AddReference())
            .Verifiable();

        // Every other collaborator is strict and unstubbed, so touching the store would fail the test.
        var factory = CreateFactory(mocks, tenantResolver.Object, tenantCache.Object);

        await using var result = await factory.CreateTenantForRequestAsync(
            new DefaultHttpContext(),
            Mock.Of<OpenIdEnvironment>(),
            Mock.Of<OpenIdPlatform>(),
            Mock.Of<IPropertyBag>(),
            CancellationToken.None
        );

        Assert.True(result.IsActive);
        Assert.Equal("tenant-1", result.Value.TenantId);
        mocks.Verify();
    }

    [Fact]
    public async Task CreateTenantForRequestAsync_WhenCacheMisses_SeedsTheResolvedTenant()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        var tenantResolver = mocks.Create<ITenantResolver>();
        var tenantCache = mocks.Create<IOpenIdTenantCache>();
        var provisioner = mocks.Create<ITenantProvisioner>();

        var tenantId = "acme";
        tenantResolver
            .Setup(x => x.TryGetTenantId(It.IsAny<HttpContext>(), out tenantId))
            .Returns(true)
            .Verifiable();
        tenantCache
            .Setup(x =>
                x.TryGetAsync("acme", It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(default(AsyncSharedReferenceLease<OpenIdTenant>))
            .Verifiable();
        tenantResolver
            .Setup(x =>
                x.ResolveTenantAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedTenant("https://op.example/acme"))
            .Verifiable();

        // The seed is the point of the test; failing it stops the materialization before the collaborators this test
        // does not stub.
        var seeded = new InvalidOperationException("seeded");
        provisioner
            .Setup(x => x.SeedAsync("acme", It.IsAny<CancellationToken>()))
            .ThrowsAsync(seeded)
            .Verifiable();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(provisioner.Object)
                .BuildServiceProvider(),
        };

        var teamProvider = CreateTeamProvider(mocks, isDisabled: false);

        var factory = CreateFactory(
            mocks,
            tenantResolver.Object,
            tenantCache.Object,
            teamProvider: teamProvider.Object
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.CreateTenantForRequestAsync(
                httpContext,
                Mock.Of<OpenIdEnvironment>(),
                Mock.Of<OpenIdPlatform>(),
                Mock.Of<IPropertyBag>(),
                CancellationToken.None
            )
        );

        Assert.Same(seeded, exception);
        mocks.Verify();
    }

    [Fact]
    public async Task MaterializeTenantAsync_WhenTheTeamIsDisabled_ThrowsNotFound()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        var tenantResolver = mocks.Create<ITenantResolver>();
        var tenantCache = mocks.Create<IOpenIdTenantCache>();
        var provisioner = mocks.Create<ITenantProvisioner>();

        var tenantId = "acme";
        tenantResolver
            .Setup(x => x.TryGetTenantId(It.IsAny<HttpContext>(), out tenantId))
            .Returns(true)
            .Verifiable();
        tenantCache
            .Setup(x =>
                x.TryGetAsync("acme", It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(default(AsyncSharedReferenceLease<OpenIdTenant>))
            .Verifiable();
        tenantResolver
            .Setup(x =>
                x.ResolveTenantAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedTenant("https://op.example/acme"))
            .Verifiable();

        // The strict provisioner has no SeedAsync setup, so a seed would fail the test too.
        var teamProvider = CreateTeamProvider(mocks, isDisabled: true);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(provisioner.Object)
                .BuildServiceProvider(),
        };

        var factory = CreateFactory(
            mocks,
            tenantResolver.Object,
            tenantCache.Object,
            teamProvider: teamProvider.Object
        );

        var exception = await Assert.ThrowsAsync<HttpResultException>(async () =>
            await factory.CreateTenantForRequestAsync(
                httpContext,
                Mock.Of<OpenIdEnvironment>(),
                Mock.Of<OpenIdPlatform>(),
                Mock.Of<IPropertyBag>(),
                CancellationToken.None
            )
        );

        Assert.Contains("is disabled", exception.Message, StringComparison.Ordinal);
        provisioner.Verify(
            x => x.SeedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        mocks.Verify();
    }

    [Fact]
    public async Task MaterializeTenantAsync_WhenTheTeamIsEnabled_SeedsAfterTheTeamCheck()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        var tenantResolver = mocks.Create<ITenantResolver>();
        var tenantCache = mocks.Create<IOpenIdTenantCache>();
        var provisioner = mocks.Create<ITenantProvisioner>();

        var calls = new List<string>();

        var tenantId = "acme";
        tenantResolver
            .Setup(x => x.TryGetTenantId(It.IsAny<HttpContext>(), out tenantId))
            .Returns(true)
            .Verifiable();
        tenantCache
            .Setup(x =>
                x.TryGetAsync("acme", It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(default(AsyncSharedReferenceLease<OpenIdTenant>))
            .Verifiable();
        tenantResolver
            .Setup(x =>
                x.ResolveTenantAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreatePersistedTenant("https://op.example/acme"))
            .Verifiable();

        // Failing the seed stops the materialization before the collaborators this test does not stub.
        var seeded = new InvalidOperationException("seeded");
        provisioner
            .Setup(x => x.SeedAsync("acme", It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("seed"))
            .ThrowsAsync(seeded)
            .Verifiable();

        var teamProvider = CreateTeamProvider(mocks, isDisabled: false, () => calls.Add("team"));

        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(provisioner.Object)
                .BuildServiceProvider(),
        };

        var factory = CreateFactory(
            mocks,
            tenantResolver.Object,
            tenantCache.Object,
            teamProvider: teamProvider.Object
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.CreateTenantForRequestAsync(
                httpContext,
                Mock.Of<OpenIdEnvironment>(),
                Mock.Of<OpenIdPlatform>(),
                Mock.Of<IPropertyBag>(),
                CancellationToken.None
            )
        );

        Assert.Same(seeded, exception);
        Assert.Equal(["team", "seed"], calls);
        mocks.Verify();
    }

    #endregion

    #region CreateControlPlaneTenantAsync Tests

    [Fact]
    public async Task CreateControlPlaneTenantAsync_WhenCached_ReturnsTheCachedTenantWithoutReadingTheStore()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        var tenantResolver = mocks.Create<ITenantResolver>();
        var tenantCache = mocks.Create<IOpenIdTenantCache>();

        // The strict resolver has no ResolveControlPlaneTenantAsync setup, so a store read fails the test.
        tenantResolver.Setup(x => x.GetControlPlaneTenantId()).Returns("root").Verifiable();

        var cachedTenant = Mock.Of<OpenIdTenant>(tenant => tenant.TenantId == "root");
        await using var cachedLease = cachedTenant.AsSharedReference();

        tenantCache
            .Setup(x =>
                x.TryGetAsync("root", It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(cachedLease.AddReference())
            .Verifiable();

        var factory = CreateFactory(mocks, tenantResolver.Object, tenantCache.Object);

        await using var result = await factory.CreateControlPlaneTenantAsync(
            new DefaultHttpContext(),
            Mock.Of<OpenIdEnvironment>(),
            Mock.Of<OpenIdPlatform>(),
            Mock.Of<IPropertyBag>(),
            CancellationToken.None
        );

        Assert.True(result.IsActive);
        Assert.Equal("root", result.Value.TenantId);
        mocks.Verify();
    }

    [Fact]
    public async Task CreateControlPlaneTenantAsync_WhenCacheMisses_SeedsTheControlPlaneTenant()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        var tenantResolver = mocks.Create<ITenantResolver>();
        var tenantCache = mocks.Create<IOpenIdTenantCache>();
        var provisioner = mocks.Create<ITenantProvisioner>();

        tenantResolver.Setup(x => x.GetControlPlaneTenantId()).Returns("root").Verifiable();
        tenantResolver
            .Setup(x =>
                x.ResolveControlPlaneTenantAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreatePersistedTenant("https://op.example/root", tenantId: "root"))
            .Verifiable();
        tenantCache
            .Setup(x =>
                x.TryGetAsync("root", It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(default(AsyncSharedReferenceLease<OpenIdTenant>))
            .Verifiable();

        // The seed is the point of the test; failing it stops the materialization before the collaborators this test
        // does not stub.
        var seeded = new InvalidOperationException("seeded");
        provisioner
            .Setup(x => x.SeedAsync("root", It.IsAny<CancellationToken>()))
            .ThrowsAsync(seeded)
            .Verifiable();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(provisioner.Object)
                .BuildServiceProvider(),
        };

        var teamProvider = CreateTeamProvider(mocks, isDisabled: false);

        var factory = CreateFactory(
            mocks,
            tenantResolver.Object,
            tenantCache.Object,
            teamProvider: teamProvider.Object
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.CreateControlPlaneTenantAsync(
                httpContext,
                Mock.Of<OpenIdEnvironment>(),
                Mock.Of<OpenIdPlatform>(),
                Mock.Of<IPropertyBag>(),
                CancellationToken.None
            )
        );

        Assert.Same(seeded, exception);
        mocks.Verify();
    }

    #endregion

    #region CreateTenantByIdAsync Tests

    [Fact]
    public async Task CreateTenantByIdAsync_WhenTenantNotFound_Throws()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();
        var storeManager = mocks.Create<IStoreManager>();
        var tenantStore = mocks.Create<ITenantStore>();

        storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(storeManager.Object)
            .Verifiable();
        storeManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(tenantStore.Object)
            .Verifiable();
        storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        tenantStore
            .Setup(x => x.GetOrDefaultAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();

        var factory = CreateFactory(
            mocks,
            mocks.Create<ITenantResolver>().Object,
            mocks.Create<IOpenIdTenantCache>().Object,
            storeManagerFactory.Object
        );

        await Assert.ThrowsAsync<HttpResultException>(async () =>
            await factory.CreateTenantByIdAsync(
                new DefaultHttpContext(),
                Mock.Of<OpenIdEnvironment>(),
                Mock.Of<OpenIdPlatform>(),
                "missing",
                Mock.Of<IPropertyBag>(),
                CancellationToken.None
            )
        );

        mocks.Verify();
    }

    #endregion

    #region GetTenantIssuerAsync Tests

    [Fact]
    public async Task GetTenantIssuerAsync_WhenTenantHasIssuer_ReturnsIt()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var factory = new TestableTenantFactory(mocks);

        var issuer = await factory.InvokeGetTenantIssuerAsync(
            CreateHttpContext(),
            CreatePersistedTenant("https://op.example/acme")
        );

        Assert.Equal("https://op.example/acme", issuer);
        mocks.Verify();
    }

    [Fact]
    public async Task GetTenantIssuerAsync_WhenTenantHasNoIssuer_FailsClosedRatherThanUsingTheRequest()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var factory = new TestableTenantFactory(mocks);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.InvokeGetTenantIssuerAsync(
                CreateHttpContext(),
                CreatePersistedTenant(issuer: null)
            )
        );

        Assert.Contains("'acme' has no issuer", exception.Message, StringComparison.Ordinal);
        mocks.Verify();
    }

    #endregion

    #region GetTenantBaseAddressAsync Tests

    [Fact]
    public async Task GetTenantBaseAddressAsync_WhenGivenTheIssuer_ReturnsItAsIs()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        // The issuer already names the tenant route, so the route is not bound again (https://op.example/acme/acme).
        var factory = new TestableTenantFactory(mocks);

        var baseAddress = await factory.InvokeGetTenantBaseAddressAsync(
            CreateHttpContext(),
            CreatePersistedTenant("https://op.example/acme"),
            "https://op.example/acme"
        );

        Assert.Equal("https", baseAddress.Scheme);
        Assert.Equal(new HostString("op.example"), baseAddress.Host);
        Assert.Equal(new PathString("/acme"), baseAddress.Path);
        mocks.Verify();
    }

    [Fact]
    public async Task GetTenantBaseAddressAsync_WhenRequestComesFromAnotherHost_StillUsesTheIssuer()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var factory = new TestableTenantFactory(mocks);

        // The request arrives on a host and path base of its own (a spoofed Host header, say); neither may reach the
        // URLs the server publishes.
        var baseAddress = await factory.InvokeGetTenantBaseAddressAsync(
            CreateHttpContext(),
            CreatePersistedTenant("https://op.example:8443/acme"),
            "https://op.example:8443/acme"
        );

        Assert.Equal("https://op.example:8443/acme", baseAddress.ToString());
        mocks.Verify();
    }

    #endregion

    #region GetTenantSecretsAsync Tests

    [Fact]
    public async Task GetTenantSecretsAsync_WhenAKeyCannotBeUnprotected_FailsNamingTheTenantAndTheMasterKey()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var persistedTenant = CreatePersistedTenant("https://op.example/acme");
        var protector = mocks.Create<ISecretProtector>().Object;
        var secretProtectorProvider = mocks.Create<ISecretProtectorProvider>();
        secretProtectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForTenant("acme"),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(protector)
            .Verifiable();
        var masterKeyFailure = new CryptographicException("The master key was not found.");
        var secretSerializer = mocks.Create<ISecretSerializer>();
        secretSerializer
            .Setup(x => x.DeserializeSecrets(protector, persistedTenant.Secrets.Value))
            .Throws(masterKeyFailure)
            .Verifiable();
        var factory = new TestableTenantFactory(
            mocks,
            secretSerializer.Object,
            secretProtectorProvider.Object
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.InvokeGetTenantSecretsAsync(persistedTenant)
        );

        Assert.Contains("tenant with id 'acme'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("master key", exception.Message, StringComparison.Ordinal);
        Assert.Same(masterKeyFailure, exception.InnerException);
        mocks.Verify();
    }

    [Fact]
    public async Task GetTenantSecretsAsync_WhenTheMasterKeyCannotBeLoaded_FailsNamingTheTenantAndTheMasterKey()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var persistedTenant = CreatePersistedTenant("https://op.example/acme");
        var masterKeyFailure = new CryptographicException("The master key was not found.");
        var secretProtectorProvider = mocks.Create<ISecretProtectorProvider>();
        secretProtectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForTenant("acme"),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(masterKeyFailure)
            .Verifiable();
        var factory = new TestableTenantFactory(
            mocks,
            secretProtectorProvider: secretProtectorProvider.Object
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.InvokeGetTenantSecretsAsync(persistedTenant)
        );

        Assert.Contains("tenant with id 'acme'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("master key", exception.Message, StringComparison.Ordinal);
        Assert.Same(masterKeyFailure, exception.InnerException);
        mocks.Verify();
    }

    #endregion

    #region GetTenantSettingsAsync Tests

    [Fact]
    public async Task GetTenantSettingsAsync_LayersTheParentProviderUnderTheTenantDocument()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var persistedTenant = CreatePersistedTenant("https://op.example/acme");
        var parentCollection = mocks.Create<IReadOnlySettingCollection>();
        var openIdTeam = CreateTeamWithSettings(mocks, parentCollection.Object);
        var resolver = mocks.Create<IEffectiveSettingsResolver>();
        resolver
            .Setup(x =>
                x.ReadScopeSettings(
                    parentCollection.Object,
                    It.IsAny<string>(),
                    It.IsAny<JsonElement>()
                )
            )
            .Returns([CreateEnvironmentNameSetting()])
            .Verifiable();
        var dataSources = new List<ICollectionDataSource<Setting>>();
        var factory = CreateSettingsFactory(mocks, resolver.Object, dataSources);

        var lease = await factory.InvokeGetTenantSettingsAsync(
            Mock.Of<OpenIdEnvironment>(),
            openIdTeam.Object,
            persistedTenant
        );

        Assert.Equal(2, dataSources.Count);
        await lease.DisposeAsync();
        Assert.Same(parentCollection.Object, dataSources[0].Collection);
        mocks.Verify();
    }

    [Fact]
    public async Task GetTenantSettingsAsync_ReadsTheTenantDocumentAtTheTenantScope()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var persistedTenant = CreatePersistedTenant("https://op.example/acme");
        var tenantDocument = persistedTenant.Settings.Value;
        var parentCollection = mocks.Create<IReadOnlySettingCollection>();
        var openIdTeam = CreateTeamWithSettings(mocks, parentCollection.Object);
        var resolver = mocks.Create<IEffectiveSettingsResolver>();
        resolver
            .Setup(x =>
                x.ReadScopeSettings(
                    It.IsAny<IReadOnlySettingCollection>(),
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(document => JsonElement.DeepEquals(document, tenantDocument))
                )
            )
            .Returns([CreateEnvironmentNameSetting()])
            .Verifiable();
        var dataSources = new List<ICollectionDataSource<Setting>>();
        var factory = CreateSettingsFactory(mocks, resolver.Object, dataSources);

        var lease = await factory.InvokeGetTenantSettingsAsync(
            Mock.Of<OpenIdEnvironment>(),
            openIdTeam.Object,
            persistedTenant
        );

        Assert.Equal(2, dataSources.Count);
        await lease.DisposeAsync();
        mocks.Verify();
    }

    #endregion

    private static Setting CreateEnvironmentNameSetting() =>
        new Setting<string>(
            new SettingDescriptor<string>
            {
                Name = "environment_name",
                AppliesTo = [OpenIdSettingScopes.Tenant],
            },
            "sandbox"
        );

    private static Mock<OpenIdTeam> CreateTeamWithSettings(
        MockRepository mocks,
        IReadOnlySettingCollection parentCollection
    )
    {
        var parentProvider = mocks.Create<IReadOnlySettingCollectionProvider>();
        parentProvider.Setup(x => x.Collection).Returns(parentCollection).Verifiable();

        var openIdTeam = mocks.Create<OpenIdTeam>();
        openIdTeam.Setup(x => x.SettingsProvider).Returns(parentProvider.Object).Verifiable();
        return openIdTeam;
    }

    private static TestableTenantFactory CreateSettingsFactory(
        MockRepository mocks,
        IEffectiveSettingsResolver resolver,
        List<ICollectionDataSource<Setting>> capturedDataSources
    )
    {
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
            .Returns(pollingSource.Object)
            .Verifiable();

        var provider = mocks.Create<IReadOnlySettingCollectionProvider>();
        provider.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        var providerFactory = mocks.Create<IReadOnlySettingCollectionProviderFactory>();
        providerFactory
            .Setup(x => x.Create(It.IsAny<IEnumerable<ICollectionDataSource<Setting>>>(), true))
            .Callback(
                (IEnumerable<ICollectionDataSource<Setting>> dataSources, bool _) =>
                    capturedDataSources.AddRange(dataSources)
            )
            .Returns(provider.Object)
            .Verifiable();

        return new TestableTenantFactory(
            mocks,
            effectiveSettingsResolver: resolver,
            settingCollectionProviderFactory: providerFactory.Object,
            collectionDataSourceFactory: dataSourceFactory.Object
        );
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;
        request.Scheme = "http";
        request.Host = new HostString("attacker.example:5000");
        request.PathBase = "/base";
        request.RouteValues["tenantId"] = "acme";
        return httpContext;
    }

    private static PersistedTenant CreatePersistedTenant(
        string? issuer,
        string tenantId = "acme"
    ) =>
        new()
        {
            TenantId = tenantId,
            TeamId = "platform",
            Issuer = issuer,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                ConcurrencyToken = "token",
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = "token",
                Value = [],
            },
        };

    private sealed class TestableTenantFactory(
        MockRepository mocks,
        ISecretSerializer? secretSerializer = null,
        ISecretProtectorProvider? secretProtectorProvider = null,
        IEffectiveSettingsResolver? effectiveSettingsResolver = null,
        IReadOnlySettingCollectionProviderFactory? settingCollectionProviderFactory = null,
        ICollectionDataSourceFactory? collectionDataSourceFactory = null
    )
        : DefaultOpenIdTenantFactory(
            mocks.Create<ITenantResolver>().Object,
            Options.Create(new OpenIdOptions()),
            mocks.Create<IStoreManagerFactory>().Object,
            mocks.Create<IOpenIdTenantCache>().Object,
            settingCollectionProviderFactory
                ?? mocks.Create<IReadOnlySettingCollectionProviderFactory>().Object,
            mocks.Create<ISettingSerializer>().Object,
            secretSerializer ?? mocks.Create<ISecretSerializer>().Object,
            secretProtectorProvider ?? mocks.Create<ISecretProtectorProvider>().Object,
            mocks.Create<ISecretKeyCollectionProviderFactory>().Object,
            collectionDataSourceFactory ?? mocks.Create<ICollectionDataSourceFactory>().Object,
            effectiveSettingsResolver ?? mocks.Create<IEffectiveSettingsResolver>().Object,
            mocks.Create<IOpenIdTeamProvider>().Object
        )
    {
        public ValueTask<
            AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider>
        > InvokeGetTenantSettingsAsync(
            OpenIdEnvironment openIdEnvironment,
            OpenIdTeam openIdTeam,
            PersistedTenant persistedTenant
        ) =>
            GetTenantSettingsAsync(
                CreateHttpContext(),
                openIdEnvironment,
                openIdTeam,
                persistedTenant,
                Mock.Of<IPropertyBag>(),
                CancellationToken.None
            );

        public ValueTask<
            AsyncSharedReferenceLease<ISecretKeyCollectionProvider>
        > InvokeGetTenantSecretsAsync(PersistedTenant persistedTenant) =>
            GetTenantSecretsAsync(
                CreateHttpContext(),
                persistedTenant,
                persistedTenant.Issuer ?? string.Empty,
                default,
                default,
                Mock.Of<IPropertyBag>(),
                CancellationToken.None
            );

        public ValueTask<string> InvokeGetTenantIssuerAsync(
            HttpContext httpContext,
            PersistedTenant persistedTenant
        ) =>
            GetTenantIssuerAsync(
                httpContext,
                persistedTenant,
                default,
                Mock.Of<IPropertyBag>(),
                CancellationToken.None
            );

        public ValueTask<UriDescriptor> InvokeGetTenantBaseAddressAsync(
            HttpContext httpContext,
            PersistedTenant persistedTenant,
            string tenantIssuer
        ) =>
            GetTenantBaseAddressAsync(
                httpContext,
                persistedTenant,
                tenantIssuer,
                default,
                Mock.Of<IPropertyBag>(),
                CancellationToken.None
            );
    }
}
