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
using System.Security.Cryptography.X509Certificates;

namespace NCode.Jose.SecretKeys;

partial struct SecretKeyReader
{
    /// <summary>
    /// Reads a <see cref="RsaSecretKey"/> from the source buffer.
    /// </summary>
    /// <param name="metadata">The metadata for the secret key.</param>
    /// <param name="encoding">Specifies the type of encoding to use when reading the asymmetric key.</param>
    /// <returns>The <see cref="RsaSecretKey"/> that was read.</returns>
    public RsaSecretKey ReadRsa(KeyMetadata metadata, AsymmetricSecretKeyEncoding encoding) =>
        ReadRsa(metadata, encoding, certificate: null);

    private static RsaSecretKey CreateRsaSecretKey(KeyMetadata metadata, AsymmetricAlgorithm key, X509Certificate2? certificate) =>
        SecretKeyFactory.Create<RsaSecretKey>(key, pkcs8PrivateKey =>
            new RsaSecretKey(metadata, key.KeySize, pkcs8PrivateKey, certificate));

    private RsaSecretKey ReadRsa(KeyMetadata metadata, AsymmetricSecretKeyEncoding encoding, X509Certificate2? certificate)
    {
        using var key = ReadAsymmetricKey(RSA.Create, encoding, ImportRsa);
        return CreateRsaSecretKey(metadata, key, certificate);
    }

    private static RsaSecretKey ReadRsa(KeyMetadata metadata, ReadOnlySpan<char> pem, X509Certificate2? certificate)
    {
        using var key = ImportRsa(pem);
        return CreateRsaSecretKey(metadata, key, certificate);
    }

    private static RSA ImportRsa(ReadOnlySpan<char> pem) =>
        ImportAsymmetricKeyPem(RSA.Create, pem, label => label switch
        {
            PemLabels.RsaPrivateKey =>
                (RSA key, ReadOnlySpan<byte> source, out int bytesRead) =>
                    key.ImportRSAPrivateKey(source, out bytesRead),

            PemLabels.RsaPublicKey =>
                (RSA key, ReadOnlySpan<byte> source, out int bytesRead) =>
                    key.ImportRSAPublicKey(source, out bytesRead),

            PemLabels.Pkcs8PrivateKey =>
                (RSA key, ReadOnlySpan<byte> source, out int bytesRead) =>
                    key.ImportPkcs8PrivateKey(source, out bytesRead),

            PemLabels.SpkiPublicKey =>
                (RSA key, ReadOnlySpan<byte> source, out int bytesRead) =>
                    key.ImportSubjectPublicKeyInfo(source, out bytesRead),

            _ => null
        });
}
