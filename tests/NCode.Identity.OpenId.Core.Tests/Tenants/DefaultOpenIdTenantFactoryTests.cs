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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Collections.Providers;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class DefaultOpenIdTenantFactoryTests
{
    [Fact]
    public async Task CreateTenantAsync_WhenIdentityProbeHitsCache_DoesNotReadStore()
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
            collectionDataSourceFactory.Object
        );

        await using var result = await factory.CreateTenantAsync(
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
}
