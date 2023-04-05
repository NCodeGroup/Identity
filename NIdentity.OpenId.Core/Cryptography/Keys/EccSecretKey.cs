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

/// <summary>
/// Provides a <see cref="SecretKey"/> implementation for the <c>Elliptic-Curve</c> cryptographic keys.
/// </summary>
public class EccSecretKey : SecretKey, ISupportKeySize
{
    /// <summary>
    /// Generates and returns a new <see cref="EccSecretKey"/> with random key material for the specified algorithm and optional hint for the key size.
    /// </summary>
    /// <param name="descriptor">The <see cref="AlgorithmDescriptor"/> that describes for what algorithm to generate a new cryptographic key.</param>
    /// <param name="keyBitLengthHint">An optional value that specifies the key size in bits to generate.
    /// This value is verified against the legal key sizes for the algorithm.
    /// If omitted, the first legal key size is used.</param>
    /// <returns>The newly generated <see cref="EccSecretKey"/>.</returns>
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

    private ECParameters? ParametersOrNull { get; set; }

    private ECParameters Parameters => ParametersOrNull ?? throw new ObjectDisposedException(GetType().FullName);

    /// <inheritdoc />
    public int KeyBitLength => (Parameters.D?.Length ?? Parameters.Q.X?.Length ?? Parameters.Q.Y?.Length ?? 0) * BinaryUtility.BitsPerByte;

    /// <summary>
    /// Initializes a new instance of the <see cref="EccSecretKey"/> class with the specified <see cref="ECParameters"/> containing the key material.
    /// </summary>
    /// <param name="parameters">The cryptographic material for the secret key.</param>
    public EccSecretKey(ECParameters parameters)
    {
        ParametersOrNull = parameters;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && ParametersOrNull.HasValue)
        {
            ZeroPrivateMemory(ParametersOrNull.Value);
            ParametersOrNull = null;
        }

        base.Dispose(disposing);
    }

    private static void ZeroPrivateMemory(ECParameters parameters)
    {
        CryptographicOperations.ZeroMemory(parameters.D);
    }

    /// <summary>
    /// Factory method to create an <see cref="ECDsa"/> instance from the <see cref="ECParameters"/>.
    /// </summary>
    /// <returns>The newly created <see cref="ECDsa"/> instance</returns>
    public ECDsa CreateEcdsa() => ECDsa.Create(Parameters);

    /// <summary>
    /// Factory method to create an <see cref="ECDiffieHellman"/> instance from the <see cref="ECParameters"/>.
    /// </summary>
    /// <returns>The newly created <see cref="ECDiffieHellman"/> instance</returns>
    public ECDiffieHellman CreateEcDiffieHellman() => ECDiffieHellman.Create(Parameters);
}
