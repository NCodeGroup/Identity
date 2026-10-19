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

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
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
    [Fact]
    public async Task CreateTenantForRequestAsync_WhenIdentityProbeHitsCache_DoesNotReadStore()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        var tenantResolver = mocks.Create<ITenantResolver>();
        var tenantCache = mocks.Create<IOpenIdTenantCache>();

        // Deps that must not be touched on a cache hit; a strict call would fail the test.
        var templateBinderFactory = mocks.Create<TemplateBinderFactory>();
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();
        var settingCollectionProviderFactory =
            mocks.Create<IReadOnlySettingCollectionProviderFactory>();
        var settingSerializer = mocks.Create<ISettingSerializer>();
        var secretSerializer = mocks.Create<ISecretSerializer>();
        var secretKeyCollectionProviderFactory =
            mocks.Create<ISecretKeyCollectionProviderFactory>();
        var collectionDataSourceFactory = mocks.Create<ICollectionDataSourceFactory>();

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

        var factory = new DefaultOpenIdTenantFactory(
            tenantResolver.Object,
            templateBinderFactory.Object,
            Options.Create(new OpenIdOptions()),
            storeManagerFactory.Object,
            tenantCache.Object,
            settingCollectionProviderFactory.Object,
            settingSerializer.Object,
            secretSerializer.Object,
            secretKeyCollectionProviderFactory.Object,
            collectionDataSourceFactory.Object,
            mocks.Create<ISecurityProfileCatalog>().Object
        );

        await using var result = await factory.CreateTenantForRequestAsync(
            new DefaultHttpContext(),
            Mock.Of<OpenIdEnvironment>(),
            Mock.Of<OpenIdServer>(),
            Mock.Of<IPropertyBag>(),
            CancellationToken.None
        );

        Assert.True(result.IsActive);
        Assert.Equal("tenant-1", result.Value.TenantId);

        // The strict resolver mock never stubs ResolveTenantAsync, proving the store was not read.
        tenantResolver.Verify(
            x => x.ResolveTenantAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        mocks.Verify();
    }

    [Fact]
    public async Task CreateTenantByIdAsync_WhenTenantNotFound_Throws()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        var tenantResolver = mocks.Create<ITenantResolver>();
        var tenantCache = mocks.Create<IOpenIdTenantCache>();
        var templateBinderFactory = mocks.Create<TemplateBinderFactory>();
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();
        var storeManager = mocks.Create<IStoreManager>();
        var tenantStore = mocks.Create<ITenantStore>();
        var settingCollectionProviderFactory =
            mocks.Create<IReadOnlySettingCollectionProviderFactory>();
        var settingSerializer = mocks.Create<ISettingSerializer>();
        var secretSerializer = mocks.Create<ISecretSerializer>();
        var secretKeyCollectionProviderFactory =
            mocks.Create<ISecretKeyCollectionProviderFactory>();
        var collectionDataSourceFactory = mocks.Create<ICollectionDataSourceFactory>();

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

        var factory = new DefaultOpenIdTenantFactory(
            tenantResolver.Object,
            templateBinderFactory.Object,
            Options.Create(new OpenIdOptions()),
            storeManagerFactory.Object,
            tenantCache.Object,
            settingCollectionProviderFactory.Object,
            settingSerializer.Object,
            secretSerializer.Object,
            secretKeyCollectionProviderFactory.Object,
            collectionDataSourceFactory.Object,
            mocks.Create<ISecurityProfileCatalog>().Object
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

    #region GetTenantBaseAddressAsync Tests

    [Fact]
    public async Task GetTenantBaseAddressAsync_WhenTenantHasIssuer_ReturnsTheIssuerAsIs()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        // Neither collaborator may be consulted: the issuer already names the tenant route, so binding the route again
        // would publish it twice (https://op.example/acme/acme).
        var factory = new TestableTenantFactory(
            mocks.Create<ITenantResolver>().Object,
            mocks.Create<TemplateBinderFactory>().Object,
            mocks
        );

        var httpContext = CreateHttpContext();
        var persistedTenant = CreatePersistedTenant("https://op.example/acme");

        var baseAddress = await factory.InvokeGetTenantBaseAddressAsync(
            httpContext,
            persistedTenant
        );

        Assert.Equal("https", baseAddress.Scheme);
        Assert.Equal(new HostString("op.example"), baseAddress.Host);
        Assert.Equal(new PathString("/acme"), baseAddress.Path);
        mocks.Verify();
    }

    [Fact]
    public async Task GetTenantBaseAddressAsync_WhenTenantHasNoIssuer_DerivesItFromTheRequestAndTenantRoute()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        var tenantResolver = mocks.Create<ITenantResolver>();
        tenantResolver
            .Setup(x => x.GetTenantRoute())
            .Returns(RoutePatternFactory.Parse("/{tenantId}"))
            .Verifiable();

        using var serviceProvider = new ServiceCollection().AddRouting().BuildServiceProvider();
        var templateBinderFactory = serviceProvider.GetRequiredService<TemplateBinderFactory>();

        var factory = new TestableTenantFactory(
            tenantResolver.Object,
            templateBinderFactory,
            mocks
        );

        var httpContext = CreateHttpContext();
        var persistedTenant = CreatePersistedTenant(issuer: null);

        var baseAddress = await factory.InvokeGetTenantBaseAddressAsync(
            httpContext,
            persistedTenant
        );

        Assert.Equal("http", baseAddress.Scheme);
        Assert.Equal(new HostString("localhost:5000"), baseAddress.Host);
        Assert.Equal(new PathString("/base/acme"), baseAddress.Path);
        mocks.Verify();
    }

    [Fact]
    public async Task GetTenantBaseAddressAsync_WhenRequestHasOtherRouteValues_LeavesThemOnTheRequest()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        var tenantResolver = mocks.Create<ITenantResolver>();
        tenantResolver
            .Setup(x => x.GetTenantRoute())
            .Returns(RoutePatternFactory.Parse("/{tenantId}"))
            .Verifiable();

        using var serviceProvider = new ServiceCollection().AddRouting().BuildServiceProvider();
        var templateBinderFactory = serviceProvider.GetRequiredService<TemplateBinderFactory>();

        var factory = new TestableTenantFactory(
            tenantResolver.Object,
            templateBinderFactory,
            mocks
        );

        var httpContext = CreateHttpContext();
        var routeValues = httpContext.Request.RouteValues;
        routeValues["client_id"] = "client-1";
        var persistedTenant = CreatePersistedTenant(issuer: null);

        var baseAddress = await factory.InvokeGetTenantBaseAddressAsync(
            httpContext,
            persistedTenant
        );

        // Neither consumed from the request nor leaked into the base address as a query string.
        Assert.Equal(new PathString("/base/acme"), baseAddress.Path);
        Assert.Equal("acme", routeValues["tenantId"]);
        Assert.Equal("client-1", routeValues["client_id"]);
        mocks.Verify();
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;
        request.Scheme = "http";
        request.Host = new HostString("localhost:5000");
        request.PathBase = "/base";
        request.RouteValues["tenantId"] = "acme";
        return httpContext;
    }

    private static PersistedTenant CreatePersistedTenant(string? issuer) =>
        new()
        {
            TenantId = "acme",
            Issuer = issuer,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = "acme",
            Settings = new PersistedTenantSettings
            {
                TenantId = "acme",
                ConcurrencyToken = "token",
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = "acme",
                ConcurrencyToken = "token",
                Value = [],
            },
        };

    private sealed class TestableTenantFactory(
        ITenantResolver tenantResolver,
        TemplateBinderFactory templateBinderFactory,
        MockRepository mocks
    )
        : DefaultOpenIdTenantFactory(
            tenantResolver,
            templateBinderFactory,
            Options.Create(new OpenIdOptions()),
            mocks.Create<IStoreManagerFactory>().Object,
            mocks.Create<IOpenIdTenantCache>().Object,
            mocks.Create<IReadOnlySettingCollectionProviderFactory>().Object,
            mocks.Create<ISettingSerializer>().Object,
            mocks.Create<ISecretSerializer>().Object,
            mocks.Create<ISecretKeyCollectionProviderFactory>().Object,
            mocks.Create<ICollectionDataSourceFactory>().Object,
            mocks.Create<ISecurityProfileCatalog>().Object
        )
    {
        public ValueTask<UriDescriptor> InvokeGetTenantBaseAddressAsync(
            HttpContext httpContext,
            PersistedTenant persistedTenant
        ) =>
            GetTenantBaseAddressAsync(
                httpContext,
                persistedTenant,
                default,
                Mock.Of<IPropertyBag>(),
                CancellationToken.None
            );
    }

    #endregion
}
