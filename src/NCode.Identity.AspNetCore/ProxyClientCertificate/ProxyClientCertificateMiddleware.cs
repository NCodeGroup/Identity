// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.AspNetCore.ProxyClientCertificate;

/// <summary>
/// Refuses a request whose connection does not carry the reverse proxy's client certificate, except the liveness and
/// readiness probes. A request that passes carries <see cref="IProxyClientCertificateFeature"/>.
/// </summary>
internal sealed class ProxyClientCertificateMiddleware(
    RequestDelegate next,
    ProxyClientCertificateVerifier verifier,
    ILogger<ProxyClientCertificateMiddleware> logger
)
{
    private RequestDelegate Next { get; } = next;
    private ProxyClientCertificateVerifier Verifier { get; } = verifier;
    private ILogger<ProxyClientCertificateMiddleware> Logger { get; } = logger;

    /// <summary>
    /// Verifies the connection's certificate and calls the next middleware when it is the proxy's.
    /// </summary>
    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (
            path.Equals(HealthProbeDefaults.LivenessPath, StringComparison.OrdinalIgnoreCase)
            || path.Equals(HealthProbeDefaults.ReadinessPath, StringComparison.OrdinalIgnoreCase)
        )
        {
            return Next(context);
        }

        var certificate = context.Connection.ClientCertificate;
        if (certificate is null)
        {
            Logger.ProxyClientCertificateMissing();
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        var refusal = Verifier.Verify(certificate);
        if (refusal != ProxyClientCertificateRefusal.None)
        {
            Logger.ProxyClientCertificateRefused(refusal);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Features.Set<IProxyClientCertificateFeature>(
            new ProxyClientCertificateFeature(certificate)
        );
        return Next(context);
    }
}
