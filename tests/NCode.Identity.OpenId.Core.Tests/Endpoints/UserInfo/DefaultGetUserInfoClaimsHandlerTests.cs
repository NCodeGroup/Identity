// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Handlers;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.UserInfo;

public class DefaultGetUserInfoClaimsHandlerTests : BaseTests
{
    [Fact]
    public async Task HandleAsync_AddsSubClaimFromSubjectAuthentication()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();

        var subjectAuthentication = new SubjectAuthentication(
            "scheme",
            new AuthenticationProperties(),
            new ClaimsPrincipal(new ClaimsIdentity("scheme")),
            "subject-123"
        );

        var claims = new Dictionary<string, object>();
        var command = new GetUserInfoClaimsCommand(
            mockContext.Object,
            subjectAuthentication,
            claims
        );

        var handler = new DefaultGetUserInfoClaimsHandler();
        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("subject-123", Assert.Contains("sub", claims));
    }
}
