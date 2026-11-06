// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCode.Identity.Bff.EndToEnd.BlazorApp;
using NCode.Identity.Bff.EndToEnd.BlazorApp.Components;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// Hosts the Blazor Web App of the end-to-end suite as a backend-for-frontend (BFF) application on Kestrel over
/// HTTPS, wired to an OpenID Provider, so that real browsers can drive it.
/// </summary>
public sealed class BlazorBffApplication : WebApplicationFactory<App>
{
    private readonly BffApplicationSettings _settings;
    private readonly List<ECDsa> _keys = [];

    private BlazorBffApplication(BffApplicationSettings settings)
    {
        _settings = settings;
        Origin = $"https://localhost:{settings.Port}";
        WebSocketOrigin = $"wss://localhost:{settings.Port}";
    }

    /// <summary>
    /// Gets the origin of the BFF, which is <c>https://localhost:{Port}</c>.
    /// </summary>
    public string Origin { get; }

    /// <summary>
    /// Gets the origin that a <c>WebSocket</c> connects to, which is <c>wss://localhost:{Port}</c>.
    /// </summary>
    public string WebSocketOrigin { get; }

    /// <summary>
    /// Gets the counters of what the host saw.
    /// </summary>
    public HostCounters Counters => Services.GetRequiredService<HostCounters>();

    /// <summary>
    /// Builds and starts the Blazor BFF application.
    /// </summary>
    public static async Task<BlazorBffApplication> StartAsync(BffApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var application = new BlazorBffApplication(settings);
        try
        {
            application.UseKestrel(kestrel =>
                kestrel.ListenLocalhost(
                    settings.Port,
                    listen => listen.UseHttps(settings.Certificate.Certificate)
                )
            );
            application.StartServer();
            return application;
        }
        catch
        {
            await application.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        foreach (var key in _keys)
        {
            key.Dispose();
        }
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(BffHostSetup.CreateConfiguration(_settings, Origin))
        );
        builder.ConfigureTestServices(services =>
            BffHostSetup.AddServices(services, _settings, _keys)
        );
    }
}
