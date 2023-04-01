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

public class RsaSecretKey : SecretKey
{
    public static RsaSecretKey GenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default)
    {
        using var rsa = RSA.Create();

        if (descriptor is ISupportLegalSizes supportLegalSizes)
        {
            rsa.KeySize = KeySizesUtility.GetLegalSize(keyBitLengthHint, supportLegalSizes.LegalSizes);
        }

        return new RsaSecretKey(rsa.ExportParameters(includePrivateParameters: true));
    }

    private RSAParameters? RSAParametersOrNull { get; set; }

    private RSAParameters RSAParameters => RSAParametersOrNull ?? throw new ObjectDisposedException(GetType().FullName);

    public RsaSecretKey(RSAParameters rsaParameters)
    {
        RSAParametersOrNull = rsaParameters;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && RSAParametersOrNull.HasValue)
        {
            ZeroPrivateMemory(RSAParametersOrNull.Value);
            RSAParametersOrNull = null;
        }

        base.Dispose(disposing);
    }

    private static void ZeroPrivateMemory(RSAParameters rsaParameters)
    {
        CryptographicOperations.ZeroMemory(rsaParameters.D);
    }

    public RSA CreateRSA() => RSA.Create(RSAParameters);
}
