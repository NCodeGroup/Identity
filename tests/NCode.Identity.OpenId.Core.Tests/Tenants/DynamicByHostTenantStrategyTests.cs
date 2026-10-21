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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tenants.Strategies;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class DynamicByHostTenantStrategyTests : IDisposable
{
    private const string TenantId = "acme";
    private const string IssuerHost = "acme.auth.example";
    private const string AliasHost = "acme.mtls.example";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }

    public DynamicByHostTenantStrategyTests()
    {
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    private DynamicByHostTenantStrategy CreateStrategy(
        DynamicByHostTenantOptions? options = null
    ) =>
        new(
            MockRepository.Create<IStoreManagerFactory>().Object,
            Options.Create(new TenantResolutionOptions { DynamicByHost = options })
        );

    private void SetupIssuerHostHolder(string? holderTenantId)
    {
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.ListTenantIdsByHostAsync(IssuerHost, CancellationToken.None))
            .ReturnsAsync(holderTenantId is null ? [] : [holderTenantId])
            .Verifiable();
    }

    private void SetupHostHolder(string host, string? holderTenantId)
    {
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockTenantStore
            .Setup(x => x.ListTenantIdsByHostAsync(host, CancellationToken.None))
            .ReturnsAsync(holderTenantId is null ? [] : [holderTenantId])
            .Verifiable();
    }

    private static PersistedTenant CreatePersistedTenant(string tenantId) =>
        new()
        {
            TenantId = tenantId,
            ConcurrencyToken = "ct",
            Issuer = $"https://{IssuerHost}",
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                ConcurrencyToken = "ct",
                Value = JsonSerializer.SerializeToElement(new { }),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = "ct",
                Value = [],
            },
        };

    #region TryGetTenantId Tests

    [Fact]
    public void TryGetTenantId_AlwaysReturnsFalse_BecauseDomainMappingRequiresStore()
    {
        var strategy = CreateStrategy();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("tenant-1.example.com");

        var result = strategy.TryGetTenantId(httpContext, out var tenantId);

        Assert.False(result);
        Assert.Null(tenantId);
    }

    #endregion

    #region ValidateTenantAsync Tests

    [Fact]
    public async Task ValidateTenantAsync_WhenIssuerIsAbsent_Fails()
    {
        // No request-derived fallback reaches a host tenant: only the host of its issuer routes to it.
        var failures = await CreateStrategy()
            .ValidateTenantAsync(
                TenantId,
                issuer: null,
                null,
                MockStoreManager.Object,
                CancellationToken.None
            );

        var failure = Assert.Single(failures);
        Assert.False(failure.IsConflict);
        Assert.Contains("requires an issuer", failure.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenIssuerHostIsFree_Succeeds()
    {
        SetupIssuerHostHolder(holderTenantId: null);

        var failures = await CreateStrategy()
            .ValidateTenantAsync(
                TenantId,
                new Uri($"https://{IssuerHost}"),
                null,
                MockStoreManager.Object,
                CancellationToken.None
            );

        Assert.Empty(failures);
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenTheTenantItselfHoldsTheIssuerHost_Succeeds()
    {
        SetupIssuerHostHolder(TenantId);

        var failures = await CreateStrategy()
            .ValidateTenantAsync(
                TenantId,
                new Uri($"https://{IssuerHost}"),
                null,
                MockStoreManager.Object,
                CancellationToken.None
            );

        Assert.Empty(failures);
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenAnotherTenantHoldsTheIssuerHost_ReportsAConflict()
    {
        SetupIssuerHostHolder("globex");

        var failures = await CreateStrategy()
            .ValidateTenantAsync(
                TenantId,
                new Uri($"https://{IssuerHost}/other"),
                null,
                MockStoreManager.Object,
                CancellationToken.None
            );

        var failure = Assert.Single(failures);
        Assert.True(failure.IsConflict);
        Assert.Contains($"'{IssuerHost}'", failure.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenHostPatternMapsTheIssuerHostElsewhere_Fails()
    {
        SetupIssuerHostHolder(holderTenantId: null);

        var failures = await CreateStrategy(
                new DynamicByHostTenantOptions { RegexPattern = "^[^.]+" }
            )
            .ValidateTenantAsync(
                TenantId,
                new Uri($"https://{IssuerHost}"),
                null,
                MockStoreManager.Object,
                CancellationToken.None
            );

        var failure = Assert.Single(failures);
        Assert.False(failure.IsConflict);
        Assert.Contains("maps it to 'acme'", failure.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenIssuerDoesNotEndWithTheTenantPath_Fails()
    {
        SetupIssuerHostHolder(holderTenantId: null);

        var failures = await CreateStrategy(
                new DynamicByHostTenantOptions { TenantPath = "/identity" }
            )
            .ValidateTenantAsync(
                TenantId,
                new Uri($"https://{IssuerHost}"),
                null,
                MockStoreManager.Object,
                CancellationToken.None
            );

        var failure = Assert.Single(failures);
        Assert.Contains("must end with '/identity'", failure.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenAliasHostIsFree_Succeeds()
    {
        SetupIssuerHostHolder(holderTenantId: null);
        SetupHostHolder(AliasHost, holderTenantId: null);

        var failures = await CreateStrategy()
            .ValidateTenantAsync(
                TenantId,
                new Uri($"https://{IssuerHost}"),
                new Uri($"https://{AliasHost}"),
                MockStoreManager.Object,
                CancellationToken.None
            );

        Assert.Empty(failures);
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenAnotherTenantHoldsTheAliasHost_ReportsAConflict()
    {
        SetupIssuerHostHolder(holderTenantId: null);
        SetupHostHolder(AliasHost, "globex");

        var failures = await CreateStrategy()
            .ValidateTenantAsync(
                TenantId,
                new Uri($"https://{IssuerHost}"),
                new Uri($"https://{AliasHost}"),
                MockStoreManager.Object,
                CancellationToken.None
            );

        var failure = Assert.Single(failures);
        Assert.True(failure.IsConflict);
        Assert.Contains($"'{AliasHost}'", failure.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenTheTenantAndAnotherAlreadyShareTheAliasHost_ReportsAConflict()
    {
        SetupIssuerHostHolder(holderTenantId: null);
        MockTenantStore
            .Setup(x => x.ListTenantIdsByHostAsync(AliasHost, CancellationToken.None))
            .ReturnsAsync([TenantId, "globex"])
            .Verifiable();

        var failures = await CreateStrategy()
            .ValidateTenantAsync(
                TenantId,
                new Uri($"https://{IssuerHost}"),
                new Uri($"https://{AliasHost}"),
                MockStoreManager.Object,
                CancellationToken.None
            );

        var failure = Assert.Single(failures);
        Assert.True(failure.IsConflict);
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenAliasHostIsTheIssuerHost_Fails()
    {
        SetupIssuerHostHolder(holderTenantId: null);

        var failures = await CreateStrategy()
            .ValidateTenantAsync(
                TenantId,
                new Uri($"https://{IssuerHost}"),
                new Uri($"https://{IssuerHost.ToUpperInvariant()}"),
                MockStoreManager.Object,
                CancellationToken.None
            );

        var failure = Assert.Single(failures);
        Assert.False(failure.IsConflict);
        Assert.Contains(
            "must differ from the issuer host",
            failure.Detail,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenHostPatternMapsTheAliasHostElsewhere_Fails()
    {
        const string aliasHost = "acme.mtls.example.internal";
        SetupIssuerHostHolder(holderTenantId: null);
        SetupHostHolder(aliasHost, holderTenantId: null);

        var failures = await CreateStrategy(
                new DynamicByHostTenantOptions { RegexPattern = "^[^.]+\\.(auth|mtls)\\.example" }
            )
            .ValidateTenantAsync(
                TenantId,
                new Uri($"https://{IssuerHost}"),
                new Uri($"https://{aliasHost}"),
                MockStoreManager.Object,
                CancellationToken.None
            );

        var failure = Assert.Single(failures);
        Assert.False(failure.IsConflict);
        Assert.Contains(
            $"mTLS endpoint alias host '{aliasHost}' does not reach the tenant",
            failure.Detail,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region GetDefaultIssuerAsync Tests

    [Fact]
    public async Task GetDefaultIssuerAsync_WhenTemplateIsConfigured_ExpandsItForTheTenant()
    {
        var strategy = CreateStrategy(
            new DynamicByHostTenantOptions { IssuerTemplate = "https://{tenantId}.auth.example" }
        );

        var issuer = await strategy.GetDefaultIssuerAsync(TenantId, CancellationToken.None);

        Assert.Equal($"https://{IssuerHost}", issuer);
    }

    #endregion

    #region ResolveTenantAsync Tests

    [Fact]
    public async Task ResolveTenantAsync_WhenATenantHoldsTheHost_ReturnsIt()
    {
        var holder = CreatePersistedTenant(TenantId);
        var strategy = CreateResolvingStrategy(IssuerHost, holder);

        var result = await strategy.ResolveTenantAsync(
            CreateHttpContext(IssuerHost, provisioner: null),
            CancellationToken.None
        );

        Assert.Same(holder, result);
    }

    [Fact]
    public async Task ResolveTenantAsync_WhenNoTenantHoldsTheHost_ThrowsNotFound()
    {
        var strategy = CreateResolvingStrategy(IssuerHost, holder: null);

        // A missing workload tenant is never created on demand: only the management API creates one.
        await Assert.ThrowsAsync<HttpResultException>(async () =>
            await strategy.ResolveTenantAsync(
                CreateHttpContext(IssuerHost, provisioner: null),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task ResolveTenantAsync_WhenTheRootTenantsHostIsNotYetHeld_CreatesTheRootTenant()
    {
        const string rootHost = "auth.example";
        var root = CreatePersistedTenant(RootTenantOptions.DefaultTenantId);
        var provisioner = MockRepository.Create<ITenantProvisioner>();
        provisioner
            .Setup(x =>
                x.EnsureCreatedAsync(
                    RootTenantOptions.DefaultTenantId,
                    RootTenantOptions.DefaultDisplayName,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(root)
            .Verifiable();
        var strategy = CreateResolvingStrategy(
            rootHost,
            holder: null,
            rootIssuer: $"https://{rootHost}"
        );

        var result = await strategy.ResolveTenantAsync(
            CreateHttpContext(rootHost, provisioner.Object),
            CancellationToken.None
        );

        Assert.Same(root, result);
    }

    #endregion

    private DynamicByHostTenantStrategy CreateResolvingStrategy(
        string issuerHost,
        PersistedTenant? holder,
        string? rootIssuer = null
    )
    {
        var storeManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultByHostAsync(issuerHost, It.IsAny<CancellationToken>()))
            .ReturnsAsync(holder)
            .Verifiable();

        return new DynamicByHostTenantStrategy(
            storeManagerFactory.Object,
            Options.Create(
                new TenantResolutionOptions
                {
                    Root = { Issuer = rootIssuer },
                    DynamicByHost = new DynamicByHostTenantOptions
                    {
                        IssuerTemplate = "https://{tenantId}.auth.example",
                    },
                }
            )
        );
    }

    private static DefaultHttpContext CreateHttpContext(
        string host,
        ITenantProvisioner? provisioner
    )
    {
        var services = new ServiceCollection();
        if (provisioner is not null)
            services.AddSingleton(provisioner);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
        httpContext.Request.Host = new HostString(host);
        return httpContext;
    }
}
