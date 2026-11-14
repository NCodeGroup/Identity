// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client.Options;

namespace NCode.Identity.Bff.Server;

/// <summary>
/// Stops the host as it starts when <see cref="BffTokenOptions.RefreshLeaseDuration"/> does not outlast the protocol
/// client's <see cref="OpenIdClientOptions.TotalTimeout"/>. The composition root makes this check because only it
/// knows both the BFF and the protocol client: a DPoP nonce retry sends the refresh request twice under one lease, so
/// a shorter lease could expire while the rotated refresh token is still in flight. It runs as a start check rather
/// than an options validator, because a validator reads no other options.
/// </summary>
/// <param name="bffOptions">The options of the BFF.</param>
/// <param name="clientOptions">The options of the protocol client that sends the refresh request.</param>
// See ADR-1291 and project-template ADR-0106.
internal sealed class BffRefreshLeaseStartupCheck(
    IOptions<BffOptions> bffOptions,
    IOptions<OpenIdClientOptions> clientOptions
) : IHostedLifecycleService
{
    private static TimeSpan Margin { get; } = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        var tokens = bffOptions.Value.Tokens;
        if (tokens is null)
            return Task.CompletedTask;

        var lease = tokens.RefreshLeaseDuration;
        var minimum = clientOptions.Value.TotalTimeout * 2 + Margin;
        if (lease < minimum)
        {
            throw new InvalidOperationException(
                $"Bff:Tokens:RefreshLeaseDuration ({lease}) must be at least twice OpenIdClientOptions.TotalTimeout plus 10 seconds ({minimum}), because a DPoP nonce retry sends the refresh request twice under one lease."
            );
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
