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

using System.Text;
using System.Text.Json;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parsers;

public class DurationSecondsParserTests : BaseTests
{
    private readonly OpenIdEnvironment _environment;
    private readonly DurationSecondsParser _parser = CommonParameterParsers.DurationSeconds;
    private readonly ParameterDescriptor _descriptor;

    public DurationSecondsParserTests()
    {
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);
        mockEnvironment.Setup(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        _environment = mockEnvironment.Object;
        _descriptor = new ParameterDescriptor(
            new KnownParameter<TimeSpan?>("expires_in", _parser) { AllowMissingStringValues = true }
        );
    }

    private string Write(TimeSpan? value)
    {
        var parameter = _parser.Create(_environment, _descriptor, _parser, value);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            _parser.Write(
                writer,
                _environment,
                parameter,
                SerializationFormat.OpenId,
                JsonSerializerOptions.Default
            );
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private TimeSpan? Read(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        Assert.True(reader.Read());
        var parameter = _parser.Read(
            ref reader,
            _environment,
            _descriptor,
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
            _parser.GetStringValues(_environment, _descriptor, TimeSpan.FromHours(1))
        );
    }

    [Fact]
    public void Parse_WhenSecondsString_ReturnsTimeSpan()
    {
        Assert.Equal(TimeSpan.FromHours(1), _parser.Parse(_environment, _descriptor, "3600"));
    }

    #endregion
}
