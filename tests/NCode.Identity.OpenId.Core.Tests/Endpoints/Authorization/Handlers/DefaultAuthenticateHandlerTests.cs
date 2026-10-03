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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Authorization.Handlers;

public class DefaultAuthenticateHandlerTests : BaseTests
{
    #region Scaffolding

    private DefaultAuthenticateHandler CreateHandler(string? subjectId) =>
        new(Options.Create(new OpenIdOptions { GetSubjectId = _ => subjectId }));

    private (
        AuthenticateCommand command,
        Mock<IAuthenticationService> authService,
        Mock<IOpenIdError> error
    ) CreateScaffold()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockAuthRequest = CreateStrictMock<IAuthorizationRequest>();
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        var mockAuthService = CreateStrictMock<IAuthenticationService>();

        var services = new ServiceCollection();
        services.AddSingleton(mockAuthService.Object);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };

        mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        var command = new AuthenticateCommand(
            mockContext.Object,
            mockClient.Object,
            mockAuthRequest.Object
        );

        return (command, mockAuthService, mockError);
    }

    private void SetupAuthenticate(
        Mock<IAuthenticationService> mockAuthService,
        AuthenticateResult result
    ) =>
        mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()))
            .ReturnsAsync(result)
            .Verifiable();

    private static AuthenticateResult SuccessResult()
    {
        var identity = new ClaimsIdentity("scheme");
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, "scheme"));
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAuthenticationNone_ReturnsUndefined()
    {
        var (command, mockAuthService, _) = CreateScaffold();
        SetupAuthenticate(mockAuthService, AuthenticateResult.NoResult());

        var result = await CreateHandler("subject-id").HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsUndefined);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthenticationFailed_ReturnsFailedError()
    {
        var (command, mockAuthService, mockError) = CreateScaffold();
        SetupAuthenticate(mockAuthService, AuthenticateResult.Fail(new Exception("boom")));

        var result = await CreateHandler("subject-id").HandleAsync(command, CancellationToken.None);

        Assert.True(result.HasError);
        Assert.Same(mockError.Object, result.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectIdMissing_ReturnsFailedError()
    {
        var (command, mockAuthService, mockError) = CreateScaffold();
        SetupAuthenticate(mockAuthService, SuccessResult());

        var result = await CreateHandler(subjectId: null)
            .HandleAsync(command, CancellationToken.None);

        Assert.True(result.HasError);
        Assert.Same(mockError.Object, result.Error);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthenticated_ReturnsTicket()
    {
        var (command, mockAuthService, _) = CreateScaffold();
        SetupAuthenticate(mockAuthService, SuccessResult());

        var result = await CreateHandler("subject-id").HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsAuthenticated);
        Assert.Equal("subject-id", result.Ticket.Value.SubjectId);
    }

    #endregion
}
