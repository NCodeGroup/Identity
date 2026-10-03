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
using Moq;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Handlers;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.UserInfo;

public class DefaultGetRequestedUserInfoClaimsHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string Jti = "jti-123";

    private (
        Mock<IPersistedGrantService> grantService,
        GetUserInfoClaimsCommand command,
        Dictionary<string, object> claims
    ) CreateScaffold(ClaimsPrincipal subject)
    {
        var mockGrantService = CreateStrictMock<IPersistedGrantService>();

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.TenantId).Returns(TenantId);

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object);

        var subjectAuthentication = new SubjectAuthentication(
            "scheme",
            new AuthenticationProperties(),
            subject,
            "subject-123"
        );

        var claims = new Dictionary<string, object>();
        var command = new GetUserInfoClaimsCommand(
            mockContext.Object,
            subjectAuthentication,
            claims
        );

        return (mockGrantService, command, claims);
    }

    private static ClaimsPrincipal CreateSubject(params Claim[] claims)
    {
        var identity = new ClaimsIdentity("scheme");
        identity.AddClaims(claims);
        return new ClaimsPrincipal(identity);
    }

    private void SetupGrant(
        Mock<IPersistedGrantService> mockGrantService,
        GetUserInfoClaimsCommand command,
        IRequestClaims? payload
    )
    {
        var grantId = new PersistedGrantId
        {
            TenantId = TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.UserInfoClaims,
            GrantKey = Jti,
        };
        mockGrantService
            .Setup(x =>
                x.CreateGrantId(TenantId, OpenIdConstants.PersistedGrantTypes.UserInfoClaims, Jti)
            )
            .Returns(grantId)
            .Verifiable();

        PersistedGrant<IRequestClaims>? grant = payload is null
            ? null
            : new PersistedGrant<IRequestClaims>
            {
                TenantId = TenantId,
                ClientId = "client-1",
                SubjectId = "subject-123",
                Payload = payload,
            };
        mockGrantService
            .Setup(x =>
                x.GetOrDefaultAsync<IRequestClaims>(
                    command.OpenIdContext,
                    grantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.FromResult(grant))
            .Verifiable();
    }

    [Fact]
    public async Task HandleAsync_CopiesRequestedClaimFromSubject()
    {
        var subject = CreateSubject(new Claim("jti", Jti), new Claim("email", "alice@example.com"));
        var (mockGrantService, command, claims) = CreateScaffold(subject);

        var mockPayload = CreateStrictMock<IRequestClaims>();
        IReadOnlyDictionary<string, IRequestClaim?> userInfo = new Dictionary<
            string,
            IRequestClaim?
        >
        {
            ["email"] = null,
        };
        mockPayload.Setup(x => x.UserInfo).Returns(userInfo).Verifiable();
        SetupGrant(mockGrantService, command, mockPayload.Object);

        var handler = new DefaultGetRequestedUserInfoClaimsHandler(mockGrantService.Object);
        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("alice@example.com", Assert.Contains("email", claims));
    }

    [Fact]
    public async Task HandleAsync_DoesNotOverrideClaimAlreadyProvided()
    {
        var subject = CreateSubject(
            new Claim("jti", Jti),
            new Claim("email", "from-subject@example.com")
        );
        var (mockGrantService, command, claims) = CreateScaffold(subject);
        claims["email"] = "from-store@example.com";

        var mockPayload = CreateStrictMock<IRequestClaims>();
        IReadOnlyDictionary<string, IRequestClaim?> userInfo = new Dictionary<
            string,
            IRequestClaim?
        >
        {
            ["email"] = null,
        };
        mockPayload.Setup(x => x.UserInfo).Returns(userInfo).Verifiable();
        SetupGrant(mockGrantService, command, mockPayload.Object);

        var handler = new DefaultGetRequestedUserInfoClaimsHandler(mockGrantService.Object);
        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("from-store@example.com", Assert.Contains("email", claims));
    }

    [Fact]
    public async Task HandleAsync_NoGrant_DoesNothing()
    {
        var subject = CreateSubject(new Claim("jti", Jti));
        var (mockGrantService, command, claims) = CreateScaffold(subject);

        SetupGrant(mockGrantService, command, payload: null);

        var handler = new DefaultGetRequestedUserInfoClaimsHandler(mockGrantService.Object);
        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(claims);
    }

    [Fact]
    public async Task HandleAsync_NoJti_DoesNotQueryGrantStore()
    {
        var subject = CreateSubject(new Claim("email", "alice@example.com"));
        var (mockGrantService, command, claims) = CreateScaffold(subject);

        var handler = new DefaultGetRequestedUserInfoClaimsHandler(mockGrantService.Object);
        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(claims);
    }
}
