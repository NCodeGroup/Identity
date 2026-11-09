// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Hubs;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Hubs;

public class BffHubFilterTests : BaseTests
{
    private static DateTimeOffset Now { get; } = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);
    private static TimeSpan Interval { get; } = TimeSpan.FromMinutes(1);
    private static TimeSpan SignalTimeout { get; } = TimeSpan.FromSeconds(5);
    private static MethodInfo Method { get; } =
        typeof(TestHub).GetMethod(nameof(TestHub.Ping))
        ?? throw new InvalidOperationException("TestHub.Ping is missing.");

    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly CancellationToken _connectionAborted = new CancellationTokenSource().Token;
    private readonly Dictionary<object, object?> _items = [];
    private readonly DefaultBffConnectionSessionAccessor _accessor = new();
    private readonly ServiceProvider _serviceProvider =
        new ServiceCollection().BuildServiceProvider();
    private readonly TestHub _hub = new();
    private readonly Mock<IBffConnectionRevalidator> _revalidator;
    private readonly FakeLogger<BffHubFilter> _logger = new();

    public BffHubFilterTests()
    {
        _revalidator = CreateStrictMock<IBffConnectionRevalidator>();
    }

    private BffHubFilter CreateFilter() =>
        new(_accessor, _revalidator.Object, _timeProvider, _logger);

    private static TaskCompletionSource CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void SetupFeatures(
        Mock<HubCallerContext> context,
        IFeatureCollection features
    ) => context.SetupGet(x => x.Features).Returns(features).Verifiable();

    private void SetupWatchContext(Mock<HubCallerContext> context)
    {
        SetupFeatures(context, new FeatureCollection());
        context.SetupGet(x => x.ConnectionAborted).Returns(_connectionAborted).Verifiable();
    }

    private void SetupIdleWatch(
        Mock<HubCallerContext> context,
        Mock<IBffConnectionSession> session,
        Action<CancellationToken>? onWait = null
    )
    {
        SetupWatchContext(context);
        _revalidator
            .Setup(x => x.WaitUntilEndedAsync(session.Object, It.IsAny<CancellationToken>()))
            .Returns(
                (IBffConnectionSession _, CancellationToken token) =>
                {
                    onWait?.Invoke(token);
                    return new ValueTask(Task.Delay(Timeout.InfiniteTimeSpan, token));
                }
            )
            .Verifiable();
    }

    private Mock<HubCallerContext> CreateContext(ClaimsPrincipal? user)
    {
        var context = CreateStrictMock<HubCallerContext>();
        context.SetupGet(x => x.User).Returns(user).Verifiable();
        return context;
    }

    private Mock<HubCallerContext> CreateConnectionContext(
        Mock<IBffConnectionSession> session,
        bool validate,
        bool readsItems = true
    )
    {
        var user = new ClaimsPrincipal(
            new BffConnectionIdentity(new ClaimsIdentity("test"), session.Object)
        );
        var context = CreateContext(user);
        if (readsItems)
            context.SetupGet(x => x.Items).Returns(_items).Verifiable();
        if (validate)
            context.SetupGet(x => x.ConnectionAborted).Returns(_connectionAborted).Verifiable();
        return context;
    }

    private Mock<IBffConnectionSession> CreateSession(
        bool readsInterval = false,
        bool? ended = null
    )
    {
        var session = CreateStrictMock<IBffConnectionSession>();
        if (ended is { } value)
        {
            session
                .SetupGet(x => x.SessionEnded)
                .Returns(new CancellationToken(value))
                .Verifiable();
        }
        if (readsInterval)
            session.SetupGet(x => x.RevalidationInterval).Returns(Interval).Verifiable();
        return session;
    }

    private static IFeatureCollection CreateHubFeatures(bool selfRevalidating)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(
            new Endpoint(
                requestDelegate: null,
                selfRevalidating
                    ? new EndpointMetadataCollection(new BffSelfRevalidatingHubAttribute())
                    : EndpointMetadataCollection.Empty,
                displayName: "hub"
            )
        );
        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new TestHttpContextFeature { HttpContext = httpContext });
        return features;
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

    [Fact]
    public async Task OnConnectedAsync_SessionEnds_AbortsConnection()
    {
        var session = CreateSession();
        var context = CreateConnectionContext(session, validate: false);
        var aborted = CreateSignal();
        context.Setup(x => x.Abort()).Callback(() => aborted.SetResult()).Verifiable();
        SetupWatchContext(context);
        _revalidator
            .Setup(x => x.WaitUntilEndedAsync(session.Object, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await CreateFilter()
            .OnConnectedAsync(
                new HubLifetimeContext(context.Object, _serviceProvider, _hub),
                _ => Task.CompletedTask
            );

        await aborted.Task.WaitAsync(SignalTimeout);
        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task OnConnectedAsync_SelfRevalidatingHub_DoesNotWatch()
    {
        var session = CreateSession();
        var context = CreateConnectionContext(session, validate: false);
        SetupFeatures(context, CreateHubFeatures(selfRevalidating: true));

        await CreateFilter()
            .OnConnectedAsync(
                new HubLifetimeContext(context.Object, _serviceProvider, _hub),
                _ => Task.CompletedTask
            );

        Assert.Empty(_revalidator.Invocations);
    }

    [Fact]
    public async Task OnConnectedAsync_NextThrows_StartsNoWatchAndRethrows()
    {
        var session = CreateSession();
        var context = CreateConnectionContext(session, validate: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateFilter()
                .OnConnectedAsync(
                    new HubLifetimeContext(context.Object, _serviceProvider, _hub),
                    _ => throw new InvalidOperationException("connect")
                )
        );

        Assert.Empty(_revalidator.Invocations);
        Assert.DoesNotContain(_items.Values, v => v is CancellationTokenSource);
    }

    [Fact]
    public async Task OnConnectedAsync_WatchFaults_LogsAndAborts()
    {
        var session = CreateSession();
        var context = CreateConnectionContext(session, validate: false);
        var aborted = CreateSignal();
        context.Setup(x => x.Abort()).Callback(() => aborted.SetResult()).Verifiable();
        SetupWatchContext(context);
        _revalidator
            .Setup(x => x.WaitUntilEndedAsync(session.Object, It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("fault"))
            .Verifiable();

        await CreateFilter()
            .OnConnectedAsync(
                new HubLifetimeContext(context.Object, _serviceProvider, _hub),
                _ => Task.CompletedTask
            );

        await aborted.Task.WaitAsync(SignalTimeout);
        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(EventIds.HubRevalidationFaulted, record.Id.Id);
        Assert.IsType<InvalidOperationException>(record.Exception);
    }

    [Fact]
    public async Task OnConnectedAsync_ConnectionAborted_EndsWatch()
    {
        var session = CreateSession();
        var context = CreateConnectionContext(session, validate: false);
        using var connectionAborted = new CancellationTokenSource();
        context.SetupGet(x => x.ConnectionAborted).Returns(connectionAborted.Token).Verifiable();
        SetupFeatures(context, new FeatureCollection());
        var waiting = CreateSignal();
        Task? wait = null;
        _revalidator
            .Setup(x => x.WaitUntilEndedAsync(session.Object, It.IsAny<CancellationToken>()))
            .Returns(
                (IBffConnectionSession _, CancellationToken token) =>
                {
                    wait = Task.Delay(Timeout.InfiniteTimeSpan, token);
                    waiting.SetResult();
                    return new ValueTask(wait);
                }
            )
            .Verifiable();
        await CreateFilter()
            .OnConnectedAsync(
                new HubLifetimeContext(context.Object, _serviceProvider, _hub),
                _ => Task.CompletedTask
            );
        await waiting.Task.WaitAsync(SignalTimeout);

        await connectionAborted.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => wait!.WaitAsync(SignalTimeout));
    }

    #endregion

    #region OnDisconnectedAsync Tests

    [Fact]
    public async Task OnDisconnectedAsync_CancelsWatch()
    {
        var session = CreateSession();
        var context = CreateConnectionContext(session, validate: false);
        var watching = CreateSignal();
        var token = CancellationToken.None;
        SetupIdleWatch(
            context,
            session,
            captured =>
            {
                token = captured;
                watching.SetResult();
            }
        );
        var filter = CreateFilter();
        var lifetime = new HubLifetimeContext(context.Object, _serviceProvider, _hub);
        await filter.OnConnectedAsync(lifetime, _ => Task.CompletedTask);
        await watching.Task.WaitAsync(SignalTimeout);
        Exception? seen = null;
        var exception = new InvalidOperationException("disconnected");

        await filter.OnDisconnectedAsync(
            lifetime,
            exception,
            (_, e) =>
            {
                seen = e;
                return Task.CompletedTask;
            }
        );

        Assert.True(token.IsCancellationRequested);
        Assert.Same(exception, seen);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithoutWatch_CallsNext()
    {
        var context = CreateStrictMock<HubCallerContext>();
        context.SetupGet(x => x.Items).Returns(_items).Verifiable();
        var called = false;

        await CreateFilter()
            .OnDisconnectedAsync(
                new HubLifetimeContext(context.Object, _serviceProvider, _hub),
                null,
                (_, _) =>
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
        var session = CreateSession(readsInterval: true, ended: false);
        var context = CreateConnectionContext(session, validate: false);
        SetupIdleWatch(context, session);
        var filter = CreateFilter();
        await filter.OnConnectedAsync(
            new HubLifetimeContext(context.Object, _serviceProvider, _hub),
            _ => Task.CompletedTask
        );
        _timeProvider.Advance(Interval - TimeSpan.FromSeconds(1));

        var result = await filter.InvokeMethodAsync(CreateInvocation(context.Object), Next);

        Assert.Equal("result", result);
    }

    [Fact]
    public async Task InvokeMethodAsync_IntervalElapsed_ValidatesWithInvocationTime()
    {
        var session = CreateSession(readsInterval: true, ended: false);
        var context = CreateConnectionContext(session, validate: true);
        SetupIdleWatch(context, session);
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
        var session = CreateSession(ended: false);
        var context = CreateConnectionContext(session, validate: true);
        SetupFeatures(context, new FeatureCollection());
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
    public async Task InvokeMethodAsync_SelfRevalidatingHubSessionEnded_ThrowsWithoutAborting()
    {
        var session = CreateSession(ended: false);
        var context = CreateConnectionContext(session, validate: true);
        SetupFeatures(context, CreateHubFeatures(selfRevalidating: true));
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
    public async Task InvokeMethodAsync_SelfRevalidatingHubAfterSessionEnded_CallsNextWithoutTheConnection()
    {
        var session = CreateSession(ended: true);
        var context = CreateConnectionContext(session, validate: false, readsItems: false);
        SetupFeatures(context, CreateHubFeatures(selfRevalidating: true));
        IBffConnectionSession? seen = session.Object;

        var result = await CreateFilter()
            .InvokeMethodAsync(
                CreateInvocation(context.Object),
                invocation =>
                {
                    seen = _accessor.Current;
                    return Next(invocation);
                }
            );

        Assert.Equal("result", result);
        Assert.Null(seen);
    }

    [Fact]
    public async Task InvokeMethodAsync_AfterSessionEnded_AbortsAndThrowsWithoutValidating()
    {
        var session = CreateSession(ended: true);
        var context = CreateConnectionContext(session, validate: false, readsItems: false);
        SetupFeatures(context, CreateHubFeatures(selfRevalidating: false));
        context.Setup(x => x.Abort()).Verifiable();
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
        var session = CreateSession(ended: false);
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
        var session = CreateSession(ended: false);
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

    /// <summary>
    /// An <see cref="IHttpContextFeature"/> that serves the <see cref="Microsoft.AspNetCore.Http.HttpContext"/> that a
    /// test assigns.
    /// </summary>
    private sealed class TestHttpContextFeature : IHttpContextFeature
    {
        /// <inheritdoc />
        public HttpContext? HttpContext { get; set; }
    }
}
