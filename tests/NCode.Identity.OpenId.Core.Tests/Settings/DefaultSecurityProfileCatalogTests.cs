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
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class DefaultSecurityProfileCatalogTests : BaseTests
{
    private DefaultSecurityProfileCatalog CreateCatalog(params ISecurityProfile[] profiles)
    {
        var serializer = CreateStrictMock<ISettingSerializer>();
        var factory = CreateStrictMock<ISettingCollectionFactory>();
        return new DefaultSecurityProfileCatalog(profiles, serializer.Object, factory.Object);
    }

    private DefaultSecurityProfileCatalog CreateCatalog(
        Mock<ISettingCollectionFactory> mockFactory,
        params ISecurityProfile[] profiles
    ) => new(profiles, CreateStrictMock<ISettingSerializer>().Object, mockFactory.Object);

    private Mock<IReadOnlySettingCollection> CreateSettings(params string[]? profileNames)
    {
        var settings = CreateLooseMock<IReadOnlySettingCollection>();
        IReadOnlyCollection<string>? value = profileNames;
        settings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.SecurityProfiles, out value))
            .Returns(profileNames is not null);
        return settings;
    }

    #region TryGetProfile

    [Fact]
    public void TryGetProfile_WhenRegistered_ReturnsProfile()
    {
        var profile = new FakeProfile("fake");
        var catalog = CreateCatalog(profile);

        var found = catalog.TryGetProfile("fake", out var resolved);

        Assert.True(found);
        Assert.Same(profile, resolved);
    }

    [Fact]
    public void TryGetProfile_WhenUnknown_ReturnsFalse()
    {
        var catalog = CreateCatalog(new FakeProfile("fake"));

        Assert.False(catalog.TryGetProfile("other", out _));
    }

    [Fact]
    public void TryGetProfile_WhenEmptyName_ReturnsFalse()
    {
        var catalog = CreateCatalog(new FakeProfile("fake"));

        Assert.False(catalog.TryGetProfile(string.Empty, out _));
    }

    #endregion

    #region EvaluateRequirementsAsync

    [Fact]
    public async Task EvaluateRequirementsAsync_InvokesOnlyRequirementsForTheContextPhase()
    {
        var stringRequirement = new FakeRequirement<string>();
        var intRequirement = new FakeRequirement<int>();
        var profile = new FakeProfile("fake", stringRequirement, intRequirement);
        var catalog = CreateCatalog(profile);
        var settings = CreateSettings("fake");

        await catalog.EvaluateRequirementsAsync(settings.Object, "hello", CancellationToken.None);

        Assert.Equal(1, stringRequirement.InvocationCount);
        Assert.Equal("hello", stringRequirement.LastContext);
        Assert.Equal(0, intRequirement.InvocationCount);
    }

    [Fact]
    public async Task EvaluateRequirementsAsync_WhenNoProfileSelected_InvokesNothing()
    {
        var requirement = new FakeRequirement<string>();
        var catalog = CreateCatalog(new FakeProfile("fake", requirement));
        var settings = CreateSettings(profileNames: null);

        await catalog.EvaluateRequirementsAsync(settings.Object, "hello", CancellationToken.None);

        Assert.Equal(0, requirement.InvocationCount);
    }

    [Fact]
    public async Task EvaluateRequirementsAsync_WhenTheSelectionIsEmpty_InvokesNothing()
    {
        var requirement = new FakeRequirement<string>();
        var catalog = CreateCatalog(new FakeProfile("fake", requirement));
        var settings = CreateSettings();

        await catalog.EvaluateRequirementsAsync(settings.Object, "hello", CancellationToken.None);

        Assert.Equal(0, requirement.InvocationCount);
    }

    [Fact]
    public async Task EvaluateRequirementsAsync_WhenSeveralProfilesAreSelected_InvokesEachInOrdinalOrder()
    {
        List<string> invocations = [];
        var alphaRequirement = new FakeRequirement<string>(() => invocations.Add("alpha"));
        var betaRequirement = new FakeRequirement<string>(() => invocations.Add("beta"));
        var catalog = CreateCatalog(
            new FakeProfile("beta", betaRequirement),
            new FakeProfile("alpha", alphaRequirement)
        );
        var settings = CreateSettings("beta", "alpha", "beta");

        await catalog.EvaluateRequirementsAsync(settings.Object, "hello", CancellationToken.None);

        Assert.Equal(["alpha", "beta"], invocations);
    }

    [Fact]
    public async Task EvaluateRequirementsAsync_WhenProfilesShareARequirement_InvokesItOnce()
    {
        var sharedRequirement = new FakeRequirement<string>();
        var catalog = CreateCatalog(
            new FakeProfile("alpha", sharedRequirement),
            new FakeProfile("beta", sharedRequirement)
        );
        var settings = CreateSettings("alpha", "beta");

        await catalog.EvaluateRequirementsAsync(settings.Object, "hello", CancellationToken.None);

        Assert.Equal(1, sharedRequirement.InvocationCount);
    }

    [Fact]
    public async Task EvaluateRequirementsAsync_WhenProfileUnknown_FailsClosed()
    {
        var requirement = new FakeRequirement<string>();
        var catalog = CreateCatalog(new FakeProfile("fake", requirement));
        var settings = CreateSettings("other");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await catalog.EvaluateRequirementsAsync(
                settings.Object,
                "hello",
                CancellationToken.None
            )
        );

        Assert.Contains("'other'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, requirement.InvocationCount);
    }

    [Fact]
    public async Task EvaluateRequirementsAsync_WhenOneSelectedProfileIsUnknown_EvaluatesNone()
    {
        var requirement = new FakeRequirement<string>();
        var catalog = CreateCatalog(new FakeProfile("fake", requirement));
        var settings = CreateSettings("fake", "other");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await catalog.EvaluateRequirementsAsync(
                settings.Object,
                "hello",
                CancellationToken.None
            )
        );

        Assert.Equal(0, requirement.InvocationCount);
    }

    #endregion

    #region Expand Tests

    [Fact]
    public void Expand_WhenNoProfileSelected_ReturnsTheSettingsUnchanged()
    {
        var mockFactory = CreateStrictMock<ISettingCollectionFactory>();
        var catalog = CreateCatalog(mockFactory, new FakeProfile("fake"));
        IReadOnlyCollection<Setting> scopeSettings = [];
        var collection = CreateSettingCollection();
        SelectProfiles(collection);
        mockFactory.Setup(x => x.Create(scopeSettings)).Returns(collection.Object).Verifiable();

        var result = catalog.Expand(scopeSettings);

        Assert.Same(scopeSettings, result);
    }

    [Fact]
    public void Expand_WhenSeveralProfilesAreSelected_MergesEachCeilingSeparatelyInOrdinalOrder()
    {
        var mockFactory = CreateStrictMock<ISettingCollectionFactory>();
        List<string> applied = [];
        var alpha = new FakeProfile("alpha") { OnApply = () => applied.Add("alpha") };
        var beta = new FakeProfile("beta") { OnApply = () => applied.Add("beta") };
        var catalog = CreateCatalog(mockFactory, beta, alpha);
        IReadOnlyCollection<Setting> scopeSettings = [];
        var alphaCeiling = CreateSettingCollection();
        var betaCeiling = CreateSettingCollection();
        SetupCreatedCollections(
            mockFactory,
            alphaCeiling,
            betaCeiling,
            CreateMergedDefaults(),
            CreateProfileDefaults(),
            CreateProfileDefaults()
        );
        var declared = CreateSettingCollection();
        SelectProfiles(declared, "beta", "alpha", "beta");
        var withAlpha = CreateSettingCollection();
        var withBoth = CreateSettingCollection();
        SelectProfiles(withBoth, "alpha", "beta");
        mockFactory.Setup(x => x.Create(scopeSettings)).Returns(declared.Object).Verifiable();
        declared.Setup(x => x.Merge(alphaCeiling.Object)).Returns(withAlpha.Object).Verifiable();
        withAlpha.Setup(x => x.Merge(betaCeiling.Object)).Returns(withBoth.Object).Verifiable();

        var result = catalog.Expand(scopeSettings);

        Assert.Same(withBoth.Object, result);
        Assert.Equal(["alpha", "beta"], applied);
    }

    [Fact]
    public void Expand_WhenACeilingSelectsAnotherProfile_AppliesThatProfileToo()
    {
        var mockFactory = CreateStrictMock<ISettingCollectionFactory>();
        List<string> applied = [];
        var extended = new FakeProfile("extended") { OnApply = () => applied.Add("extended") };
        var baseline = new FakeProfile("baseline") { OnApply = () => applied.Add("baseline") };
        var catalog = CreateCatalog(mockFactory, extended, baseline);
        IReadOnlyCollection<Setting> scopeSettings = [];
        var extendedCeiling = CreateSettingCollection();
        var baselineCeiling = CreateSettingCollection();
        SetupCreatedCollections(
            mockFactory,
            extendedCeiling,
            baselineCeiling,
            CreateMergedDefaults(),
            CreateProfileDefaults(),
            CreateProfileDefaults()
        );
        var declared = CreateSettingCollection();
        SelectProfiles(declared, "extended");
        var withExtended = CreateSettingCollection();
        SelectProfiles(withExtended, "baseline", "extended");
        var withBoth = CreateSettingCollection();
        SelectProfiles(withBoth, "baseline", "extended");
        mockFactory.Setup(x => x.Create(scopeSettings)).Returns(declared.Object).Verifiable();
        declared
            .Setup(x => x.Merge(extendedCeiling.Object))
            .Returns(withExtended.Object)
            .Verifiable();
        withExtended
            .Setup(x => x.Merge(baselineCeiling.Object))
            .Returns(withBoth.Object)
            .Verifiable();

        var result = catalog.Expand(scopeSettings);

        Assert.Same(withBoth.Object, result);
        Assert.Equal(["extended", "baseline"], applied);
    }

    [Fact]
    public void Expand_WhenAProfileSetsADefault_MergesTheScopeSettingsOverIt()
    {
        var mockFactory = CreateStrictMock<ISettingCollectionFactory>();
        var setting = CreateDefaultSetting("PS256");
        var alpha = new FakeProfile("alpha")
        {
            OnDefaults = builder => builder.Set(DefaultKey, "PS256"),
        };
        var catalog = CreateCatalog(mockFactory, alpha);
        IReadOnlyCollection<Setting> scopeSettings = [];
        var ceiling = CreateSettingCollection();
        var defaults = CreateSettingCollection();
        var alphaDefaults = CreateProfileDefaults(setting);
        SetupCreatedCollections(mockFactory, ceiling, defaults, alphaDefaults);
        var declared = CreateSettingCollection();
        SelectProfiles(declared, "alpha");
        var withAlpha = CreateSettingCollection();
        SelectProfiles(withAlpha, "alpha");
        var withDefaults = CreateSettingCollection();
        Setting? none = null;
        mockFactory.Setup(x => x.Create(scopeSettings)).Returns(declared.Object).Verifiable();
        declared.Setup(x => x.Merge(ceiling.Object)).Returns(withAlpha.Object).Verifiable();
        defaults.Setup(x => x.TryGet(DefaultSettingName, out none)).Returns(false).Verifiable();
        defaults.Setup(x => x.Set(setting)).Verifiable();
        defaults.Setup(x => x.Count).Returns(1).Verifiable();
        defaults.Setup(x => x.Merge(withAlpha.Object)).Returns(withDefaults.Object).Verifiable();

        var result = catalog.Expand(scopeSettings);

        Assert.Same(withDefaults.Object, result);
    }

    [Fact]
    public void Expand_WhenSeveralProfilesDefaultTheSameSetting_TheFirstInOrdinalOrderSuppliesIt()
    {
        var mockFactory = CreateStrictMock<ISettingCollectionFactory>();
        var alphaSetting = CreateDefaultSetting("PS256");
        var betaSetting = CreateDefaultSetting("ES256");
        var alpha = new FakeProfile("alpha")
        {
            OnDefaults = builder => builder.Set(DefaultKey, "PS256"),
        };
        var beta = new FakeProfile("beta")
        {
            OnDefaults = builder => builder.Set(DefaultKey, "ES256"),
        };
        var catalog = CreateCatalog(mockFactory, beta, alpha);
        IReadOnlyCollection<Setting> scopeSettings = [];
        var alphaCeiling = CreateSettingCollection();
        var betaCeiling = CreateSettingCollection();
        var defaults = CreateSettingCollection();
        SetupCreatedCollections(
            mockFactory,
            alphaCeiling,
            betaCeiling,
            defaults,
            CreateProfileDefaults(alphaSetting),
            CreateProfileDefaults(betaSetting)
        );
        var declared = CreateSettingCollection();
        SelectProfiles(declared, "beta", "alpha");
        var withAlpha = CreateSettingCollection();
        var withBoth = CreateSettingCollection();
        SelectProfiles(withBoth, "alpha", "beta");
        var withDefaults = CreateSettingCollection();
        Setting? none = null;
        mockFactory.Setup(x => x.Create(scopeSettings)).Returns(declared.Object).Verifiable();
        declared.Setup(x => x.Merge(alphaCeiling.Object)).Returns(withAlpha.Object).Verifiable();
        withAlpha.Setup(x => x.Merge(betaCeiling.Object)).Returns(withBoth.Object).Verifiable();
        defaults
            .SetupSequence(x => x.TryGet(DefaultSettingName, out none))
            .Returns(false)
            .Returns(true);
        defaults.Setup(x => x.Set(alphaSetting)).Verifiable();
        defaults.Setup(x => x.Count).Returns(1).Verifiable();
        defaults.Setup(x => x.Merge(withBoth.Object)).Returns(withDefaults.Object).Verifiable();

        var result = catalog.Expand(scopeSettings);

        Assert.Same(withDefaults.Object, result);
    }

    [Fact]
    public void Expand_WhenProfileUnknown_FailsClosed()
    {
        var mockFactory = CreateStrictMock<ISettingCollectionFactory>();
        var catalog = CreateCatalog(mockFactory, new FakeProfile("fake"));
        IReadOnlyCollection<Setting> scopeSettings = [];
        var collection = CreateSettingCollection();
        SelectProfiles(collection, "fapi-2");
        mockFactory.Setup(x => x.Create(scopeSettings)).Returns(collection.Object).Verifiable();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            catalog.Expand(scopeSettings)
        );

        Assert.Contains("'fapi-2'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Expand_WhenGivenADocument_DeserializesItForItsScope()
    {
        var mockSerializer = CreateStrictMock<ISettingSerializer>();
        var mockFactory = CreateStrictMock<ISettingCollectionFactory>();
        var catalog = new DefaultSecurityProfileCatalog(
            [new FakeProfile("fake")],
            mockSerializer.Object,
            mockFactory.Object
        );
        var settingsJson = JsonElement.Parse("{}");
        var jsonOptions = new JsonSerializerOptions();
        IReadOnlyCollection<Setting> scopeSettings = [];
        mockSerializer
            .Setup(x =>
                x.DeserializeSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Client,
                    jsonOptions
                )
            )
            .Returns(scopeSettings)
            .Verifiable();
        var collection = CreateSettingCollection();
        SelectProfiles(collection);
        mockFactory.Setup(x => x.Create(scopeSettings)).Returns(collection.Object).Verifiable();

        var result = catalog.Expand(settingsJson, OpenIdSettingScopes.Client, jsonOptions);

        Assert.Same(scopeSettings, result);
    }

    #endregion

    #region Scaffolding

    private Mock<ISettingCollection> CreateSettingCollection() =>
        CreateStrictMock<ISettingCollection>();

    private static void SelectProfiles(
        Mock<ISettingCollection> settings,
        params string[] profileNames
    )
    {
        IReadOnlyCollection<string>? value = profileNames;
        settings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.SecurityProfiles, out value))
            .Returns(true)
            .Verifiable();
    }

    private const string DefaultSettingName = "signed_response_alg";

    private static SettingKey<string> DefaultKey { get; } = new(DefaultSettingName);

    private static Setting<string> CreateDefaultSetting(string value) =>
        new SettingDescriptor<string>
        {
            Name = DefaultSettingName,
            AppliesTo = ["client"],
            OnMerge = SettingMerge.Replace,
        }.Create(value);

    // The factory hands out, in order, each profile's ceiling, the merged defaults, and each profile's own defaults.
    private static void SetupCreatedCollections(
        Mock<ISettingCollectionFactory> mockFactory,
        params Mock<ISettingCollection>[] collections
    )
    {
        var pending = new Queue<ISettingCollection>(
            collections.Select(collection => collection.Object)
        );
        mockFactory.Setup(x => x.Create()).Returns(() => pending.Dequeue()).Verifiable();
    }

    private Mock<ISettingCollection> CreateMergedDefaults()
    {
        var defaults = CreateSettingCollection();
        defaults.Setup(x => x.Count).Returns(0).Verifiable();
        return defaults;
    }

    private Mock<ISettingCollection> CreateProfileDefaults(Setting<string>? setting = null)
    {
        var profileDefaults = CreateSettingCollection();
        List<Setting> contents = [];
        if (setting is not null)
        {
            Setting<string>? stored = setting;
            profileDefaults.Setup(x => x.Set(DefaultKey, setting.Value)).Verifiable();
            profileDefaults.Setup(x => x.TryGet(DefaultKey, out stored)).Returns(true).Verifiable();
            contents.Add(setting);
        }

        profileDefaults
            .Setup(x => x.GetEnumerator())
            .Returns(() => contents.GetEnumerator())
            .Verifiable();
        return profileDefaults;
    }

    private sealed class FakeProfile(string name, params ISecurityProfileRequirement[] requirements)
        : ISecurityProfile
    {
        public string ProfileName => name;
        public IReadOnlyCollection<ISecurityProfileRequirement> Requirements => requirements;
        public Action? OnApply { get; init; }

        public Action<ISecurityProfileDefaultsBuilder>? OnDefaults { get; init; }

        public void ApplyCeilingSettings(ISecurityProfileCeilingBuilder ceiling) =>
            OnApply?.Invoke();

        public void ApplyDefaultSettings(ISecurityProfileDefaultsBuilder defaults) =>
            OnDefaults?.Invoke(defaults);
    }

    private sealed class FakeRequirement<TContext>(Action? onEvaluate = null)
        : ISecurityProfileRequirement<TContext>
    {
        public int InvocationCount { get; private set; }
        public TContext? LastContext { get; private set; }

        public ValueTask EvaluateAsync(TContext context, CancellationToken cancellationToken)
        {
            InvocationCount++;
            LastContext = context;
            onEvaluate?.Invoke();
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
