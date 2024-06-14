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
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

// TODO: unit tests for IgnoreErrors

public class ResponseModeParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<OpenIdServer> MockOpenIdServer { get; }

    public ResponseModeParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdServer = MockRepository.Create<OpenIdServer>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Serialize_GivenQuery_ThenValid()
    {
        var parser = new ResponseModeParser();
        var result = parser.Serialize(MockOpenIdServer.Object, ResponseMode.Query);
        Assert.Equal("query", result);
    }

    [Fact]
    public void Serialize_GivenFragment_ThenValid()
    {
        var parser = new ResponseModeParser();
        var result = parser.Serialize(MockOpenIdServer.Object, ResponseMode.Fragment);
        Assert.Equal("fragment", result);
    }

    [Fact]
    public void Serialize_GivenFormPost_ThenValid()
    {
        var parser = new ResponseModeParser();
        var result = parser.Serialize(MockOpenIdServer.Object, ResponseMode.FormPost);
        Assert.Equal("form_post", result);
    }

    [Fact]
    public void Serialize_GivenUnknown_ThenEmpty()
    {
        var parser = new ResponseModeParser();
        var result = parser.Serialize(MockOpenIdServer.Object, ResponseMode.Unspecified);
        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        var parser = new ResponseModeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<ResponseMode?>(parameterName, parser)
        {
            Optional = true,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(server, descriptor, stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenRequired_ThenThrows()
    {
        var parser = new ResponseModeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdServer
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

        var knownParameter = new KnownParameter<ResponseMode?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenMultipleValues_ThenThrows()
    {
        var parser = new ResponseModeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdServer
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

        var knownParameter = new KnownParameter<ResponseMode?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenQueryWithValidCase_ThenValid()
    {
        var parser = new ResponseModeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "query";

        var knownParameter = new KnownParameter<ResponseMode?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(server, descriptor, stringValues);
        Assert.Equal(ResponseMode.Query, result);
    }

    [Fact]
    public void Parse_GivenQueryWithInvalidCase_ThenThrows()
    {
        var parser = new ResponseModeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "QUERY";

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdServer
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
            .SetupSet(x => x.Description = $"The request includes an invalid value for the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<ResponseMode?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenFragmentWithValidCase_ThenValid()
    {
        var parser = new ResponseModeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "fragment";

        var knownParameter = new KnownParameter<ResponseMode?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(server, descriptor, stringValues);
        Assert.Equal(ResponseMode.Fragment, result);
    }

    [Fact]
    public void Parse_GivenFragmentWithInvalidCase_ThenThrows()
    {
        var parser = new ResponseModeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "FRAGMENT";

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdServer
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
            .SetupSet(x => x.Description = $"The request includes an invalid value for the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<ResponseMode?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenFormPostWithValidCase_ThenValid()
    {
        var parser = new ResponseModeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "form_post";

        var knownParameter = new KnownParameter<ResponseMode?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(server, descriptor, stringValues);
        Assert.Equal(ResponseMode.FormPost, result);
    }

    [Fact]
    public void Parse_GivenFormPostWithInvalidCase_ThenThrows()
    {
        var parser = new ResponseModeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "FORM_POST";

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdServer
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
            .SetupSet(x => x.Description = $"The request includes an invalid value for the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<ResponseMode?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenInvalidValue_ThenThrows()
    {
        var parser = new ResponseModeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "invalid_value";

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdServer
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
            .SetupSet(x => x.Description = $"The request includes an invalid value for the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<ResponseMode?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }
}
