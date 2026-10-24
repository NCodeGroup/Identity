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

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Contexts;
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
        mockContext.SetupGet(x => x.Mediator).Returns(MockMediator.Object);
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
                x.SendAsync<EndSessionCommand, EndSessionDisposition>(
                    It.IsAny<EndSessionCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(disposition)
            .Verifiable();
    }

    #endregion

    #region HandleRouteAsync Tests

    [Fact]
    public async Task HandleRouteAsync_WhenThePostLogoutRedirectIsPermitted_RendersTheIframesAndTheEncodedRedirect()
    {
        SetupMediator(new EndSessionDisposition(RedirectUri, [FrontChannelLogoutUri]));

        var result = await Provider.HandleRouteAsync(HttpContext, CancellationToken.None);

        var content = Assert.IsType<ContentHttpResult>(result);
        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        Assert.NotNull(content.ResponseContent);
        Assert.Contains(
            $"<iframe style=\"display:none\" src=\"{HtmlEncoder.Default.Encode(FrontChannelLogoutUri)}\"></iframe>",
            content.ResponseContent
        );
        Assert.Contains(
            $"data-url=\"{HtmlEncoder.Default.Encode(RedirectUri)}\"",
            content.ResponseContent
        );
        Assert.DoesNotContain(RedirectUri, content.ResponseContent);
        Assert.DoesNotContain("You have been logged out.", content.ResponseContent);
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
            $"<iframe style=\"display:none\" src=\"{HtmlEncoder.Default.Encode(FrontChannelLogoutUri)}\"></iframe>",
            content.ResponseContent
        );
        Assert.Contains("<p>You have been logged out.</p>", content.ResponseContent);
        Assert.DoesNotContain("data-url", content.ResponseContent);
        Assert.DoesNotContain("<script>", content.ResponseContent);
    }

    #endregion
}
