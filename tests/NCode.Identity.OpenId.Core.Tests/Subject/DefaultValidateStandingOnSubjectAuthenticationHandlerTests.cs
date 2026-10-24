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

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using NCode.Registration.AspNetCore;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Subject;

public class DefaultValidateStandingOnSubjectAuthenticationHandlerTests : BaseTests
{
    private const string PrincipalId = "principal-1";

    private DefaultValidateStandingOnSubjectAuthenticationHandler Handler { get; } = new();

    #region Scaffolding

    private ValidateSubjectAuthenticationCommand CreateCommand(
        Mock<OpenIdContext> mockContext,
        OperationDisposition<IOpenIdError> disposition
    )
    {
        var subjectAuthentication = new SubjectAuthentication(
            "scheme",
            new AuthenticationProperties(),
            new ClaimsPrincipal(new ClaimsIdentity("test")),
            PrincipalId
        );

        return new ValidateSubjectAuthenticationCommand(
            mockContext.Object,
            CreateStrictMock<OpenIdClient>().Object,
            CreateStrictMock<IOpenIdRequest>().Object,
            subjectAuthentication,
            disposition,
            EnforceAuthenticationAge: false
        );
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNoErrorIsRecorded_ValidatesTheStandingWithTheSameDisposition()
    {
        var mockMediator = CreateStrictMock<IMediator>();
        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();

        ValidateSubjectStandingCommand? sent = null;
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateSubjectStandingCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ValidateSubjectStandingCommand command, CancellationToken _) => sent = command
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var disposition = new OperationDisposition<IOpenIdError>();
        var command = CreateCommand(mockContext, disposition);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Same(mockContext.Object, sent?.OpenIdContext);
        Assert.Same(disposition, sent?.OperationDisposition);
        Assert.Equal(PrincipalId, sent?.SubjectAuthentication.PrincipalId);
    }

    [Fact]
    public async Task HandleAsync_WhenAnErrorIsRecorded_SendsNothing()
    {
        var mockInitialError = CreateLooseMock<IOpenIdError>();
        var disposition = new OperationDisposition<IOpenIdError>
        {
            Error = mockInitialError.Object,
        };
        var command = CreateCommand(CreateStrictMock<OpenIdContext>(), disposition);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Same(mockInitialError.Object, disposition.Error);
    }

    #endregion
}
