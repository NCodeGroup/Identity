// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Contexts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.EndSession;

public class DefaultParkEndSessionRequestHandlerTests : BaseTests
{
    private const string ContinueUrl = "https://op.example/oauth2/continue?state=k";

    public DefaultParkEndSessionRequestHandlerTests()
    {
        MockContinueService = CreateStrictMock<IContinueService>();
        Handler = new DefaultParkEndSessionRequestHandler(MockContinueService.Object);
    }

    private Mock<IContinueService> MockContinueService { get; }
    private DefaultParkEndSessionRequestHandler Handler { get; }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenCalled_ParksTheRequestBoundToTheSessionForTenMinutes()
    {
        var openIdContext = CreateStrictMock<OpenIdContext>().Object;
        var mockRequest = CreateStrictMock<IEndSessionRequest>();
        mockRequest.SetupGet(x => x.IdTokenHint).Returns("hint").Verifiable();
        mockRequest.SetupGet(x => x.ClientId).Returns("client-1").Verifiable();
        mockRequest
            .SetupGet(x => x.PostLogoutRedirectUri)
            .Returns("https://rp.example/after-logout")
            .Verifiable();
        mockRequest.SetupGet(x => x.State).Returns("state-1").Verifiable();
        var subject = new EndSessionSubject(
            "subject-1",
            "sid-1",
            "client-1",
            [],
            RequiresConfirmation: true,
            LoginScheme: null
        );
        var expectedContinuation = new EndSessionContinuation(
            "hint",
            "client-1",
            "https://rp.example/after-logout",
            "state-1",
            "sid-1"
        );

        MockContinueService
            .Setup(x =>
                x.GetContinueUrlAsync(
                    openIdContext,
                    OpenIdConstants.ContinueCodes.EndSession,
                    null,
                    "subject-1",
                    TimeSpan.FromMinutes(10),
                    expectedContinuation,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(ContinueUrl)
            .Verifiable();

        var command = new ParkEndSessionRequestCommand(openIdContext, mockRequest.Object, subject);
        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(ContinueUrl, result);
    }

    #endregion
}
