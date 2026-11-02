// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff.Diagnostics;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Tests.Diagnostics;

/// <summary>
/// Provides <see cref="BffMetrics"/> for tests that do not assert on the instruments. Its meter belongs to a factory of
/// its own, so a <c>MetricCollector</c> that a test scopes to its own factory never sees these measurements.
/// </summary>
internal static class BffTestTelemetry
{
    private static ServiceProvider Provider { get; } =
        new ServiceCollection().AddMetrics().BuildServiceProvider();

    /// <summary>
    /// Gets metrics whose measurements no test observes.
    /// </summary>
    public static BffMetrics Metrics { get; } = new(Provider.GetRequiredService<IMeterFactory>());

    /// <summary>
    /// Wraps a store the way the BFF's components receive it.
    /// </summary>
    /// <param name="store">The store to wrap.</param>
    /// <returns>The instrumented store.</returns>
    public static BffInstrumentedSessionStore Instrument(IBffSessionStore store) =>
        new(store, Metrics);
}
