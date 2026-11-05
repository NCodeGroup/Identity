// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// Serves a blank HTML page over HTTPS on loopback ports, which browsers use as the origin of a page that is
/// not the backend-for-frontend (BFF).
/// </summary>
public sealed class OriginServer : IAsyncDisposable
{
    private const string BlankPage =
        "<!doctype html><html><head><title>origin</title></head><body></body></html>";

    private readonly WebApplication _app;

    private OriginServer(WebApplication app)
    {
        _app = app;
    }

    /// <summary>
    /// Starts a server that listens on each of the ports.
    /// </summary>
    public static async Task<OriginServer> StartAsync(
        TestCertificate certificate,
        IReadOnlyList<int> ports
    )
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Microsoft.Extensions.Hosting.Environments.Production,
            }
        );
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            foreach (var port in ports)
            {
                kestrel.ListenLocalhost(port, listen => listen.UseHttps(certificate.Certificate));
            }
        });

        var app = builder.Build();
        app.MapGet("/", static () => Results.Content(BlankPage, "text/html"));

        await app.StartAsync();
        return new OriginServer(app);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
    }
}
