// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// Provides the default implementation of <see cref="ISecretKeyTypeCatalog"/>, indexing the registered
/// <see cref="ISecretKeyTypeDescriptor"/> instances by key type and by JSON Web Key <c>kty</c>. When multiple
/// descriptors share a key type (for example an application-registered override), the last one registered wins; the
/// <c>kty</c> index is built from the descriptors that remain.
/// </summary>
internal sealed class DefaultSecretKeyTypeCatalog : ISecretKeyTypeCatalog
{
    private FrozenDictionary<string, ISecretKeyTypeDescriptor> DescriptorsByKeyType { get; }

    private FrozenDictionary<string, ISecretKeyTypeDescriptor> DescriptorsByJwkKeyType { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<ISecretKeyTypeDescriptor> Descriptors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSecretKeyTypeCatalog"/> class.
    /// </summary>
    /// <param name="descriptors">The registered secret key-type descriptors.</param>
    public DefaultSecretKeyTypeCatalog(IEnumerable<ISecretKeyTypeDescriptor> descriptors)
    {
        var map = new Dictionary<string, ISecretKeyTypeDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            map[descriptor.KeyType] = descriptor;
        }

        DescriptorsByKeyType = map.ToFrozenDictionary(StringComparer.Ordinal);
        Descriptors = DescriptorsByKeyType.Values;

        var jwkMap = new Dictionary<string, ISecretKeyTypeDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in Descriptors)
        {
            if (descriptor.JwkKeyType is not { } jwkKeyTypeDescriptor)
                continue;

            var jwkKeyType = jwkKeyTypeDescriptor.Name;

            if (!jwkMap.TryAdd(jwkKeyType, descriptor))
            {
                throw new InvalidOperationException(
                    $"The secret key types '{jwkMap[jwkKeyType].KeyType}' and '{descriptor.KeyType}' are both "
                        + $"represented by the JSON Web Key type '{jwkKeyType}'."
                );
            }
        }

        DescriptorsByJwkKeyType = jwkMap.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public bool TryGet(
        string keyType,
        [NotNullWhen(true)] out ISecretKeyTypeDescriptor? descriptor
    ) => DescriptorsByKeyType.TryGetValue(keyType, out descriptor);

    /// <inheritdoc />
    public bool TryGetByJwkKeyType(
        string jwkKeyType,
        [NotNullWhen(true)] out ISecretKeyTypeDescriptor? descriptor
    ) => DescriptorsByJwkKeyType.TryGetValue(jwkKeyType, out descriptor);
}
