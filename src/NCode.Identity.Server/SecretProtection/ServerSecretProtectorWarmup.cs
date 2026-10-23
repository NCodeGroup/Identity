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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.Logging;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Loads the <see cref="ISecretProtector"/> of the server when the host starts, so that the Data Protection key ring,
/// which protects and unprotects synchronously, finds it ready. It must start before the hosted service that loads the
/// key ring.
/// </summary>
/// <remarks>
/// It also warns when a Production host protects its master keys under a root key that is a local file.
/// </remarks>
// See ADR-0235.
internal class ServerSecretProtectorWarmup(
    ISecretProtectorProvider provider,
    IOptions<OpenIdOptions> openIdOptions,
    IOptions<SecretProtectionOptions> protectionOptions,
    IHostEnvironment hostEnvironment,
    ServerSecretProtectorHolder holder,
    ILogger<ServerSecretProtectorWarmup> logger
) : IHostedService
{
    private ISecretProtectorProvider Provider { get; } = provider;

    // Read on use: Value runs the OpenIdOptions validators, which construct the client-authentication handlers, which depend on the server factory.
    private IOptions<OpenIdOptions> OpenIdOptionsAccessor { get; } = openIdOptions;

    private IOptions<SecretProtectionOptions> ProtectionOptionsAccessor { get; } =
        protectionOptions;

    private IHostEnvironment HostEnvironment { get; } = hostEnvironment;

    private ServerSecretProtectorHolder Holder { get; } = holder;

    private ILogger<ServerSecretProtectorWarmup> Logger { get; } = logger;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var serverId = GetServerId();

        Holder.Protector = await Provider.GetProtectorAsync(
            SecretOwner.ForServer(serverId),
            [],
            cancellationToken
        );

        if (
            HostEnvironment.IsProduction()
            && string.Equals(
                ProtectionOptionsAccessor.Value.RootKeyProvider,
                RootKeyProviderNames.Local,
                StringComparison.Ordinal
            )
        )
        {
            Logger.LocalRootKeyInProduction();
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Gets the <c>ServerId</c> from the configurable options, or uses the default if not set.
    /// </summary>
    private string GetServerId()
    {
        var serverId = OpenIdOptionsAccessor.Value.Server.ServerId;

        if (string.IsNullOrEmpty(serverId))
        {
            serverId = OpenIdServerOptions.DefaultServerId;
        }

        return serverId;
    }
}
