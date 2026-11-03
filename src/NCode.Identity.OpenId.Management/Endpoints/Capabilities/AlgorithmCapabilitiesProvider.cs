// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

/// <summary>
/// Contributes the <c>algorithms</c> section to the management capabilities document, projecting each supported
/// signature and key-management <see cref="KeyedAlgorithm"/> (from the live <see cref="IAlgorithmCollectionProvider"/>)
/// with its implied use and each compatible secret type with the key sizes and curves legal for it, so the console
/// create-secret form can drive its algorithm, use, key-size and curve inputs from discovery instead of hard-coded
/// defaults.
/// </summary>
// See ADR-1067.
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
                KeyTypes = GetKeyTypes(algorithm, descriptors),
            })
            .ToList();

        // First contributor wins, so a replacement provider registered earlier is not overwritten.
        command.Capabilities.TryAdd(SectionName, algorithms);

        return ValueTask.CompletedTask;
    }

    // See ADR-1389: an algorithm may accept several key types, each with its own sizes, so each compatible secret type
    // is listed once, with the sizes and curves of the algorithm's first key type it is of, in the order the algorithm
    // prefers them.
    private static List<AlgorithmKeyTypeCapabilityResource> GetKeyTypes(
        KeyedAlgorithm algorithm,
        List<ISecretKeyTypeDescriptor> descriptors
    ) =>
        [
            .. algorithm.KeyTypes.SelectMany(algorithmKeyType =>
                descriptors
                    .Where(descriptor =>
                        algorithm.TryGetKeyType(descriptor.KeyClrType, out var first)
                        && ReferenceEquals(first, algorithmKeyType)
                    )
                    .Select(descriptor => new AlgorithmKeyTypeCapabilityResource
                    {
                        SecretType = descriptor.KeyType,
                        KeyBitSizes = algorithmKeyType
                            .KeyBitSizes.Select(keySizes => new KeyBitSizesResource
                            {
                                MinBits = keySizes.MinSize,
                                MaxBits = keySizes.MaxSize,
                                SkipBits = keySizes.SkipSize,
                            })
                            .ToList(),
                        Curves =
                        [
                            .. algorithmKeyType.Curves.Select(
                                curve => new AlgorithmCurveCapabilityResource
                                {
                                    CurveName = curve.Name,
                                    CurveSizeBits = curve.KeySizeBits,
                                }
                            ),
                        ],
                    })
            ),
        ];
}
