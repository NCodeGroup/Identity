// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Serialization;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Environments;

public class DefaultOpenIdEnvironmentTests : BaseTests
{
    private DefaultOpenIdEnvironment CreateEnvironment(
        params IOpenIdJsonConverterProvider[] jsonConverterProviders
    ) =>
        new(
            CreateStrictMock<IDataProtector>().Object,
            CreateStrictMock<IDataProtector>().Object,
            CreateStrictMock<IKnownParameterCollectionProvider>().Object,
            CreateStrictMock<IOpenIdMessageFactorySelector>().Object,
            jsonConverterProviders,
            (_, _) => throw new NotSupportedException(),
            _ => throw new NotSupportedException(),
            (_, _, _) => throw new NotSupportedException()
        );

    #region JsonSerializerOptions Tests

    [Fact]
    public void JsonSerializerOptions_WhenReadTwice_ReturnsTheSameReadOnlyInstance()
    {
        var mockProvider = CreateStrictMock<IOpenIdJsonConverterProvider>();
        var environment = CreateEnvironment(mockProvider.Object);

        mockProvider
            .Setup(x => x.GetJsonConverters(environment))
            .Returns([new StubJsonConverter()])
            .Verifiable();

        var first = environment.JsonSerializerOptions;
        var second = environment.JsonSerializerOptions;

        Assert.Same(first, second);
        Assert.True(first.IsReadOnly);
        mockProvider.Verify(x => x.GetJsonConverters(environment), Times.Once());
    }

    [Fact]
    public void JsonSerializerOptions_WhenCreated_UsesWebDefaultsWithLenientReading()
    {
        var environment = CreateEnvironment();

        var options = environment.JsonSerializerOptions;

        Assert.True(options.PropertyNameCaseInsensitive);
        Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.Equal(JsonNumberHandling.AllowReadingFromString, options.NumberHandling);
        Assert.Equal(JsonCommentHandling.Skip, options.ReadCommentHandling);
        Assert.True(options.AllowTrailingCommas);
        Assert.NotNull(options.TypeInfoResolver);
    }

    [Fact]
    public void JsonSerializerOptions_WhenProvidersSupplyConverters_ContainsEveryConverter()
    {
        var firstConverter = new StubJsonConverter();
        var secondConverter = new StubJsonConverter();
        var mockFirstProvider = CreateStrictMock<IOpenIdJsonConverterProvider>();
        var mockSecondProvider = CreateStrictMock<IOpenIdJsonConverterProvider>();
        var environment = CreateEnvironment(mockFirstProvider.Object, mockSecondProvider.Object);

        mockFirstProvider
            .Setup(x => x.GetJsonConverters(environment))
            .Returns([firstConverter])
            .Verifiable();
        mockSecondProvider
            .Setup(x => x.GetJsonConverters(environment))
            .Returns([secondConverter])
            .Verifiable();

        var converters = environment.JsonSerializerOptions.Converters;

        Assert.Equal([firstConverter, secondConverter], converters);
    }

    #endregion

    private sealed class StubJsonConverter : JsonConverter<object>
    {
        public override object Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        ) => throw new NotSupportedException();

        public override void Write(
            Utf8JsonWriter writer,
            object value,
            JsonSerializerOptions options
        ) => throw new NotSupportedException();
    }
}
