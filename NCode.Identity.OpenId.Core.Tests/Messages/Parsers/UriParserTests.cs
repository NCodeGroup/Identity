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

using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;
using Xunit;
using UriParser = NCode.Identity.OpenId.Messages.Parsers.UriParser;

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

// TODO: unit tests for IgnoreErrors

public class UriParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }

    public UriParserTests()
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
        var parser = new UriParser();
        var environment = MockOpenIdEnvironment.Object;
        var descriptor = new ParameterDescriptor(new KnownParameter<Uri?>("parameterName", parser) { AllowMissingStringValues = false });

        var stringValue = parser.Serialize(environment, descriptor, null);
        Assert.Equal(StringValues.Empty, stringValue);
    }

    [Fact]
    public void Serialize_GivenValue_ThenValid()
    {
        var parser = new UriParser();
        var environment = MockOpenIdEnvironment.Object;
        var descriptor = new ParameterDescriptor(new KnownParameter<Uri?>("parameterName", parser) { AllowMissingStringValues = false });

        var uri = new Uri("http://localhost/path1/path2?key1=value1&key2");

        var stringValue = parser.Serialize(environment, descriptor, uri);
        Assert.Equal(uri.AbsoluteUri, stringValue);
    }

    [Fact]
    public void Parse_GivenNull_WhenOptional_ThenValid()
    {
        var parser = new UriParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<Uri?>(parameterName, parser)
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
        var parser = new UriParser();
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

        var knownParameter = new KnownParameter<Uri?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(environment, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenMultipleValues_ThenThrows()
    {
        var parser = new UriParser();
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

        var knownParameter = new KnownParameter<Uri?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(environment, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenSingleValue_ThenValid()
    {
        var parser = new UriParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        const string url = "http://localhost/path1/path2?key1=value1&key2";
        var stringValues = new[] { url };
        var expectedValue = new Uri(url);

        var knownParameter = new KnownParameter<Uri?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(environment, descriptor, stringValues);
        Assert.Equal(expectedValue, result);
    }
}
