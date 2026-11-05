// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public sealed class InteractiveConsentChallengeHandlerTests : IDisposable
{
    private const string ContinueUrl = "https://op.example/acme/oauth2/continue?state=k";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<ITenantResolver> MockTenantResolver { get; }
    private ServiceProvider ServiceProvider { get; }
    private InteractiveConsentChallengeHandler Handler { get; }

    public InteractiveConsentChallengeHandlerTests()
    {
        MockTenantResolver = MockRepository.Create<ITenantResolver>();
        ServiceProvider = new ServiceCollection().AddRouting().BuildServiceProvider();
        Handler = new InteractiveConsentChallengeHandler(
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

    private ConsentChallengeCommand CreateCommand(IReadOnlyList<string> uiLocales)
    {
        MockTenantResolver
            .Setup(x => x.GetTenantRoute())
            .Returns(RoutePatternFactory.Parse("/{tenantId}"))
            .Verifiable();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["tenantId"] = "acme";

        var mockContext = MockRepository.Create<OpenIdContext>();
        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        var mockRequest = MockRepository.Create<IAuthorizationRequest>();
        mockRequest.SetupGet(x => x.UiLocales).Returns(uiLocales).Verifiable();

        return new ConsentChallengeCommand(
            mockContext.Object,
            MockRepository.Create<OpenIdClient>().Object,
            mockRequest.Object,
            ContinueUrl
        );
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_Always_RedirectsToTheTenantsConsentPageWithTheContinueUrl()
    {
        var command = CreateCommand([]);

        var disposition = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.WasHandled);
        var redirect = Assert.IsType<RedirectHttpResult>(disposition.HttpResult);
        Assert.Equal(
            "/acme/account/consent?ReturnUrl=" + Uri.EscapeDataString(ContinueUrl),
            redirect.Url
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTheRequestPrefersLanguages_CarriesThemToTheConsentPage()
    {
        var command = CreateCommand(["fr-CA", "de"]);

        var disposition = await Handler.HandleAsync(command, CancellationToken.None);

        var redirect = Assert.IsType<RedirectHttpResult>(disposition.HttpResult);
        Assert.Equal(
            "/acme/account/consent?ReturnUrl="
                + Uri.EscapeDataString(ContinueUrl)
                + "&ui_locales=fr-CA%20de",
            redirect.Url
        );
    }

    #endregion
}
