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
/// Provides a <see cref="SecretKey"/> implementation for the <c>RSA</c> cryptographic keys.
/// </summary>
public class RsaSecretKey : SecretKey
{
    /// <summary>
    /// Generates and returns a new <see cref="RsaSecretKey"/> with random key material for the specified algorithm and optional hint for the key size.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="descriptor">The <see cref="AlgorithmDescriptor"/> that describes for what algorithm to generate a new cryptographic key.</param>
    /// <param name="keyBitLengthHint">An optional value that specifies the key size in bits to generate.
    /// This value is verified against the legal key sizes for the algorithm.
    /// If omitted, the first legal key size is used.</param>
    /// <returns>The newly generated <see cref="RsaSecretKey"/>.</returns>
    public static RsaSecretKey GenerateNewKey(string keyId, AlgorithmDescriptor descriptor, int? keyBitLengthHint = default)
    {
        using var rsa = RSA.Create();

        rsa.KeySize = KeySizesUtility.GetLegalSize(descriptor, keyBitLengthHint);

        return new RsaSecretKey(keyId, rsa.ExportParameters(includePrivateParameters: true));
    }

    private RSAParameters? ParametersOrNull { get; set; }

    private RSAParameters Parameters => ParametersOrNull ?? throw new ObjectDisposedException(GetType().FullName);

    /// <inheritdoc />
    public override int KeyBitLength => (Parameters.Modulus?.Length ?? 0) * BinaryUtility.BitsPerByte;

    /// <summary>
    /// Initializes a new instance of the <see cref="RsaSecretKey"/> class with the specified <see cref="RSAParameters"/> containing the key material.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="parameters">The cryptographic material for the secret key.</param>
    public RsaSecretKey(string keyId, RSAParameters parameters)
        : base(keyId)
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

    private static void ZeroPrivateMemory(RSAParameters parameters)
    {
        CryptographicOperations.ZeroMemory(parameters.D);
    }

    /// <summary>
    /// Factory method to create an <see cref="RSA"/> instance from the <see cref="RSAParameters"/>.
    /// </summary>
    /// <returns>The newly created <see cref="RSA"/> instance</returns>
    public RSA CreateRsa() => RSA.Create(Parameters);
}
