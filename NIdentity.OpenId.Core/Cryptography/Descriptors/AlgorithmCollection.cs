#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using NIdentity.OpenId.Cryptography.CryptoProvider.Aead.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Descriptors;
using NIdentity.OpenId.Cryptography.CryptoProvider.Signature.Descriptors;

namespace NIdentity.OpenId.Cryptography.Descriptors;

public interface IAlgorithmCollection
{
    IEnumerable<AlgorithmDescriptor> Descriptors { get; }

    bool TryGetSignatureAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out SignatureAlgorithmDescriptor descriptor);

    bool TryGetKeyWrapAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out KeyWrapAlgorithmDescriptor descriptor);

    bool TryGetAuthenticatedEncryptionAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out AuthenticatedEncryptionAlgorithmDescriptor descriptor);
}

/// <summary>
/// Provides a default implementation for the <see cref="IAlgorithmCollection"/> interface.
/// </summary>
public class AlgorithmCollection : IAlgorithmCollection
{
    /// <inheritdoc />
    public IEnumerable<AlgorithmDescriptor> Descriptors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmCollection"/> class.
    /// </summary>
    /// <param name="filters">A collection of <see cref="IAlgorithmDescriptorFilter"/> instances.</param>
    /// <param name="providers">A collection of <see cref="IAlgorithmDescriptorProvider"/> instances.</param>
    public AlgorithmCollection(
        IEnumerable<IAlgorithmDescriptorFilter> filters,
        IEnumerable<IAlgorithmDescriptorProvider> providers)
    {
        var filtersList = filters.ToList();
        Descriptors = providers
            .SelectMany(provider => provider.Load())
            .Where(descriptor => filtersList.All(filter => !filter.Exclude(descriptor)))
            .ToArray();
    }

    private bool TryGetAlgorithm<T>(string algorithmCode, [MaybeNullWhen(false)] out T descriptor)
        where T : AlgorithmDescriptor
    {
        descriptor = Descriptors.OfType<T>().FirstOrDefault(item => item.AlgorithmCode == algorithmCode);
        return descriptor != null;
    }

    /// <inheritdoc />
    public bool TryGetSignatureAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out SignatureAlgorithmDescriptor descriptor) =>
        TryGetAlgorithm(algorithmCode, out descriptor);

    /// <inheritdoc />
    public bool TryGetKeyWrapAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out KeyWrapAlgorithmDescriptor descriptor) =>
        TryGetAlgorithm(algorithmCode, out descriptor);

    /// <inheritdoc />
    public bool TryGetAuthenticatedEncryptionAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out AuthenticatedEncryptionAlgorithmDescriptor descriptor) =>
        TryGetAlgorithm(algorithmCode, out descriptor);
}
