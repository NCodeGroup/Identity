// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Blazor.Client.Logging;
using NCode.Identity.Bff.Blazor.Client.Options;

namespace NCode.Identity.Bff.Blazor.Client;

/// <summary>
/// Holds the browser's view of the BFF session and probes the BFF's user endpoint to learn whether the session lives:
/// every <see cref="BffBlazorClientOptions.ProbeInterval"/>, when the session's idle timeout ends, and on demand.
/// </summary>
internal sealed class BffClientSession(
    IHttpClientFactory httpClientFactory,
    NavigationManager navigation,
    IOptions<BffBlazorClientOptions> options,
    TimeProvider timeProvider,
    ILogger<BffClientSession> logger
) : IDisposable
{
    private const string SubjectClaimType = "sub";

    private static TimeSpan IdleExpiryGrace { get; } = TimeSpan.FromSeconds(1);

    private int _started;
    private int _invalidated;
    private int _disposed;
    private CancellationTokenSource? _wake;

    private BffBlazorClientOptions ClientOptions { get; } = options.Value;

    private Lock Gate { get; } = new();

    private CancellationTokenSource Disposal { get; } = new();

    private BffClientUser? CurrentUser { get; set; }

    private Task<AuthenticationState>? CurrentTask { get; set; }

    /// <summary>
    /// Occurs when a probe finds that the session's user signed in, signed out or became another user. The argument is
    /// the resulting authentication state.
    /// </summary>
    public event Action<Task<AuthenticationState>>? Changed;

    /// <summary>
    /// Makes <paramref name="user"/> the current state. The user is ignored once monitoring has started.
    /// </summary>
    /// <param name="user">The user a persisted <c>/bff/user</c> document describes.</param>
    public void Seed(BffClientUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        lock (Gate)
        {
            if (Volatile.Read(ref _started) != 0)
                return;

            CurrentUser = user;
            CurrentTask = Task.FromResult(StateOf(user));
        }
    }

    /// <summary>
    /// Gets the current authentication state and starts monitoring the session on the first call. Without a seed, the
    /// current state is the result of the first probe.
    /// </summary>
    /// <returns>The task of the current authentication state.</returns>
    public Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        TaskCompletionSource<AuthenticationState>? first = null;
        bool startMonitor;
        Task<AuthenticationState> current;

        lock (Gate)
        {
            startMonitor = Interlocked.CompareExchange(ref _started, 1, 0) == 0;

            if (CurrentTask is null)
            {
                first = new TaskCompletionSource<AuthenticationState>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                CurrentTask = first.Task;
            }

            current = CurrentTask;
        }

        if (startMonitor)
            _ = MonitorAsync(first, Disposal.Token).AsTask();

        return current;
    }

    /// <summary>
    /// Wakes the monitor so that it probes the session now.
    /// </summary>
    public void Invalidate()
    {
        Interlocked.Exchange(ref _invalidated, 1);

        var wake = Volatile.Read(ref _wake);
        if (wake is null)
            return;

        try
        {
            wake.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The wait that owned the token has already ended; the next wait sees the invalidation.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Disposal.Cancel();
        Disposal.Dispose();
    }

    private async ValueTask MonitorAsync(
        TaskCompletionSource<AuthenticationState>? first,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (first is not null)
            {
                var (known, user) = await ProbeAsync(cancellationToken);
                Apply(known, user, first);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                await WaitAsync(cancellationToken);

                var (known, user) = await ProbeAsync(cancellationToken);
                Apply(known, user, null);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            first?.TrySetCanceled(cancellationToken);
        }
    }

    private async ValueTask WaitAsync(CancellationToken cancellationToken)
    {
        var delay = ClientOptions.ProbeInterval;

        if (CurrentUser?.IdleExpiresAt is { } idleExpiresAt)
        {
            var untilIdleExpiry = idleExpiresAt + IdleExpiryGrace - timeProvider.GetUtcNow();
            if (untilIdleExpiry > TimeSpan.Zero && untilIdleExpiry < delay)
                delay = untilIdleExpiry;
        }

        using var wake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Volatile.Write(ref _wake, wake);

        try
        {
            if (Interlocked.Exchange(ref _invalidated, 0) == 0)
                await Task.Delay(delay, timeProvider, wake.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Interlocked.Exchange(ref _invalidated, 0);
        }
        finally
        {
            Interlocked.CompareExchange(ref _wake, null, wake);
        }
    }

    private async ValueTask<(bool Known, BffClientUser? User)> ProbeAsync(
        CancellationToken cancellationToken
    )
    {
        var current = ClientOptions;

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                navigation.ToAbsoluteUri(current.UserPath)
            );
            request.Headers.Add(current.AntiforgeryHeaderName, current.AntiforgeryHeaderValue);

            var client = httpClientFactory.CreateClient(BffBlazorClientDefaults.HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return (true, null);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var user = await ReadUserAsync(response, cancellationToken);
                if (user is not null)
                    return (true, user);

                logger.ProbeDocumentUnreadable(null);
            }
            else
            {
                logger.ProbeStatusUnexpected((int)response.StatusCode);
            }
        }
        catch (HttpRequestException exception)
        {
            logger.ProbeFailed(exception);
        }
        catch (JsonException exception)
        {
            logger.ProbeDocumentUnreadable(exception);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.ProbeFailed(exception);
        }

        return (false, null);
    }

    private static async ValueTask<BffClientUser?> ReadUserAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var userDocument = await JsonSerializer.DeserializeAsync<JsonElement>(
            stream,
            cancellationToken: cancellationToken
        );

        return BffClientUserReader.TryRead(userDocument, out var user) ? user : null;
    }

    private void Apply(
        bool known,
        BffClientUser? user,
        TaskCompletionSource<AuthenticationState>? first
    )
    {
        Task<AuthenticationState>? changed = null;

        lock (Gate)
        {
            if (first is not null)
            {
                CurrentUser = known ? user : null;
                first.TrySetResult(StateOf(CurrentUser));
            }
            else if (known)
            {
                var differs = Differs(CurrentUser, user);
                CurrentUser = user;

                if (differs)
                {
                    changed = Task.FromResult(StateOf(user));
                    CurrentTask = changed;
                }
            }
        }

        if (changed is not null)
            Changed?.Invoke(changed);
    }

    private static bool Differs(BffClientUser? current, BffClientUser? next)
    {
        if (current is null || next is null)
            return current is null != next is null;

        return !string.Equals(
                current.Principal.FindFirst(SubjectClaimType)?.Value,
                next.Principal.FindFirst(SubjectClaimType)?.Value,
                StringComparison.Ordinal
            )
            || !string.Equals(
                current.Principal.FindFirst(BffClaimTypes.LogoutSid)?.Value,
                next.Principal.FindFirst(BffClaimTypes.LogoutSid)?.Value,
                StringComparison.Ordinal
            );
    }

    private static AuthenticationState StateOf(BffClientUser? user) =>
        new(user?.Principal ?? new ClaimsPrincipal(new ClaimsIdentity()));
}
