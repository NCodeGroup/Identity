// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Jose;
using Xunit;

namespace NCode.Identity.Bff.Tests.Endpoints;

public class DefaultBffUserDocumentFactoryTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private static BffSessionState CreateState() =>
        new(
            new string('a', 43),
            "store-key",
            1,
            new BffSessionMetadata
            {
                Frontend = BffTestFrontends.Name,
                Authority = BffTestFrontends.Authority,
                ClientId = BffTestFrontends.ClientId,
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
                    new BffStoredClaim { Type = JoseClaimNames.Payload.Sub, Value = "sub-1" },
                    new BffStoredClaim { Type = "name", Value = "Ada" },
                    new BffStoredClaim { Type = "email", Value = "ada@example.com" },
                    new BffStoredClaim { Type = "role", Value = "admin" },
                    new BffStoredClaim { Type = "role", Value = "user" },
                    new BffStoredClaim { Type = "secret", Value = "hidden" },
                ],
            }
        );

    private static DefaultHttpContext CreateGuardedHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(
            new Endpoint(
                null,
                new EndpointMetadataCollection(BffUserEndpointMetadata.Instance),
                "test"
            )
        );
        return httpContext;
    }

    private DefaultBffUserDocumentFactory CreateFactory(
        HttpContext httpContext,
        BffSessionState? session,
        params IBffUserContributor[] contributors
    ) => CreateFrontendFactory(httpContext, session, null, contributors);

    private DefaultBffUserDocumentFactory CreateFrontendFactory(
        HttpContext httpContext,
        BffSessionState? session,
        Action<BffFrontendOptions>? configure,
        params IBffUserContributor[] contributors
    )
    {
        var frontends = BffTestFrontends.CreateCatalog(frontend =>
        {
            frontend.UserClaims = ["email", "role", "phone_number"];
            configure?.Invoke(frontend);
        });
        var timeProvider = new FakeTimeProvider(Now);
        var cookies = new BffCookies(timeProvider);
        var manager = CreateStrictMock<IBffSessionManager>();
        var resolver = new BffSessionResolver(manager.Object, cookies, [], frontends, timeProvider);

        var feature = new BffRequestFeature
        {
            IsResolved = true,
            Session = session,
            CheckedEndpoint = httpContext.GetEndpoint(),
        };
        httpContext.Features.Set(feature);
        return new DefaultBffUserDocumentFactory(resolver, contributors, frontends);
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_UnguardedEndpoint_Throws()
    {
        var httpContext = new DefaultHttpContext();
        var factory = CreateFactory(httpContext, CreateState());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.CreateAsync(httpContext, CancellationToken.None)
        );
    }

    [Fact]
    public async Task CreateAsync_NoSession_ReturnsNull()
    {
        var httpContext = CreateGuardedHttpContext();
        var factory = CreateFactory(httpContext, null);

        var document = await factory.CreateAsync(httpContext, CancellationToken.None);

        Assert.Null(document);
    }

    [Fact]
    public async Task CreateAsync_WithSession_WritesAllowListedClaims()
    {
        var httpContext = CreateGuardedHttpContext();
        var factory = CreateFactory(httpContext, CreateState());

        var document = await factory.CreateAsync(httpContext, CancellationToken.None);

        var root = Assert.NotNull(document);
        Assert.Equal("sub-1", root.GetProperty(JoseClaimNames.Payload.Sub).GetString());
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

        Assert.Equal("/bff/logout", root.GetProperty("logoutUrl").GetString());
        Assert.Equal("guard+/1", root.GetProperty("logoutSid").GetString());

        var text = root.GetRawText();
        Assert.DoesNotContain("id-token", text);
        Assert.DoesNotContain("sid-1", text);
        Assert.DoesNotContain("op.example", text);
    }

    [Fact]
    public async Task CreateAsync_FrontendWithItsOwnPrefix_ReturnsItsLogoutUrl()
    {
        var httpContext = CreateGuardedHttpContext();
        var factory = CreateFrontendFactory(
            httpContext,
            CreateState(),
            frontend => frontend.PathPrefix = "/admin/bff"
        );

        var document = await factory.CreateAsync(httpContext, CancellationToken.None);

        var root = Assert.NotNull(document);
        Assert.Equal("/admin/bff/logout", root.GetProperty("logoutUrl").GetString());
    }

    #endregion

    #region User Contributor Tests

    private IBffUserContributor CreateContributor(Action<BffUserContext> contribute)
    {
        var contributor = CreateStrictMock<IBffUserContributor>();
        contributor
            .Setup(x =>
                x.ContributeAsync(It.IsAny<BffUserContext>(), It.IsAny<CancellationToken>())
            )
            .Callback<BffUserContext, CancellationToken>((context, _) => contribute(context))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        return contributor.Object;
    }

    [Fact]
    public async Task CreateAsync_WithUserContributors_WritesTheirFieldsBesideTheReservedOnes()
    {
        var httpContext = CreateGuardedHttpContext();
        var factory = CreateFactory(
            httpContext,
            CreateState(),
            CreateContributor(context =>
            {
                Assert.Equal("sub-1", context.Metadata.Subject);
                Assert.True(context.Principal.HasClaim("email", "ada@example.com"));
                context.AddField("tenant", "acme");
                context.AddField("features", new JsonArray("beta", "reports"));
            }),
            CreateContributor(context => context.AddField("unread", null))
        );

        var document = await factory.CreateAsync(httpContext, CancellationToken.None);

        var root = Assert.NotNull(document);
        Assert.Equal("sub-1", root.GetProperty(JoseClaimNames.Payload.Sub).GetString());
        Assert.Equal("acme", root.GetProperty("tenant").GetString());
        Assert.Equal(
            ["beta", "reports"],
            root.GetProperty("features").EnumerateArray().Select(x => x.GetString())
        );
        Assert.Equal(JsonValueKind.Null, root.GetProperty("unread").ValueKind);
    }

    [Fact]
    public async Task CreateAsync_WhenAContributorUsesAReservedField_Throws()
    {
        var httpContext = CreateGuardedHttpContext();
        var factory = CreateFactory(
            httpContext,
            CreateState(),
            CreateContributor(context => context.AddField("sub", "someone-else"))
        );

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await factory.CreateAsync(httpContext, CancellationToken.None)
        );

        Assert.Equal("name", exception.ParamName);
    }

    #endregion
}
