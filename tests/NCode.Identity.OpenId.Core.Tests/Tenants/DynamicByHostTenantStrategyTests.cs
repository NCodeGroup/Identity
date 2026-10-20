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
using Microsoft.Extensions.Options;
using Moq;
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
            .Setup(x => x.GetOrDefaultByIssuerHostAsync(IssuerHost, CancellationToken.None))
            .ReturnsAsync(holderTenantId is null ? null : CreatePersistedTenant(holderTenantId))
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
                MockStoreManager.Object,
                CancellationToken.None
            );

        var failure = Assert.Single(failures);
        Assert.Contains("must end with '/identity'", failure.Detail, StringComparison.Ordinal);
    }

    #endregion
}
