#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

public class ParameterParserTests : IDisposable
{
    private MockRepository MockRepository { get; }

    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }

    public ParameterParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdEnvironment = MockRepository.Create<OpenIdEnvironment>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Load_ThenValid()
    {
        const string parameterName = "parameterName";
        StringValues stringValues = "stringValues";
        const string parsedValue = "parsedValue";

        var environment = MockOpenIdEnvironment.Object;
        var descriptor = new ParameterDescriptor(parameterName, ParameterLoader.Default);
        var parameterParser = new TestParameterParser<string>();
        var parameter = Parameter.Create(environment, descriptor, parameterParser, parsedValue);

        parameterParser.ParseCallback = (env, desc, values) =>
        {
            Assert.Same(environment, env);
            Assert.Equal(descriptor, desc);
            Assert.Equal(stringValues, values);
            return parsedValue;
        };

        parameterParser.CreateCallback = (env, desc, values, value) =>
        {
            Assert.Same(environment, env);
            Assert.Equal(descriptor, desc);
            Assert.Equal(parsedValue, value);
            return parameter;
        };

        var result = parameterParser.Load(environment, descriptor, stringValues);
        Assert.Same(result, parameter);
    }

    [Fact]
    public void Read_String_OpenId()
    {
        const string parameterName = "parameterName";
        StringValues stringValues = "stringValues";
        StringValues parsedValue = "parsedValue";
        const SerializationFormat format = SerializationFormat.OpenId;

        var options = JsonSerializerOptions.Web;
        var jsonData = JsonSerializer.SerializeToUtf8Bytes(parsedValue);
        var reader = new Utf8JsonReader(jsonData);
        Assert.True(reader.Read());

        var environment = MockOpenIdEnvironment.Object;
        var parser = ParameterParsers.StringValues;
        var descriptor = new ParameterDescriptor(parameterName, parser);
        var parameter = Parameter.Create(environment, descriptor, parser, parsedValue);

        var parameterParser = new TestParameterParser<string>();

        parameterParser.DeserializeCallback = () => stringValues;

        parameterParser.ParseCallback = (env, desc, values) =>
        {
            Assert.Same(environment, env);
            Assert.Equal(descriptor, desc);
            Assert.Equal(stringValues, values);
            return parsedValue;
        };

        parameterParser.CreateCallback = (env, desc, values, value) =>
        {
            Assert.Same(environment, env);
            Assert.Equal(descriptor, desc);
            Assert.Equal(parsedValue, value);
            return parameter;
        };

        var result = parameterParser.Read(ref reader, environment, descriptor, format, options);
        Assert.Same(result, parameter);
    }

    [Fact]
    public void Read_String_Json()
    {
        const string parameterName = "parameterName";
        StringValues stringValues = "stringValues";
        const string parsedValue = nameof(parsedValue);
        const SerializationFormat format = SerializationFormat.Json;

        var options = JsonSerializerOptions.Web;
        var jsonData = JsonSerializer.SerializeToUtf8Bytes(parsedValue);
        var reader = new Utf8JsonReader(jsonData);
        Assert.True(reader.Read());

        var environment = MockOpenIdEnvironment.Object;
        var parser = ParameterParsers.StringValues;
        var descriptor = new ParameterDescriptor(parameterName, parser);
        var parameter = Parameter.Create(environment, descriptor, parser, parsedValue);

        var parameterParser = new TestParameterParser<string>();

        parameterParser.DeserializeCallback = () => parsedValue;

        parameterParser.SerializeCallback = (env, desc, value) =>
        {
            Assert.Same(environment, env);
            Assert.Equal(descriptor, desc);
            Assert.Same(parsedValue, value);
            return stringValues;
        };

        parameterParser.CreateCallback = (env, desc, values, value) =>
        {
            Assert.Same(environment, env);
            Assert.Equal(descriptor, desc);
            Assert.Equal(parsedValue, value);
            return parameter;
        };

        var result = parameterParser.Read(ref reader, environment, descriptor, format, options);
        Assert.Same(result, parameter);
    }

    [Fact]
    public void Read_NonString_Json()
    {
        const string parameterName = "parameterName";
        StringValues stringValues = "stringValues";
        StringValues parsedValue = "parsedValue";
        const SerializationFormat format = SerializationFormat.Json;

        var options = JsonSerializerOptions.Web;
        var jsonData = JsonSerializer.SerializeToUtf8Bytes(parsedValue);
        var reader = new Utf8JsonReader(jsonData);
        Assert.True(reader.Read());

        var environment = MockOpenIdEnvironment.Object;
        var parser = ParameterParsers.StringValues;
        var descriptor = new ParameterDescriptor(parameterName, parser);
        var parameter = Parameter.Create(environment, descriptor, parser, parsedValue);

        var parameterParser = new TestParameterParser<object>();

        parameterParser.DeserializeCallback = () => parsedValue;

        parameterParser.SerializeCallback = (env, desc, value) =>
        {
            Assert.Same(environment, env);
            Assert.Equal(descriptor, desc);
            Assert.Same(parsedValue, value);
            return stringValues;
        };

        parameterParser.CreateCallback = (env, desc, values, value) =>
        {
            Assert.Same(environment, env);
            Assert.Equal(descriptor, desc);
            Assert.Equal(parsedValue, value);
            return parameter;
        };

        var result = parameterParser.Read(ref reader, environment, descriptor, format, options);
        Assert.Same(result, parameter);
    }
}
