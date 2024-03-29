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
using NIdentity.OpenId.Exceptions;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Servers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

// TODO: unit tests for IgnoreErrors

public class PromptTypeParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<OpenIdServer> MockOpenIdServer { get; }

    public PromptTypeParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdServer = MockRepository.Create<OpenIdServer>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Serialize_GivenNone_ThenValid()
    {
        var parser = new PromptTypeParser();
        var result = parser.Serialize(MockOpenIdServer.Object, PromptTypes.None);
        Assert.Equal("none", result);
    }

    [Fact]
    public void Serialize_GivenLogin_ThenValid()
    {
        var parser = new PromptTypeParser();
        var result = parser.Serialize(MockOpenIdServer.Object, PromptTypes.Login);
        Assert.Equal("login", result);
    }

    [Fact]
    public void Serialize_GivenConsent_ThenValid()
    {
        var parser = new PromptTypeParser();
        var result = parser.Serialize(MockOpenIdServer.Object, PromptTypes.Consent);
        Assert.Equal("consent", result);
    }

    [Fact]
    public void Serialize_GivenSelectAccount_ThenValid()
    {
        var parser = new PromptTypeParser();
        var result = parser.Serialize(MockOpenIdServer.Object, PromptTypes.SelectAccount);
        Assert.Equal("select_account", result);
    }

    [Fact]
    public void Serialize_GivenUnknown_ThenEmpty()
    {
        var parser = new PromptTypeParser();
        var result = parser.Serialize(MockOpenIdServer.Object, PromptTypes.Unspecified);
        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        var parser = new PromptTypeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<PromptTypes?>(parameterName, parser)
        {
            Optional = true,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(server, descriptor, stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenRequired_ThenThrows()
    {
        var parser = new PromptTypeParser();
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

        var knownParameter = new KnownParameter<PromptTypes?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenMultipleValues_ThenValid()
    {
        var parser = new PromptTypeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "login select_account";
        const PromptTypes expectedResult = PromptTypes.Login | PromptTypes.SelectAccount;

        var knownParameter = new KnownParameter<PromptTypes?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(server, descriptor, stringValues);
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void Parse_GivenNoneWithValidCase_ThenValid()
    {
        var parser = new PromptTypeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "none";

        var knownParameter = new KnownParameter<PromptTypes?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(server, descriptor, stringValues);
        Assert.Equal(PromptTypes.None, result);
    }

    [Fact]
    public void Parse_GivenNoneWithInvalidCase_ThenThrows()
    {
        var parser = new PromptTypeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "NONE";

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

        var knownParameter = new KnownParameter<PromptTypes?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenLoginWithValidCase_ThenValid()
    {
        var parser = new PromptTypeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "login";

        var knownParameter = new KnownParameter<PromptTypes?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(server, descriptor, stringValues);
        Assert.Equal(PromptTypes.Login, result);
    }

    [Fact]
    public void Parse_GivenLoginWithInvalidCase_ThenThrows()
    {
        var parser = new PromptTypeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "LOGIN";

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

        var knownParameter = new KnownParameter<PromptTypes?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenConsentWithValidCase_ThenValid()
    {
        var parser = new PromptTypeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "consent";

        var knownParameter = new KnownParameter<PromptTypes?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(server, descriptor, stringValues);
        Assert.Equal(PromptTypes.Consent, result);
    }

    [Fact]
    public void Parse_GivenConsentWithInvalidCase_ThenThrows()
    {
        var parser = new PromptTypeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "CONSENT";

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

        var knownParameter = new KnownParameter<PromptTypes?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenSelectAccountWithValidCase_ThenValid()
    {
        var parser = new PromptTypeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "select_account";

        var knownParameter = new KnownParameter<PromptTypes?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(server, descriptor, stringValues);
        Assert.Equal(PromptTypes.SelectAccount, result);
    }

    [Fact]
    public void Parse_GivenSelectAccountWithInvalidCase_ThenThrows()
    {
        var parser = new PromptTypeParser();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        const string stringValues = "SELECT_ACCOUNT";

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

        var knownParameter = new KnownParameter<PromptTypes?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenInvalidValue_ThenThrows()
    {
        var parser = new PromptTypeParser();
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

        var knownParameter = new KnownParameter<PromptTypes?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }
}
