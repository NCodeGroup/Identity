#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
