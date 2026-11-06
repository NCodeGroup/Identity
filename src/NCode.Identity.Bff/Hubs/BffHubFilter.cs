// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NCode.Identity.Bff.Logging;

namespace NCode.Identity.Bff.Hubs;

/// <summary>
/// Revalidates the BFF session that a hub connection holds once its revalidation interval has passed, slides the
/// session from the invocation, ends the connection when the session ended, and makes the connection current for the
/// invocation so an outbound call obtains its token through <see cref="IBffConnectionSessionAccessor"/>. A connection
/// of a hub that is not marked with <see cref="BffSelfRevalidatingHubAttribute"/> is also watched while it is idle:
/// it is aborted once <see cref="IBffConnectionRevalidator"/> reports that its session ended.
/// </summary>
/// <param name="connections">The accessor of the current connection session.</param>
/// <param name="revalidator">The revalidator that waits for the session of an idle connection to end.</param>
/// <param name="timeProvider">The time provider.</param>
/// <param name="logger">The logger.</param>
internal sealed class BffHubFilter(
    IBffConnectionSessionAccessor connections,
    IBffConnectionRevalidator revalidator,
    TimeProvider timeProvider,
    ILogger<BffHubFilter> logger
) : IHubFilter
{
    private static readonly object ValidatedAtKey = new();
    private static readonly object WatchKey = new();

    /// <inheritdoc />
    public async Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var connection = context.Context.User?.GetBffConnectionSession();
        if (connection is not null)
            context.Context.Items[ValidatedAtKey] = timeProvider.GetUtcNow();

        await next(context);

        if (connection is not null && !IsSelfRevalidating(context.Context))
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(
                context.Context.ConnectionAborted
            );
            context.Context.Items[WatchKey] = source;
            _ = WatchAsync(context.Context, connection, source.Token).AsTask();
        }
    }

    /// <inheritdoc />
    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (
            context.Context.Items.Remove(WatchKey, out var value)
            && value is CancellationTokenSource source
        )
        {
            await source.CancelAsync();
            source.Dispose();
        }

        await next(context, exception);
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next
    )
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);

        var context = invocationContext.Context;
        if (context.User?.GetBffConnectionSession() is not { } connection)
            return await next(invocationContext);

        var now = timeProvider.GetUtcNow();
        if (
            !context.Items.TryGetValue(ValidatedAtKey, out var stored)
            || stored is not DateTimeOffset validatedAt
            || now - validatedAt >= connection.RevalidationInterval
        )
        {
            await RevalidateAsync(context, connection, now, context.ConnectionAborted);
        }

        using (connections.Use(connection))
        {
            return await next(invocationContext);
        }
    }

    private static bool IsSelfRevalidating(HubCallerContext context)
    {
        var httpContext = context.GetHttpContext();
        var endpoint = httpContext?.GetEndpoint();
        var attribute = endpoint?.Metadata.GetMetadata<BffSelfRevalidatingHubAttribute>();
        return attribute is not null;
    }

    private async ValueTask WatchAsync(
        HubCallerContext context,
        IBffConnectionSession connection,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await revalidator.WaitUntilEndedAsync(connection, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.HubRevalidationFaulted(exception);
        }

        context.Abort();
    }

    private static async ValueTask RevalidateAsync(
        HubCallerContext context,
        IBffConnectionSession connection,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        ClaimsPrincipal? principal;
        try
        {
            principal = await connection.ValidateAsync(now, cancellationToken);
        }
        catch (BffStoreUnavailableException exception)
        {
            throw new HubException("The BFF session store is unavailable.", exception);
        }

        if (principal is null)
        {
            context.Abort();
            throw new HubException("The BFF session ended.");
        }

        context.Items[ValidatedAtKey] = now;
    }
}
