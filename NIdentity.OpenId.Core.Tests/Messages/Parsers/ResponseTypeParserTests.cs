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
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Results;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

// TODO: unit tests for IgnoreErrors

public class ResponseTypeParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IOpenIdMessageContext> MockOpenIdContext { get; }

    public ResponseTypeParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdContext = MockRepository.Create<IOpenIdMessageContext>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Serialize_GivenNone_ThenValid()
    {
        var parser = new ResponseTypeParser();
        var result = parser.Serialize(MockOpenIdContext.Object, ResponseTypes.None);
        Assert.Equal("none", result);
    }

    [Fact]
    public void Serialize_GivenCode_ThenValid()
    {
        var parser = new ResponseTypeParser();
        var result = parser.Serialize(MockOpenIdContext.Object, ResponseTypes.Code);
        Assert.Equal("code", result);
    }

    [Fact]
    public void Serialize_GivenIdToken_ThenValid()
    {
        var parser = new ResponseTypeParser();
        var result = parser.Serialize(MockOpenIdContext.Object, ResponseTypes.IdToken);
        Assert.Equal("id_token", result);
    }

    [Fact]
    public void Serialize_GivenToken_ThenValid()
    {
        var parser = new ResponseTypeParser();
        var result = parser.Serialize(MockOpenIdContext.Object, ResponseTypes.Token);
        Assert.Equal("token", result);
    }

    [Fact]
    public void Serialize_GivenUnknown_ThenEmpty()
    {
        var parser = new ResponseTypeParser();
        var result = parser.Serialize(MockOpenIdContext.Object, ResponseTypes.Unspecified);
        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        var parser = new ResponseTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<ResponseTypes?>(
            parameterName,
            optional: true,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenRequired_ThenThrows()
    {
        var parser = new ResponseTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdContext
            .Setup(_ => _.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
        mockOpenIdErrorFactory
            .Setup(_ => _.Create(OpenIdConstants.ErrorCodes.InvalidRequest))
            .Returns(mockOpenIdError.Object)
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Code)
            .Returns(OpenIdConstants.ErrorCodes.InvalidRequest)
            .Verifiable();

        mockOpenIdError
            .SetupSet(_ => _.Description = $"The request is missing the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<ResponseTypes?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenMultipleValues_ThenValid()
    {
        var parser = new ResponseTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "code id_token token";
        const ResponseTypes expectedResult = ResponseTypes.Code | ResponseTypes.IdToken | ResponseTypes.Token;

        var knownParameter = new KnownParameter<ResponseTypes?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void Parse_GivenNoneWithValidCase_ThenValid()
    {
        var parser = new ResponseTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "none";

        var knownParameter = new KnownParameter<ResponseTypes?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(ResponseTypes.None, result);
    }

    [Fact]
    public void Parse_GivenNoneWithInvalidCase_ThenThrows()
    {
        var parser = new ResponseTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "NONE";

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdContext
            .Setup(_ => _.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
        mockOpenIdErrorFactory
            .Setup(_ => _.Create(OpenIdConstants.ErrorCodes.InvalidRequest))
            .Returns(mockOpenIdError.Object)
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Code)
            .Returns(OpenIdConstants.ErrorCodes.InvalidRequest)
            .Verifiable();

        mockOpenIdError
            .SetupSet(_ => _.Description = $"The request includes an invalid value for the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<ResponseTypes?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenCodeWithValidCase_ThenValid()
    {
        var parser = new ResponseTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "code";

        var knownParameter = new KnownParameter<ResponseTypes?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(ResponseTypes.Code, result);
    }

    [Fact]
    public void Parse_GivenCodeWithInvalidCase_ThenThrows()
    {
        var parser = new ResponseTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "CODE";

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdContext
            .Setup(_ => _.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
        mockOpenIdErrorFactory
            .Setup(_ => _.Create(OpenIdConstants.ErrorCodes.InvalidRequest))
            .Returns(mockOpenIdError.Object)
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Code)
            .Returns(OpenIdConstants.ErrorCodes.InvalidRequest)
            .Verifiable();

        mockOpenIdError
            .SetupSet(_ => _.Description = $"The request includes an invalid value for the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<ResponseTypes?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenIdTokenWithValidCase_ThenValid()
    {
        var parser = new ResponseTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "id_token";

        var knownParameter = new KnownParameter<ResponseTypes?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(ResponseTypes.IdToken, result);
    }

    [Fact]
    public void Parse_GivenIdTokenWithInvalidCase_ThenThrows()
    {
        var parser = new ResponseTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "ID_TOKEN";

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdContext
            .Setup(_ => _.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
        mockOpenIdErrorFactory
            .Setup(_ => _.Create(OpenIdConstants.ErrorCodes.InvalidRequest))
            .Returns(mockOpenIdError.Object)
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Code)
            .Returns(OpenIdConstants.ErrorCodes.InvalidRequest)
            .Verifiable();

        mockOpenIdError
            .SetupSet(_ => _.Description = $"The request includes an invalid value for the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<ResponseTypes?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenTokenWithValidCase_ThenValid()
    {
        var parser = new ResponseTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "token";

        var knownParameter = new KnownParameter<ResponseTypes?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(ResponseTypes.Token, result);
    }

    [Fact]
    public void Parse_GivenTokenWithInvalidCase_ThenThrows()
    {
        var parser = new ResponseTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "TOKEN";

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdContext
            .Setup(_ => _.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
        mockOpenIdErrorFactory
            .Setup(_ => _.Create(OpenIdConstants.ErrorCodes.InvalidRequest))
            .Returns(mockOpenIdError.Object)
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Code)
            .Returns(OpenIdConstants.ErrorCodes.InvalidRequest)
            .Verifiable();

        mockOpenIdError
            .SetupSet(_ => _.Description = $"The request includes an invalid value for the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<ResponseTypes?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenInvalidValue_ThenThrows()
    {
        var parser = new ResponseTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "invalid_value";

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdContext
            .Setup(_ => _.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
        mockOpenIdErrorFactory
            .Setup(_ => _.Create(OpenIdConstants.ErrorCodes.InvalidRequest))
            .Returns(mockOpenIdError.Object)
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Code)
            .Returns(OpenIdConstants.ErrorCodes.InvalidRequest)
            .Verifiable();

        mockOpenIdError
            .SetupSet(_ => _.Description = $"The request includes an invalid value for the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<ResponseTypes?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }
}
