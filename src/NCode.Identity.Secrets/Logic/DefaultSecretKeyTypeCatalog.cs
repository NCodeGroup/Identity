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

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Secrets.Logic;

/// <summary>
/// Provides the default implementation of <see cref="ISecretKeyTypeCatalog"/>, indexing the registered
/// <see cref="ISecretKeyTypeDescriptor"/> instances by key type. When multiple descriptors share a key type (for
/// example an application-registered override), the last one registered wins.
/// </summary>
internal sealed class DefaultSecretKeyTypeCatalog : ISecretKeyTypeCatalog
{
    private FrozenDictionary<string, ISecretKeyTypeDescriptor> DescriptorsByKeyType { get; }

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
    }

    /// <inheritdoc />
    public bool TryGet(
        string keyType,
        [NotNullWhen(true)] out ISecretKeyTypeDescriptor? descriptor
    ) => DescriptorsByKeyType.TryGetValue(keyType, out descriptor);
}
