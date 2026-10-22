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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Collections.Providers;
using NCode.Disposables;
using NCode.Identity.Exceptions;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Persistence;
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
        IStoreManagerFactory? storeManagerFactory = null
    ) =>
        new(
            tenantResolver,
            Options.Create(new OpenIdOptions()),
            storeManagerFactory ?? mocks.Create<IStoreManagerFactory>().Object,
            tenantCache,
            mocks.Create<IReadOnlySettingCollectionProviderFactory>().Object,
            mocks.Create<ISettingSerializer>().Object,
            mocks.Create<ISecretSerializer>().Object,
            mocks.Create<ISecretKeyCollectionProviderFactory>().Object,
            mocks.Create<ICollectionDataSourceFactory>().Object,
            mocks.Create<IEffectiveSettingsResolver>().Object
        );

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
            Mock.Of<OpenIdServer>(),
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

        var factory = CreateFactory(mocks, tenantResolver.Object, tenantCache.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.CreateTenantForRequestAsync(
                httpContext,
                Mock.Of<OpenIdEnvironment>(),
                Mock.Of<OpenIdServer>(),
                Mock.Of<IPropertyBag>(),
                CancellationToken.None
            )
        );

        Assert.Same(seeded, exception);
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
            Mock.Of<OpenIdServer>(),
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

        var factory = CreateFactory(mocks, tenantResolver.Object, tenantCache.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.CreateControlPlaneTenantAsync(
                httpContext,
                Mock.Of<OpenIdEnvironment>(),
                Mock.Of<OpenIdServer>(),
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
                Mock.Of<OpenIdServer>(),
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
    public async Task GetTenantSecretsAsync_WhenAKeyCannotBeUnprotected_FailsNamingTheTenantAndTheKeyRing()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var persistedTenant = CreatePersistedTenant("https://op.example/acme");
        var keyRingFailure = new CryptographicException("The key was not found in the key ring.");
        var secretSerializer = mocks.Create<ISecretSerializer>();
        secretSerializer
            .Setup(x => x.DeserializeSecrets(persistedTenant.Secrets.Value))
            .Throws(keyRingFailure)
            .Verifiable();
        var factory = new TestableTenantFactory(mocks, secretSerializer.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.InvokeGetTenantSecretsAsync(persistedTenant)
        );

        Assert.Contains("tenant with id 'acme'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Data Protection key ring", exception.Message, StringComparison.Ordinal);
        Assert.Same(keyRingFailure, exception.InnerException);
        mocks.Verify();
    }

    #endregion

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
        ISecretSerializer? secretSerializer = null
    )
        : DefaultOpenIdTenantFactory(
            mocks.Create<ITenantResolver>().Object,
            Options.Create(new OpenIdOptions()),
            mocks.Create<IStoreManagerFactory>().Object,
            mocks.Create<IOpenIdTenantCache>().Object,
            mocks.Create<IReadOnlySettingCollectionProviderFactory>().Object,
            mocks.Create<ISettingSerializer>().Object,
            secretSerializer ?? mocks.Create<ISecretSerializer>().Object,
            mocks.Create<ISecretKeyCollectionProviderFactory>().Object,
            mocks.Create<ICollectionDataSourceFactory>().Object,
            mocks.Create<IEffectiveSettingsResolver>().Object
        )
    {
        public ValueTask<
            AsyncSharedReferenceLease<ISecretKeyCollectionProvider>
        > InvokeGetTenantSecretsAsync(PersistedTenant persistedTenant) =>
            GetTenantSecretsAsync(
                CreateHttpContext(),
                null!,
                null!,
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
