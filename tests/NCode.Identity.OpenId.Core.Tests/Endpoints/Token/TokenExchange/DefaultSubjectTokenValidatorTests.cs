#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using Moq;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.TokenExchange;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.TokenExchange;

public class DefaultSubjectTokenValidatorTests : BaseTests
{
    private Mock<IJsonWebTokenService> MockJsonWebTokenService { get; }
    private DefaultSubjectTokenValidator Validator { get; }

    public DefaultSubjectTokenValidatorTests()
    {
        MockJsonWebTokenService = CreateStrictMock<IJsonWebTokenService>();
        Validator = new DefaultSubjectTokenValidator(MockJsonWebTokenService.Object);
    }

    [Fact]
    public async Task ValidateAsync_WhenTokenTypeNotJwt_ReturnsFailWithoutValidatingJwt()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        var mockClient = CreateStrictMock<OpenIdClient>();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        var result = await Validator.ValidateAsync(
            mockContext.Object,
            mockClient.Object,
            "some-saml-assertion",
            "urn:ietf:params:oauth:token-type:saml2",
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.Same(mockError.Object, result.Error);
    }
}
