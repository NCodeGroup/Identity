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
using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.Descriptors;

namespace NIdentity.OpenId.Cryptography.Keys;

public class EccSecretKey : SecretKey
{
    public static EccSecretKey GenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default) => descriptor.AlgorithmType switch
    {
        AlgorithmTypes.KeyManagement => EcdhGenerateNewKey(descriptor, keyBitLengthHint),
        AlgorithmTypes.DigitalSignature => EcdsaGenerateNewKey(descriptor, keyBitLengthHint),
        _ => throw new InvalidOperationException()
    };

    private static EccSecretKey EcdsaGenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default)
    {
        using var ecc = ECDsa.Create();

        if (descriptor is ISupportLegalSizes supportLegalSizes)
        {
            ecc.KeySize = KeySizesUtility.GetLegalSize(keyBitLengthHint, supportLegalSizes.LegalSizes);
        }

        return new EccSecretKey(ecc.ExportParameters(includePrivateParameters: true));
    }

    private static EccSecretKey EcdhGenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default)
    {
        using var ecc = ECDiffieHellman.Create();

        if (descriptor is ISupportLegalSizes supportLegalSizes)
        {
            ecc.KeySize = KeySizesUtility.GetLegalSize(keyBitLengthHint, supportLegalSizes.LegalSizes);
        }

        return new EccSecretKey(ecc.ExportParameters(includePrivateParameters: true));
    }

    private ECParameters? ECParametersOrNull { get; set; }

    private ECParameters ECParameters => ECParametersOrNull ?? throw new ObjectDisposedException(GetType().FullName);

    public EccSecretKey(ECParameters ecParameters)
    {
        ECParametersOrNull = ecParameters;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && ECParametersOrNull.HasValue)
        {
            ZeroPrivateMemory(ECParametersOrNull.Value);
            ECParametersOrNull = null;
        }

        base.Dispose(disposing);
    }

    private static void ZeroPrivateMemory(ECParameters ecParameters)
    {
        CryptographicOperations.ZeroMemory(ecParameters.D);
    }

    public ECDsa CreateECDsa() => ECDsa.Create(ECParameters);

    public ECDiffieHellman CreateECDiffieHellman() => ECDiffieHellman.Create(ECParameters);
}
