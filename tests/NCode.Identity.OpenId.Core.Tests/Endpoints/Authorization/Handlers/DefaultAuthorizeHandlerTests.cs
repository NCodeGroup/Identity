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
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Consent;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Authorization.Handlers;

public class DefaultAuthorizeHandlerTests : BaseTests
{
    private Mock<IConsentService> MockConsentService { get; }
    private Mock<IAuditEventRecorder> MockAuditEventRecorder { get; }
    private DefaultAuthorizeHandler Handler { get; }

    public DefaultAuthorizeHandlerTests()
    {
        var mockLogger = CreateLooseMock<ILogger<DefaultAuthorizeHandler>>();
        MockConsentService = CreateStrictMock<IConsentService>();
        MockAuditEventRecorder = CreateStrictMock<IAuditEventRecorder>();
        Handler = new DefaultAuthorizeHandler(
            MockConsentService.Object,
            MockAuditEventRecorder.Object,
            mockLogger.Object
        );
    }

    #region Scaffolding

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    private (
        AuthorizeCommand command,
        Mock<OpenIdContext> context,
        Mock<OpenIdClient> client,
        Mock<IReadOnlySettingCollection> settings,
        Mock<IAuthorizationRequest> authRequest,
        Mock<IMediator> mediator,
        Mock<IOpenIdError> error
    ) CreateScaffold(IReadOnlyList<string> promptTypes)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockAuthRequest = CreateStrictMock<IAuthorizationRequest>();
        var mockMediator = CreateStrictMock<IMediator>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        // ErrorFactory and PromptTypes are read at the top of every path.
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockAuthRequest.SetupGet(x => x.PromptTypes).Returns(promptTypes).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        // Consent is disabled by default; individual tests opt in.
        mockClient.SetupGet(x => x.ClientId).Returns("client-id");
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object);
        mockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.RequireConsent)).Returns(false);

        var command = new AuthorizeCommand(
            mockContext.Object,
            mockClient.Object,
            mockAuthRequest.Object,
            CreateSubjectAuthentication()
        );

        return (
            command,
            mockContext,
            mockClient,
            mockSettings,
            mockAuthRequest,
            mockMediator,
            mockError
        );
    }

    private void SetupValidateSubject(
        Mock<OpenIdContext> mockContext,
        Mock<IMediator> mockMediator,
        IOpenIdError? subjectError
    )
    {
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateSubjectAuthenticationCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ValidateSubjectAuthenticationCommand cmd, CancellationToken _) =>
                    cmd.OperationDisposition.Error = subjectError
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenCreateAccountPrompt_ReturnsChallengeRequired()
    {
        var (command, _, _, _, _, _, _) = CreateScaffold([
            OpenIdConstants.PromptTypes.CreateAccount,
        ]);

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.ChallengeRequired);
        Assert.False(result.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenLoginPrompt_ReturnsChallengeRequired()
    {
        var (command, _, _, _, _, _, _) = CreateScaffold([OpenIdConstants.PromptTypes.Login]);

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.ChallengeRequired);
        Assert.False(result.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectValid_ReturnsAuthorized()
    {
        var (command, mockContext, _, _, _, mockMediator, _) = CreateScaffold([]);

        SetupValidateSubject(mockContext, mockMediator, subjectError: null);
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordAuthorizationGrantedAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<OpenIdClient>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.ChallengeRequired);
        Assert.False(result.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectInvalidWithoutNonePrompt_ReturnsChallengeRequired()
    {
        var (command, mockContext, _, _, _, mockMediator, mockError) = CreateScaffold([]);

        SetupValidateSubject(mockContext, mockMediator, mockError.Object);

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.ChallengeRequired);
        Assert.False(result.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectInvalidWithNonePrompt_ReturnsLoginRequiredError()
    {
        var (command, mockContext, _, _, _, mockMediator, mockError) = CreateScaffold([
            OpenIdConstants.PromptTypes.None,
        ]);

        SetupValidateSubject(mockContext, mockMediator, mockError.Object);
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordAuthorizationDeniedAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<OpenIdClient>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.ChallengeRequired);
        Assert.True(result.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenConsentRequiredAndNotGranted_ReturnsConsentRequired()
    {
        var (command, mockContext, _, mockSettings, mockAuthRequest, mockMediator, _) =
            CreateScaffold([]);

        SetupValidateSubject(mockContext, mockMediator, subjectError: null);
        mockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.RequireConsent)).Returns(true);
        mockAuthRequest.SetupGet(x => x.Scopes).Returns(["openid"]).Verifiable();
        MockConsentService
            .Setup(x =>
                x.HasConsentAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false)
            .Verifiable();

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.ConsentRequired);
        Assert.False(result.ChallengeRequired);
        Assert.False(result.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenConsentRequiredAndGranted_ReturnsAuthorized()
    {
        var (command, mockContext, _, mockSettings, mockAuthRequest, mockMediator, _) =
            CreateScaffold([]);

        SetupValidateSubject(mockContext, mockMediator, subjectError: null);
        mockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.RequireConsent)).Returns(true);
        mockAuthRequest.SetupGet(x => x.Scopes).Returns(["openid"]).Verifiable();
        MockConsentService
            .Setup(x =>
                x.HasConsentAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true)
            .Verifiable();
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordAuthorizationGrantedAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<OpenIdClient>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.ConsentRequired);
        Assert.False(result.ChallengeRequired);
        Assert.False(result.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenConsentRequiredWithNonePrompt_ReturnsConsentRequiredError()
    {
        var (command, mockContext, _, mockSettings, mockAuthRequest, mockMediator, _) =
            CreateScaffold([OpenIdConstants.PromptTypes.None]);

        SetupValidateSubject(mockContext, mockMediator, subjectError: null);
        mockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.RequireConsent)).Returns(true);
        mockAuthRequest.SetupGet(x => x.Scopes).Returns(["openid"]).Verifiable();
        MockConsentService
            .Setup(x =>
                x.HasConsentAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false)
            .Verifiable();
        MockAuditEventRecorder
            .Setup(x =>
                x.RecordAuthorizationDeniedAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<OpenIdClient>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.ConsentRequired);
        Assert.False(result.ChallengeRequired);
        Assert.True(result.HasError);
    }

    #endregion
}
