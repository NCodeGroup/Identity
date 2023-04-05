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
using NIdentity.OpenId.Cryptography.Keys;

namespace NIdentity.OpenId.Cryptography.Binary;

public static class KeySizesUtility
{
    public static void AssertLegalSize(SecretKey secretKey, AlgorithmDescriptor descriptor)
    {
        if (secretKey is ISupportKeySize supportKeySize &&
            descriptor is ISupportLegalSizes supportLegalSizes &&
            !IsLegalSize(supportLegalSizes.LegalSizes, supportKeySize.KeyBitLength))
        {
            throw new InvalidOperationException();
        }
    }

    public static int GetLegalSize(AlgorithmDescriptor descriptor, int? keyBitLengthHint, int? keyBitLengthDefault = null)
    {
        var legalSizes = descriptor is ISupportLegalSizes supportLegalSizes ? supportLegalSizes.LegalSizes : Array.Empty<KeySizes>();
        return GetLegalSize(legalSizes, keyBitLengthHint, keyBitLengthDefault);
    }

    public static int GetLegalSize(IEnumerable<KeySizes> legalSizes, int? keyBitLengthHint, int? keyBitLengthDefault = null)
    {
        KeySizes? first = null;

        foreach (var legalSize in legalSizes)
        {
            first ??= legalSize;

            if (keyBitLengthHint.HasValue && IsLegalSize(legalSize, keyBitLengthHint.Value))
            {
                return keyBitLengthHint.Value;
            }
        }

        return first?.MinSize ?? keyBitLengthDefault ?? throw new InvalidOperationException();
    }

    public static bool IsLegalSize(IEnumerable<KeySizes> legalSizes, int size) =>
        legalSizes.Any(legalSize => IsLegalSize(legalSize, size));

    public static bool IsLegalSize(KeySizes legalSize, int size) =>
        size >= legalSize.MinSize &&
        size <= legalSize.MaxSize &&
        (size - legalSize.MinSize) % legalSize.SkipSize == 0;
}
