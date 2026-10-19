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
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private DefaultSettingSerializer CreateSerializer(params SettingDescriptor[] descriptors)
    {
        var mockProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
        mockProvider
            .Setup(x => x.Collection)
            .Returns(new DefaultSettingDescriptorCollection(descriptors))
            .Verifiable();
        return new DefaultSettingSerializer(mockProvider.Object);
    }

    private static JsonElement Parse(string json) => JsonElement.Parse(json);

    #region DeserializeSettings Tests

    [Fact]
    public void DeserializeSettings_WhenNull_ReturnsEmpty()
    {
        var serializer = new DefaultSettingSerializer(
            CreateStrictMock<ISettingDescriptorCollectionProvider>().Object
        );

        var result = serializer.DeserializeSettings(Parse("null"), _jsonOptions);

        Assert.Empty(result);
    }

    [Fact]
    public void DeserializeSettings_WhenNotObject_ThrowsJsonException()
    {
        var serializer = new DefaultSettingSerializer(
            CreateStrictMock<ISettingDescriptorCollectionProvider>().Object
        );

        Assert.Throws<JsonException>(() =>
            serializer.DeserializeSettings(Parse("[]"), _jsonOptions)
        );
    }

    [Fact]
    public void DeserializeSettings_WhenRegistered_CreatesSettingsFromValues()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<string> { Name = "a" },
            new SettingDescriptor<bool> { Name = "b" },
            new SettingDescriptor<IReadOnlyCollection<string>> { Name = "c" }
        );

        var result = serializer.DeserializeSettings(
            Parse("""{"a":"value","b":true,"c":["x","y"]}"""),
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
            new SettingDescriptor<string> { Name = "a", Default = "fallback" }
        );

        var result = serializer.DeserializeSettings(Parse("""{"a":null}"""), _jsonOptions);

        var setting = Assert.Single(result);
        Assert.Equal("fallback", setting.GetValue());
    }

    [Fact]
    public void DeserializeSettings_WhenNullValueAndNoDefault_ThrowsJsonException()
    {
        var serializer = CreateSerializer(new SettingDescriptor<string> { Name = "a" });

        Assert.Throws<JsonException>(() =>
            serializer.DeserializeSettings(Parse("""{"a":null}"""), _jsonOptions)
        );
    }

    [Fact]
    public void DeserializeSettings_WhenSettingIsNotRegistered_ThrowsNamingIt()
    {
        var serializer = CreateSerializer(new SettingDescriptor<string> { Name = "a" });

        var exception = Assert.Throws<JsonException>(() =>
            serializer.DeserializeSettings(
                Parse("""{"a":"value","tenant_issuer":"x"}"""),
                _jsonOptions
            )
        );

        Assert.Contains("'tenant_issuer'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeserializeSettings_WhenValueHasWrongType_ThrowsNamingIt()
    {
        var serializer = CreateSerializer(new SettingDescriptor<bool> { Name = "require_pkce" });

        var exception = Assert.Throws<JsonException>(() =>
            serializer.DeserializeSettings(Parse("""{"require_pkce":"yes"}"""), _jsonOptions)
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
                Rule = SettingRules.OneOf("public", "pairwise"),
            }
        );

        var exception = Assert.Throws<JsonException>(() =>
            serializer.DeserializeSettings(Parse("""{"subject_type":"pairwse"}"""), _jsonOptions)
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
                Rule = SettingRules.AtLeast(TimeSpan.FromSeconds(1)),
            }
        );

        var result = serializer.DeserializeSettings(
            Parse("""{"lifetime":"00:05:00"}"""),
            _jsonOptions
        );

        var setting = Assert.Single(result);
        Assert.Equal(TimeSpan.FromMinutes(5), setting.GetValue());
    }

    #endregion

    #region ValidateSettings Tests

    [Fact]
    public void ValidateSettings_WhenNull_ReturnsNoErrors()
    {
        var serializer = new DefaultSettingSerializer(
            CreateStrictMock<ISettingDescriptorCollectionProvider>().Object
        );

        var errors = serializer.ValidateSettings(Parse("null"), _jsonOptions);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSettings_WhenValid_ReturnsNoErrors()
    {
        var serializer = CreateSerializer(new SettingDescriptor<TimeSpan> { Name = "a" });

        var errors = serializer.ValidateSettings(Parse("""{"a":"00:05:00"}"""), _jsonOptions);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateSettings_WhenNotObject_ReportsDocumentError()
    {
        var serializer = new DefaultSettingSerializer(
            CreateStrictMock<ISettingDescriptorCollectionProvider>().Object
        );

        var errors = serializer.ValidateSettings(Parse("[]"), _jsonOptions);

        var error = Assert.Single(errors);
        Assert.Equal(string.Empty, error.SettingName);
    }

    [Fact]
    public void ValidateSettings_WhenSeveralSettingsAreInvalid_ReportsEachOne()
    {
        var serializer = CreateSerializer(
            new SettingDescriptor<bool> { Name = "flag" },
            new SettingDescriptor<IReadOnlyCollection<string>> { Name = "list" },
            new SettingDescriptor<string> { Name = "text" },
            new SettingDescriptor<string> { Name = "valid" }
        );

        var errors = serializer.ValidateSettings(
            Parse("""{"unknown":1,"flag":"yes","list":"one","text":null,"valid":"ok"}"""),
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
            new SettingDescriptor<IReadOnlyCollection<string>> { Name = "list" }
        );

        var errors = serializer.ValidateSettings(Parse("""{"list":["a",null]}"""), _jsonOptions);

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
                Rule = SettingRules.OneOf("public", "pairwise"),
            },
            new SettingDescriptor<IReadOnlyCollection<string>>
            {
                Name = "code_challenge_methods_supported",
                Rule = SettingRules.Each(SettingRules.OneOf("S256", "plain")),
            },
            new SettingDescriptor<int> { Name = "length", Rule = SettingRules.Between(8, 32) }
        );

        var errors = serializer.ValidateSettings(
            Parse(
                """{"subject_type":"pairwse","code_challenge_methods_supported":["S256","S512"],"length":4}"""
            ),
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
                Default = "public",
                Rule = SettingRules.OneOf("public", "pairwise"),
            }
        );

        var errors = serializer.ValidateSettings(Parse("""{"subject_type":null}"""), _jsonOptions);

        Assert.Empty(errors);
    }

    #endregion
}
