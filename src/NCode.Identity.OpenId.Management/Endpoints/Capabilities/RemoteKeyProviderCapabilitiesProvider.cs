// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

/// <summary>
/// Contributes the <c>remoteKeyProviders</c> section to the management capabilities document, publishing each remote
/// key provider that the <see cref="IRemoteKeyDescriptorCatalog"/> describes, which is each registered provider, and,
/// for each, the algorithm rows a create or bind request accepts, so clients can discover which key management
/// services can hold a tenant's signing keys and the keys they hold them for.
/// </summary>
// See ADR-1474.
// See ADR-1476.
internal sealed class RemoteKeyProviderCapabilitiesProvider(
    IRemoteKeyDescriptorCatalog remoteKeyDescriptorCatalog,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ISecretKeyTypeCatalog secretKeyTypeCatalog
) : ICommandHandler<DiscoverManagementCapabilitiesCommand>
{
    /// <summary>
    /// The capability section name under which the remote key provider capabilities are published.
    /// </summary>
    public const string SectionName = "remoteKeyProviders";

    private IRemoteKeyDescriptorCatalog RemoteKeyDescriptorCatalog { get; } =
        remoteKeyDescriptorCatalog;

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

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
                Algorithms = RemoteKeyDescriptorCatalog
                    .Algorithms.Where(row =>
                        string.Equals(row.Provider, provider.Name, StringComparison.Ordinal)
                        && IsAccepted(row)
                    )
                    .Select(row => new RemoteKeyAlgorithmCapabilityResource
                    {
                        Algorithm = row.Algorithm,
                        SecretType = row.SecretType,
                        KeySizeBits = row.KeySizeBits,
                        Curve = row.Curve,
                    })
                    .ToList(),
            })
            .OrderBy(resource => resource.Name, StringComparer.Ordinal)
            .ToList();

        // First contributor wins, so a replacement provider registered earlier is not overwritten.
        command.Capabilities.TryAdd(SectionName, providers);

        return ValueTask.CompletedTask;
    }

    // A row the secret validator would refuse, such as one whose algorithm no package registers, is not offered.
    private bool IsAccepted(RemoteSigningAlgorithm row) =>
        SecretKeyTypeCatalog.TryGet(row.SecretType, out var descriptor)
        && descriptor is IRemoteSecretKeyTypeDescriptor
        && AlgorithmCollectionProvider.Collection.TryGetSignatureAlgorithm(
            row.Algorithm,
            out var signatureAlgorithm
        )
        && signatureAlgorithm.TryGetKeyType(descriptor.KeyClrType, out _);
}
