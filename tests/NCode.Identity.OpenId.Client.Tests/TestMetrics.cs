// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Client.Metrics;

namespace NCode.Identity.OpenId.Client.Tests;

/// <summary>
/// Creates the client's <see cref="ProviderMetrics"/> on a meter factory of their own, so a collector sees only the
/// measurements of the test that created them.
/// </summary>
internal sealed class TestMetrics : IDisposable
{
    private ServiceProvider ServiceProvider { get; } =
        new ServiceCollection().AddMetrics().BuildServiceProvider();

    public IMeterFactory MeterFactory => ServiceProvider.GetRequiredService<IMeterFactory>();

    public ProviderMetrics CreateProviderMetrics() => new(MeterFactory);

    public void Dispose() => ServiceProvider.Dispose();
}
