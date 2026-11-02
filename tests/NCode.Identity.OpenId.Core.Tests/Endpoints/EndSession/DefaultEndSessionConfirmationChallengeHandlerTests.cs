// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Contexts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.EndSession;

public class DefaultEndSessionConfirmationChallengeHandlerTests : BaseTests
{
    private const string ContinueUrl = "https://op.example/oauth2/continue?state=a&b";

    private DefaultEndSessionConfirmationChallengeHandler Handler { get; } = new();

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenCalled_RendersAFormThatPostsToTheContinueUrl()
    {
        var httpContext = new DefaultHttpContext();
        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        var request = CreateLooseMock<IEndSessionRequest>();

        var disposition = await Handler.HandleAsync(
            new EndSessionConfirmationChallengeCommand(
                mockContext.Object,
                request.Object,
                ContinueUrl
            ),
            CancellationToken.None
        );

        Assert.True(disposition.WasHandled);
        var content = Assert.IsType<ContentHttpResult>(disposition.HttpResult);
        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        Assert.NotNull(content.ResponseContent);
        Assert.Contains(
            "action=\"https://op.example/oauth2/continue?state=a&amp;b\"",
            content.ResponseContent
        );
        Assert.Contains("id=\"logout-confirm\"", content.ResponseContent);

        var policy = httpContext.Response.Headers.ContentSecurityPolicy.ToString();
        Assert.Contains("default-src 'none'", policy);
        Assert.DoesNotContain("form-action", policy);
    }

    #endregion
}
