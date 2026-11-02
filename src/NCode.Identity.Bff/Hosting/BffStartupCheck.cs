// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Hosting;

/// <summary>
/// Fails startup when the configured client authentication method has no registered authenticator.
/// </summary>
internal class BffStartupCheck(
    IEnumerable<IClientAuthenticator> authenticators,
    IOptions<BffOptions> options
) : IHostedService
{
    private readonly IEnumerable<IClientAuthenticator> _authenticators = authenticators;
    private readonly IOptions<BffOptions> _options = options;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var method = _options.Value.ClientAuthentication.Method;
        if (
            !_authenticators.Any(authenticator =>
                string.Equals(authenticator.MethodName, method, StringComparison.Ordinal)
            )
        )
        {
            throw new InvalidOperationException(
                $"No client authenticator is registered for the method '{method}' configured at Bff:ClientAuthentication:Method."
            );
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
