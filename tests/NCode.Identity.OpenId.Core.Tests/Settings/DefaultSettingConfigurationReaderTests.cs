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

using Microsoft.Extensions.Configuration;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class DefaultSettingConfigurationReaderTests : BaseTests
{
    private const string SectionPath = "OpenId:Server:Settings";

    private DefaultSettingConfigurationReader CreateReader()
    {
        var descriptors = new TestSettingDescriptorCollection(
            new SettingDescriptor<string> { Name = "text", AppliesTo = ["client"] },
            new SettingDescriptor<bool> { Name = "flag", AppliesTo = ["client"] },
            new SettingDescriptor<int> { Name = "count", AppliesTo = ["client"] },
            new SettingDescriptor<TimeSpan> { Name = "lifetime", AppliesTo = ["client"] },
            new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = "list",
                AppliesTo = ["client"],
            },
            new SettingDescriptor<string>
            {
                Name = "mode",
                AppliesTo = ["client"],
                Rule = SettingRules.OneOf("poll", "ping", "push"),
            },
            new SettingDescriptor<TimeSpan>
            {
                Name = "timeout",
                AppliesTo = ["client"],
                Rule = SettingRules.AtLeast(TimeSpan.FromSeconds(1)),
            },
            new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = "methods",
                AppliesTo = ["client"],
                Rule = SettingRules.Each(SettingRules.OneOf("S256", "plain")),
            },
            new SettingDescriptor<bool> { Name = "tenant_only", AppliesTo = ["tenant"] },
            new SettingDescriptor<bool> { Name = "nowhere", AppliesTo = [] }
        );

        var mockProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
        mockProvider.Setup(x => x.Collection).Returns(descriptors).Verifiable();

        var mockScopeProvider = CreateStrictMock<ISettingScopeCollectionProvider>();
        mockScopeProvider
            .Setup(x => x.Collection)
            .Returns(TestSettingScopeCollection.OpenId)
            .Verifiable();

        return new DefaultSettingConfigurationReader(mockProvider.Object, mockScopeProvider.Object);
    }

    private static IConfigurationSection CreateSection(params (string Key, string? Value)[] values)
    {
        var data = values.Select(pair => new KeyValuePair<string, string?>(
            $"{SectionPath}:{pair.Key}",
            pair.Value
        ));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        return configuration.GetSection(SectionPath);
    }

    #region Read Tests

    [Fact]
    public void Read_WhenEverySettingIsRegistered_TypesEachByItsDescriptor()
    {
        var reader = CreateReader();
        var section = CreateSection(
            ("text", "value"),
            ("flag", "true"),
            ("count", "3"),
            ("lifetime", "00:05:00"),
            ("list:0", "a"),
            ("list:1", "b")
        );

        var settings = reader
            .Read(section, OpenIdSettingScopes.Server)
            .ToDictionary(x => x.Descriptor.Name, x => x.GetValue());

        Assert.Equal("value", settings["text"]);
        Assert.True(Assert.IsType<bool>(settings["flag"]));
        Assert.Equal(3, Assert.IsType<int>(settings["count"]));
        Assert.Equal(TimeSpan.FromMinutes(5), Assert.IsType<TimeSpan>(settings["lifetime"]));
        Assert.Equal(["a", "b"], Assert.IsAssignableFrom<IEnumerable<string>>(settings["list"]));
    }

    [Fact]
    public void Read_WhenListSettingIsAnEmptyArray_ReadsAnEmptyList()
    {
        var reader = CreateReader();
        var section = CreateSection(("list", null));

        var setting = Assert.Single(reader.Read(section, OpenIdSettingScopes.Server));

        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(setting.GetValue()));
    }

    [Fact]
    public void Read_WhenSectionIsEmpty_ReadsNothing()
    {
        var reader = CreateReader();

        var settings = reader.Read(CreateSection(), OpenIdSettingScopes.Server);

        Assert.Empty(settings);
    }

    [Fact]
    public void Read_WhenSettingsCannotBeHonored_ThrowsListingEveryOne()
    {
        var reader = CreateReader();
        var section = CreateSection(("tenant_issuer", "https://op.example"), ("flag", "yes"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            reader.Read(section, OpenIdSettingScopes.Server)
        );

        Assert.Contains($"'{SectionPath}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            $"{SectionPath}:tenant_issuer",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains($"{SectionPath}:flag", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_WhenValueIsOutsideTheDomain_ThrowsInsteadOfLoadingIt()
    {
        var reader = CreateReader();
        var section = CreateSection(("mode", "pull"), ("text", "value"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            reader.Read(section, OpenIdSettingScopes.Server)
        );

        Assert.Contains($"{SectionPath}:mode", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WhenEverySettingCanBeHonored_ReturnsNoErrors()
    {
        var reader = CreateReader();
        var section = CreateSection(("text", ""), ("flag", "false"), ("list:0", "a"));

        var errors = reader.Validate(section, OpenIdSettingScopes.Server);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WhenNameIsNotRegistered_ReportsItsPath()
    {
        var reader = CreateReader();
        var section = CreateSection(("tenant_issuer", "https://op.example"));

        var error = Assert.Single(reader.Validate(section, OpenIdSettingScopes.Server));

        Assert.Equal($"'{SectionPath}:tenant_issuer' is not a registered setting.", error);
    }

    [Fact]
    public void Validate_WhenNameDiffersOnlyByCase_ReportsIt()
    {
        var reader = CreateReader();
        var section = CreateSection(("Flag", "true"));

        var error = Assert.Single(reader.Validate(section, OpenIdSettingScopes.Server));

        Assert.Contains("not a registered setting", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("flag", "yes")]
    [InlineData("count", "many")]
    [InlineData("lifetime", "5m")]
    public void Validate_WhenValueDoesNotConvert_ReportsIt(string name, string value)
    {
        var reader = CreateReader();
        var section = CreateSection((name, value));

        var error = Assert.Single(reader.Validate(section, OpenIdSettingScopes.Server));

        Assert.Contains($"{SectionPath}:{name}", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenListSettingHasASingleValue_ReportsIt()
    {
        var reader = CreateReader();
        var section = CreateSection(("list", "authorization_code"));

        var error = Assert.Single(reader.Validate(section, OpenIdSettingScopes.Server));

        Assert.Contains("must be a list of values", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("flag:0")]
    [InlineData("text:nested")]
    public void Validate_WhenSingleValueSettingHasEntries_ReportsIt(string key)
    {
        var reader = CreateReader();
        var section = CreateSection((key, "true"));

        var error = Assert.Single(reader.Validate(section, OpenIdSettingScopes.Server));

        Assert.Contains("must be a single value", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenValueIsOutsideTheDomain_ReportsItsPathAndDomain()
    {
        var reader = CreateReader();
        var section = CreateSection(("mode", "pull"));

        var error = Assert.Single(reader.Validate(section, OpenIdSettingScopes.Server));

        Assert.Equal($"'{SectionPath}:mode' must be one of 'poll', 'ping', or 'push'.", error);
    }

    [Fact]
    public void Validate_WhenDurationIsBelowTheDomain_ReportsIt()
    {
        var reader = CreateReader();
        var section = CreateSection(("timeout", "-00:05:00"));

        var error = Assert.Single(reader.Validate(section, OpenIdSettingScopes.Server));

        Assert.Equal($"'{SectionPath}:timeout' must be at least 00:00:01.", error);
    }

    [Fact]
    public void Validate_WhenListValueIsOutsideTheDomain_NamesTheValue()
    {
        var reader = CreateReader();
        var section = CreateSection(("methods:0", "S256"), ("methods:1", "S512"));

        var error = Assert.Single(reader.Validate(section, OpenIdSettingScopes.Server));

        Assert.Contains($"'{SectionPath}:methods'", error, StringComparison.Ordinal);
        Assert.Contains("'S512' is not", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenValuesAreInTheirDomains_ReturnsNoErrors()
    {
        var reader = CreateReader();
        var section = CreateSection(
            ("mode", "ping"),
            ("timeout", "00:00:30"),
            ("methods:0", "S256")
        );

        var errors = reader.Validate(section, OpenIdSettingScopes.Server);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WhenTheSettingDoesNotApplyWithinTheScope_ReportsItsPathAndScopes()
    {
        var reader = CreateReader();
        var section = CreateSection(("tenant_only", "true"));

        var error = Assert.Single(reader.Validate(section, OpenIdSettingScopes.Client));

        Assert.Equal($"'{SectionPath}:tenant_only' does not apply to the 'client' scope.", error);
    }

    [Fact]
    public void Validate_WhenTheSettingAppliesToNoScope_ReportsItEvenForTheServer()
    {
        var reader = CreateReader();
        var section = CreateSection(("nowhere", "true"));

        var error = Assert.Single(reader.Validate(section, OpenIdSettingScopes.Server));

        Assert.Equal($"'{SectionPath}:nowhere' does not apply to the 'server' scope.", error);
    }

    [Fact]
    public void Validate_WhenTheScopeIsNotRegistered_Throws()
    {
        // The scope is checked before any descriptor is consulted.
        var mockScopeProvider = CreateStrictMock<ISettingScopeCollectionProvider>();
        mockScopeProvider
            .Setup(x => x.Collection)
            .Returns(TestSettingScopeCollection.OpenId)
            .Verifiable();
        var reader = new DefaultSettingConfigurationReader(
            CreateStrictMock<ISettingDescriptorCollectionProvider>().Object,
            mockScopeProvider.Object
        );

        var exception = Assert.Throws<ArgumentException>(() =>
            reader.Validate(CreateSection(), "organization")
        );

        Assert.Equal("scopeName", exception.ParamName);
    }

    [Fact]
    public void Validate_WhenTheSettingAppliesBelowTheScope_ReturnsNoErrors()
    {
        var reader = CreateReader();
        var section = CreateSection(("tenant_only", "true"), ("flag", "true"));

        var errors = reader.Validate(section, OpenIdSettingScopes.Server);

        Assert.Empty(errors);
    }

    #endregion
}
