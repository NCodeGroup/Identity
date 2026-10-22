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

using Microsoft.Extensions.Options;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Handles <see cref="ServerSettingsChangedCommand"/> by dispatching a <see cref="TenantKeyReseedJob"/> when the change
/// permits a configured algorithm, that is, when some algorithm list of the server's effective settings gained one.
/// The pass over every tenant runs in the background, so nothing proportional to the number of tenants runs in the
/// request that changed the settings.
/// </summary>
/// <remarks>
/// The handler only compares the server's own effective settings. A tenant's list only narrows the server's, so a
/// tenant gains an algorithm only when the server's list gains it or is lifted, and the job decides, tenant by tenant,
/// which ones the change newly affects. The handler is idempotent, because dispatching the job again provisions
/// nothing that is already provisioned.
/// </remarks>
// See ADR-0169 (decision 10).
internal sealed class ServerSettingsChangedTenantKeyHandler(
    IOptions<OpenIdOptions> optionsAccessor,
    ITenantKeyPolicy tenantKeyPolicy,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IJobDispatcher jobDispatcher
) : ICommandHandler<ServerSettingsChangedCommand>
{
    private OpenIdTenantKeyProvisioningOptions Options { get; } =
        optionsAccessor.Value.Tenant.KeyProvisioning;

    private ITenantKeyPolicy TenantKeyPolicy { get; } = tenantKeyPolicy;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private IJobDispatcher JobDispatcher { get; } = jobDispatcher;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ServerSettingsChangedCommand command,
        CancellationToken cancellationToken
    )
    {
        if (!Options.Enabled)
        {
            return;
        }

        // The startup validator refuses options that cannot be honored, and the seed reports them, so there is nothing
        // for a settings change to provision.
        var resolution = TenantKeyPolicy.ResolveSpecifications(Options);
        if (resolution.Errors.Count > 0)
        {
            return;
        }

        var previous = await EffectiveSettingsResolver.ResolveServerAsync(
            command.PreviousSettings,
            cancellationToken
        );
        var current = await EffectiveSettingsResolver.ResolveServerAsync(
            command.Settings,
            cancellationToken
        );

        var added = TenantKeyUsage.FindAdded(
            Options.AlgorithmSettings,
            previous,
            current,
            resolution.Specifications.Select(specification => specification.Algorithm)
        );
        if (added.Count == 0)
        {
            return;
        }

        await JobDispatcher.DispatchAsync(
            new TenantKeyReseedJob
            {
                PreviousServerSettings = command.PreviousSettings,
                ServerSettings = command.Settings,
                Algorithms = added,
            },
            options: null,
            cancellationToken
        );
    }
}
