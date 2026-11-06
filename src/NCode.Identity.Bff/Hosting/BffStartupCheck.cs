// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Hosting;

/// <summary>
/// Fails startup when the configured client authentication method of every frontend has no registered authenticator.
/// </summary>
internal class BffStartupCheck(
    IEnumerable<IClientAuthenticator> authenticators,
    BffFrontendCatalog frontends
) : IHostedService
{
    private readonly IEnumerable<IClientAuthenticator> _authenticators = authenticators;
    private readonly BffFrontendCatalog _frontends = frontends;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var frontend in _frontends.Frontends)
        {
            var method = frontend.Options.ClientAuthentication.Method;
            if (
                !_authenticators.Any(authenticator =>
                    string.Equals(authenticator.MethodName, method, StringComparison.Ordinal)
                )
            )
            {
                throw new InvalidOperationException(
                    $"No client authenticator is registered for the method '{method}' configured at Bff:Frontends:{frontend.Name}:ClientAuthentication:Method."
                );
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
