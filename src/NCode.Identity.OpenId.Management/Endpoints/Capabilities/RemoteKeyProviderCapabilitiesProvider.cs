// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

/// <summary>
/// Contributes the <c>remoteKeyProviders</c> section to the management capabilities document, listing the name of each
/// remote key provider that the <see cref="IRemoteKeyDescriptorCatalog"/> describes, which is each registered
/// provider, so clients can discover which key management services can hold a tenant's signing keys.
/// </summary>
// See ADR-1474.
internal sealed class RemoteKeyProviderCapabilitiesProvider(
    IRemoteKeyDescriptorCatalog remoteKeyDescriptorCatalog
) : ICommandHandler<DiscoverManagementCapabilitiesCommand>
{
    /// <summary>
    /// The capability section name under which the remote key provider capabilities are published.
    /// </summary>
    public const string SectionName = "remoteKeyProviders";

    private IRemoteKeyDescriptorCatalog RemoteKeyDescriptorCatalog { get; } =
        remoteKeyDescriptorCatalog;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        DiscoverManagementCapabilitiesCommand command,
        CancellationToken cancellationToken
    )
    {
        var providers = RemoteKeyDescriptorCatalog
            .Providers.Select(provider => new RemoteKeyProviderCapabilityResource
            {
                Name = provider.Name,
            })
            .OrderBy(resource => resource.Name, StringComparer.Ordinal)
            .ToList();

        // First contributor wins, so a replacement provider registered earlier is not overwritten.
        command.Capabilities.TryAdd(SectionName, providers);

        return ValueTask.CompletedTask;
    }
}
