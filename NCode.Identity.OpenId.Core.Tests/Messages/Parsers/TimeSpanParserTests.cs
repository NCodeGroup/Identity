﻿#region Copyright Preamble

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

using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

// TODO: unit tests for IgnoreErrors

public class TimeSpanParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }

    public TimeSpanParserTests()
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
    public void Serialize_GivenNull_ThenEmpty()
    {
        var parser = new TimeSpanParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        var descriptor = new ParameterDescriptor(parameterName, ParameterLoader.Default);

        var stringValue = parser.Serialize(environment, descriptor, null);
        Assert.Equal(StringValues.Empty, stringValue);
    }

    [Fact]
    public void Serialize_GivenValue_ThenValid()
    {
        var parser = new TimeSpanParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        var descriptor = new ParameterDescriptor(parameterName, ParameterLoader.Default);

        var timeSpan = TimeSpan.FromSeconds(123.456);

        var stringValue = parser.Serialize(environment, descriptor, timeSpan);
        Assert.Equal("123", stringValue);
    }

    [Fact]
    public void Parse_GivenNull_WhenOptional_ThenValid()
    {
        var parser = new TimeSpanParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<TimeSpan?>(parameterName, parser)
        {
            AllowMissingStringValues = true,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(environment, descriptor, stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_GivenNull_WhenRequired_ThenThrows()
    {
        var parser = new TimeSpanParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdEnvironment
            .Setup(x => x.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
        mockOpenIdErrorFactory
            .Setup(x => x.Create(OpenIdConstants.ErrorCodes.InvalidRequest))
            .Returns(mockOpenIdError.Object)
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Code)
            .Returns(OpenIdConstants.ErrorCodes.InvalidRequest)
            .Verifiable();

        mockOpenIdError
            .SetupSet(x => x.Description = $"The request is missing the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<TimeSpan?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(environment, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenMultipleValues_DisallowMultipleValues_ThenThrows()
    {
        var parser = new TimeSpanParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "123", "456" };

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdEnvironment
            .Setup(x => x.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
        mockOpenIdErrorFactory
            .Setup(x => x.Create(OpenIdConstants.ErrorCodes.InvalidRequest))
            .Returns(mockOpenIdError.Object)
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Code)
            .Returns(OpenIdConstants.ErrorCodes.InvalidRequest)
            .Verifiable();

        mockOpenIdError
            .SetupSet(x => x.Description = $"The request includes the '{parameterName}' parameter more than once.")
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<TimeSpan?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(environment, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenMultipleValues_AllowMultipleValues_ThenValid()
    {
        var parser = new TimeSpanParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "123", "456" };
        var expectedValue = TimeSpan.FromSeconds(123 + 456);

        var knownParameter = new KnownParameter<TimeSpan?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
            AllowMultipleStringValues = true
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(environment, descriptor, stringValues);
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void Parse_GivenSingleValue_ThenValid()
    {
        var parser = new TimeSpanParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "123" };
        var expectedValue = TimeSpan.FromSeconds(123);

        var knownParameter = new KnownParameter<TimeSpan?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
            AllowMultipleStringValues = true
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(environment, descriptor, stringValues);
        Assert.Equal(expectedValue, result);
    }
}
