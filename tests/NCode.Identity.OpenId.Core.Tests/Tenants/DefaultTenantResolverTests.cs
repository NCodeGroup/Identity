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
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class DefaultTenantResolverTests
{
    private const string TenantId = "acme";
    private const string Issuer = "https://op.example/acme";

    #region TryGetTenantId Tests

    [Fact]
    public void TryGetTenantId_DelegatesToSelectedStrategy()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = mocks.Create<ITenantStrategy>();

        strategy
            .Setup(x => x.StrategyCode)
            .Returns(OpenIdConstants.TenantStrategyCodes.DynamicByPath)
            .Verifiable();

        var expectedTenantId = "tenant-1";
        strategy
            .Setup(x => x.TryGetTenantId(It.IsAny<HttpContext>(), out expectedTenantId))
            .Returns(true)
            .Verifiable();

        var options = Options.Create(
            new TenantResolutionOptions
            {
                StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByPath,
            }
        );

        var resolver = new DefaultTenantResolver(options, [strategy.Object]);

        var result = resolver.TryGetTenantId(new DefaultHttpContext(), out var tenantId);

        Assert.True(result);
        Assert.Equal("tenant-1", tenantId);
        mocks.Verify();
    }

    #endregion

    #region ValidateTenantAsync Tests

    private static DefaultTenantResolver CreateResolver(ITenantStrategy strategy) =>
        new(
            Options.Create(
                new TenantResolutionOptions
                {
                    StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByPath,
                }
            ),
            [strategy]
        );

    private static Mock<ITenantStrategy> CreateStrategy(MockRepository mocks)
    {
        var strategy = mocks.Create<ITenantStrategy>();
        strategy
            .Setup(x => x.StrategyCode)
            .Returns(OpenIdConstants.TenantStrategyCodes.DynamicByPath);
        return strategy;
    }

    private static Mock<ITenantStore> CreateStore(
        MockRepository mocks,
        Mock<IStoreManager> storeManager,
        string tenantId,
        bool isTenantIdRetired = false
    )
    {
        var store = mocks.Create<ITenantStore>();
        storeManager.Setup(x => x.GetStore<ITenantStore>()).Returns(store.Object).Verifiable();
        store
            .Setup(x => x.IsTenantIdRetiredAsync(tenantId, CancellationToken.None))
            .ReturnsAsync(isTenantIdRetired)
            .Verifiable();
        return store;
    }

    private static PersistedTenant CreatePersistedTenant(string tenantId) =>
        new()
        {
            TenantId = tenantId,
            ConcurrencyToken = "ct",
            Issuer = Issuer,
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

    [Fact]
    public async Task ValidateTenantAsync_WhenIdentifierAndIssuerAreInvalid_ReportsBothWithoutConsultingTheStrategy()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = mocks.Create<ITenantStrategy>();
        var storeManager = mocks.Create<IStoreManager>();

        // The strategy is never selected: the remaining rules are only meaningful for a well-formed address.
        var failures = await CreateResolver(strategy.Object)
            .ValidateTenantAsync(
                "Acme",
                "http://op.example",
                storeManager.Object,
                CancellationToken.None
            );

        Assert.Equal(2, failures.Count);
        Assert.All(failures, failure => Assert.False(failure.IsConflict));
        Assert.Contains("'Acme'", failures[0].Detail, StringComparison.Ordinal);
        Assert.Contains("'http://op.example'", failures[1].Detail, StringComparison.Ordinal);
        mocks.Verify();
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenIssuerIsAbsent_DelegatesToTheStrategyWithoutAnIssuerLookup()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks);
        var storeManager = mocks.Create<IStoreManager>();
        CreateStore(mocks, storeManager, TenantId);

        strategy
            .Setup(x =>
                x.ValidateTenantAsync(TenantId, null, storeManager.Object, CancellationToken.None)
            )
            .ReturnsAsync([])
            .Verifiable();

        var failures = await CreateResolver(strategy.Object)
            .ValidateTenantAsync(
                TenantId,
                string.Empty,
                storeManager.Object,
                CancellationToken.None
            );

        Assert.Empty(failures);
        mocks.Verify();
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenAnotherTenantHoldsTheIssuer_ReportsAConflictAndTheStrategysFailures()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks);
        var storeManager = mocks.Create<IStoreManager>();
        var store = CreateStore(mocks, storeManager, TenantId);
        store
            .Setup(x => x.GetOrDefaultByIssuerAsync(Issuer, CancellationToken.None))
            .ReturnsAsync(CreatePersistedTenant("globex"))
            .Verifiable();
        strategy
            .Setup(x =>
                x.ValidateTenantAsync(
                    TenantId,
                    It.Is<Uri>(uri => uri.OriginalString == Issuer),
                    storeManager.Object,
                    CancellationToken.None
                )
            )
            .ReturnsAsync([new TenantValidationFailure { Detail = "route" }])
            .Verifiable();

        var failures = await CreateResolver(strategy.Object)
            .ValidateTenantAsync(TenantId, Issuer, storeManager.Object, CancellationToken.None);

        Assert.Equal(2, failures.Count);
        Assert.True(failures[0].IsConflict);
        Assert.Contains(Issuer, failures[0].Detail, StringComparison.Ordinal);
        Assert.Equal("route", failures[1].Detail);
        mocks.Verify();
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenTheTenantItselfHoldsTheIssuer_Succeeds()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks);
        var storeManager = mocks.Create<IStoreManager>();
        var store = CreateStore(mocks, storeManager, TenantId);
        store
            .Setup(x => x.GetOrDefaultByIssuerAsync(Issuer, CancellationToken.None))
            .ReturnsAsync(CreatePersistedTenant(TenantId))
            .Verifiable();
        store
            .Setup(x => x.GetRetiredIssuerHolderOrDefaultAsync(Issuer, CancellationToken.None))
            .ReturnsAsync((string?)null)
            .Verifiable();
        strategy
            .Setup(x =>
                x.GetDefaultIssuerAsync(RootTenantOptions.DefaultTenantId, CancellationToken.None)
            )
            .ReturnsAsync("https://op.example/root")
            .Verifiable();
        strategy
            .Setup(x =>
                x.ValidateTenantAsync(
                    TenantId,
                    It.IsAny<Uri>(),
                    storeManager.Object,
                    CancellationToken.None
                )
            )
            .ReturnsAsync([])
            .Verifiable();

        var failures = await CreateResolver(strategy.Object)
            .ValidateTenantAsync(TenantId, Issuer, storeManager.Object, CancellationToken.None);

        Assert.Empty(failures);
        mocks.Verify();
    }

    [Theory]
    [InlineData("https://op.example/root")]
    [InlineData("HTTPS://OP.EXAMPLE/root")]
    public async Task ValidateTenantAsync_WhenIssuerIsTheRootTenantsAddress_ReportsAConflict(
        string issuer
    )
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks);
        var storeManager = mocks.Create<IStoreManager>();
        var store = CreateStore(mocks, storeManager, TenantId);
        store
            .Setup(x => x.GetOrDefaultByIssuerAsync(issuer, CancellationToken.None))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
        store
            .Setup(x => x.GetRetiredIssuerHolderOrDefaultAsync(issuer, CancellationToken.None))
            .ReturnsAsync((string?)null)
            .Verifiable();
        strategy
            .Setup(x =>
                x.GetDefaultIssuerAsync(RootTenantOptions.DefaultTenantId, CancellationToken.None)
            )
            .ReturnsAsync("https://op.example/root")
            .Verifiable();
        strategy
            .Setup(x =>
                x.ValidateTenantAsync(
                    TenantId,
                    It.IsAny<Uri>(),
                    storeManager.Object,
                    CancellationToken.None
                )
            )
            .ReturnsAsync([])
            .Verifiable();

        var failures = await CreateResolver(strategy.Object)
            .ValidateTenantAsync(TenantId, issuer, storeManager.Object, CancellationToken.None);

        var failure = Assert.Single(failures);
        Assert.True(failure.IsConflict);
        Assert.Contains("reserved for the root tenant", failure.Detail, StringComparison.Ordinal);
        mocks.Verify();
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenTheRootTenantTakesItsOwnAddress_Succeeds()
    {
        const string rootIssuer = "https://op.example/root";

        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks);
        var storeManager = mocks.Create<IStoreManager>();
        var store = CreateStore(mocks, storeManager, RootTenantOptions.DefaultTenantId);
        store
            .Setup(x => x.GetOrDefaultByIssuerAsync(rootIssuer, CancellationToken.None))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
        store
            .Setup(x => x.GetRetiredIssuerHolderOrDefaultAsync(rootIssuer, CancellationToken.None))
            .ReturnsAsync((string?)null)
            .Verifiable();
        strategy
            .Setup(x =>
                x.ValidateTenantAsync(
                    RootTenantOptions.DefaultTenantId,
                    It.IsAny<Uri>(),
                    storeManager.Object,
                    CancellationToken.None
                )
            )
            .ReturnsAsync([])
            .Verifiable();

        var failures = await CreateResolver(strategy.Object)
            .ValidateTenantAsync(
                RootTenantOptions.DefaultTenantId,
                rootIssuer,
                storeManager.Object,
                CancellationToken.None
            );

        Assert.Empty(failures);
        mocks.Verify();
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenTheIdentifierBelongedToADeletedTenant_ReportsAConflict()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks);
        var storeManager = mocks.Create<IStoreManager>();
        CreateStore(mocks, storeManager, TenantId, isTenantIdRetired: true);

        strategy
            .Setup(x =>
                x.ValidateTenantAsync(TenantId, null, storeManager.Object, CancellationToken.None)
            )
            .ReturnsAsync([])
            .Verifiable();

        var failures = await CreateResolver(strategy.Object)
            .ValidateTenantAsync(TenantId, null, storeManager.Object, CancellationToken.None);

        var failure = Assert.Single(failures);
        Assert.True(failure.IsConflict);
        Assert.Contains($"'{TenantId}'", failure.Detail, StringComparison.Ordinal);
        Assert.Contains("never reissued", failure.Detail, StringComparison.Ordinal);
        mocks.Verify();
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenAnotherTenantGaveUpTheIssuer_ReportsAConflict()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks);
        var storeManager = mocks.Create<IStoreManager>();
        var store = CreateStore(mocks, storeManager, TenantId);

        store
            .Setup(x => x.GetOrDefaultByIssuerAsync(Issuer, CancellationToken.None))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
        store
            .Setup(x => x.GetRetiredIssuerHolderOrDefaultAsync(Issuer, CancellationToken.None))
            .ReturnsAsync("globex")
            .Verifiable();
        strategy
            .Setup(x =>
                x.ValidateTenantAsync(
                    TenantId,
                    It.IsAny<Uri>(),
                    storeManager.Object,
                    CancellationToken.None
                )
            )
            .ReturnsAsync([])
            .Verifiable();

        var failures = await CreateResolver(strategy.Object)
            .ValidateTenantAsync(TenantId, Issuer, storeManager.Object, CancellationToken.None);

        var failure = Assert.Single(failures);
        Assert.True(failure.IsConflict);
        Assert.Contains(Issuer, failure.Detail, StringComparison.Ordinal);
        Assert.Contains("never reissued", failure.Detail, StringComparison.Ordinal);
        mocks.Verify();
    }

    [Fact]
    public async Task ValidateTenantAsync_WhenTheTenantItselfGaveUpTheIssuer_Succeeds()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks);
        var storeManager = mocks.Create<IStoreManager>();
        var store = CreateStore(mocks, storeManager, TenantId);

        store
            .Setup(x => x.GetOrDefaultByIssuerAsync(Issuer, CancellationToken.None))
            .ReturnsAsync((PersistedTenant?)null)
            .Verifiable();
        store
            .Setup(x => x.GetRetiredIssuerHolderOrDefaultAsync(Issuer, CancellationToken.None))
            .ReturnsAsync(TenantId.ToUpperInvariant())
            .Verifiable();
        strategy
            .Setup(x =>
                x.GetDefaultIssuerAsync(RootTenantOptions.DefaultTenantId, CancellationToken.None)
            )
            .ReturnsAsync("https://op.example/root")
            .Verifiable();
        strategy
            .Setup(x =>
                x.ValidateTenantAsync(
                    TenantId,
                    It.IsAny<Uri>(),
                    storeManager.Object,
                    CancellationToken.None
                )
            )
            .ReturnsAsync([])
            .Verifiable();

        var failures = await CreateResolver(strategy.Object)
            .ValidateTenantAsync(TenantId, Issuer, storeManager.Object, CancellationToken.None);

        Assert.Empty(failures);
        mocks.Verify();
    }

    #endregion

    #region GetDefaultIssuerAsync Tests

    [Fact]
    public async Task GetDefaultIssuerAsync_DelegatesToTheSelectedStrategy()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = CreateStrategy(mocks);
        strategy
            .Setup(x => x.GetDefaultIssuerAsync(TenantId, CancellationToken.None))
            .ReturnsAsync(Issuer)
            .Verifiable();

        var issuer = await CreateResolver(strategy.Object)
            .GetDefaultIssuerAsync(TenantId, CancellationToken.None);

        Assert.Equal(Issuer, issuer);
        mocks.Verify();
    }

    [Theory]
    [InlineData("Acme")]
    [InlineData("acme corp")]
    [InlineData("")]
    public async Task GetDefaultIssuerAsync_WhenIdentifierIsInvalid_ReturnsNoneWithoutTheStrategy(
        string tenantId
    )
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var strategy = mocks.Create<ITenantStrategy>();

        var issuer = await CreateResolver(strategy.Object)
            .GetDefaultIssuerAsync(tenantId, CancellationToken.None);

        Assert.Null(issuer);
        mocks.Verify();
    }

    #endregion
}
