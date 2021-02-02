#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System;
using Microsoft.Extensions.Primitives;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers
{
    public class PromptTypeParserTests : IDisposable
    {
        private readonly MockRepository _mockRepository;
        private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

        public PromptTypeParserTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
            _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void Serialize_GivenNone_ThenValid()
        {
            var parser = new PromptTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, PromptTypes.None);
            Assert.Equal("none", result);
        }

        [Fact]
        public void Serialize_GivenLogin_ThenValid()
        {
            var parser = new PromptTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, PromptTypes.Login);
            Assert.Equal("login", result);
        }

        [Fact]
        public void Serialize_GivenConsent_ThenValid()
        {
            var parser = new PromptTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, PromptTypes.Consent);
            Assert.Equal("consent", result);
        }

        [Fact]
        public void Serialize_GivenSelectAccount_ThenValid()
        {
            var parser = new PromptTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, PromptTypes.SelectAccount);
            Assert.Equal("select_account", result);
        }

        [Fact]
        public void Serialize_GivenUnknown_ThenEmpty()
        {
            var parser = new PromptTypeParser();
            var result = parser.Serialize(_mockOpenIdMessageContext.Object, PromptTypes.Unknown);
            Assert.Equal(StringValues.Empty, result);
        }

        [Fact]
        public void TryParse_GivenEmpty_WhenOptional_ThenSuccess()
        {
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var stringValues = Array.Empty<string>();

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenMultipleValues_WithNone_ThenError()
        {
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "none login";

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenMultipleValues_WithoutNone_ThenSuccess()
        {
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "login consent";
            const PromptTypes expectedResult = PromptTypes.Login | PromptTypes.Consent;

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            Assert.Null(result.ErrorDetails);
            Assert.Equal(expectedResult, result.Value);
        }

        [Fact]
        public void TryParse_GivenNoneWithValidCase_ThenSuccess()
        {
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "none";

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            Assert.Equal(PromptTypes.None, result.Value);
        }

        [Fact]
        public void TryParse_GivenNoneWithInvalidCase_ThenError()
        {
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "NONE";

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenLoginWithValidCase_ThenSuccess()
        {
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "login";

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            Assert.Equal(PromptTypes.Login, result.Value);
        }

        [Fact]
        public void TryParse_GivenLoginWithInvalidCase_ThenError()
        {
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "LOGIN";

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenConsentWithValidCase_ThenSuccess()
        {
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "consent";

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            Assert.Equal(PromptTypes.Consent, result.Value);
        }

        [Fact]
        public void TryParse_GivenConsentWithInvalidCase_ThenError()
        {
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "CONSENT";

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenSelectAccountWithValidCase_ThenSuccess()
        {
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "select_account";

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            Assert.Equal(PromptTypes.SelectAccount, result.Value);
        }

        [Fact]
        public void TryParse_GivenSelectAccountWithInvalidCase_ThenError()
        {
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "SELECT_ACCOUNT";

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenInvalidValue_ThenError()
        {
            var parser = new PromptTypeParser();
            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            const string stringValues = "invalid_value";

            var knownParameter = new KnownParameter<PromptTypes?>(
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
            Assert.Null(result.Value);
        }
    }
}
