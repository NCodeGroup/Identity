// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

public sealed class CapabilitiesApiEndpointHandlerTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IMediator> MockMediator { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private CapabilitiesApiEndpointHandler Handler { get; }

    public CapabilitiesApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockMediator = MockRepository.Create<IMediator>();
        MockContext = MockRepository.Create<OpenIdContext>();
        Handler = new CapabilitiesApiEndpointHandler();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private static DefaultHttpContext CreateHttpContext(bool authenticated) =>
        new()
        {
            User = authenticated
                ? new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test"))
                : new ClaimsPrincipal(new ClaimsIdentity()),
        };

    #endregion

    #region GetCapabilitiesAsync Tests

    [Fact]
    public async Task GetCapabilitiesAsync_WhenTheCallerIsAnonymous_ReturnsAuthenticationRequiredWithoutReadingTheContext()
    {
        var httpContext = CreateHttpContext(authenticated: false);

        var result = await Handler.GetCapabilitiesAsync(httpContext, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
        Assert.Null(httpContext.Features.Get<IOpenIdContextFeature>());
    }

    [Fact]
    public async Task GetCapabilitiesAsync_WhenTheCallerIsAuthenticated_ReturnsTheDiscoveredCapabilities()
    {
        var httpContext = CreateHttpContext(authenticated: true);
        var mockFeature = MockRepository.Create<IOpenIdContextFeature>();
        mockFeature.Setup(x => x.OpenIdContext).Returns(MockContext.Object).Verifiable();
        httpContext.Features.Set(mockFeature.Object);
        MockContext.Setup(x => x.Mediator).Returns(MockMediator.Object).Verifiable();
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<DiscoverManagementCapabilitiesCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Handler.GetCapabilitiesAsync(httpContext, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<ManagementCapabilitiesResource>>(result);
        Assert.NotNull(json.Value);
    }

    #endregion

    #region Map Tests

    [Theory]
    [InlineData("GET", "/capabilities", new[] { 401 })]
    public async Task Map_Always_DeclaresAndDescribesEveryProblemARouteAnswers(
        string httpMethod,
        string routePattern,
        int[] statusCodes
    )
    {
        Assert.Equal(
            statusCodes,
            await EndpointProblemMetadata.ReadStatusCodesAsync(Handler, httpMethod, routePattern)
        );
        Assert.Empty(
            await EndpointProblemMetadata.ReadUndescribedStatusCodesAsync(
                Handler,
                httpMethod,
                routePattern
            )
        );
    }

    #endregion
}
