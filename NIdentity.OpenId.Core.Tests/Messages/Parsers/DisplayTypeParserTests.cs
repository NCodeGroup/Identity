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
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Exceptions;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Results;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

// TODO: unit tests for IgnoreErrors

public class DisplayTypeParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<OpenIdContext> MockOpenIdContext { get; }

    public DisplayTypeParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdContext = MockRepository.Create<OpenIdContext>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Serialize_GivenPage_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var result = parser.Serialize(MockOpenIdContext.Object, DisplayType.Page);
        Assert.Equal("page", result);
    }

    [Fact]
    public void Serialize_GivenPopup_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var result = parser.Serialize(MockOpenIdContext.Object, DisplayType.Popup);
        Assert.Equal("popup", result);
    }

    [Fact]
    public void Serialize_GivenTouch_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var result = parser.Serialize(MockOpenIdContext.Object, DisplayType.Touch);
        Assert.Equal("touch", result);
    }

    [Fact]
    public void Serialize_GivenWap_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var result = parser.Serialize(MockOpenIdContext.Object, DisplayType.Wap);
        Assert.Equal("wap", result);
    }

    [Fact]
    public void Serialize_GivenUnknown_ThenEmpty()
    {
        var parser = new DisplayTypeParser();
        var result = parser.Serialize(MockOpenIdContext.Object, DisplayType.Unspecified);
        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<DisplayType?>(parameterName, parser)
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
        var parser = new DisplayTypeParser();
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

        var knownParameter = new KnownParameter<DisplayType?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() => { parser.Parse(context, descriptor, stringValues); });
    }

    [Fact]
    public void Parse_GivenMultipleValues_ThenThrows()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "page", "wap" };

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

        var knownParameter = new KnownParameter<DisplayType?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenPageWithValidCase_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "page";

        var knownParameter = new KnownParameter<DisplayType?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false,
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(DisplayType.Page, result);
    }

    [Fact]
    public void Parse_GivenPageWithInvalidCase_ThenThrows()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "PAGE";

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

        var knownParameter = new KnownParameter<DisplayType?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false,
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenPopupWithValidCase_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "popup";

        var knownParameter = new KnownParameter<DisplayType?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false,
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(DisplayType.Popup, result);
    }

    [Fact]
    public void Parse_GivenPopupWithInvalidCase_ThenThrows()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "POPUP";

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

        var knownParameter = new KnownParameter<DisplayType?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenTouchWithValidCase_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "touch";

        var knownParameter = new KnownParameter<DisplayType?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(DisplayType.Touch, result);
    }

    [Fact]
    public void Parse_GivenTouchWithInvalidCase_ThenThrows()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "TOUCH";

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

        var knownParameter = new KnownParameter<DisplayType?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenWapWithValidCase_ThenValid()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "wap";

        var knownParameter = new KnownParameter<DisplayType?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(DisplayType.Wap, result);
    }

    [Fact]
    public void Parse_GivenWapWithInvalidCase_ThenThrows()
    {
        var parser = new DisplayTypeParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "WAP";

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

        var knownParameter = new KnownParameter<DisplayType?>(parameterName, parser)
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
        var parser = new DisplayTypeParser();
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

        var knownParameter = new KnownParameter<DisplayType?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }
}
