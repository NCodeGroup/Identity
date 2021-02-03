using System;
using Microsoft.Extensions.Primitives;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers
{
    public class DisplayTypeParserTests : IDisposable
    {
        private readonly MockRepository _mockRepository;
        private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

        public DisplayTypeParserTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
            _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void Serialize_GivenPage_ThenValid()
        {
            var parser = new DisplayTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, DisplayType.Page);
            Assert.Equal("page", result);
        }

        [Fact]
        public void Serialize_GivenPopup_ThenValid()
        {
            var parser = new DisplayTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, DisplayType.Popup);
            Assert.Equal("popup", result);
        }

        [Fact]
        public void Serialize_GivenTouch_ThenValid()
        {
            var parser = new DisplayTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, DisplayType.Touch);
            Assert.Equal("touch", result);
        }

        [Fact]
        public void Serialize_GivenWap_ThenValid()
        {
            var parser = new DisplayTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, DisplayType.Wap);
            Assert.Equal("wap", result);
        }

        [Fact]
        public void Serialize_GivenUnknown_ThenEmpty()
        {
            var parser = new DisplayTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, DisplayType.Unknown);
            Assert.Equal(StringValues.Empty, result);
        }

        [Fact]
        public void TryParse_GivenEmpty_WhenOptional_ThenSuccess()
        {
            var parser = new DisplayTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<DisplayType?>(
                parameterName,
                optional: true,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenEmpty_WhenRequired_ThenError()
        {
            var parser = new DisplayTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<DisplayType?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }

        [Fact]
        public void TryParse_GivenMultipleValues_ThenError()
        {
            var parser = new DisplayTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = new[] { "page", "wap" };

            var knownParameter = new KnownParameter<DisplayType?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }

        [Fact]
        public void TryParse_GivenPageWithValidCase_ThenSuccess()
        {
            var parser = new DisplayTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "page";

            var knownParameter = new KnownParameter<DisplayType?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(DisplayType.Page, result.Value);
        }

        [Fact]
        public void TryParse_GivenPageWithInvalidCase_ThenError()
        {
            var parser = new DisplayTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "PAGE";

            var knownParameter = new KnownParameter<DisplayType?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }

        [Fact]
        public void TryParse_GivenPopupWithValidCase_ThenSuccess()
        {
            var parser = new DisplayTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "popup";

            var knownParameter = new KnownParameter<DisplayType?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(DisplayType.Popup, result.Value);
        }

        [Fact]
        public void TryParse_GivenPopupWithInvalidCase_ThenError()
        {
            var parser = new DisplayTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "POPUP";

            var knownParameter = new KnownParameter<DisplayType?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }

        [Fact]
        public void TryParse_GivenTouchWithValidCase_ThenSuccess()
        {
            var parser = new DisplayTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "touch";

            var knownParameter = new KnownParameter<DisplayType?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(DisplayType.Touch, result.Value);
        }

        [Fact]
        public void TryParse_GivenTouchWithInvalidCase_ThenError()
        {
            var parser = new DisplayTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "TOUCH";

            var knownParameter = new KnownParameter<DisplayType?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }

        [Fact]
        public void TryParse_GivenWapWithValidCase_ThenSuccess()
        {
            var parser = new DisplayTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "wap";

            var knownParameter = new KnownParameter<DisplayType?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Equal(DisplayType.Wap, result.Value);
        }

        [Fact]
        public void TryParse_GivenWapWithInvalidCase_ThenError()
        {
            var parser = new DisplayTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "WAP";

            var knownParameter = new KnownParameter<DisplayType?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }

        [Fact]
        public void TryParse_GivenInvalidValue_ThenError()
        {
            var parser = new DisplayTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "invalid_value";

            var knownParameter = new KnownParameter<DisplayType?>(
                parameterName,
                optional: false,
                allowMultipleValues: false,
                parser);

            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(
                context,
                descriptor,
                stringValues,
                out var result);

            Assert.False(success);
            Assert.True(result.HasError);
        }
    }
}
