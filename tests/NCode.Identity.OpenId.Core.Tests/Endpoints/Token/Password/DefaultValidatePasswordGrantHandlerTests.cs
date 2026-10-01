#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Password;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.Password;

public class DefaultValidatePasswordGrantHandlerTests : BaseTests
{
    private DefaultValidatePasswordGrantHandler Handler { get; } = new();

    #region Scaffolding

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    private (
        Mock<OpenIdContext> context,
        Mock<IMediator> mediator,
        ValidateTokenGrantCommand<PasswordGrant> command
    ) CreateScaffold()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockMediator = CreateStrictMock<IMediator>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();

        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();

        var command = new ValidateTokenGrantCommand<PasswordGrant>(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            new PasswordGrant(CreateSubjectAuthentication())
        );

        return (mockContext, mockMediator, command);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenSubjectValid_CompletesSuccessfully()
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
    public async Task HandleAsync_WhenSubjectInvalid_ThrowsOpenIdException()
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
