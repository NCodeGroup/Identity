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
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Results;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

public class CodeChallengeMethodParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<OpenIdContext> MockOpenIdContext { get; }

    public CodeChallengeMethodParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdContext = MockRepository.Create<OpenIdContext>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Serialize_GivenPlain_ThenValid()
    {
        var parser = new CodeChallengeMethodParser();
        var result = parser.Serialize(MockOpenIdContext.Object, CodeChallengeMethod.Plain);
        Assert.Equal("plain", result);
    }

    [Fact]
    public void Serialize_GivenS256_ThenValid()
    {
        var parser = new CodeChallengeMethodParser();
        var result = parser.Serialize(MockOpenIdContext.Object, CodeChallengeMethod.Sha256);
        Assert.Equal("S256", result);
    }

    [Fact]
    public void Serialize_GivenUnknown_ThenEmpty()
    {
        var parser = new CodeChallengeMethodParser();
        var result = parser.Serialize(MockOpenIdContext.Object, CodeChallengeMethod.Unspecified);
        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        var parser = new CodeChallengeMethodParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(parameterName, parser)
        {
            Optional = true,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenRequired_ThenThrows()
    {
        var parser = new CodeChallengeMethodParser();
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

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenEmpty_WhenRequiredWithIgnoreErrors_ThenValid()
    {
        var parser = new CodeChallengeMethodParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_GivenMultipleValues_ThenThrows()
    {
        var parser = new CodeChallengeMethodParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

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

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenPlainWithValidCase_ThenValid()
    {
        var parser = new CodeChallengeMethodParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "plain";

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(CodeChallengeMethod.Plain, result);
    }

    [Fact]
    public void Parse_GivenPlainWithInvalidCase_ThenThrows()
    {
        var parser = new CodeChallengeMethodParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "PLAIN";

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
            .SetupSet(x => x.Description = $"The request includes an invalid value for the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenS256WithValidCase_ThenValid()
    {
        var parser = new CodeChallengeMethodParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "S256";

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(CodeChallengeMethod.Sha256, result);
    }

    [Fact]
    public void Parse_GivenS256WithInvalidCase_ThenThrows()
    {
        var parser = new CodeChallengeMethodParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "s256";

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
            .SetupSet(x => x.Description = $"The request includes an invalid value for the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenInvalidValue_ThenThrows()
    {
        var parser = new CodeChallengeMethodParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "invalid_value";

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
            .SetupSet(x => x.Description = $"The request includes an invalid value for the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<CodeChallengeMethod?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }
}
