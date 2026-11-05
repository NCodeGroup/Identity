// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Auditing;

public sealed class DefaultManagementTenantEnvironmentResolverTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }

    public DefaultManagementTenantEnvironmentResolverTests()
    {
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTenantStore = MockRepository.Create<ITenantStore>();
        MockEffectiveSettingsResolver = MockRepository.Create<IEffectiveSettingsResolver>();
    }

    public void Dispose() => MockRepository.Verify();

    private DefaultManagementTenantEnvironmentResolver CreateResolver() =>
        new(
            MockStoreManagerFactory.Object,
            MockEffectiveSettingsResolver.Object,
            MockRepository
                .Create<ILogger<DefaultManagementTenantEnvironmentResolver>>(MockBehavior.Loose)
                .Object
        );

    private static PersistedTenant CreateTenant() =>
        new()
        {
            TenantId = TenantId,
            TeamId = "platform",
            Issuer = null,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = TenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = TenantId,
                Value = JsonElement.Parse("""{"environment_type":"staging"}"""),
            },
            Secrets = null!,
        };

    private void SetupStore(PersistedTenant? tenant)
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(CancellationToken.None))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockTenantStore
            .Setup(x => x.GetOrDefaultAsync(TenantId, CancellationToken.None))
            .ReturnsAsync(tenant)
            .Verifiable();
    }

    private void SetupResolution(PersistedTenant tenant, string environmentType)
    {
        var mockPlatformSettings = MockRepository.Create<IReadOnlySettingCollection>();
        var mockTenantSettings = MockRepository.Create<IReadOnlySettingCollection>();
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync("platform", CancellationToken.None))
            .ReturnsAsync(mockPlatformSettings.Object)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveStored(
                    mockPlatformSettings.Object,
                    OpenIdSettingScopes.Tenant,
                    TenantId,
                    It.Is<JsonElement>(element =>
                        element.GetRawText() == tenant.Settings.Value.GetRawText()
                    )
                )
            )
            .Returns(mockTenantSettings.Object)
            .Verifiable();
        mockTenantSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.EnvironmentType))
            .Returns(environmentType)
            .Verifiable();
    }

    #region GetEnvironmentTypeAsync Tests

    [Fact]
    public async Task GetEnvironmentTypeAsync_WhenTheTenantExists_ReturnsItsEffectiveTypeAndRemembersIt()
    {
        var tenant = CreateTenant();
        SetupStore(tenant);
        SetupResolution(tenant, "staging");
        var httpContext = new DefaultHttpContext();
        var resolver = CreateResolver();

        var first = await resolver.GetEnvironmentTypeAsync(
            httpContext,
            TenantId,
            CancellationToken.None
        );
        var second = await resolver.GetEnvironmentTypeAsync(
            httpContext,
            TenantId,
            CancellationToken.None
        );

        Assert.Equal("staging", first);
        Assert.Equal("staging", second);
        MockTenantStore.Verify(
            x => x.GetOrDefaultAsync(TenantId, CancellationToken.None),
            Times.Once()
        );
    }

    [Fact]
    public async Task GetEnvironmentTypeAsync_WhenTheTypeIsRemembered_DoesNotReadTheStore()
    {
        var httpContext = new DefaultHttpContext();
        ManagementTenantEnvironment.Remember(httpContext, TenantId, "development");

        var result = await CreateResolver()
            .GetEnvironmentTypeAsync(httpContext, TenantId, CancellationToken.None);

        Assert.Equal("development", result);
    }

    [Fact]
    public async Task GetEnvironmentTypeAsync_WhenTheTenantIsMissing_ReturnsNull()
    {
        SetupStore(null);

        var result = await CreateResolver()
            .GetEnvironmentTypeAsync(new DefaultHttpContext(), TenantId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetEnvironmentTypeAsync_WhenTheStoreFails_ReturnsNull()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("store down"))
            .Verifiable();

        var result = await CreateResolver()
            .GetEnvironmentTypeAsync(new DefaultHttpContext(), TenantId, CancellationToken.None);

        Assert.Null(result);
    }

    #endregion
}
