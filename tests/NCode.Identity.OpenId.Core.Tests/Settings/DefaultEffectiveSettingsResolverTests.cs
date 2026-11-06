// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class DefaultEffectiveSettingsResolverTests : BaseTests
{
    private Mock<OpenIdEnvironment> MockEnvironment { get; }
    private Mock<IOpenIdEnvironmentProvider> MockEnvironmentProvider { get; }
    private Mock<IOpenIdPlatformProvider> MockPlatformProvider { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<ITenantEnvironmentCatalog> MockTenantEnvironmentCatalog { get; }
    private Mock<ISecurityProfileCatalog> MockSecurityProfileCatalog { get; }
    private DefaultEffectiveSettingsResolver Resolver { get; }

    public DefaultEffectiveSettingsResolverTests()
    {
        MockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        MockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        MockPlatformProvider = CreateStrictMock<IOpenIdPlatformProvider>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        MockTenantEnvironmentCatalog = CreateStrictMock<ITenantEnvironmentCatalog>();
        MockSecurityProfileCatalog = CreateStrictMock<ISecurityProfileCatalog>();

        Resolver = new DefaultEffectiveSettingsResolver(
            MockEnvironmentProvider.Object,
            MockPlatformProvider.Object,
            MockStoreManagerFactory.Object,
            MockSettingSerializer.Object,
            MockTenantEnvironmentCatalog.Object,
            MockSecurityProfileCatalog.Object
        );
    }

    #region ReadScopeSettings Tests

    [Fact]
    public void ReadScopeSettings_ExpandsTheDocumentWithTheEnvironmentsJsonOptions()
    {
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> expanded = new List<Setting>();
        SetupExpand(OpenIdSettingScopes.Client, "foo", jsonOptions, expanded);

        var parent = CreateStrictMock<IReadOnlySettingCollection>().Object;

        var result = Resolver.ReadScopeSettings(
            parent,
            OpenIdSettingScopes.Client,
            CreateDocument("foo")
        );

        Assert.Same(expanded, result);
    }

    [Fact]
    public void ReadScopeSettings_PassesTheParentToTheEnvironmentCatalog()
    {
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> expanded = new List<Setting>();
        var parent = CreateStrictMock<IReadOnlySettingCollection>().Object;
        SetupExpand(OpenIdSettingScopes.Client, "foo", jsonOptions, expanded, parent);

        var result = Resolver.ReadScopeSettings(
            parent,
            OpenIdSettingScopes.Client,
            CreateDocument("foo")
        );

        Assert.Same(expanded, result);
    }

    #endregion

    #region Resolve Tests

    [Fact]
    public void Resolve_MergesTheScopeSettingsOntoTheParent()
    {
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> expanded = new List<Setting>();
        SetupExpand(OpenIdSettingScopes.Tenant, "foo", jsonOptions, expanded);
        var effective = CreateLooseMock<ISettingCollection>().Object;
        var mockParent = CreateStrictMock<IReadOnlySettingCollection>();
        mockParent.Setup(x => x.Merge(expanded)).Returns(effective).Verifiable();

        var result = Resolver.Resolve(
            mockParent.Object,
            OpenIdSettingScopes.Tenant,
            CreateDocument("foo")
        );

        Assert.Same(effective, result);
    }

    #endregion

    #region ResolveStored Tests

    [Fact]
    public void ResolveStored_MergesTheScopeSettingsOntoTheParent()
    {
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> expanded = new List<Setting>();
        SetupExpand(OpenIdSettingScopes.Tenant, "foo", jsonOptions, expanded);
        var effective = CreateLooseMock<ISettingCollection>().Object;
        var mockParent = CreateStrictMock<IReadOnlySettingCollection>();
        mockParent.Setup(x => x.Merge(expanded)).Returns(effective).Verifiable();

        var result = Resolver.ResolveStored(
            mockParent.Object,
            OpenIdSettingScopes.Tenant,
            "tenant-1",
            CreateDocument("foo")
        );

        Assert.Same(effective, result);
    }

    [Fact]
    public void ResolveStored_WhenTheDocumentCannotBeHonored_ThrowsNamingTheOwner()
    {
        const string documentError =
            "The settings are invalid: 'it_unregistered_setting' is not a registered setting.";
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        var cause = new JsonException(documentError);
        SetupExpandThrows(OpenIdSettingScopes.Tenant, "foo", jsonOptions, cause);
        var parent = CreateStrictMock<IReadOnlySettingCollection>().Object;

        var exception = Assert.Throws<UnreadableSettingsException>(() =>
            Resolver.ResolveStored(
                parent,
                OpenIdSettingScopes.Tenant,
                "tenant-1",
                CreateDocument("foo")
            )
        );

        Assert.Equal(OpenIdSettingScopes.Tenant, exception.ScopeName);
        Assert.Equal("tenant-1", exception.OwnerId);
        Assert.Equal(documentError, exception.DocumentError);
        Assert.Same(cause, exception.InnerException);
        Assert.Equal(
            "The stored settings document of tenant 'tenant-1' cannot be honored: " + documentError,
            exception.Message
        );
    }

    #endregion

    #region ResolvePlatformAsync Tests

    [Fact]
    public async Task ResolvePlatformAsync_WithADocument_MergesItOntoTheConfiguredSettings()
    {
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> expanded = new List<Setting>();
        SetupExpand(OpenIdSettingScopes.Platform, "proposed", jsonOptions, expanded);
        var effective = CreateLooseMock<ISettingCollection>().Object;
        var mockConfigured = CreateStrictMock<IReadOnlySettingCollection>();
        mockConfigured.Setup(x => x.Merge(expanded)).Returns(effective).Verifiable();
        SetupPlatform(mockConfigured.Object);

        var result = await Resolver.ResolvePlatformAsync(
            CreateDocument("proposed"),
            CancellationToken.None
        );

        Assert.Same(effective, result);
    }

    [Fact]
    public async Task ResolvePlatformAsync_ReadsTheCommittedDocumentFromTheStore()
    {
        // The platform's polled settings provider is strict with no setup: reading it fails the test.
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> expanded = new List<Setting>();
        SetupExpand(OpenIdSettingScopes.Platform, "committed", jsonOptions, expanded);
        var effective = CreateLooseMock<ISettingCollection>().Object;
        var mockConfigured = CreateStrictMock<IReadOnlySettingCollection>();
        mockConfigured.Setup(x => x.Merge(expanded)).Returns(effective).Verifiable();
        SetupPlatform(mockConfigured.Object);
        var mockPlatformStore = SetupPlatformStore();
        mockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PersistedPlatformSettings
                {
                    ConcurrencyToken = "token",
                    Value = CreateDocument("committed"),
                }
            )
            .Verifiable();

        var result = await Resolver.ResolvePlatformAsync(CancellationToken.None);

        Assert.Same(effective, result);
    }

    [Fact]
    public async Task ResolvePlatformAsync_WhenTheCommittedDocumentCannotBeHonored_ThrowsNamingThePlatform()
    {
        // The platform's polled settings provider is strict with no setup, and so is its Merge: neither is reached.
        const string documentError = "boom";
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        SetupExpandThrows(
            OpenIdSettingScopes.Platform,
            "committed",
            jsonOptions,
            new JsonException(documentError)
        );
        var configured = CreateStrictMock<IReadOnlySettingCollection>().Object;
        SetupPlatform(configured);
        var mockPlatformStore = SetupPlatformStore();
        mockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PersistedPlatformSettings
                {
                    ConcurrencyToken = "token",
                    Value = CreateDocument("committed"),
                }
            )
            .Verifiable();

        var exception = await Assert.ThrowsAsync<UnreadableSettingsException>(async () =>
            await Resolver.ResolvePlatformAsync(CancellationToken.None)
        );

        Assert.Equal(OpenIdSettingScopes.Platform, exception.ScopeName);
        Assert.Equal(OpenIdConstants.PlatformResourceId, exception.OwnerId);
        Assert.Equal(documentError, exception.DocumentError);
    }

    [Fact]
    public async Task ResolvePlatformAsync_WhenTheStoreHoldsNoSettings_ReturnsTheConfiguredSettings()
    {
        // The catalog is strict with no setup: expanding a document fails the test.
        SetupEnvironment();
        var configured = CreateStrictMock<IReadOnlySettingCollection>().Object;
        SetupPlatform(configured);
        var mockPlatformStore = SetupPlatformStore();
        mockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();

        var result = await Resolver.ResolvePlatformAsync(CancellationToken.None);

        Assert.Same(configured, result);
    }

    #endregion

    #region ResolveTeamAsync Tests

    [Fact]
    public async Task ResolveTeamAsync_MergesTheCommittedTeamDocumentOntoThePlatform()
    {
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> expanded = new List<Setting>();
        SetupExpand(OpenIdSettingScopes.Team, "committed", jsonOptions, expanded);
        var effective = CreateLooseMock<ISettingCollection>().Object;
        var mockPlatform = CreateStrictMock<IReadOnlySettingCollection>();
        mockPlatform.Setup(x => x.Merge(expanded)).Returns(effective).Verifiable();
        SetupPlatform(mockPlatform.Object);
        var (mockPlatformStore, mockTeamStore) = SetupPlatformAndTeamStores();
        mockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();
        mockTeamStore
            .Setup(x => x.GetLineageAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateTeam("team-1", "committed")])
            .Verifiable();

        var result = await Resolver.ResolveTeamAsync("team-1", CancellationToken.None);

        Assert.Same(effective, result);
    }

    [Fact]
    public async Task ResolveTeamAsync_WhenTheTeamDocumentCannotBeHonored_ThrowsNamingTheTeam()
    {
        const string documentError = "boom";
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        SetupExpandThrows(
            OpenIdSettingScopes.Team,
            "committed",
            jsonOptions,
            new JsonException(documentError)
        );
        SetupPlatform(CreateStrictMock<IReadOnlySettingCollection>().Object);
        var (mockPlatformStore, mockTeamStore) = SetupPlatformAndTeamStores();
        mockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();
        mockTeamStore
            .Setup(x => x.GetLineageAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateTeam("team-1", "committed")])
            .Verifiable();

        var exception = await Assert.ThrowsAsync<UnreadableSettingsException>(async () =>
            await Resolver.ResolveTeamAsync("team-1", CancellationToken.None)
        );

        Assert.Equal(OpenIdSettingScopes.Team, exception.ScopeName);
        Assert.Equal("team-1", exception.OwnerId);
        Assert.Equal(documentError, exception.DocumentError);
    }

    [Fact]
    public async Task ResolveTeamAsync_WhenTheTeamIsMissing_Throws()
    {
        SetupEnvironment();
        SetupPlatform(CreateStrictMock<IReadOnlySettingCollection>().Object);
        var (mockPlatformStore, mockTeamStore) = SetupPlatformAndTeamStores();
        mockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();
        mockTeamStore
            .Setup(x => x.GetLineageAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .Verifiable();

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await Resolver.ResolveTeamAsync("team-1", CancellationToken.None)
        );

        Assert.Equal("The team 'team-1' does not exist.", exception.Message);
    }

    [Fact]
    public async Task ResolveTeamAsync_WithADocument_MergesItOntoTheCommittedPlatform()
    {
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> expanded = new List<Setting>();
        SetupExpand(OpenIdSettingScopes.Team, "proposed", jsonOptions, expanded);
        var effective = CreateLooseMock<ISettingCollection>().Object;
        var mockPlatform = CreateStrictMock<IReadOnlySettingCollection>();
        mockPlatform.Setup(x => x.Merge(expanded)).Returns(effective).Verifiable();
        SetupPlatform(mockPlatform.Object);
        var (mockPlatformStore, mockTeamStore) = SetupPlatformAndTeamStores();
        mockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();
        mockTeamStore
            .Setup(x => x.GetLineageAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateTeam("team-1", "committed")])
            .Verifiable();

        var result = await Resolver.ResolveTeamAsync(
            "team-1",
            CreateDocument("proposed"),
            CancellationToken.None
        );

        Assert.Same(effective, result);
    }

    [Fact]
    public async Task ResolveTeamAsync_WithAParentTeam_MergesTheParentsDocumentFirst()
    {
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> parentExpanded = new List<Setting>();
        IReadOnlyCollection<Setting> childExpanded = new List<Setting>();
        SetupExpand(OpenIdSettingScopes.Team, "parent-value", jsonOptions, parentExpanded);
        SetupExpand(OpenIdSettingScopes.Team, "child-value", jsonOptions, childExpanded);
        var effective = CreateLooseMock<ISettingCollection>().Object;
        var mockParentEffective = CreateStrictMock<ISettingCollection>();
        mockParentEffective.Setup(x => x.Merge(childExpanded)).Returns(effective).Verifiable();
        var mockPlatform = CreateStrictMock<IReadOnlySettingCollection>();
        mockPlatform
            .Setup(x => x.Merge(parentExpanded))
            .Returns(mockParentEffective.Object)
            .Verifiable();
        SetupPlatform(mockPlatform.Object);
        var (mockPlatformStore, mockTeamStore) = SetupPlatformAndTeamStores();
        mockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();
        mockTeamStore
            .Setup(x => x.GetLineageAsync("child", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateTeam("parent", "parent-value"),
                CreateTeam("child", "child-value", "parent"),
            ])
            .Verifiable();

        var result = await Resolver.ResolveTeamAsync("child", CancellationToken.None);

        Assert.Same(effective, result);
    }

    [Fact]
    public async Task ResolveTeamAsync_WithADocumentForAChildTeam_MergesItOntoTheParentsEffectiveSettings()
    {
        // The child team's committed document is not set up: reading it fails the test.
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> parentExpanded = new List<Setting>();
        IReadOnlyCollection<Setting> proposedExpanded = new List<Setting>();
        var effective = CreateLooseMock<ISettingCollection>().Object;
        var mockParentEffective = CreateStrictMock<ISettingCollection>();
        mockParentEffective.Setup(x => x.Merge(proposedExpanded)).Returns(effective).Verifiable();
        SetupExpand(OpenIdSettingScopes.Team, "parent-value", jsonOptions, parentExpanded);
        SetupExpand(
            OpenIdSettingScopes.Team,
            "proposed",
            jsonOptions,
            proposedExpanded,
            mockParentEffective.Object
        );
        var mockPlatform = CreateStrictMock<IReadOnlySettingCollection>();
        mockPlatform
            .Setup(x => x.Merge(parentExpanded))
            .Returns(mockParentEffective.Object)
            .Verifiable();
        SetupPlatform(mockPlatform.Object);
        var (mockPlatformStore, mockTeamStore) = SetupPlatformAndTeamStores();
        mockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();
        mockTeamStore
            .Setup(x => x.GetLineageAsync("child", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateTeam("parent", "parent-value"),
                CreateTeam("child", "child-value", "parent"),
            ])
            .Verifiable();

        var result = await Resolver.ResolveTeamAsync(
            "child",
            CreateDocument("proposed"),
            CancellationToken.None
        );

        Assert.Same(effective, result);
    }

    #endregion

    #region ResolveTeamParentAsync Tests

    [Fact]
    public async Task ResolveTeamParentAsync_ForATopLevelTeam_ReturnsThePlatformsSettings()
    {
        // The catalog is strict with no setup: expanding the team's own document fails the test.
        SetupEnvironment();
        var platform = CreateStrictMock<IReadOnlySettingCollection>().Object;
        SetupPlatform(platform);
        var (mockPlatformStore, mockTeamStore) = SetupPlatformAndTeamStores();
        mockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();
        mockTeamStore
            .Setup(x => x.GetLineageAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateTeam("team-1", "committed")])
            .Verifiable();

        var result = await Resolver.ResolveTeamParentAsync("team-1", CancellationToken.None);

        Assert.Same(platform, result);
    }

    [Fact]
    public async Task ResolveTeamParentAsync_ForAChildTeam_ReturnsTheParentsEffectiveSettings()
    {
        // The child team's committed document is not set up: reading it fails the test.
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> parentExpanded = new List<Setting>();
        SetupExpand(OpenIdSettingScopes.Team, "parent-value", jsonOptions, parentExpanded);
        var parentEffective = CreateStrictMock<ISettingCollection>().Object;
        var mockPlatform = CreateStrictMock<IReadOnlySettingCollection>();
        mockPlatform.Setup(x => x.Merge(parentExpanded)).Returns(parentEffective).Verifiable();
        SetupPlatform(mockPlatform.Object);
        var (mockPlatformStore, mockTeamStore) = SetupPlatformAndTeamStores();
        mockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();
        mockTeamStore
            .Setup(x => x.GetLineageAsync("child", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateTeam("parent", "parent-value"),
                CreateTeam("child", "child-value", "parent"),
            ])
            .Verifiable();

        var result = await Resolver.ResolveTeamParentAsync("child", CancellationToken.None);

        Assert.Same(parentEffective, result);
    }

    [Fact]
    public async Task ResolveTeamParentAsync_WhenTheTeamIsMissing_Throws()
    {
        SetupEnvironment();
        SetupPlatform(CreateStrictMock<IReadOnlySettingCollection>().Object);
        var (mockPlatformStore, mockTeamStore) = SetupPlatformAndTeamStores();
        mockPlatformStore
            .Setup(x => x.GetSettingsOrDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedPlatformSettings?)null)
            .Verifiable();
        mockTeamStore
            .Setup(x => x.GetLineageAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .Verifiable();

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await Resolver.ResolveTeamParentAsync("team-1", CancellationToken.None)
        );

        Assert.Equal("The team 'team-1' does not exist.", exception.Message);
    }

    #endregion

    #region Helpers

    private static PersistedTeam CreateTeam(
        string teamId,
        string propertyName,
        string? parentTeamId = null
    ) =>
        new()
        {
            TeamId = teamId,
            ConcurrencyToken = "token",
            DisplayName = teamId,
            IsDisabled = false,
            ParentTeamId = parentTeamId,
            Settings = CreateTeamSettings(teamId, propertyName),
        };

    private static PersistedTeamSettings CreateTeamSettings(string teamId, string propertyName) =>
        new()
        {
            TeamId = teamId,
            ConcurrencyToken = "token",
            Value = CreateDocument(propertyName),
        };

    private (Mock<IPlatformStore> Platform, Mock<ITeamStore> Team) SetupPlatformAndTeamStores()
    {
        var mockPlatformStore = CreateStrictMock<IPlatformStore>();
        var mockTeamStore = CreateStrictMock<ITeamStore>();
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager
            .Setup(x => x.GetStore<IPlatformStore>())
            .Returns(mockPlatformStore.Object)
            .Verifiable();
        mockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(mockTeamStore.Object)
            .Verifiable();
        mockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStoreManager.Object)
            .Verifiable();

        return (mockPlatformStore, mockTeamStore);
    }

    private static JsonElement CreateDocument(string propertyName) =>
        JsonSerializer.SerializeToElement(
            new Dictionary<string, string> { [propertyName] = "value" }
        );

    private static bool HasProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out _);

    private void SetupEnvironment(JsonSerializerOptions? jsonOptions = null)
    {
        MockEnvironmentProvider.Setup(x => x.Get()).Returns(MockEnvironment.Object).Verifiable();

        if (jsonOptions is not null)
        {
            MockEnvironment
                .SetupGet(x => x.JsonSerializerOptions)
                .Returns(jsonOptions)
                .Verifiable();
        }
    }

    private void SetupExpand(
        string scopeName,
        string propertyName,
        JsonSerializerOptions jsonOptions,
        IReadOnlyCollection<Setting> expanded,
        IReadOnlySettingCollection? parentSettings = null
    )
    {
        // Each list holds a distinct setting so that Moq, which matches collection arguments by their elements,
        // tells the expansions of different documents apart.
        IReadOnlyCollection<Setting> deserialized = new List<Setting>
        {
            CreateMarkerSetting(scopeName, propertyName),
        };
        IReadOnlyCollection<Setting> selected = new List<Setting>
        {
            CreateMarkerSetting(scopeName, propertyName),
        };

        MockSettingSerializer
            .Setup(x =>
                x.DeserializeSettings(
                    It.Is<JsonElement>(e => HasProperty(e, propertyName)),
                    scopeName,
                    jsonOptions
                )
            )
            .Returns(deserialized)
            .Verifiable();

        MockTenantEnvironmentCatalog
            .Setup(x =>
                x.SelectImpliedProfiles(
                    It.Is<IReadOnlySettingCollection>(p =>
                        parentSettings == null || ReferenceEquals(p, parentSettings)
                    ),
                    scopeName,
                    deserialized
                )
            )
            .Returns(selected)
            .Verifiable();

        MockSecurityProfileCatalog
            .Setup(x => x.Expand(selected, scopeName))
            .Returns(expanded)
            .Verifiable();
    }

    private static Setting CreateMarkerSetting(string scopeName, string propertyName) =>
        new Setting<bool>(
            new SettingDescriptor<bool> { Name = propertyName, AppliesTo = [scopeName] },
            true
        );

    private void SetupExpandThrows(
        string scopeName,
        string propertyName,
        JsonSerializerOptions jsonOptions,
        JsonException exception
    ) =>
        MockSettingSerializer
            .Setup(x =>
                x.DeserializeSettings(
                    It.Is<JsonElement>(e => HasProperty(e, propertyName)),
                    scopeName,
                    jsonOptions
                )
            )
            .Throws(exception)
            .Verifiable();

    private Mock<OpenIdPlatform> SetupPlatform(IReadOnlySettingCollection configuredSettings)
    {
        var mockSettingsProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        mockSettingsProvider.SetupGet(x => x.Collection).Returns(configuredSettings).Verifiable();

        var mockPlatform = CreateStrictMock<OpenIdPlatform>();
        mockPlatform
            .SetupGet(x => x.ConfiguredSettingsProvider)
            .Returns(mockSettingsProvider.Object)
            .Verifiable();

        MockPlatformProvider
            .Setup(x => x.GetAsync(MockEnvironment.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPlatform.Object)
            .Verifiable();

        return mockPlatform;
    }

    private Mock<IPlatformStore> SetupPlatformStore()
    {
        var mockPlatformStore = CreateStrictMock<IPlatformStore>();
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager
            .Setup(x => x.GetStore<IPlatformStore>())
            .Returns(mockPlatformStore.Object)
            .Verifiable();
        mockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStoreManager.Object)
            .Verifiable();

        return mockPlatformStore;
    }

    #endregion
}
