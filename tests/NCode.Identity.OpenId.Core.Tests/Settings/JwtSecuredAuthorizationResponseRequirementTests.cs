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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class JwtSecuredAuthorizationResponseRequirementTests : BaseTests
{
    #region Scaffolding

    private (AuthorizationRequestProfileContext context, Func<string?> capturedCode) CreateContext(
        string? securedResponseMode,
        params string[] responseTypes
    )
    {
        string? capturedCode = null;
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => capturedCode = code)
            .Returns(mockError.Object);

        var mockContext = CreateLooseMock<OpenIdContext>();
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);

        var mockRequest = CreateStrictMock<IAuthorizationRequest>();
        IReadOnlyList<string> values = responseTypes;
        mockRequest.SetupGet(x => x.ResponseTypes).Returns(values).Verifiable();

        var mockHandler = CreateStrictMock<IAuthorizationResponseModeHandler>();
        mockHandler.SetupGet(x => x.SecuredResponseMode).Returns(securedResponseMode).Verifiable();

        var mockClient = CreateLooseMock<OpenIdClient>();

        return (
            new AuthorizationRequestProfileContext(
                mockContext.Object,
                mockClient.Object,
                mockRequest.Object,
                new AuthorizationResponseDelivery(
                    "https://client.example/callback",
                    mockHandler.Object,
                    State: null
                )
            ),
            () => capturedCode
        );
    }

    #endregion

    #region EvaluateAsync Tests

    [Theory]
    [InlineData(OpenIdConstants.ResponseModes.Query)]
    [InlineData(OpenIdConstants.ResponseModes.Fragment)]
    [InlineData(OpenIdConstants.ResponseModes.FormPost)]
    public async Task EvaluateAsync_WhenCodeUsesAJwtSecuredResponseMode_Completes(
        string securedResponseMode
    )
    {
        var requirement = new JwtSecuredAuthorizationResponseRequirement();
        var (context, _) = CreateContext(securedResponseMode, OpenIdConstants.ResponseTypes.Code);

        var exception = await Record.ExceptionAsync(async () =>
            await requirement.EvaluateAsync(context, CancellationToken.None)
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task EvaluateAsync_WhenCodeIdTokenUsesAPlainResponseMode_Completes()
    {
        var requirement = new JwtSecuredAuthorizationResponseRequirement();
        var (context, _) = CreateContext(
            securedResponseMode: null,
            OpenIdConstants.ResponseTypes.Code,
            OpenIdConstants.ResponseTypes.IdToken
        );

        var exception = await Record.ExceptionAsync(async () =>
            await requirement.EvaluateAsync(context, CancellationToken.None)
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task EvaluateAsync_WhenCodeUsesAPlainResponseMode_ThrowsInvalidRequest()
    {
        var requirement = new JwtSecuredAuthorizationResponseRequirement();
        var (context, capturedCode) = CreateContext(
            securedResponseMode: null,
            OpenIdConstants.ResponseTypes.Code
        );

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await requirement.EvaluateAsync(context, CancellationToken.None)
        );

        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidRequest, capturedCode());
    }

    #endregion
}
