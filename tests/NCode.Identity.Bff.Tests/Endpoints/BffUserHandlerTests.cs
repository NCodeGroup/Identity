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

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.Buffers;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Endpoints;

public class BffUserHandlerTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static BffSessionState CreateState() =>
        new(
            new string('a', 43),
            "store-key",
            1,
            new BffSessionMetadata
            {
                Frontend = BffDefaults.FrontendName,
                Issuer = "https://op.example",
                Subject = "sub-1",
                SessionId = "sid-1",
                CreatedAt = Now,
                LastAccessedAt = Now,
                AbsoluteExpiresAt = Now.AddHours(8),
                IdleExpiresAt = Now.AddHours(1),
            },
            new BffSessionPayload
            {
                IdToken = "id-token",
                LogoutGuard = "guard+/1",
                Claims =
                [
                    new BffStoredClaim { Type = "sub", Value = "sub-1" },
                    new BffStoredClaim { Type = "name", Value = "Ada" },
                    new BffStoredClaim { Type = "email", Value = "ada@example.com" },
                    new BffStoredClaim { Type = "role", Value = "admin" },
                    new BffStoredClaim { Type = "role", Value = "user" },
                    new BffStoredClaim { Type = "secret", Value = "hidden" },
                ],
            }
        );

    private BffUserHandler CreateHandler(HttpContext httpContext, BffSessionState? session)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new BffOptions { UserClaims = ["email", "role", "phone_number"] }
        );
        var timeProvider = new MutableTimeProvider(Now);
        var cookies = new BffCookies(options, timeProvider);
        var manager = CreateStrictMock<IBffSessionManager>();
        var resolver = new BffSessionResolver(manager.Object, cookies, [], options, timeProvider);

        var feature = new BffRequestFeature { IsResolved = true, Session = session };
        httpContext.Features.Set(feature);
        return new BffUserHandler(resolver, options);
    }

    private static async Task<string> ReadBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, SecureEncoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_NoSession_WritesUnauthorizedProblem()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var handler = CreateHandler(httpContext, null);

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal("no-store", httpContext.Response.Headers.CacheControl.ToString());
        var body = await ReadBodyAsync(httpContext);
        Assert.Contains(BffProblemTypes.SessionRequired, body);
    }

    [Fact]
    public async Task HandleAsync_WithSession_WritesAllowListedClaims()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var handler = CreateHandler(httpContext, CreateState());

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", httpContext.Response.ContentType);
        var body = await ReadBodyAsync(httpContext);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("sub-1", root.GetProperty("sub").GetString());
        Assert.Equal("Ada", root.GetProperty("name").GetString());

        var claims = root.GetProperty("claims");
        Assert.Equal("ada@example.com", claims.GetProperty("email").GetString());
        var roles = claims.GetProperty("role").EnumerateArray().Select(x => x.GetString());
        Assert.Equal(["admin", "user"], roles);
        Assert.False(claims.TryGetProperty("secret", out _));
        Assert.False(claims.TryGetProperty("phone_number", out _));

        var session = root.GetProperty("session");
        Assert.Equal(
            Now.AddHours(8).ToUnixTimeSeconds(),
            session.GetProperty("expiresAt").GetInt64()
        );
        Assert.Equal(
            Now.AddHours(1).ToUnixTimeSeconds(),
            session.GetProperty("idleExpiresAt").GetInt64()
        );

        Assert.Equal("/bff/logout?sid=guard%2B%2F1", root.GetProperty("logoutUrl").GetString());
        Assert.DoesNotContain("id-token", body);
        Assert.DoesNotContain("sid-1", body);
        Assert.DoesNotContain("op.example", body);
    }

    #endregion
}
