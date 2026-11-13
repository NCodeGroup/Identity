// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class RootSettingsCollectionDataSourceTests : BaseTests
{
    private const string SectionPath = "OpenId:Platform:Settings";

    private static IChangeToken NeverChanges { get; } =
        new CancellationChangeToken(CancellationToken.None);

    private int CreatedCollectionCount { get; set; }

    private RootSettingsCollectionDataSource CreateDataSource(
        IConfigurationRoot configuration,
        ILogger<RootSettingsCollectionDataSource> logger
    )
    {
        var mockCatalog = CreateStrictMock<ISecurityProfileCatalog>();
        mockCatalog
            .Setup(x =>
                x.Expand(
                    It.IsAny<IReadOnlyCollection<Setting>>(),
                    OpenIdSettingScopes.Platform,
                    It.IsAny<IReadOnlyCollection<Setting>>()
                )
            )
            .Returns(
                (IReadOnlyCollection<Setting> settings, string _, IReadOnlyCollection<Setting> _) =>
                    settings
            )
            .Verifiable();

        return CreateDataSource(configuration, logger, mockCatalog.Object);
    }

    private RootSettingsCollectionDataSource CreateDataSource(
        IConfigurationRoot configuration,
        ILogger<RootSettingsCollectionDataSource> logger,
        ISecurityProfileCatalog securityProfileCatalog
    ) =>
        CreateDataSource(
            configuration,
            logger,
            securityProfileCatalog,
            new TestSettingDescriptorCollection(
                new SettingDescriptor<bool> { Name = "flag", AppliesTo = ["client"] }
            ),
            [],
            () => CreateLooseMock<ISettingCollection>().Object
        );

    private RootSettingsCollectionDataSource CreateDataSource(
        IConfigurationRoot configuration,
        ILogger<RootSettingsCollectionDataSource> logger,
        ISecurityProfileCatalog securityProfileCatalog,
        ISettingDescriptorCollection descriptors,
        IEnumerable<IDefaultSettingsProvider> defaultSettingsProviders,
        Func<ISettingCollection> createCollection
    )
    {
        var options = new OpenIdOptions
        {
            Platform = { Settings = configuration.GetSection(SectionPath) },
        };

        var mockAlgorithmCollection = CreateStrictMock<IAlgorithmCollection>();
        mockAlgorithmCollection
            .Setup(x => x.GetEnumerator())
            .Returns(() => Enumerable.Empty<Algorithm>().GetEnumerator())
            .Verifiable();
        var mockAlgorithmProvider = CreateStrictMock<IAlgorithmCollectionProvider>();
        mockAlgorithmProvider
            .Setup(x => x.Collection)
            .Returns(mockAlgorithmCollection.Object)
            .Verifiable();
        mockAlgorithmProvider.Setup(x => x.GetChangeToken()).Returns(NeverChanges).Verifiable();

        var mockDescriptorProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
        mockDescriptorProvider.Setup(x => x.Collection).Returns(descriptors).Verifiable();
        mockDescriptorProvider.Setup(x => x.GetChangeToken()).Returns(NeverChanges).Verifiable();

        var mockFactory = CreateStrictMock<ISettingCollectionFactory>();
        mockFactory
            .Setup(x => x.Create())
            .Callback(() => CreatedCollectionCount++)
            .Returns(createCollection)
            .Verifiable();

        var mockScopeProvider = CreateStrictMock<ISettingScopeCollectionProvider>();
        mockScopeProvider
            .Setup(x => x.Collection)
            .Returns(TestSettingScopeCollection.OpenId)
            .Verifiable();

        return new RootSettingsCollectionDataSource(
            Options.Create(options),
            mockAlgorithmProvider.Object,
            mockDescriptorProvider.Object,
            mockFactory.Object,
            defaultSettingsProviders,
            new DefaultSettingConfigurationReader(
                mockDescriptorProvider.Object,
                mockScopeProvider.Object
            ),
            securityProfileCatalog,
            logger
        );
    }

    private static IConfigurationRoot CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { [$"{SectionPath}:flag"] = "true" }
            )
            .Build();

    #region Collection Property Tests

    [Fact]
    public void Collection_IsThePlatformSettingsExpandedWithTheProfilesTheySelect()
    {
        IReadOnlyCollection<Setting> expanded = [];
        IReadOnlyCollection<Setting>? capturedScope = null;
        IReadOnlyCollection<Setting>? capturedBaseline = null;
        var mockCatalog = CreateStrictMock<ISecurityProfileCatalog>();
        mockCatalog
            .Setup(x =>
                x.Expand(
                    It.IsAny<IReadOnlyCollection<Setting>>(),
                    OpenIdSettingScopes.Platform,
                    It.IsAny<IReadOnlyCollection<Setting>>()
                )
            )
            .Callback(
                (
                    IReadOnlyCollection<Setting> scope,
                    string _,
                    IReadOnlyCollection<Setting> baseline
                ) =>
                {
                    capturedScope = scope;
                    capturedBaseline = baseline;
                }
            )
            .Returns(expanded)
            .Verifiable();

        using var dataSource = CreateDataSource(
            CreateConfiguration(),
            CreateLooseMock<ILogger<RootSettingsCollectionDataSource>>().Object,
            mockCatalog.Object
        );

        Assert.Same(expanded, dataSource.Collection);
        Assert.NotNull(capturedScope);
        Assert.NotNull(capturedBaseline);
        Assert.NotSame(capturedBaseline, capturedScope);
    }

    #endregion

    #region Grant Types Enabled Tests

    [Fact]
    public void Collection_GrantTypesEnabledConfigured_AddsToBaseline()
    {
        var configuration = CreateGrantTypesConfiguration(
            enabled: [OpenIdProtocol.GrantTypes.ClientCredentials]
        );

        var (scope, baseline) = LoadGrantTypes(configuration);

        Assert.Equal(
            [
                OpenIdProtocol.GrantTypes.AuthorizationCode,
                OpenIdProtocol.GrantTypes.RefreshToken,
                OpenIdProtocol.GrantTypes.ClientCredentials,
            ],
            GetValues(baseline, OpenIdSettingNames.GrantTypesSupported)
        );
        Assert.Equal(
            [OpenIdProtocol.GrantTypes.ClientCredentials],
            GetValues(scope, OpenIdSettingNames.GrantTypesEnabled)
        );
        Assert.Empty(GetValues(scope, OpenIdSettingNames.GrantTypesSupported));
    }

    [Fact]
    public void Collection_GrantTypesSupportedAndEnabledConfigured_KeepsConfiguredList()
    {
        var configuration = CreateGrantTypesConfiguration(
            supported: [OpenIdProtocol.GrantTypes.AuthorizationCode],
            enabled: [OpenIdProtocol.GrantTypes.ClientCredentials]
        );

        var (scope, _) = LoadGrantTypes(configuration);

        Assert.Equal(
            [OpenIdProtocol.GrantTypes.AuthorizationCode],
            GetValues(scope, OpenIdSettingNames.GrantTypesSupported)
        );
    }

    [Fact]
    public void Collection_GrantTypesEnabledNotConfigured_LeavesTheBaseline()
    {
        var configuration = CreateGrantTypesConfiguration();

        var (scope, baseline) = LoadGrantTypes(configuration);

        Assert.Equal(
            [OpenIdProtocol.GrantTypes.AuthorizationCode, OpenIdProtocol.GrantTypes.RefreshToken],
            GetValues(baseline, OpenIdSettingNames.GrantTypesSupported)
        );
        Assert.Empty(GetValues(scope, OpenIdSettingNames.GrantTypesEnabled));
    }

    [Fact]
    public void Collection_GrantTypesEnabledUnknownGrant_IsRejected()
    {
        var configuration = CreateGrantTypesConfiguration(enabled: ["not_a_grant"]);
        var mockDescriptorProvider = CreateLooseMock<ISettingDescriptorCollectionProvider>();
        mockDescriptorProvider.Setup(x => x.Collection).Returns(CreateGrantTypesDescriptors());
        var mockScopeProvider = CreateLooseMock<ISettingScopeCollectionProvider>();
        mockScopeProvider.Setup(x => x.Collection).Returns(TestSettingScopeCollection.OpenId);
        var reader = new DefaultSettingConfigurationReader(
            mockDescriptorProvider.Object,
            mockScopeProvider.Object
        );

        var errors = reader.Validate(
            configuration.GetSection(SectionPath),
            OpenIdSettingScopes.Platform
        );

        var error = Assert.Single(errors);
        Assert.StartsWith($"'{SectionPath}:grant_types_enabled' ", error);
    }

    private static TestSettingDescriptorCollection CreateGrantTypesDescriptors() =>
        new(
            new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.GrantTypesSupported,
                AppliesTo = [OpenIdSettingScopes.Tenant, OpenIdSettingScopes.Client],
                Rule = SettingRules.Each(OpenIdSettingRules.GrantType()),
                OnMerge = SettingMerge.Intersect,
            },
            new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = OpenIdSettingNames.GrantTypesEnabled,
                AppliesTo = [OpenIdSettingScopes.Platform],
                Rule = SettingRules.Each(OpenIdSettingRules.GrantType()),
                OnMerge = SettingMerge.Union,
            }
        );

    private static IConfigurationRoot CreateGrantTypesConfiguration(
        string[]? supported = null,
        string[]? enabled = null
    )
    {
        var values = new Dictionary<string, string?>();
        for (var index = 0; index < (supported?.Length ?? 0); index++)
        {
            values[$"{SectionPath}:grant_types_supported:{index}"] = supported![index];
        }

        for (var index = 0; index < (enabled?.Length ?? 0); index++)
        {
            values[$"{SectionPath}:grant_types_enabled:{index}"] = enabled![index];
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private (
        IReadOnlyCollection<Setting> Scope,
        IReadOnlyCollection<Setting> Baseline
    ) LoadGrantTypes(IConfigurationRoot configuration)
    {
        IReadOnlyCollection<Setting>? capturedScope = null;
        IReadOnlyCollection<Setting>? capturedBaseline = null;
        var mockCatalog = CreateStrictMock<ISecurityProfileCatalog>();
        mockCatalog
            .Setup(x =>
                x.Expand(
                    It.IsAny<IReadOnlyCollection<Setting>>(),
                    OpenIdSettingScopes.Platform,
                    It.IsAny<IReadOnlyCollection<Setting>>()
                )
            )
            .Callback(
                (
                    IReadOnlyCollection<Setting> scope,
                    string _,
                    IReadOnlyCollection<Setting> baseline
                ) =>
                {
                    capturedScope = scope;
                    capturedBaseline = baseline;
                }
            )
            .Returns(
                (IReadOnlyCollection<Setting> scope, string _, IReadOnlyCollection<Setting> _) =>
                    scope
            )
            .Verifiable();

        var descriptors = CreateGrantTypesDescriptors();

        var mockDefaultSettingsProvider = CreateStrictMock<IDefaultSettingsProvider>();
        mockDefaultSettingsProvider
            .Setup(x => x.Configure(It.IsAny<ISettingCollection>()))
            .Callback(
                (ISettingCollection settings) =>
                    settings.Set(
                        OpenIdSettingKeys.GrantTypesSupported,
                        [
                            OpenIdProtocol.GrantTypes.AuthorizationCode,
                            OpenIdProtocol.GrantTypes.RefreshToken,
                        ]
                    )
            )
            .Verifiable();

        using var dataSource = CreateDataSource(
            configuration,
            CreateLooseMock<ILogger<RootSettingsCollectionDataSource>>().Object,
            mockCatalog.Object,
            descriptors,
            [mockDefaultSettingsProvider.Object],
            () => new FakeSettingCollection(descriptors)
        );

        Assert.NotNull(capturedScope);
        Assert.NotNull(capturedBaseline);
        return (capturedScope, capturedBaseline);
    }

    private static string[] GetValues(IReadOnlyCollection<Setting> settings, string settingName) =>
        settings
            .Where(setting => setting.Descriptor.Name == settingName)
            .Select(setting => ((IReadOnlyCollection<string>)setting.GetValue()).ToArray())
            .SingleOrDefault()
        ?? [];

    #endregion

    #region Reload Tests

    [Fact]
    public void Reload_WhenSettingsCanBeHonored_ReloadsTheCollection()
    {
        var configuration = CreateConfiguration();
        using var dataSource = CreateDataSource(
            configuration,
            CreateLooseMock<ILogger<RootSettingsCollectionDataSource>>().Object
        );
        var initialCollection = dataSource.Collection;
        var changeToken = dataSource.GetChangeToken();

        configuration[$"{SectionPath}:flag"] = "false";
        configuration.Reload();

        Assert.NotSame(initialCollection, dataSource.Collection);
        Assert.Equal(4, CreatedCollectionCount);
        Assert.True(changeToken.HasChanged);
    }

    [Theory]
    [InlineData("unregistered_setting", "https://op.example")]
    [InlineData("flag", "yes")]
    public void Reload_WhenSettingsCannotBeHonored_KeepsThePreviousSettingsAndLogsAnError(
        string name,
        string value
    )
    {
        var configuration = CreateConfiguration();
        var mockLogger = CreateStrictMock<ILogger<RootSettingsCollectionDataSource>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Error)).Returns(true).Verifiable();
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable();
        using var dataSource = CreateDataSource(configuration, mockLogger.Object);
        var initialCollection = dataSource.Collection;
        var changeToken = dataSource.GetChangeToken();

        configuration[$"{SectionPath}:{name}"] = value;
        configuration.Reload();

        Assert.Same(initialCollection, dataSource.Collection);
        Assert.Equal(2, CreatedCollectionCount);
        Assert.False(changeToken.HasChanged);
    }

    #endregion

    #region Scaffolding

    private sealed class FakeSettingCollection(ISettingDescriptorCollection descriptors)
        : ISettingCollection
    {
        private Dictionary<string, Setting> Settings { get; } = new(StringComparer.Ordinal);

        public int Count => Settings.Count;

        public IEnumerator<Setting> GetEnumerator() => Settings.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Set(Setting setting) => Settings[setting.Descriptor.Name] = setting;

        // A setting without a descriptor is not held, so the computed supported-algorithm values the data source sets
        // are ignored.
        public void Set<TValue>(SettingKey<TValue> key, TValue value)
            where TValue : notnull
        {
            if (descriptors.TryGet(key, out var descriptor))
            {
                Set(descriptor.Create(value));
            }
        }

        public bool TryGetValue<TValue>(
            SettingKey<TValue> key,
            [MaybeNullWhen(false)] out TValue value
        )
            where TValue : notnull
        {
            if (
                Settings.TryGetValue(key.SettingName, out var setting)
                && setting is Setting<TValue> typed
            )
            {
                value = typed.Value;
                return true;
            }

            value = default;
            return false;
        }

        public bool Remove<TValue>(SettingKey<TValue> key)
            where TValue : notnull => throw new NotSupportedException();

        public TValue GetValue<TValue>(SettingKey<TValue> key)
            where TValue : notnull => throw new NotSupportedException();

        public bool TryGet(string settingName, [MaybeNullWhen(false)] out Setting setting) =>
            throw new NotSupportedException();

        public bool TryGet<TValue>(
            SettingKey<TValue> key,
            [MaybeNullWhen(false)] out Setting<TValue> setting
        )
            where TValue : notnull => throw new NotSupportedException();

        public bool TryGetValue(string settingName, [MaybeNullWhen(false)] out object value) =>
            throw new NotSupportedException();

        public ISettingCollection Merge(IEnumerable<Setting> otherCollection) =>
            throw new NotSupportedException();

        public ISettingCollection Combine(IEnumerable<Setting> otherCollection) =>
            throw new NotSupportedException();
    }

    #endregion
}
