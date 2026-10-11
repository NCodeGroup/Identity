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

using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Identity.Secrets.Logic;
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
            })
            .ToList();

        // First contributor wins, so a replacement provider registered earlier is not overwritten.
        command.Capabilities.TryAdd(SectionName, secretTypes);

        return ValueTask.CompletedTask;
    }
}
