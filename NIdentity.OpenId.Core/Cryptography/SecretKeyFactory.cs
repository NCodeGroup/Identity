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

using System.Security.Cryptography;
using NIdentity.OpenId.Cryptography.Descriptors;

namespace NIdentity.OpenId.Cryptography;

public interface ISecretKeyFactory
{
    Type SecretKeyType { get; }

    SecretKey GenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default);
}

public abstract class SecretKeyFactory<TKey> : ISecretKeyFactory
    where TKey : SecretKey
{
    /// <inheritdoc />
    public Type SecretKeyType => typeof(TKey);

    /// <inheritdoc />
    public SecretKey GenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default) =>
        CoreGenerateNewKey(descriptor, keyBitLengthHint);

    protected abstract TKey CoreGenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default);

    protected static int GetLegalSize(int? hint, IEnumerable<KeySizes> legalSizes)
    {
        foreach (var legalSize in legalSizes)
        {
            if (hint.HasValue && IsLegalSize(hint.Value, legalSize))
            {
                return hint.Value;
            }

            return legalSize.MinSize;
        }

        throw new InvalidOperationException();
    }

    protected static bool IsLegalSize(int size, KeySizes legalSize) =>
        size >= legalSize.MinSize &&
        size <= legalSize.MaxSize &&
        (size - legalSize.MinSize) % legalSize.SkipSize == 0;
}

public abstract class SecretKeyFactory<TKey, TFactory> : SecretKeyFactory<TKey>
    where TKey : SecretKey
    where TFactory : SecretKeyFactory<TKey, TFactory>, new()
{
    public static ISecretKeyFactory Default { get; } = new TFactory();
}
