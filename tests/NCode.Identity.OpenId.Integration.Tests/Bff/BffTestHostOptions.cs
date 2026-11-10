// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;

namespace NCode.Identity.OpenId.Integration.Tests.Bff;

/// <summary>
/// Provides the optional inputs of <see cref="BffTestHost.StartAsync"/>.
/// </summary>
internal readonly record struct BffTestHostOptions
{
    /// <summary>
    /// Gets the settings that are added after the host's default in-memory settings.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? ExtraConfiguration { get; init; }

    /// <summary>
    /// Gets the queue that records the HTTP exchanges of the host's provider client.
    /// </summary>
    public ConcurrentQueue<RecordedHttpExchange>? Exchanges { get; init; }

    /// <summary>
    /// Gets a value indicating whether authentication runs before the BFF middleware, a misordered pipeline.
    /// </summary>
    public bool AuthenticateBeforeBff { get; init; }

    /// <summary>
    /// Gets the configuration source that a test reloads while the host runs.
    /// </summary>
    public IConfigurationSource? ReloadableConfiguration { get; init; }

    /// <summary>
    /// Gets the callback that registers additional services before the BFF is added.
    /// </summary>
    public Action<IServiceCollection>? ConfigureServices { get; init; }
}
