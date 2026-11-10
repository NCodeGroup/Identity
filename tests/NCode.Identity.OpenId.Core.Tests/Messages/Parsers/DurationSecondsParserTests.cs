// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parsers;

public class DurationSecondsParserTests : BaseTests
{
    private OpenIdEnvironment Environment { get; }
    private DurationSecondsParser Parser { get; } = CommonParameterParsers.DurationSeconds;
    private ParameterDescriptor Descriptor { get; }

    public DurationSecondsParserTests()
    {
        Environment = CreateStrictMock<OpenIdEnvironment>().Object;
        Descriptor = new ParameterDescriptor(
            new KnownParameter<TimeSpan?>("expires_in", Parser) { AllowMissingStringValues = true }
        );
    }

    private string Write(TimeSpan? value)
    {
        var parameter = Parser.Create(Environment, Descriptor, Parser, value);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            Parser.Write(
                writer,
                Environment,
                parameter,
                SerializationFormat.OpenId,
                JsonSerializerOptions.Default
            );
            writer.WriteEndObject();
        }
        return SecureEncoding.UTF8.GetString(stream.ToArray());
    }

    private TimeSpan? Read(string json)
    {
        var reader = new Utf8JsonReader(SecureEncoding.UTF8.GetBytes(json));
        Assert.True(reader.Read());
        var parameter = Parser.Read(
            ref reader,
            Environment,
            Descriptor,
            SerializationFormat.OpenId,
            JsonSerializerOptions.Default
        );
        return ((IParameter<TimeSpan?>)parameter).ParsedValue;
    }

    #region Write Tests

    [Fact]
    public void Write_WhenValue_EmitsJsonNumberOfSeconds()
    {
        Assert.Equal("{\"expires_in\":3600}", Write(TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Write_WhenNull_EmitsJsonNull()
    {
        Assert.Equal("{\"expires_in\":null}", Write(null));
    }

    #endregion

    #region Read Tests

    [Fact]
    public void Read_WhenJsonNumber_ReturnsTimeSpan()
    {
        Assert.Equal(TimeSpan.FromHours(1), Read("3600"));
    }

    [Fact]
    public void Read_WhenJsonNull_ReturnsNull()
    {
        Assert.Null(Read("null"));
    }

    #endregion

    #region GetStringValues / Parse Tests

    [Fact]
    public void GetStringValues_WhenValue_ReturnsWholeSeconds()
    {
        Assert.Equal(
            "3600",
            Parser.GetStringValues(Environment, Descriptor, TimeSpan.FromHours(1))
        );
    }

    [Fact]
    public void Parse_WhenSecondsString_ReturnsTimeSpan()
    {
        Assert.Equal(TimeSpan.FromHours(1), Parser.Parse(Environment, Descriptor, "3600"));
    }

    #endregion
}
