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

    private Mock<IReadOnlySettingCollection> CreateSettings(string? profileName)
    {
        var settings = CreateLooseMock<IReadOnlySettingCollection>();
        var value = profileName;
        settings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.SecurityProfile, out value))
            .Returns(!string.IsNullOrEmpty(profileName));
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
        var settings = CreateSettings(profileName: null);

        await catalog.EvaluateRequirementsAsync(settings.Object, "hello", CancellationToken.None);

        Assert.Equal(0, requirement.InvocationCount);
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

    #endregion

    #region Expand Tests

    [Fact]
    public void Expand_WhenNoProfileSelected_ReturnsTheSettingsUnchanged()
    {
        var mockFactory = CreateStrictMock<ISettingCollectionFactory>();
        var catalog = new DefaultSecurityProfileCatalog(
            [new FakeProfile("fake")],
            CreateStrictMock<ISettingSerializer>().Object,
            mockFactory.Object
        );
        IReadOnlyCollection<Setting> scopeSettings = [];
        var collection = CreateSettingCollection(profileName: null);
        mockFactory.Setup(x => x.Create(scopeSettings)).Returns(collection.Object).Verifiable();

        var result = catalog.Expand(scopeSettings);

        Assert.Same(scopeSettings, result);
    }

    [Fact]
    public void Expand_WhenProfileUnknown_FailsClosed()
    {
        var mockFactory = CreateStrictMock<ISettingCollectionFactory>();
        var catalog = new DefaultSecurityProfileCatalog(
            [new FakeProfile("fake")],
            CreateStrictMock<ISettingSerializer>().Object,
            mockFactory.Object
        );
        IReadOnlyCollection<Setting> scopeSettings = [];
        var collection = CreateSettingCollection("fapi-2");
        mockFactory.Setup(x => x.Create(scopeSettings)).Returns(collection.Object).Verifiable();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            catalog.Expand(scopeSettings)
        );

        Assert.Contains("'fapi-2'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Scaffolding

    private Mock<ISettingCollection> CreateSettingCollection(string? profileName)
    {
        var settings = CreateLooseMock<ISettingCollection>();
        var value = profileName;
        settings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.SecurityProfile, out value))
            .Returns(!string.IsNullOrEmpty(profileName));
        return settings;
    }

    private sealed class FakeProfile(string name, params ISecurityProfileRequirement[] requirements)
        : ISecurityProfile
    {
        public string ProfileName => name;
        public IReadOnlyCollection<ISecurityProfileRequirement> Requirements => requirements;

        public void ApplyCeilingSettings(ISettingCollection ceiling) { }
    }

    private sealed class FakeRequirement<TContext> : ISecurityProfileRequirement<TContext>
    {
        public int InvocationCount { get; private set; }
        public TContext? LastContext { get; private set; }

        public ValueTask EvaluateAsync(TContext context, CancellationToken cancellationToken)
        {
            InvocationCount++;
            LastContext = context;
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
