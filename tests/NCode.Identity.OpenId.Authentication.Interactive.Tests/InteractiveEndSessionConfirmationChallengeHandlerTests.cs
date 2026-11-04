// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public sealed class InteractiveEndSessionConfirmationChallengeHandlerTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<ITenantResolver> MockTenantResolver { get; }
    private ServiceProvider ServiceProvider { get; }
    private InteractiveEndSessionConfirmationChallengeHandler Handler { get; }

    public InteractiveEndSessionConfirmationChallengeHandlerTests()
    {
        MockTenantResolver = MockRepository.Create<ITenantResolver>();
        ServiceProvider = new ServiceCollection().AddRouting().BuildServiceProvider();
        Handler = new InteractiveEndSessionConfirmationChallengeHandler(
            new InteractiveSurfacePaths(
                MockTenantResolver.Object,
                ServiceProvider.GetRequiredService<TemplateBinderFactory>()
            )
        );
    }

    public void Dispose()
    {
        ServiceProvider.Dispose();
        MockRepository.Verify();
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenTheTenantIsServedUnderAPath_RedirectsToItsConfirmationPage()
    {
        MockTenantResolver
            .Setup(x => x.GetTenantRoute())
            .Returns(RoutePatternFactory.Parse("/{tenantId}"))
            .Verifiable();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["tenantId"] = "acme";

        var mockContext = MockRepository.Create<OpenIdContext>();
        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        var mockRequest = MockRepository.Create<IEndSessionRequest>();
        mockRequest.SetupGet(x => x.UiLocales).Returns((List<string>?)null).Verifiable();

        const string continueUrl = "https://op.example/acme/oauth2/continue?state=k";

        var disposition = await Handler.HandleAsync(
            new EndSessionConfirmationChallengeCommand(
                mockContext.Object,
                mockRequest.Object,
                continueUrl
            ),
            CancellationToken.None
        );

        Assert.True(disposition.WasHandled);
        var redirect = Assert.IsType<RedirectHttpResult>(disposition.HttpResult);
        Assert.Equal(
            "/acme/account/logout/confirm?ReturnUrl=" + Uri.EscapeDataString(continueUrl),
            redirect.Url
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTheRequestPrefersLanguages_CarriesThemToTheConfirmationPage()
    {
        MockTenantResolver
            .Setup(x => x.GetTenantRoute())
            .Returns(RoutePatternFactory.Parse(""))
            .Verifiable();

        var httpContext = new DefaultHttpContext();

        var mockContext = MockRepository.Create<OpenIdContext>();
        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        var mockRequest = MockRepository.Create<IEndSessionRequest>();
        mockRequest.SetupGet(x => x.UiLocales).Returns(["fr-CA", "de"]).Verifiable();

        const string continueUrl = "https://op.example/oauth2/continue?state=k";

        var disposition = await Handler.HandleAsync(
            new EndSessionConfirmationChallengeCommand(
                mockContext.Object,
                mockRequest.Object,
                continueUrl
            ),
            CancellationToken.None
        );

        var redirect = Assert.IsType<RedirectHttpResult>(disposition.HttpResult);
        Assert.Equal(
            "/account/logout/confirm?ReturnUrl="
                + Uri.EscapeDataString(continueUrl)
                + "&ui_locales=fr-CA%20de",
            redirect.Url
        );
    }

    #endregion
}
