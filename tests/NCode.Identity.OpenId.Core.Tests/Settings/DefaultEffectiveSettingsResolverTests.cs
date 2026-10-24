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
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class DefaultEffectiveSettingsResolverTests : BaseTests
{
    private const string ServerId = "default";

    private Mock<OpenIdEnvironment> MockEnvironment { get; }
    private Mock<IOpenIdEnvironmentProvider> MockEnvironmentProvider { get; }
    private Mock<IOpenIdServerProvider> MockServerProvider { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<ISecurityProfileCatalog> MockSecurityProfileCatalog { get; }
    private DefaultEffectiveSettingsResolver Resolver { get; }

    public DefaultEffectiveSettingsResolverTests()
    {
        MockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        MockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        MockServerProvider = CreateStrictMock<IOpenIdServerProvider>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockSecurityProfileCatalog = CreateStrictMock<ISecurityProfileCatalog>();

        Resolver = new DefaultEffectiveSettingsResolver(
            MockEnvironmentProvider.Object,
            MockServerProvider.Object,
            MockStoreManagerFactory.Object,
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

        var result = Resolver.ReadScopeSettings(OpenIdSettingScopes.Client, CreateDocument("foo"));

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
            "The settings are invalid: 'it_retired_setting' is not a registered setting.";
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

    #region ResolveServerAsync Tests

    [Fact]
    public async Task ResolveServerAsync_WithADocument_MergesItOntoTheConfiguredSettings()
    {
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> expanded = new List<Setting>();
        SetupExpand(OpenIdSettingScopes.Server, "proposed", jsonOptions, expanded);
        var effective = CreateLooseMock<ISettingCollection>().Object;
        var mockConfigured = CreateStrictMock<IReadOnlySettingCollection>();
        mockConfigured.Setup(x => x.Merge(expanded)).Returns(effective).Verifiable();
        SetupServer(mockConfigured.Object);

        var result = await Resolver.ResolveServerAsync(
            CreateDocument("proposed"),
            CancellationToken.None
        );

        Assert.Same(effective, result);
    }

    [Fact]
    public async Task ResolveServerAsync_ReadsTheCommittedDocumentFromTheStore()
    {
        // The server's polled settings provider is strict with no setup: reading it fails the test.
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        IReadOnlyCollection<Setting> expanded = new List<Setting>();
        SetupExpand(OpenIdSettingScopes.Server, "committed", jsonOptions, expanded);
        var effective = CreateLooseMock<ISettingCollection>().Object;
        var mockConfigured = CreateStrictMock<IReadOnlySettingCollection>();
        mockConfigured.Setup(x => x.Merge(expanded)).Returns(effective).Verifiable();
        var mockServer = SetupServer(mockConfigured.Object);
        mockServer.SetupGet(x => x.ServerId).Returns(ServerId).Verifiable();
        var mockServerStore = SetupServerStore();
        mockServerStore
            .Setup(x => x.GetSettingsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PersistedServerSettings
                {
                    ServerId = ServerId,
                    ConcurrencyToken = "token",
                    Value = CreateDocument("committed"),
                }
            )
            .Verifiable();

        var result = await Resolver.ResolveServerAsync(CancellationToken.None);

        Assert.Same(effective, result);
    }

    [Fact]
    public async Task ResolveServerAsync_WhenTheCommittedDocumentCannotBeHonored_ThrowsNamingTheServer()
    {
        // The server's polled settings provider is strict with no setup, and so is its Merge: neither is reached.
        const string documentError = "boom";
        var jsonOptions = new JsonSerializerOptions();
        SetupEnvironment(jsonOptions);
        SetupExpandThrows(
            OpenIdSettingScopes.Server,
            "committed",
            jsonOptions,
            new JsonException(documentError)
        );
        var configured = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var mockServer = SetupServer(configured);
        mockServer.SetupGet(x => x.ServerId).Returns(ServerId).Verifiable();
        var mockServerStore = SetupServerStore();
        mockServerStore
            .Setup(x => x.GetSettingsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PersistedServerSettings
                {
                    ServerId = ServerId,
                    ConcurrencyToken = "token",
                    Value = CreateDocument("committed"),
                }
            )
            .Verifiable();

        var exception = await Assert.ThrowsAsync<UnreadableSettingsException>(async () =>
            await Resolver.ResolveServerAsync(CancellationToken.None)
        );

        Assert.Equal(OpenIdSettingScopes.Server, exception.ScopeName);
        Assert.Equal(ServerId, exception.OwnerId);
        Assert.Equal(documentError, exception.DocumentError);
    }

    [Fact]
    public async Task ResolveServerAsync_WhenTheStoreHoldsNoSettings_ReturnsTheConfiguredSettings()
    {
        // The catalog is strict with no setup: expanding a document fails the test.
        SetupEnvironment();
        var configured = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var mockServer = SetupServer(configured);
        mockServer.SetupGet(x => x.ServerId).Returns(ServerId).Verifiable();
        var mockServerStore = SetupServerStore();
        mockServerStore
            .Setup(x => x.GetSettingsOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedServerSettings?)null)
            .Verifiable();

        var result = await Resolver.ResolveServerAsync(CancellationToken.None);

        Assert.Same(configured, result);
    }

    #endregion

    #region Helpers

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
        IReadOnlyCollection<Setting> expanded
    ) =>
        MockSecurityProfileCatalog
            .Setup(x =>
                x.Expand(
                    It.Is<JsonElement>(e => HasProperty(e, propertyName)),
                    scopeName,
                    jsonOptions
                )
            )
            .Returns(expanded)
            .Verifiable();

    private void SetupExpandThrows(
        string scopeName,
        string propertyName,
        JsonSerializerOptions jsonOptions,
        JsonException exception
    ) =>
        MockSecurityProfileCatalog
            .Setup(x =>
                x.Expand(
                    It.Is<JsonElement>(e => HasProperty(e, propertyName)),
                    scopeName,
                    jsonOptions
                )
            )
            .Throws(exception)
            .Verifiable();

    private Mock<OpenIdServer> SetupServer(IReadOnlySettingCollection configuredSettings)
    {
        var mockSettingsProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        mockSettingsProvider.SetupGet(x => x.Collection).Returns(configuredSettings).Verifiable();

        var mockServer = CreateStrictMock<OpenIdServer>();
        mockServer
            .SetupGet(x => x.ConfiguredSettingsProvider)
            .Returns(mockSettingsProvider.Object)
            .Verifiable();

        MockServerProvider
            .Setup(x => x.GetAsync(MockEnvironment.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockServer.Object)
            .Verifiable();

        return mockServer;
    }

    private Mock<IServerStore> SetupServerStore()
    {
        var mockServerStore = CreateStrictMock<IServerStore>();
        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager
            .Setup(x => x.GetStore<IServerStore>())
            .Returns(mockServerStore.Object)
            .Verifiable();
        mockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStoreManager.Object)
            .Verifiable();

        return mockServerStore;
    }

    #endregion
}
