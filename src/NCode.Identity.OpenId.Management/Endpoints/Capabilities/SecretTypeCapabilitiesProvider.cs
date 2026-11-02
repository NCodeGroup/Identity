// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

/// <summary>
/// Contributes the <c>secretTypes</c> section to the management capabilities document, projecting each registered
/// <see cref="ISecretKeyTypeDescriptor"/> so clients can discover which secret types the server supports and which
/// expose an exportable public key.
/// </summary>
internal sealed class SecretTypeCapabilitiesProvider(ISecretKeyTypeCatalog secretKeyTypeCatalog)
    : ICommandHandler<DiscoverManagementCapabilitiesCommand>
{
    /// <summary>
    /// The capability section name under which the secret-type capabilities are published.
    /// </summary>
    public const string SectionName = "secretTypes";

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        DiscoverManagementCapabilitiesCommand command,
        CancellationToken cancellationToken
    )
    {
        var secretTypes = SecretKeyTypeCatalog
            .Descriptors.Select(descriptor => new SecretTypeCapabilityResource
            {
                SecretType = descriptor.KeyType,
                IsAsymmetric = descriptor.IsAsymmetric,
                SupportsPublicKeyExport = descriptor.SupportsPublicKeyExport,
                SupportsPublicKeyImport = descriptor.PublicKeyImportFormats.Count > 0,
            })
            .ToList();

        // First contributor wins, so a replacement provider registered earlier is not overwritten.
        command.Capabilities.TryAdd(SectionName, secretTypes);

        return ValueTask.CompletedTask;
    }
}
