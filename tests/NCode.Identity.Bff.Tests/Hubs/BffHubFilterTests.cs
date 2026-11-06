// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Hubs;
using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Hubs;

public class BffHubFilterTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly MethodInfo Method = typeof(TestHub).GetMethod(nameof(TestHub.Ping))!;

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly CancellationToken _connectionAborted = new CancellationTokenSource().Token;
    private readonly Dictionary<object, object?> _items = [];
    private readonly DefaultBffConnectionSessionAccessor _accessor = new();
    private readonly ServiceProvider _serviceProvider =
        new ServiceCollection().BuildServiceProvider();
    private readonly TestHub _hub = new();

    private BffHubFilter CreateFilter() => new(_accessor, _timeProvider);

    private Mock<HubCallerContext> CreateContext(ClaimsPrincipal? user)
    {
        var context = CreateStrictMock<HubCallerContext>();
        context.SetupGet(x => x.User).Returns(user).Verifiable();
        return context;
    }

    private Mock<HubCallerContext> CreateConnectionContext(
        Mock<IBffConnectionSession> session,
        bool validate
    )
    {
        var user = new ClaimsPrincipal(
            new BffConnectionIdentity(new ClaimsIdentity("test"), session.Object)
        );
        var context = CreateContext(user);
        context.SetupGet(x => x.Items).Returns(_items).Verifiable();
        if (validate)
            context.SetupGet(x => x.ConnectionAborted).Returns(_connectionAborted).Verifiable();
        return context;
    }

    private Mock<IBffConnectionSession> CreateSession(bool readsInterval = false)
    {
        var session = CreateStrictMock<IBffConnectionSession>();
        if (readsInterval)
            session.SetupGet(x => x.RevalidationInterval).Returns(Interval).Verifiable();
        return session;
    }

    private HubInvocationContext CreateInvocation(HubCallerContext context) =>
        new(context, _serviceProvider, _hub, Method, []);

    private static ValueTask<object?> Next(HubInvocationContext _) => new((object?)"result");

    #region OnConnectedAsync Tests

    [Fact]
    public async Task OnConnectedAsync_WithoutConnection_CallsNext()
    {
        var context = CreateContext(new ClaimsPrincipal());
        var lifetime = new HubLifetimeContext(context.Object, _serviceProvider, _hub);
        var called = false;

        await CreateFilter()
            .OnConnectedAsync(
                lifetime,
                _ =>
                {
                    called = true;
                    return Task.CompletedTask;
                }
            );

        Assert.True(called);
    }

    #endregion

    #region InvokeMethodAsync Tests

    [Fact]
    public async Task InvokeMethodAsync_WithoutConnection_CallsNext()
    {
        var context = CreateContext(new ClaimsPrincipal());

        var result = await CreateFilter().InvokeMethodAsync(CreateInvocation(context.Object), Next);

        Assert.Equal("result", result);
    }

    [Fact]
    public async Task InvokeMethodAsync_WithinInterval_SkipsValidation()
    {
        var session = CreateSession(readsInterval: true);
        var context = CreateConnectionContext(session, validate: false);
        var filter = CreateFilter();
        await filter.OnConnectedAsync(
            new HubLifetimeContext(context.Object, _serviceProvider, _hub),
            _ => Task.CompletedTask
        );
        _timeProvider.Advance(Interval - TimeSpan.FromSeconds(1));

        var result = await filter.InvokeMethodAsync(CreateInvocation(context.Object), Next);

        Assert.Equal("result", result);
        session.Verify(
            x => x.ValidateAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task InvokeMethodAsync_IntervalElapsed_ValidatesWithInvocationTime()
    {
        var session = CreateSession(readsInterval: true);
        var context = CreateConnectionContext(session, validate: true);
        var filter = CreateFilter();
        await filter.OnConnectedAsync(
            new HubLifetimeContext(context.Object, _serviceProvider, _hub),
            _ => Task.CompletedTask
        );
        _timeProvider.Advance(Interval);
        session
            .Setup(x => x.ValidateAsync(_timeProvider.GetUtcNow(), _connectionAborted))
            .Returns(new ValueTask<ClaimsPrincipal?>(new ClaimsPrincipal()))
            .Verifiable();

        var result = await filter.InvokeMethodAsync(CreateInvocation(context.Object), Next);

        Assert.Equal("result", result);
    }

    [Fact]
    public async Task InvokeMethodAsync_SessionEnded_AbortsAndThrows()
    {
        var session = CreateSession();
        var context = CreateConnectionContext(session, validate: true);
        context.Setup(x => x.Abort()).Verifiable();
        session
            .Setup(x => x.ValidateAsync(Now, _connectionAborted))
            .Returns(new ValueTask<ClaimsPrincipal?>((ClaimsPrincipal?)null))
            .Verifiable();
        var called = false;

        await Assert.ThrowsAsync<HubException>(async () =>
            await CreateFilter()
                .InvokeMethodAsync(
                    CreateInvocation(context.Object),
                    invocation =>
                    {
                        called = true;
                        return Next(invocation);
                    }
                )
        );

        Assert.False(called);
    }

    [Fact]
    public async Task InvokeMethodAsync_StoreUnavailable_Throws()
    {
        var session = CreateSession();
        var context = CreateConnectionContext(session, validate: true);
        session
            .Setup(x => x.ValidateAsync(Now, _connectionAborted))
            .Throws(new BffStoreUnavailableException("unavailable"))
            .Verifiable();

        await Assert.ThrowsAsync<HubException>(async () =>
            await CreateFilter().InvokeMethodAsync(CreateInvocation(context.Object), Next)
        );
    }

    [Fact]
    public async Task InvokeMethodAsync_MakesConnectionCurrent()
    {
        var session = CreateSession();
        var context = CreateConnectionContext(session, validate: true);
        session
            .Setup(x => x.ValidateAsync(Now, _connectionAborted))
            .Returns(new ValueTask<ClaimsPrincipal?>(new ClaimsPrincipal()))
            .Verifiable();
        IBffConnectionSession? seen = null;

        await CreateFilter()
            .InvokeMethodAsync(
                CreateInvocation(context.Object),
                invocation =>
                {
                    seen = _accessor.Current;
                    return Next(invocation);
                }
            );

        Assert.Same(session.Object, seen);
        Assert.Null(_accessor.Current);
    }

    #endregion

    private sealed class TestHub : Hub
    {
        public static string Ping() => "pong";
    }
}
