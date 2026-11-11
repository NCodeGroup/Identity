// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.EndSession;

public class DefaultEndSessionEndpointProviderTests : BaseTests
{
    private const string FrontChannelLogoutUri =
        "https://rp.example/frontchannel?iss=https%3A%2F%2Fop.example&sid=s1";

    private const string RedirectUri = "https://rp.example/after-logout?state=a\"b";

    public DefaultEndSessionEndpointProviderTests()
    {
        MockMediator = CreateStrictMock<IMediator>();

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.SetupGet(x => x.Mediator).Returns(MockMediator.Object).Verifiable();
        OpenIdContext = mockContext.Object;

        HttpContext = new DefaultHttpContext();
        HttpContext.Features.Set<IOpenIdContextFeature>(
            Mock.Of<IOpenIdContextFeature>(feature => feature.OpenIdContext == OpenIdContext)
        );

        Provider = new DefaultEndSessionEndpointProvider();
    }

    private Mock<IMediator> MockMediator { get; }
    private OpenIdContext OpenIdContext { get; }
    private DefaultHttpContext HttpContext { get; }
    private DefaultEndSessionEndpointProvider Provider { get; }

    #region Scaffolding

    private void SetupMediator(EndSessionDisposition disposition)
    {
        SetupLoadAndResolve(
            new EndSessionSubject(
                "subject-1",
                "sid-1",
                null,
                [],
                RequiresConfirmation: false,
                LoginSchemes: [null]
            )
        );
        SetupEndSession(disposition);
    }

    private void SetupLoadAndResolve(EndSessionSubject subject)
    {
        MockMediator
            .Setup(x =>
                x.SendAsync<LoadEndSessionRequestCommand, IEndSessionRequest>(
                    It.IsAny<LoadEndSessionRequestCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateLooseMock<IEndSessionRequest>().Object)
            .Verifiable();

        MockMediator
            .Setup(x =>
                x.SendAsync<ResolveEndSessionSubjectCommand, EndSessionSubject>(
                    It.IsAny<ResolveEndSessionSubjectCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(subject)
            .Verifiable();
    }

    private void SetupEndSession(EndSessionDisposition disposition) =>
        MockMediator
            .Setup(x =>
                x.SendAsync<EndSessionCommand, EndSessionDisposition>(
                    It.IsAny<EndSessionCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(disposition)
            .Verifiable();

    private void SetupChallenge(ReadOnlyEndpointDisposition challenge)
    {
        MockMediator
            .Setup(x =>
                x.SendAsync<ParkEndSessionRequestCommand, string>(
                    It.Is<ParkEndSessionRequestCommand>(c =>
                        c.OpenIdContext == OpenIdContext && c.Subject.SessionId == "sid-1"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("https://op.example/oauth2/continue?state=k")
            .Verifiable();

        MockMediator
            .Setup(x =>
                x.SendAsync<EndSessionConfirmationChallengeCommand, ReadOnlyEndpointDisposition>(
                    It.Is<EndSessionConfirmationChallengeCommand>(c =>
                        c.ContinueUrl == "https://op.example/oauth2/continue?state=k"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(challenge)
            .Verifiable();
    }

    #endregion

    #region HandleRouteAsync Tests

    [Fact]
    public async Task HandleRouteAsync_WhenTheLogoutRequiresConfirmation_ReturnsTheChallengeWithoutEndingTheSession()
    {
        SetupLoadAndResolve(
            new EndSessionSubject(
                "subject-1",
                "sid-1",
                null,
                [],
                RequiresConfirmation: true,
                LoginSchemes: [null]
            )
        );
        var challengeResult = TypedResults.Redirect("/account/logout/confirm");
        SetupChallenge(ReadOnlyEndpointDisposition.Handled(challengeResult));

        var result = await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        Assert.Same(challengeResult, result);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenTheChallengeIsUnhandled_EndsTheSessionUnprompted()
    {
        SetupLoadAndResolve(
            new EndSessionSubject(
                "subject-1",
                "sid-1",
                null,
                [],
                RequiresConfirmation: true,
                LoginSchemes: [null]
            )
        );
        SetupChallenge(ReadOnlyEndpointDisposition.UnHandled());
        SetupEndSession(new EndSessionDisposition(null, []));

        var result = await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        var content = Assert.IsType<ContentHttpResult>(result);
        Assert.Equal("You have been logged out.", content.ResponseContent);
    }

    [Fact]
    public async Task HandleRouteAsync_WhenThePostLogoutRedirectIsPermitted_RendersTheIframesAndTheEncodedRedirect()
    {
        SetupMediator(new EndSessionDisposition(RedirectUri, [FrontChannelLogoutUri]));

        var result = await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        var content = Assert.IsType<ContentHttpResult>(result);
        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        Assert.NotNull(content.ResponseContent);
        Assert.Contains(
            $"<iframe hidden src=\"{HtmlEncoder.Default.Encode(FrontChannelLogoutUri)}\"></iframe>",
            content.ResponseContent
        );
        Assert.Contains(
            $"data-url=\"{HtmlEncoder.Default.Encode(RedirectUri)}\"",
            content.ResponseContent
        );
        Assert.DoesNotContain(RedirectUri, content.ResponseContent);
        Assert.DoesNotContain("You have been logged out.", content.ResponseContent);
        Assert.DoesNotContain("style=", content.ResponseContent);

        const string scriptStart = "<script>";
        var start =
            content.ResponseContent.IndexOf(scriptStart, StringComparison.Ordinal)
            + scriptStart.Length;
        var end = content.ResponseContent.IndexOf("</script>", start, StringComparison.Ordinal);
        var script = content.ResponseContent[start..end];
        var hash = Convert.ToBase64String(SHA256.HashData(SecureEncoding.UTF8.GetBytes(script)));

        var headers = HttpContext.Response.Headers;
        Assert.Equal(
            $"default-src 'none'; frame-src https://rp.example; script-src 'sha256-{hash}'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
            headers.ContentSecurityPolicy.ToString()
        );
        Assert.Equal("DENY", headers.XFrameOptions.ToString());
    }

    [Fact]
    public async Task HandleRouteAsync_WhenThePostLogoutRedirectIsRefused_RendersTheIframesWithoutARedirect()
    {
        SetupMediator(new EndSessionDisposition(null, [FrontChannelLogoutUri]));

        var result = await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        var content = Assert.IsType<ContentHttpResult>(result);
        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        Assert.NotNull(content.ResponseContent);
        Assert.Contains(
            $"<iframe hidden src=\"{HtmlEncoder.Default.Encode(FrontChannelLogoutUri)}\"></iframe>",
            content.ResponseContent
        );
        Assert.Contains("<p>You have been logged out.</p>", content.ResponseContent);
        Assert.DoesNotContain("data-url", content.ResponseContent);
        Assert.DoesNotContain("<script>", content.ResponseContent);
        Assert.Equal(
            "default-src 'none'; frame-src https://rp.example; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
            HttpContext.Response.Headers.ContentSecurityPolicy.ToString()
        );
    }

    [Fact]
    public async Task HandleRouteAsync_WhenSeveralLogoutUrisShareAnOrigin_FramesEachOriginOnce()
    {
        SetupMediator(
            new EndSessionDisposition(
                null,
                ["https://rp.example/a", "https://rp.example/b?x=1", "https://other.example:8443/c"]
            )
        );

        await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        Assert.Equal(
            "default-src 'none'; frame-src https://rp.example https://other.example:8443; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
            HttpContext.Response.Headers.ContentSecurityPolicy.ToString()
        );
    }

    #endregion
}
