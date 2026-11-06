// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Bff.Blazor.Client.Options;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Client.Tests;

public class BffClientAuthenticationStateProviderTests : BaseTests
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

    private BffClientSession CreateSession(StubHttpMessageHandler handler)
    {
        var httpClientFactory = CreateStrictMock<IHttpClientFactory>();
        httpClientFactory
            .Setup(x => x.CreateClient(BffBlazorClientDefaults.HttpClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false))
            .Verifiable();

        return CreateSession(httpClientFactory.Object);
    }

    private static BffClientSession CreateSession(IHttpClientFactory httpClientFactory) =>
        new(
            httpClientFactory,
            new TestNavigationManager(),
            Microsoft.Extensions.Options.Options.Create(new BffBlazorClientOptions()),
            new FakeTimeProvider(Start),
            CreateLooseMock<ILogger<BffClientSession>>().Object
        );

    private static async Task<PersistentComponentState> CreateStateAsync(string document)
    {
        var manager = new ComponentStatePersistenceManager(
            NullLogger<ComponentStatePersistenceManager>.Instance
        );
        var store = new TestPersistentComponentStateStore(
            new Dictionary<string, byte[]>
            {
                [BffBlazorClientDefaults.UserStateKey] = JsonSerializer.SerializeToUtf8Bytes(
                    JsonElement.Parse(document)
                ),
            }
        );

        await manager.RestoreStateAsync(store);

        return manager.State;
    }

    #region GetAuthenticationStateAsync Tests

    [Fact]
    public async Task GetAuthenticationStateAsync_PersistedUser_ReturnsItWithoutProbe()
    {
        var httpClientFactory = CreateStrictMock<IHttpClientFactory>();
        using var session = CreateSession(httpClientFactory.Object);
        var state = await CreateStateAsync(UserDocument);
        using var provider = new BffClientAuthenticationStateProvider(session, state);

        var authenticationState = await provider.GetAuthenticationStateAsync().WaitAsync(Timeout);

        Assert.True(authenticationState.User.Identity?.IsAuthenticated);
        Assert.Equal("user-1", authenticationState.User.FindFirst("sub")?.Value);
        Assert.Equal(
            "guard-1",
            authenticationState.User.FindFirst(BffClientClaimTypes.LogoutSid)?.Value
        );
        httpClientFactory.VerifyNoOtherCalls();
    }

    #endregion

    #region AuthenticationStateChanged Event Tests

    [Fact]
    public async Task AuthenticationStateChanged_SessionChanges_NotifiesSubscribers()
    {
        var handler = StubHttpMessageHandler.Respond(HttpStatusCode.Unauthorized);
        using var session = CreateSession(handler);
        var state = await CreateStateAsync(UserDocument);
        using var provider = new BffClientAuthenticationStateProvider(session, state);
        var notified = new TaskCompletionSource<Task<AuthenticationState>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        provider.AuthenticationStateChanged += task => notified.TrySetResult(task);
        await provider.GetAuthenticationStateAsync().WaitAsync(Timeout);

        session.Invalidate();

        var signedOut = await (await notified.Task.WaitAsync(Timeout)).WaitAsync(Timeout);
        Assert.False(signedOut.User.Identity?.IsAuthenticated);
    }

    #endregion

    private sealed class TestPersistentComponentStateStore(IDictionary<string, byte[]> state)
        : IPersistentComponentStateStore
    {
        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() => Task.FromResult(state);

        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state) =>
            throw new NotSupportedException();
    }
}
