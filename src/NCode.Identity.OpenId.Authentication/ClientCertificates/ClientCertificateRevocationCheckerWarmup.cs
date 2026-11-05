// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Resolves the <see cref="IClientCertificateRevocationChecker"/> as the host starts, so a registered revocation
/// source that no descriptor names, a described source that is not registered, or two sources of one name stops the
/// host rather than the first mutual-TLS authentication. The checker is resolved in <see cref="StartAsync"/>, after
/// the host validates its options, so the sources it constructs read validated options.
/// </summary>
// See ADR-1400.
internal sealed class ClientCertificateRevocationCheckerWarmup(IServiceProvider serviceProvider)
    : IHostedService
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The registered revocation sources and their descriptors do not
    /// correspond.</exception>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ServiceProvider.GetRequiredService<IClientCertificateRevocationChecker>();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
