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

using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Logic;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

/// <summary>
/// Contributes the <c>algorithms</c> section to the management capabilities document, projecting each supported
/// signature and key-management <see cref="KeyedAlgorithm"/> (from the live <see cref="IAlgorithmCollectionProvider"/>)
/// with its implied use, compatible secret types, and legal key sizes, so the console create-secret form can drive
/// its algorithm, use, and key-size inputs from discovery instead of hard-coded defaults (ADR-0067).
/// </summary>
internal sealed class AlgorithmCapabilitiesProvider(
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ISecretKeyTypeCatalog secretKeyTypeCatalog
) : ICommandHandler<DiscoverManagementCapabilitiesCommand>
{
    /// <summary>
    /// The capability section name under which the supported algorithms are published.
    /// </summary>
    public const string SectionName = "algorithms";

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        DiscoverManagementCapabilitiesCommand command,
        CancellationToken cancellationToken
    )
    {
        var descriptors = SecretKeyTypeCatalog.Descriptors.ToList();

        var algorithms = AlgorithmCollectionProvider
            .Collection.OfType<KeyedAlgorithm>()
            .Where(algorithm =>
                algorithm.Type is AlgorithmType.DigitalSignature or AlgorithmType.KeyManagement
            )
            .Select(algorithm => new AlgorithmCapabilityResource
            {
                Code = algorithm.Code,
                Use =
                    algorithm.Type == AlgorithmType.DigitalSignature
                        ? SecretKeyUses.Signature
                        : SecretKeyUses.Encryption,
                SecretTypes = descriptors
                    .Where(descriptor => algorithm.KeyType.IsAssignableFrom(descriptor.KeyClrType))
                    .Select(descriptor => descriptor.KeyType)
                    .ToList(),
                KeyBitSizes = algorithm
                    .KeyBitSizes.Select(keySizes => new KeyBitSizesResource
                    {
                        MinBits = keySizes.MinSize,
                        MaxBits = keySizes.MaxSize,
                        SkipBits = keySizes.SkipSize,
                    })
                    .ToList(),
            })
            .ToList();

        // First contributor wins, so a replacement provider registered earlier is not overwritten.
        command.Capabilities.TryAdd(SectionName, algorithms);

        return ValueTask.CompletedTask;
    }
}
