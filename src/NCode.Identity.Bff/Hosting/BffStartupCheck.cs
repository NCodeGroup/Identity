// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using NCode.Identity.Bff.Frontends;

namespace NCode.Identity.Bff.Hosting;

/// <summary>
/// Builds the BFF's frontends when the host starts, so options, selection rules or a frontend-set check that fail
/// stop the host rather than the first request.
/// </summary>
// See ADR-1442.
internal sealed class BffStartupCheck(BffFrontendCatalog frontends) : IHostedService
{
    private BffFrontendCatalog Frontends { get; } = frontends;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Frontends.Current;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
