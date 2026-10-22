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
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class ConfidentialClientRequirementTests : BaseTests
{
    #region Scaffolding

    private (AuthorizationRequestProfileContext context, Func<string?> capturedCode) CreateContext(
        string registeredAuthMethod
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

        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod))
            .Returns(registeredAuthMethod)
            .Verifiable();

        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();

        var mockRequest = CreateLooseMock<IAuthorizationRequest>();

        var mockHandler = CreateLooseMock<IAuthorizationResponseModeHandler>();

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

    [Fact]
    public async Task EvaluateAsync_WhenTheClientIsRegisteredAsConfidential_Completes()
    {
        var requirement = new ConfidentialClientRequirement();
        var (context, _) = CreateContext(OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt);

        var exception = await Record.ExceptionAsync(async () =>
            await requirement.EvaluateAsync(context, CancellationToken.None)
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task EvaluateAsync_WhenTheClientIsRegisteredAsPublic_ThrowsUnauthorizedClient()
    {
        var requirement = new ConfidentialClientRequirement();
        var (context, capturedCode) = CreateContext(
            OpenIdConstants.ClientAuthenticationMethods.None
        );

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await requirement.EvaluateAsync(context, CancellationToken.None)
        );

        Assert.Equal(OpenIdConstants.ErrorCodes.UnauthorizedClient, capturedCode());
    }

    #endregion
}
