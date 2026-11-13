// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using NCode.Identity.AspNetCore;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Limits the setup attempts of each client address, token checks and root creations together, to
/// <see cref="SetupDefaults.SetupAttemptsPerMinute"/> in a fixed one-minute window, so a setup token cannot be
/// guessed by brute force. A request refused by the limit is not queued.
/// </summary>
internal sealed class SetupRateLimiter : IDisposable
{
    // The partition of a request that carries no remote address, such as one made in process.
    private const string UnknownAddressPartition = "unknown";

    // The window of the limit, which is also the longest a refused client address waits.
    private static TimeSpan Window { get; } = TimeSpan.FromMinutes(1);

    private PartitionedRateLimiter<HttpContext> Limiter { get; } =
        PartitionedRateLimiter.Create<HttpContext, string>(static httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress is { } address
                    ? RemoteAddressPartition.GetValue(address)
                    : UnknownAddressPartition,
                static _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = SetupDefaults.SetupAttemptsPerMinute,
                    Window = Window,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }
            )
        );

    /// <inheritdoc />
    public void Dispose() => Limiter.Dispose();

    /// <summary>
    /// Counts one setup attempt, a token check or a root creation, against the client address of the request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><c>null</c> if the attempt is within the limit; otherwise, the delay after which the client address
    /// has attempts again, because it has used its attempts of the current window. The delay is the one the limiter
    /// names, or the whole window when it names none.</returns>
    public async ValueTask<TimeSpan?> AcquireAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        using var lease = await Limiter.AcquireAsync(
            httpContext,
            permitCount: 1,
            cancellationToken
        );
        if (lease.IsAcquired)
        {
            return null;
        }

        return lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? retryAfter
            : Window;
    }
}
