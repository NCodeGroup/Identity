// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Bff.Blazor.Client.Logging;
using NCode.Identity.Bff.Blazor.Client.Options;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Client.Tests;

public class BffClientSessionTests : BaseTests
{
    private const string UserDocument = """
        {
          "sub": "user-1",
          "logoutUrl": "/bff/logout",
          "logoutSid": "guard-1",
          "session": { "idleExpiresAt": 1700000600 }
        }
        """;

    private static DateTimeOffset Start { get; } = new(2023, 11, 14, 22, 13, 20, TimeSpan.Zero);

    private static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(10);

    private FakeLogger<BffClientSession> Logger { get; } = new();

    private BffClientSession CreateSession(
        StubHttpMessageHandler handler,
        FakeTimeProvider time,
        ILogger<BffClientSession>? logger = null
    )
    {
        var httpClientFactory = CreateStrictMock<IHttpClientFactory>();
        httpClientFactory
            .Setup(x => x.CreateClient(BffBlazorClientDefaults.HttpClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false))
            .Verifiable();

        return new BffClientSession(
            httpClientFactory.Object,
            new TestNavigationManager(),
            Microsoft.Extensions.Options.Options.Create(new BffBlazorClientOptions()),
            time,
            logger ?? CreateLooseMock<ILogger<BffClientSession>>().Object
        );
    }

    private async Task AssertProbeKeepsUserAsync(Func<HttpResponseMessage> answer, int eventId)
    {
        using var requested = new SemaphoreSlim(0);
        var handler = new StubHttpMessageHandler(
            (_, _) =>
            {
                requested.Release();
                return Task.FromResult(answer());
            }
        );
        var time = new FakeTimeProvider(Start);
        using var session = CreateSession(handler, time, Logger);
        session.Seed(ReadUser(UserDocument));
        var changes = 0;
        session.Changed += _ => Interlocked.Increment(ref changes);
        await session.GetAuthenticationStateAsync().WaitAsync(Timeout);

        time.Advance(TimeSpan.FromMinutes(1));
        await requested.WaitAsync(Timeout);
        session.Invalidate();
        await requested.WaitAsync(Timeout);

        var state = await session.GetAuthenticationStateAsync().WaitAsync(Timeout);
        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal(0, changes);
        Assert.Contains(Logger.Collector.GetSnapshot(), record => record.Id.Id == eventId);
    }

    private static BffClientUser ReadUser(string json)
    {
        Assert.True(BffClientUserReader.TryRead(JsonElement.Parse(json), out var user));
        Assert.NotNull(user);
        return user;
    }

    private static HttpResponseMessage UserResponse() =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(JsonElement.Parse(UserDocument)) };

    #region Constructor Tests

    [Fact]
    public void Constructor_InvalidOptions_Throws()
    {
        var options = new OptionsManager<BffBlazorClientOptions>(
            new OptionsFactory<BffBlazorClientOptions>(
                [
                    new ConfigureOptions<BffBlazorClientOptions>(x =>
                        x.ProbeInterval = TimeSpan.Zero
                    ),
                ],
                [],
                [new BffBlazorClientOptionsValidator()]
            )
        );

        Assert.Throws<OptionsValidationException>(() =>
            new BffClientSession(
                CreateStrictMock<IHttpClientFactory>().Object,
                new TestNavigationManager(),
                options,
                new FakeTimeProvider(Start),
                CreateLooseMock<ILogger<BffClientSession>>().Object
            )
        );
    }

    #endregion

    #region GetAuthenticationStateAsync Tests

    [Fact]
    public async Task GetAuthenticationStateAsync_NotSeeded_ProbesWithHeader()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(UserResponse()));
        using var session = CreateSession(handler, new FakeTimeProvider(Start));

        var state = await session.GetAuthenticationStateAsync().WaitAsync(Timeout);

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("user-1", state.User.FindFirst("sub")?.Value);
        Assert.Equal("guard-1", state.User.FindFirst(BffClaimTypes.LogoutSid)?.Value);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("https://app.example/bff/user"), request.RequestUri);
        Assert.Equal("1", Assert.Single(request.Headers.GetValues("X-CSRF")));
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_SeededAndProbeAnswers401_SignsOut()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.Unauthorized);
        var time = new FakeTimeProvider(Start);
        using var session = CreateSession(handler, time);
        session.Seed(ReadUser(UserDocument));
        var changed = new TaskCompletionSource<Task<AuthenticationState>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        session.Changed += task => changed.TrySetResult(task);

        var seeded = await session.GetAuthenticationStateAsync().WaitAsync(Timeout);

        Assert.True(seeded.User.Identity?.IsAuthenticated);
        Assert.Empty(handler.Requests);

        time.Advance(TimeSpan.FromMinutes(1));

        var signedOut = await (await changed.Task.WaitAsync(Timeout)).WaitAsync(Timeout);
        Assert.False(signedOut.User.Identity?.IsAuthenticated);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public Task GetAuthenticationStateAsync_ProbeAnswers500_KeepsUser() =>
        AssertProbeKeepsUserAsync(
            () => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            EventIds.ProbeStatusUnexpected
        );

    [Fact]
    public Task GetAuthenticationStateAsync_ProbeAnswersMalformedJson_KeepsUser() =>
        AssertProbeKeepsUserAsync(
            () =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent("{"u8.ToArray()),
                },
            EventIds.ProbeDocumentUnreadable
        );

    [Fact]
    public Task GetAuthenticationStateAsync_ProbeAnswersDocumentWithoutSub_KeepsUser() =>
        AssertProbeKeepsUserAsync(
            () =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent("""{"name":"x"}"""u8.ToArray()),
                },
            EventIds.ProbeDocumentUnreadable
        );

    #endregion

    #region Invalidate Tests

    [Fact]
    public async Task Invalidate_ProbesNow()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.Unauthorized);
        using var session = CreateSession(handler, new FakeTimeProvider(Start));
        session.Seed(ReadUser(UserDocument));
        var changed = new TaskCompletionSource<Task<AuthenticationState>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        session.Changed += task => changed.TrySetResult(task);
        await session.GetAuthenticationStateAsync().WaitAsync(Timeout);

        session.Invalidate();

        var signedOut = await (await changed.Task.WaitAsync(Timeout)).WaitAsync(Timeout);
        Assert.False(signedOut.User.Identity?.IsAuthenticated);
        Assert.Single(handler.Requests);
    }

    #endregion

    #region Changed Event Tests

    [Fact]
    public async Task Changed_ProbeFindsSameUser_IsNotRaised()
    {
        using var requested = new SemaphoreSlim(0);
        var handler = new StubHttpMessageHandler(
            (_, _) =>
            {
                requested.Release();
                return Task.FromResult(UserResponse());
            }
        );
        var time = new FakeTimeProvider(Start);
        using var session = CreateSession(handler, time);
        session.Seed(ReadUser(UserDocument));
        var changes = 0;
        session.Changed += _ => Interlocked.Increment(ref changes);
        await session.GetAuthenticationStateAsync().WaitAsync(Timeout);

        time.Advance(TimeSpan.FromMinutes(1));
        await requested.WaitAsync(Timeout);
        session.Invalidate();
        await requested.WaitAsync(Timeout);

        var state = await session.GetAuthenticationStateAsync().WaitAsync(Timeout);
        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal(0, changes);
    }

    #endregion
}
