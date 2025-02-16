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

namespace NCode.Identity.OpenId.Tests.Messages;

public class OpenIdMessageTests : BaseTests
{
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }

    public OpenIdMessageTests()
    {
        MockOpenIdEnvironment = CreateStrictMock<OpenIdEnvironment>();
    }

    [Fact]
    public void Initialize_WhenNotInitialized_ThenValid()
    {
        var environment = MockOpenIdEnvironment.Object;
        var message = new OpenIdMessage();

        message.Initialize(environment, Array.Empty<Parameter>());

        Assert.Empty(message.Parameters);
        Assert.Same(environment, message.OpenIdEnvironment);
    }

    [Fact]
    public void Initialize_WhenAlreadyInitialized_ThenThrows()
    {
        var environment = MockOpenIdEnvironment.Object;
        var message = new OpenIdMessage();

        message.Initialize(environment, Array.Empty<Parameter>());

        Assert.Throws<InvalidOperationException>(() =>
            message.Initialize(environment, Array.Empty<Parameter>()));
    }

    [Fact]
    public void DefaultConstructor_WhenNoContext_ThenThrows()
    {
        var message = new OpenIdMessage();

        Assert.Throws<InvalidOperationException>(() =>
            message.OpenIdEnvironment);
    }

    [Fact]
    public void DefaultConstructor_WhenNoParameters_ThenEmpty()
    {
        var message = new OpenIdMessage();

        Assert.Empty(message.Parameters);
    }

    [Fact]
    public void GetKnownParameter_WhenNotFound_ThenValid()
    {
        var mockParameterParser = CreateStrictMock<ParameterParser<TestNestedObject?>>();

        const string parameterName = "parameterName";

        var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, mockParameterParser.Object)
        {
            AllowMissingStringValues = true,
        };

        var message = new OpenIdMessage();
        var environment = MockOpenIdEnvironment.Object;
        message.Initialize(environment, Array.Empty<Parameter>());

        var result = message.GetKnownParameter(knownParameter);
        Assert.Null(result);
    }

    [Fact]
    public void GetKnownParameter_WhenParsedValueIsSet_ThenReturnParsedValue()
    {
        const string parameterName = "parameterName";
        var parsedValue = new TestNestedObject();

        var mockParameterParser = CreateStrictMock<ParameterParser<TestNestedObject>>();
        var knownParameter = new KnownParameter<TestNestedObject>(parameterName, mockParameterParser.Object)
        {
            AllowMissingStringValues = true,
        };

        var message = new OpenIdMessage();
        var environment = MockOpenIdEnvironment.Object;
        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter<TestNestedObject>(descriptor, mockParameterParser.Object, parsedValue);
        message.Initialize(environment, [parameter]);

        var result = message.GetKnownParameter(knownParameter);
        Assert.Same(parsedValue, result);
    }

    [Fact]
    public void GetKnownParameter_WhenParsedValueIsNotSet_ThenReturnDefault()
    {
        const string parameterName = "parameterName";
        var parsedValue = new TestNestedObject
        {
            NestedPropertyName1 = "NestedPropertyValue"
        };

        var mockParameterParser = CreateStrictMock<ParameterParser<TestNestedObject>>();
        var knownParameter = new KnownParameter<TestNestedObject>(parameterName, mockParameterParser.Object)
        {
            AllowMissingStringValues = false,
        };

        var message = new OpenIdMessage();
        var environment = MockOpenIdEnvironment.Object;
        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter<TestNestedObject>(descriptor, mockParameterParser.Object, parsedValue);
        message.Initialize(environment, [parameter]);

        var result = message.GetKnownParameter(knownParameter);
        Assert.Null(result);
        Assert.Null(parameter.ParsedValue);
    }

    [Fact]
    public void SetKnownParameter_WhenContextIsNull_ThenThrows()
    {
        var mockParameterParser = CreateStrictMock<ParameterParser<TestNestedObject?>>();

        const string parameterName = "parameterName";
        var parsedValue = new TestNestedObject
        {
            NestedPropertyName1 = "NestedPropertyValue"
        };

        var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, mockParameterParser.Object)
        {
            AllowMissingStringValues = true,
        };

        var message = new OpenIdMessage();

        Assert.Throws<InvalidOperationException>(() =>
            message.SetKnownParameter(knownParameter, parsedValue));
    }

    [Fact]
    public void SetKnownParameter_GivenNullParsedValue_ThenRemovesParameter()
    {
        const string parameterName = "parameterName";
        var parsedValue = new TestNestedObject
        {
            NestedPropertyName1 = "NestedPropertyValue"
        };

        var mockParameterParser = CreateStrictMock<ParameterParser<TestNestedObject>>();
        var knownParameter = new KnownParameter<TestNestedObject>(parameterName, mockParameterParser.Object)
        {
            AllowMissingStringValues = true,
        };

        var message = new OpenIdMessage();
        var environment = MockOpenIdEnvironment.Object;
        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter<TestNestedObject>(descriptor, mockParameterParser.Object, parsedValue);
        message.Initialize(environment, [parameter]);

        message.SetKnownParameter(knownParameter, null);

        Assert.Empty(message.Parameters);
    }

    [Fact]
    public void SetKnownParameter_WhenNullParserResult_ThenRemovesParameter()
    {
        const string parameterName = "parameterName";
        var parsedValue = new TestNestedObject
        {
            NestedPropertyName1 = "NestedPropertyValue"
        };

        var mockParameterParser = CreateStrictMock<ParameterParser<TestNestedObject>>();
        var knownParameter = new KnownParameter<TestNestedObject>(parameterName, mockParameterParser.Object)
        {
            AllowMissingStringValues = true,
        };

        var message = new OpenIdMessage();
        var environment = MockOpenIdEnvironment.Object;
        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter<TestNestedObject>(descriptor, mockParameterParser.Object, parsedValue);
        message.Initialize(environment, [parameter]);

        mockParameterParser
            .Setup(x => x.GetStringValues(environment, descriptor, parsedValue))
            .Returns(StringValues.Empty)
            .Verifiable();

        message.SetKnownParameter(knownParameter, parsedValue);

        Assert.Empty(message.Parameters);
    }

    [Fact]
    public void SetKnownParameter_WhenEmpty_ThenParameterAdded()
    {
        const string parameterName = nameof(parameterName);

        var parsedValue = new TestNestedObject
        {
            NestedPropertyName1 = "NestedPropertyValue"
        };
        var stringValues = JsonSerializer.Serialize(parsedValue);

        var mockParameterParser = CreateStrictMock<ParameterParser<TestNestedObject>>();
        var mockParameter = CreateStrictMock<IParameter<TestNestedObject>>();

        var knownParameter = new KnownParameter<TestNestedObject>(parameterName, mockParameterParser.Object)
        {
            AllowMissingStringValues = true,
        };

        var message = new OpenIdMessage();
        var environment = MockOpenIdEnvironment.Object;
        message.Initialize(environment, Array.Empty<Parameter>());

        var descriptor = new ParameterDescriptor(knownParameter);

        mockParameterParser
            .Setup(x => x.GetStringValues(environment, descriptor, parsedValue))
            .Returns(stringValues)
            .Verifiable();

        mockParameterParser
            .Setup(x => x.Create(environment, descriptor, mockParameterParser.Object, parsedValue))
            .Returns(mockParameter.Object)
            .Verifiable();

        message.SetKnownParameter(knownParameter, parsedValue);

        var (actualParameterName, actualParameter) = Assert.Single(message.Parameters);
        Assert.Equal(parameterName, actualParameterName);
        Assert.Same(mockParameter.Object, actualParameter);
    }

    [Fact]
    public void SetKnownParameter_WhenExisting_ThenParameterReplaced()
    {
        const string parameterName = nameof(parameterName);

        var parsedValue = new TestNestedObject
        {
            NestedPropertyName1 = "NestedPropertyValue1"
        };
        var stringValues = JsonSerializer.Serialize(parsedValue);

        var mockParameterParser = CreateStrictMock<ParameterParser<TestNestedObject>>();
        var mockParameter = CreateStrictMock<IParameter<TestNestedObject>>();

        var knownParameter = new KnownParameter<TestNestedObject>(parameterName, mockParameterParser.Object)
        {
            AllowMissingStringValues = true,
        };

        var message = new OpenIdMessage();
        var environment = MockOpenIdEnvironment.Object;
        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = new Parameter<TestNestedObject>(descriptor, mockParameterParser.Object, parsedValue);
        message.Initialize(environment, [parameter]);

        parsedValue.NestedPropertyName1 = "NestedPropertyValue2";

        mockParameterParser
            .Setup(x => x.GetStringValues(environment, descriptor, parsedValue))
            .Returns(stringValues)
            .Verifiable();

        mockParameterParser
            .Setup(x => x.Create(environment, descriptor, mockParameterParser.Object, parsedValue))
            .Returns(mockParameter.Object)
            .Verifiable();

        message.SetKnownParameter(knownParameter, parsedValue);

        var (actualParameterName, actualParameter) = Assert.Single(message.Parameters);
        Assert.Equal(parameterName, actualParameterName);
        Assert.Same(mockParameter.Object, actualParameter);
    }
}
