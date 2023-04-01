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

namespace NIdentity.OpenId.Cryptography.Ecc;

public class EccSecretKeyFactory : SecretKeyFactory<EccSecretKey, EccSecretKeyFactory>
{
    /// <inheritdoc />
    protected override EccSecretKey CoreGenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default) => descriptor.AlgorithmType switch
    {
        AlgorithmTypes.KeyManagement => ECDiffieHellmanGenerateNewKey(descriptor, keyBitLengthHint),
        AlgorithmTypes.DigitalSignature => ECDsaGenerateNewKey(descriptor, keyBitLengthHint),
        _ => throw new InvalidOperationException()
    };

    private EccSecretKey ECDsaGenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default)
    {
        using var ecc = ECDsa.Create();

        if (descriptor is ISupportKeySizes supportKeySizes)
        {
            ecc.KeySize = GetLegalSize(keyBitLengthHint, supportKeySizes.KeySizes);
        }

        return new EccSecretKey(ecc.ExportParameters(includePrivateParameters: true));
    }

    private EccSecretKey ECDiffieHellmanGenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default)
    {
        using var ecc = ECDiffieHellman.Create();

        if (descriptor is ISupportKeySizes supportKeySizes)
        {
            ecc.KeySize = GetLegalSize(keyBitLengthHint, supportKeySizes.KeySizes);
        }

        return new EccSecretKey(ecc.ExportParameters(includePrivateParameters: true));
    }
}
