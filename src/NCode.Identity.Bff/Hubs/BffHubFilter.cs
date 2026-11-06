// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace NCode.Identity.Bff.Hubs;

/// <summary>
/// Revalidates the BFF session that a hub connection holds once its revalidation interval has passed, slides the
/// session from the invocation, ends the connection when the session ended, and makes the connection current for the
/// invocation so an outbound call obtains its token through <see cref="IBffConnectionSessionAccessor"/>.
/// </summary>
/// <param name="connections">The accessor of the current connection session.</param>
/// <param name="timeProvider">The time provider.</param>
internal sealed class BffHubFilter(
    IBffConnectionSessionAccessor connections,
    TimeProvider timeProvider
) : IHubFilter
{
    private static readonly object ValidatedAtKey = new();

    /// <inheritdoc />
    public async Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.Context.User?.GetBffConnectionSession() is not null)
            context.Context.Items[ValidatedAtKey] = timeProvider.GetUtcNow();

        await next(context);
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
            await RevalidateAsync(context, connection, now);
        }

        using (connections.Use(connection))
        {
            return await next(invocationContext);
        }
    }

    private static async ValueTask RevalidateAsync(
        HubCallerContext context,
        IBffConnectionSession connection,
        DateTimeOffset now
    )
    {
        ClaimsPrincipal? principal;
        try
        {
            principal = await connection.ValidateAsync(now, context.ConnectionAborted);
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
