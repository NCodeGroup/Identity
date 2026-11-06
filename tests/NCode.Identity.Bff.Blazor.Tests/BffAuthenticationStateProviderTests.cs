// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NCode.Identity.Bff.Blazor.Logging;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Tests;

public sealed class BffAuthenticationStateProviderTests : BaseTests
{
    private static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(10);

    private Mock<IHttpContextAccessor> MockHttpContextAccessor { get; }
    private Mock<IBffUserDocumentFactory> MockDocuments { get; }
    private Mock<IBffConnectionSession> MockSession { get; }
    private Mock<IBffConnectionRevalidator> MockRevalidator { get; }
    private FakeLogger<BffAuthenticationStateProvider> Logger { get; }
    private ComponentStatePersistenceManager Manager { get; }
    private BffAuthenticationStateProvider Provider { get; }

    public BffAuthenticationStateProviderTests()
    {
        MockHttpContextAccessor = CreateStrictMock<IHttpContextAccessor>();
        MockDocuments = CreateStrictMock<IBffUserDocumentFactory>();
        MockSession = CreateStrictMock<IBffConnectionSession>();
        MockRevalidator = CreateStrictMock<IBffConnectionRevalidator>();
        Logger = new FakeLogger<BffAuthenticationStateProvider>();
        Manager = new ComponentStatePersistenceManager(
            NullLogger<ComponentStatePersistenceManager>.Instance
        );
        Provider = new BffAuthenticationStateProvider(
            Manager.State,
            MockHttpContextAccessor.Object,
            MockDocuments.Object,
            MockRevalidator.Object,
            Logger
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Provider.Dispose();

        base.Dispose(disposing);
    }

    private static AuthenticationState CreateConnectionState(IBffConnectionSession session) =>
        new(
            new ClaimsPrincipal(
                new BffConnectionIdentity(
                    new ClaimsIdentity([new Claim("sub", "user-1")], "test"),
                    session
                )
            )
        );

    private static Task<AuthenticationState> SignedIn(IBffConnectionSession session) =>
        Task.FromResult(CreateConnectionState(session));

    private static TaskCompletionSource CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task<IReadOnlyDictionary<string, byte[]>> PersistAsync()
    {
        var store = new TestPersistentComponentStateStore();
        using var renderer = new TestRenderer();
        await Manager.PersistStateAsync(store, renderer);

        return store.Persisted;
    }

    private static DefaultHttpContext CreatePageContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(
            new Endpoint(
                requestDelegate: null,
                new EndpointMetadataCollection(new BffPageAttribute()),
                "page"
            )
        );

        return httpContext;
    }

    #region SetAuthenticationState Tests

    [Fact]
    public async Task SetAuthenticationState_SessionEnded_SignsOut()
    {
        MockRevalidator
            .Setup(x => x.WaitUntilEndedAsync(MockSession.Object, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        Provider.SetAuthenticationState(SignedIn(MockSession.Object));

        var current = await Provider.GetAuthenticationStateAsync();
        Assert.False(current.User.Identity?.IsAuthenticated);
        Assert.Empty(Logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task SetAuthenticationState_SessionAlive_KeepsUser()
    {
        var started = CreateSignal();
        var captured = CancellationToken.None;
        MockRevalidator
            .Setup(x => x.WaitUntilEndedAsync(MockSession.Object, It.IsAny<CancellationToken>()))
            .Returns(
                (IBffConnectionSession _, CancellationToken token) =>
                {
                    captured = token;
                    started.TrySetResult();
                    return new ValueTask(
                        Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token)
                    );
                }
            )
            .Verifiable();
        var changes = 0;
        Provider.SetAuthenticationState(SignedIn(MockSession.Object));
        Provider.AuthenticationStateChanged += _ => Interlocked.Increment(ref changes);

        await started.Task.WaitAsync(Timeout);

        Assert.Equal(0, Volatile.Read(ref changes));
        var current = await Provider.GetAuthenticationStateAsync();
        Assert.True(current.User.Identity?.IsAuthenticated);
        Assert.Empty(Logger.Collector.GetSnapshot());
        Assert.False(captured.IsCancellationRequested);

        Provider.Dispose();

        Assert.True(captured.IsCancellationRequested);
        Assert.Equal(0, Volatile.Read(ref changes));
    }

    [Fact]
    public async Task SetAuthenticationState_WatchFaults_SignsOutAndLogs()
    {
        MockRevalidator
            .Setup(x => x.WaitUntilEndedAsync(MockSession.Object, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromException(new InvalidOperationException("watch failed")))
            .Verifiable();

        Provider.SetAuthenticationState(SignedIn(MockSession.Object));

        var current = await Provider.GetAuthenticationStateAsync();
        Assert.False(current.User.Identity?.IsAuthenticated);
        var record = Assert.Single(Logger.Collector.GetSnapshot());
        Assert.Equal(EventIds.RevalidationFaulted, record.Id.Id);
        Assert.IsType<InvalidOperationException>(record.Exception);
    }

    [Fact]
    public async Task SetAuthenticationState_Anonymous_NeverWatches()
    {
        Provider.SetAuthenticationState(
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())))
        );

        var current = await Provider.GetAuthenticationStateAsync();
        Assert.False(current.User.Identity?.IsAuthenticated);
        Assert.Empty(Logger.Collector.GetSnapshot());
    }

    #endregion

    #region PersistStateAsync Tests

    [Fact]
    public async Task PersistStateAsync_PageRequest_PersistsDocument()
    {
        var httpContext = CreatePageContext();
        var document = JsonElement.Parse("""{ "sub": "user-1", "logoutUrl": "/bff/logout" }""");
        MockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext).Verifiable();
        MockDocuments
            .Setup(x => x.CreateAsync(httpContext, httpContext.RequestAborted))
            .ReturnsAsync((JsonElement?)document)
            .Verifiable();

        var persisted = await PersistAsync();

        var bytes = Assert.Contains(BffUserDocumentNames.PersistedStateKey, persisted);
        var user = JsonElement.Parse(bytes);
        Assert.Equal("user-1", user.GetProperty("sub").GetString());
        Assert.Equal("/bff/logout", user.GetProperty("logoutUrl").GetString());
    }

    [Fact]
    public async Task PersistStateAsync_NoHttpContext_PersistsNothing()
    {
        MockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null).Verifiable();

        var persisted = await PersistAsync();

        Assert.Empty(persisted);
    }

    #endregion

    private sealed class TestPersistentComponentStateStore : IPersistentComponentStateStore
    {
        public IReadOnlyDictionary<string, byte[]> Persisted { get; private set; } =
            new Dictionary<string, byte[]>();

        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() =>
            Task.FromResult<IDictionary<string, byte[]>>(new Dictionary<string, byte[]>());

        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state)
        {
            Persisted = state;
            return Task.CompletedTask;
        }
    }

#pragma warning disable BL0006 // PersistStateAsync takes a Renderer, and only a subclass can supply one.
    private sealed class TestRenderer()
        : Renderer(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        protected override void HandleException(Exception exception) =>
            throw new InvalidOperationException("The test renderer failed.", exception);

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) =>
            Task.CompletedTask;
    }
#pragma warning restore BL0006
}
