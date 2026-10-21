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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tenants.Strategies;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class StaticSingleTenantStrategyTests
{
    private static StaticSingleTenantStrategy CreateStrategy(
        IStoreManagerFactory storeManagerFactory,
        StaticSingleTenantOptions? options = null
    ) =>
        new(
            storeManagerFactory,
            Options.Create(new TenantResolutionOptions { StaticSingle = options })
        );

    [Fact]
    public void TryGetTenantId_WhenConfigured_ReturnsConfiguredIdWithoutStore()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();

        var strategy = CreateStrategy(
            storeManagerFactory.Object,
            new StaticSingleTenantOptions { TenantId = "tenant-1" }
        );

        var result = strategy.TryGetTenantId(new DefaultHttpContext(), out var tenantId);

        Assert.True(result);
        Assert.Equal("tenant-1", tenantId);
        mocks.Verify();
    }

    [Fact]
    public void TryGetTenantId_WhenNotConfigured_ReturnsDefaultId()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();

        var strategy = CreateStrategy(storeManagerFactory.Object);

        var result = strategy.TryGetTenantId(new DefaultHttpContext(), out var tenantId);

        Assert.True(result);
        Assert.Equal(StaticSingleTenantOptions.DefaultTenantId, tenantId);
        mocks.Verify();
    }

    [Fact]
    public async Task ResolveTenantAsync_EnsuresTheConfiguredTenantExists()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManagerFactory = mocks.Create<IStoreManagerFactory>();
        var provisioner = mocks.Create<ITenantProvisioner>();

        var persistedTenant = CreateTenant(isDisabled: false);

        provisioner
            .Setup(x =>
                x.EnsureCreatedAsync("tenant-1", "Tenant One", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(persistedTenant)
            .Verifiable();

        var strategy = CreateStrategy(
            storeManagerFactory.Object,
            new StaticSingleTenantOptions { TenantId = "tenant-1", DisplayName = "Tenant One" }
        );

        var result = await strategy.ResolveTenantAsync(
            CreateHttpContext(provisioner.Object),
            CancellationToken.None
        );

        Assert.Same(persistedTenant, result);
        mocks.Verify();
    }

    [Fact]
    public async Task ResolveTenantAsync_WhenTenantIsDisabled_ThrowsNotFound()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var provisioner = mocks.Create<ITenantProvisioner>();

        provisioner
            .Setup(x =>
                x.EnsureCreatedAsync("tenant-1", "Tenant One", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateTenant(isDisabled: true))
            .Verifiable();

        var strategy = CreateStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            new StaticSingleTenantOptions { TenantId = "tenant-1", DisplayName = "Tenant One" }
        );

        await Assert.ThrowsAsync<HttpResultException>(async () =>
            await strategy.ResolveTenantAsync(
                CreateHttpContext(provisioner.Object),
                CancellationToken.None
            )
        );

        mocks.Verify();
    }

    #region ResolveControlPlaneTenantAsync Tests

    [Fact]
    public async Task ResolveControlPlaneTenantAsync_Always_ReturnsTheConfiguredTenant()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var provisioner = mocks.Create<ITenantProvisioner>();

        var persistedTenant = CreateTenant(isDisabled: false);

        provisioner
            .Setup(x =>
                x.EnsureCreatedAsync("tenant-1", "Tenant One", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(persistedTenant)
            .Verifiable();

        var strategy = CreateStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            new StaticSingleTenantOptions { TenantId = "tenant-1", DisplayName = "Tenant One" }
        );

        // The strategy serves no other tenant, so no other tenant can issue the caller's token.
        var result = await strategy.ResolveControlPlaneTenantAsync(
            CreateHttpContext(provisioner.Object),
            CancellationToken.None
        );

        Assert.Same(persistedTenant, result);
        mocks.Verify();
    }

    #endregion

    #region IsRequiredTenant Tests

    [Theory]
    [InlineData("tenant-1", true)]
    [InlineData(RootTenantOptions.DefaultTenantId, true)]
    [InlineData("tenant-2", false)]
    public void IsRequiredTenant_RequiresTheConfiguredAndRootTenantsOnly(
        string tenantId,
        bool expected
    )
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            new StaticSingleTenantOptions { TenantId = "tenant-1" }
        );

        Assert.Equal(expected, strategy.IsRequiredTenant(tenantId));
        mocks.Verify();
    }

    #endregion

    #region GetDefaultIssuerAsync Tests

    [Fact]
    public async Task GetDefaultIssuerAsync_WhenConfiguredTenant_ReturnsTheConfiguredIssuer()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            new StaticSingleTenantOptions { TenantId = "tenant-1", Issuer = "https://op.example" }
        );

        var issuer = await strategy.GetDefaultIssuerAsync("tenant-1", CancellationToken.None);

        Assert.Equal("https://op.example", issuer);
        mocks.Verify();
    }

    [Fact]
    public async Task GetDefaultIssuerAsync_WhenAnotherTenant_ReturnsNone()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        // Not even the root tenant's configured issuer: the strategy does not serve the root tenant.
        var strategy = new StaticSingleTenantStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            Options.Create(
                new TenantResolutionOptions
                {
                    Root = { Issuer = "https://auth.example" },
                    StaticSingle = new StaticSingleTenantOptions
                    {
                        TenantId = "tenant-1",
                        Issuer = "https://op.example",
                    },
                }
            )
        );

        var issuer = await strategy.GetDefaultIssuerAsync(
            RootTenantOptions.DefaultTenantId,
            CancellationToken.None
        );

        Assert.Null(issuer);
        mocks.Verify();
    }

    #endregion

    #region GetDefaultMtlsEndpointAliasAuthorityAsync Tests

    [Fact]
    public async Task GetDefaultMtlsEndpointAliasAuthorityAsync_WhenConfiguredTenant_ReturnsTheConfiguredAlias()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            new StaticSingleTenantOptions
            {
                TenantId = "tenant-1",
                Issuer = "https://op.example",
                MtlsEndpointAliasAuthority = "https://mtls.op.example",
            }
        );

        var alias = await strategy.GetDefaultMtlsEndpointAliasAuthorityAsync(
            "tenant-1",
            CancellationToken.None
        );

        Assert.Equal("https://mtls.op.example", alias);
        mocks.Verify();
    }

    [Fact]
    public async Task GetDefaultMtlsEndpointAliasAuthorityAsync_WhenAnotherTenant_ReturnsNone()
    {
        var mocks = new MockRepository(MockBehavior.Strict);

        // Not even the root tenant's configured alias: the strategy does not serve the root tenant.
        var strategy = new StaticSingleTenantStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            Options.Create(
                new TenantResolutionOptions
                {
                    Root = { MtlsEndpointAliasAuthority = "https://mtls.example" },
                    StaticSingle = new StaticSingleTenantOptions
                    {
                        TenantId = "tenant-1",
                        Issuer = "https://op.example",
                        MtlsEndpointAliasAuthority = "https://mtls.op.example",
                    },
                }
            )
        );

        var alias = await strategy.GetDefaultMtlsEndpointAliasAuthorityAsync(
            RootTenantOptions.DefaultTenantId,
            CancellationToken.None
        );

        Assert.Null(alias);
        mocks.Verify();
    }

    #endregion

    #region ValidateTenantAsync Tests

    [Theory]
    [InlineData("https://op.example/identity", true)]
    [InlineData("https://op.example/base/identity", true)]
    [InlineData("https://op.example", false)]
    [InlineData("https://op.example/other", false)]
    public async Task ValidateTenantAsync_ChecksTheIssuerEndsWithTheTenantPath(
        string issuer,
        bool expectedValid
    )
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            new StaticSingleTenantOptions { TenantPath = "/identity" }
        );

        var failures = await strategy.ValidateTenantAsync(
            "default",
            new Uri(issuer),
            null,
            mocks.Create<IStoreManager>().Object,
            CancellationToken.None
        );

        Assert.Equal(expectedValid, failures.Count == 0);
        mocks.Verify();
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenConfiguredTenantHasNoIssuer_Fails()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            new StaticSingleTenantOptions { TenantId = "tenant-1" }
        );

        var failures = await strategy.ValidateTenantAsync(
            "tenant-1",
            issuer: null,
            null,
            mocks.Create<IStoreManager>().Object,
            CancellationToken.None
        );

        var failure = Assert.Single(failures);
        Assert.Contains("requires an issuer", failure.Detail, StringComparison.Ordinal);
        Assert.False(failure.IsConflict);
        mocks.Verify();
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenUnservedTenantHasNoIssuer_Succeeds()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(
            mocks.Create<IStoreManagerFactory>().Object,
            new StaticSingleTenantOptions { TenantId = "tenant-1" }
        );

        // The root tenant is not served by a static strategy whose tenant is another, so it needs no address.
        var failures = await strategy.ValidateTenantAsync(
            RootTenantOptions.DefaultTenantId,
            issuer: null,
            null,
            mocks.Create<IStoreManager>().Object,
            CancellationToken.None
        );

        Assert.Empty(failures);
        mocks.Verify();
    }

    #endregion

    private static DefaultHttpContext CreateHttpContext(ITenantProvisioner provisioner) =>
        new()
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(provisioner)
                .BuildServiceProvider(),
        };

    private static PersistedTenant CreateTenant(bool isDisabled) =>
        new()
        {
            TenantId = "tenant-1",
            Issuer = "https://op.example",
            ConcurrencyToken = "token",
            IsDisabled = isDisabled,
            DisplayName = "Tenant One",
            Settings = null!,
            Secrets = null!,
        };
}
