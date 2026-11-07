// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints;

public sealed class ManagementAuthenticationEndpointFilterTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    private Mock<IClientPrincipalResolver> MockClientPrincipalResolver { get; }

    private Mock<IMediator> MockMediator { get; }

    private Mock<OpenIdContext> MockContext { get; }

    private DefaultHttpContext HttpContext { get; } = new();

    private ManagementAuthenticationEndpointFilter Filter { get; }

    public ManagementAuthenticationEndpointFilterTests()
    {
        MockClientPrincipalResolver = MockRepository.Create<IClientPrincipalResolver>();
        MockMediator = MockRepository.Create<IMediator>();
        MockContext = MockRepository.Create<OpenIdContext>();

        var mockFeature = MockRepository.Create<IOpenIdContextFeature>();
        mockFeature.Setup(x => x.OpenIdContext).Returns(MockContext.Object).Verifiable();
        HttpContext.Features.Set(mockFeature.Object);

        Filter = new ManagementAuthenticationEndpointFilter(MockClientPrincipalResolver.Object);
    }

    private void SetupAuthentication(AuthenticateAccessTokenDisposition disposition)
    {
        MockContext.Setup(x => x.Mediator).Returns(MockMediator.Object).Verifiable();

        var result = new ValueTask<AuthenticateAccessTokenDisposition>(disposition);
        MockMediator
            .Setup(x =>
                x.SendAsync<AuthenticateAccessTokenCommand, AuthenticateAccessTokenDisposition>(
                    It.IsAny<AuthenticateAccessTokenCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(result)
            .Verifiable();
    }

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_WhenTheTokenAuthenticates_ResolvesTheActingClientBeforeTheRoute()
    {
        HttpContext.Request.Headers.Authorization = "Bearer access-token";
        var principal = new ClaimsPrincipal(new ClaimsIdentity("Bearer"));
        var disposition = new AuthenticateAccessTokenDisposition(principal);
        SetupAuthentication(disposition);

        var resolved = false;
        var noClient = new ValueTask<PersistedClient?>((PersistedClient?)null);
        MockClientPrincipalResolver
            .Setup(x =>
                x.GetActingClientOrDefaultAsync(
                    It.IsAny<OpenIdContext>(),
                    principal,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => resolved = true)
            .Returns(noClient)
            .Verifiable();

        var resolvedBeforeTheRoute = false;
        var expected = TypedResults.NoContent();
        EndpointFilterDelegate next = _ =>
        {
            resolvedBeforeTheRoute = resolved;
            return ValueTask.FromResult<object?>(expected);
        };
        var context = EndpointFilterInvocationContext.Create(HttpContext);

        var result = await Filter.InvokeAsync(context, next);

        Assert.Same(expected, result);
        Assert.True(resolvedBeforeTheRoute);
        Assert.Same(principal, HttpContext.User);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheTokenDoesNotAuthenticate_ResolvesNoActingClient()
    {
        HttpContext.Request.Headers.Authorization = "Bearer access-token";
        var disposition = default(AuthenticateAccessTokenDisposition);
        SetupAuthentication(disposition);

        var expected = TypedResults.NoContent();
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(expected);
        var context = EndpointFilterInvocationContext.Create(HttpContext);

        var result = await Filter.InvokeAsync(context, next);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_WithoutABearerToken_CallsTheRouteWithoutAuthenticating()
    {
        var expected = TypedResults.NoContent();
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(expected);
        var context = EndpointFilterInvocationContext.Create(HttpContext);

        var result = await Filter.InvokeAsync(context, next);

        Assert.Same(expected, result);
    }

    #endregion
}
