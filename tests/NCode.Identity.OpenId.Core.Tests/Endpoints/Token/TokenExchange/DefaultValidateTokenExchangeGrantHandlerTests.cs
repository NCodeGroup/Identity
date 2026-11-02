// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.TokenExchange;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.TokenExchange;

public class DefaultValidateTokenExchangeGrantHandlerTests : BaseTests
{
    private DefaultValidateTokenExchangeGrantHandler Handler { get; } = new();

    #region Scaffolding

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    private (
        Mock<OpenIdContext> context,
        Mock<IMediator> mediator,
        ValidateTokenGrantCommand<TokenExchangeGrant> command
    ) CreateScaffold()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockMediator = CreateStrictMock<IMediator>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();

        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();

        var command = new ValidateTokenGrantCommand<TokenExchangeGrant>(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            new TokenExchangeGrant
            {
                SubjectAuthentication = CreateSubjectAuthentication(),
                RequestedTokenType = OpenIdProtocol.TokenTypeIdentifiers.AccessToken,
            }
        );

        return (mockContext, mockMediator, command);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenTheSubjectIsValid_CompletesSuccessfully()
    {
        var (_, mockMediator, command) = CreateScaffold();

        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateSubjectAuthenticationCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTheSubjectIsRefused_ThrowsOpenIdException()
    {
        var (_, mockMediator, command) = CreateScaffold();

        var mockError = CreateLooseMock<IOpenIdError>();

        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateSubjectAuthenticationCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ValidateSubjectAuthenticationCommand cmd, CancellationToken _) =>
                    cmd.OperationDisposition.Error = mockError.Object
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    #endregion
}
