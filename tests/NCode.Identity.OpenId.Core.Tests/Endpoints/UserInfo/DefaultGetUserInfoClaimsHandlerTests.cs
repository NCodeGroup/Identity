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

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Handlers;
using NCode.Identity.OpenId.Authentication.Subject;
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
