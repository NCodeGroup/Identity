#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

namespace NCode.Identity.Settings;

public class DefaultSettingSerializerTests : BaseTests
{
    private static DefaultSettingScopeCollection Scopes { get; } =
        new([
            new SettingScopeDescriptor { Name = "server" },
            new SettingScopeDescriptor { Name = "tenant", ParentName = "server" },
            new SettingScopeDescriptor { Name = "client", ParentName = "tenant" },
            new SettingScopeDescriptor { Name = "resource_server", ParentName = "tenant" },
        ]);

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private ISettingScopeCollectionProvider CreateScopeProvider()
    {
        var mockProvider = CreateStrictMock<ISettingScopeCollectionProvider>();
        mockProvider.Setup(x => x.Collection).Returns(Scopes).Verifiable();
        return mockProvider.Object;
    }

    private DefaultSettingSerializer CreateSerializer(params SettingDescriptor[] descriptors)
    {
        var mockProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
        mockProvider
            .Setup(x => x.Collection)
            .Returns(new DefaultSettingDescriptorCollection(descriptors))
            .Verifiable();
        return new DefaultSettingSerializer(mockProvider.Object, CreateScopeProvider());
    }

    private DefaultSettingSerializer CreateSerializerWithoutDescriptors() =>
        new(CreateStrictMock<ISettingDescriptorCollectionProvider>().Object, CreateScopeProvider());

    private static JsonElement Parse(string json) => JsonElement.Parse(json);

    #region DeserializeSettings Tests

    [Fact]
    public void DeserializeSettings_WhenNull_ReturnsEmpty()
    {
        var serializer = CreateSerializerWithoutDescriptors();

        var result = serializer.DeserializeSettings(Parse("null"), "client", _jsonOptions);

        Assert.Empty(result);
    }

    [Fact]
    public void DeserializeSettings_WhenNotObject_ThrowsJsonException()
    {
        var serializer = CreateSerializerWithoutDescriptors();

        Assert.Throws<JsonException>(() =>
            serializer.DeserializeSettings(Parse("[]"), "client", _jsonOptions)
        );
    }

    [Fact]
    public void DeserializeSettings_WhenRegistered_CreatesSettingsFromValues()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<string> { Name = "a", AppliesTo = ["client"] },
            new SettingDescriptor<bool> { Name = "b", AppliesTo = ["client"] },
            new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = "c",
                AppliesTo = ["client"],
            }
        );

        var result = serializer.DeserializeSettings(
            Parse("""{"a":"value","b":true,"c":["x","y"]}"""),
            "client",
            _jsonOptions
        );

        var settings = result.ToDictionary(x => x.Descriptor.Name, x => x.GetValue());
        Assert.Equal("value", settings["a"]);
        Assert.True(Assert.IsType<bool>(settings["b"]));
        Assert.Equal(["x", "y"], Assert.IsAssignableFrom<IEnumerable<string>>(settings["c"]));
    }

    [Fact]
    public void DeserializeSettings_WhenNullValueAndDescriptorHasDefault_UsesDefault()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<string>
            {
                Name = "a",
                AppliesTo = ["client"],
                Default = "fallback",
            }
        );

        var result = serializer.DeserializeSettings(
            Parse("""{"a":null}"""),
            "client",
            _jsonOptions
        );

        var setting = Assert.Single(result);
        Assert.Equal("fallback", setting.GetValue());
    }

    [Fact]
    public void DeserializeSettings_WhenNullValueAndNoDefault_ThrowsJsonException()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<string> { Name = "a", AppliesTo = ["client"] }
        );

        Assert.Throws<JsonException>(() =>
            serializer.DeserializeSettings(Parse("""{"a":null}"""), "client", _jsonOptions)
        );
    }

    [Fact]
    public void DeserializeSettings_WhenSettingIsNotRegistered_ThrowsNamingIt()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<string> { Name = "a", AppliesTo = ["client"] }
        );

        var exception = Assert.Throws<JsonException>(() =>
            serializer.DeserializeSettings(
                Parse("""{"a":"value","tenant_issuer":"x"}"""),
                "client",
                _jsonOptions
            )
        );

        Assert.Contains("'tenant_issuer'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeserializeSettings_WhenValueHasWrongType_ThrowsNamingIt()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<bool> { Name = "require_pkce", AppliesTo = ["client"] }
        );

        var exception = Assert.Throws<JsonException>(() =>
            serializer.DeserializeSettings(
                Parse("""{"require_pkce":"yes"}"""),
                "client",
                _jsonOptions
            )
        );

        Assert.Contains("'require_pkce'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeserializeSettings_WhenValueIsOutsideTheDomain_ThrowsNamingIt()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<string>
            {
                Name = "subject_type",
                AppliesTo = ["client"],
                Rule = SettingRules.OneOf("public", "pairwise"),
            }
        );

        var exception = Assert.Throws<JsonException>(() =>
            serializer.DeserializeSettings(
                Parse("""{"subject_type":"pairwse"}"""),
                "client",
                _jsonOptions
            )
        );

        Assert.Contains(
            "The setting 'subject_type' must be one of 'public' or 'pairwise'.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void DeserializeSettings_WhenValueIsInTheDomain_CreatesTheSetting()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<TimeSpan>
            {
                Name = "lifetime",
                AppliesTo = ["client"],
                Rule = SettingRules.AtLeast(TimeSpan.FromSeconds(1)),
            }
        );

        var result = serializer.DeserializeSettings(
            Parse("""{"lifetime":"00:05:00"}"""),
            "client",
            _jsonOptions
        );

        var setting = Assert.Single(result);
        Assert.Equal(TimeSpan.FromMinutes(5), setting.GetValue());
    }

    [Fact]
    public void DeserializeSettings_WhenAStoredSettingDoesNotApplyWithinTheScope_ThrowsNamingIt()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<bool> { Name = "require_pkce", AppliesTo = ["client"] }
        );

        var exception = Assert.Throws<JsonException>(() =>
            serializer.DeserializeSettings(
                Parse("""{"require_pkce":true}"""),
                "resource_server",
                _jsonOptions
            )
        );

        Assert.Contains(
            "The setting 'require_pkce' does not apply to the 'resource_server' scope; it applies to the 'client' scope.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region ValidateSettings Tests

    [Fact]
    public void ValidateSettings_WhenNull_ReturnsNoErrors()
    {
        var serializer = CreateSerializerWithoutDescriptors();

        var errors = serializer.ValidateSettings(Parse("null"), "client", _jsonOptions);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSettings_WhenValid_ReturnsNoErrors()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<TimeSpan> { Name = "a", AppliesTo = ["client"] }
        );

        var errors = serializer.ValidateSettings(
            Parse("""{"a":"00:05:00"}"""),
            "client",
            _jsonOptions
        );

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSettings_WhenNotObject_ReportsDocumentError()
    {
        var serializer = CreateSerializerWithoutDescriptors();

        var errors = serializer.ValidateSettings(Parse("[]"), "client", _jsonOptions);

        var error = Assert.Single(errors);
        Assert.Equal(string.Empty, error.SettingName);
    }

    [Fact]
    public void ValidateSettings_WhenSeveralSettingsAreInvalid_ReportsEachOne()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<bool> { Name = "flag", AppliesTo = ["client"] },
            new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = "list",
                AppliesTo = ["client"],
            },
            new SettingDescriptor<string> { Name = "text", AppliesTo = ["client"] },
            new SettingDescriptor<string> { Name = "valid", AppliesTo = ["client"] }
        );

        var errors = serializer.ValidateSettings(
            Parse("""{"unknown":1,"flag":"yes","list":"one","text":null,"valid":"ok"}"""),
            "client",
            _jsonOptions
        );

        Assert.Equal(["unknown", "flag", "list", "text"], errors.Select(x => x.SettingName));
        Assert.Contains("not a registered setting", errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("true or false", errors[1].Message, StringComparison.Ordinal);
        Assert.Contains("an array of strings", errors[2].Message, StringComparison.Ordinal);
        Assert.Contains("not null", errors[3].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateSettings_WhenListContainsNull_ReportsATypeError()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = "list",
                AppliesTo = ["client"],
            }
        );

        var errors = serializer.ValidateSettings(
            Parse("""{"list":["a",null]}"""),
            "client",
            _jsonOptions
        );

        var error = Assert.Single(errors);
        Assert.Equal("list", error.SettingName);
        Assert.Equal("The setting 'list' must be an array of strings.", error.Message);
    }

    [Fact]
    public void ValidateSettings_WhenValuesAreOutsideTheirDomains_ReportsEachOne()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<string>
            {
                Name = "subject_type",
                AppliesTo = ["client"],
                Rule = SettingRules.OneOf("public", "pairwise"),
            },
            new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = "code_challenge_methods_supported",
                AppliesTo = ["client"],
                Rule = SettingRules.Each(SettingRules.OneOf("S256", "plain")),
            },
            new SettingDescriptor<int>
            {
                Name = "length",
                AppliesTo = ["client"],
                Rule = SettingRules.Between(8, 32),
            }
        );

        var errors = serializer.ValidateSettings(
            Parse(
                """{"subject_type":"pairwse","code_challenge_methods_supported":["S256","S512"],"length":4}"""
            ),
            "client",
            _jsonOptions
        );

        Assert.Equal(
            [
                "The setting 'subject_type' must be one of 'public' or 'pairwise'.",
                "The setting 'code_challenge_methods_supported' must list only values that are one of 'S256' or "
                    + "'plain'; 'S512' is not.",
                "The setting 'length' must be between 8 and 32.",
            ],
            errors.Select(x => x.Message)
        );
    }

    [Fact]
    public void ValidateSettings_WhenNullValueUsesTheDefault_DoesNotReport()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<string>
            {
                Name = "subject_type",
                AppliesTo = ["client"],
                Default = "public",
                Rule = SettingRules.OneOf("public", "pairwise"),
            }
        );

        var errors = serializer.ValidateSettings(
            Parse("""{"subject_type":null}"""),
            "client",
            _jsonOptions
        );

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("server")]
    [InlineData("tenant")]
    [InlineData("client")]
    public void ValidateSettings_WhenTheSettingAppliesWithinTheScope_ReturnsNoErrors(
        string scopeName
    )
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<bool> { Name = "require_pkce", AppliesTo = ["client"] }
        );

        var errors = serializer.ValidateSettings(
            Parse("""{"require_pkce":true}"""),
            scopeName,
            _jsonOptions
        );

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSettings_WhenTheSettingDoesNotApplyWithinTheScope_ReportsTheScopesItAppliesTo()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = "security_profiles",
                AppliesTo = ["tenant", "client"],
            }
        );

        var errors = serializer.ValidateSettings(
            Parse("""{"security_profiles":["fapi2"]}"""),
            "resource_server",
            _jsonOptions
        );

        var error = Assert.Single(errors);
        Assert.Equal("security_profiles", error.SettingName);
        Assert.Equal(
            "The setting 'security_profiles' does not apply to the 'resource_server' scope; it applies to the 'tenant' and 'client' scopes.",
            error.Message
        );
    }

    [Fact]
    public void ValidateSettings_WhenTheSettingDoesNotApplyWithinTheScope_ReportsItWhateverItsValue()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<bool>
            {
                Name = "session_management_supported",
                AppliesTo = ["tenant"],
            }
        );

        var errors = serializer.ValidateSettings(
            Parse("""{"session_management_supported":"yes"}"""),
            "client",
            _jsonOptions
        );

        var error = Assert.Single(errors);
        Assert.Equal(
            "The setting 'session_management_supported' does not apply to the 'client' scope; it applies to the 'tenant' scope.",
            error.Message
        );
    }

    [Theory]
    [InlineData("organization")]
    [InlineData("")]
    public void ValidateSettings_WhenTheScopeIsNotRegistered_Throws(string scopeName)
    {
        var serializer = CreateSerializerWithoutDescriptors();

        var exception = Assert.Throws<ArgumentException>(() =>
            serializer.ValidateSettings(Parse("null"), scopeName, _jsonOptions)
        );

        Assert.Equal("scopeName", exception.ParamName);
    }

    #endregion
}
