// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
        var (context, _) = CreateContext(OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt);

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
            OpenIdProtocol.ClientAuthenticationMethods.None
        );

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await requirement.EvaluateAsync(context, CancellationToken.None)
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.UnauthorizedClient, capturedCode());
    }

    #endregion
}
