// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Buffers;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// Serves a blank HTML page over HTTPS on loopback ports, which browsers use as the origin of a page that is
/// not the backend-for-frontend (BFF).
/// </summary>
public sealed class OriginServer : IAsyncDisposable
{
    private WebApplication App { get; }

    private OriginServer(WebApplication app)
    {
        App = app;
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
        app.MapGet(
            "/",
            static () =>
                Results.Content(TestPages.Blank, "text/html", contentEncoding: SecureEncoding.UTF8)
        );

        await app.StartAsync();
        return new OriginServer(app);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
    }
}
