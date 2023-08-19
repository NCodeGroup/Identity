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

namespace NCode.Cryptography.Keys;

partial struct SecretKeyReader
{
    /// <summary>
    /// Reads a <see cref="RsaSecretKey"/> from the source buffer.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="tags">The collection of tags associated with the secret key.</param>
    /// <param name="encoding">Specifies the type of encoding to use when reading the asymmetric key.</param>
    /// <returns>The <see cref="RsaSecretKey"/> that was read.</returns>
    public RsaSecretKey ReadRsa(string keyId, IEnumerable<string> tags, AsymmetricSecretKeyEncoding encoding) =>
        ReadRsa(keyId, tags, encoding, certificate: null);

    private static RsaSecretKey CreateRsaSecretKey(string keyId, IEnumerable<string> tags, AsymmetricAlgorithm key, X509Certificate2? certificate) =>
        SecretKeyFactory.Create<RsaSecretKey>(key, pkcs8PrivateKey =>
            new RsaSecretKey(keyId, tags, key.KeySize, pkcs8PrivateKey, certificate));

    private RsaSecretKey ReadRsa(string keyId, IEnumerable<string> tags, AsymmetricSecretKeyEncoding encoding, X509Certificate2? certificate)
    {
        using var key = ReadAsymmetricKey(RSA.Create, encoding, ImportRsa);
        return CreateRsaSecretKey(keyId, tags, key, certificate);
    }

    private static RsaSecretKey ReadRsa(string keyId, IEnumerable<string> tags, ReadOnlySpan<char> pem, X509Certificate2? certificate)
    {
        using var key = ImportRsa(pem);
        return CreateRsaSecretKey(keyId, tags, key, certificate);
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
