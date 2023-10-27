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
using NIdentity.OpenId.Exceptions;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Results;
using Xunit;
using UriParser = NIdentity.OpenId.Messages.Parsers.UriParser;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

// TODO: unit tests for IgnoreErrors

public class UriParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<OpenIdContext> MockOpenIdContext { get; }

    public UriParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdContext = MockRepository.Create<OpenIdContext>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Serialize_GivenNull_ThenEmpty()
    {
        var parser = new UriParser();
        var context = MockOpenIdContext.Object;

        var stringValue = parser.Serialize(context, null);
        Assert.Equal(StringValues.Empty, stringValue);
    }

    [Fact]
    public void Serialize_GivenValue_ThenValid()
    {
        var parser = new UriParser();
        var context = MockOpenIdContext.Object;

        var uri = new Uri("http://localhost/path1/path2?key1=value1&key2");

        var stringValue = parser.Serialize(context, uri);
        Assert.Equal(uri.AbsoluteUri, stringValue);
    }

    [Fact]
    public void Parse_GivenNull_WhenOptional_ThenValid()
    {
        var parser = new UriParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<Uri?>(
            parameterName,
            optional: true,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_GivenNull_WhenRequired_ThenThrows()
    {
        var parser = new UriParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdContext
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

        var knownParameter = new KnownParameter<Uri?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenMultipleValues_ThenThrows()
    {
        var parser = new UriParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "123", "456" };

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdContext
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

        var knownParameter = new KnownParameter<Uri?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenSingleValue_ThenValid()
    {
        var parser = new UriParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string url = "http://localhost/path1/path2?key1=value1&key2";
        var stringValues = new[] { url };
        var expectedValue = new Uri(url);

        var knownParameter = new KnownParameter<Uri?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(expectedValue, result);
    }
}
