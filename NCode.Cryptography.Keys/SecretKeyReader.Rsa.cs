using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Cryptography.Keys;

partial struct SecretKeyReader
{
    /// <summary>
    /// Reads a <see cref="RsaSecretKey"/> from the source buffer.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="encoding">Specifies the type of encoding to use when reading the asymmetric key.</param>
    /// <returns>The <see cref="RsaSecretKey"/> that was read.</returns>
    public RsaSecretKey ReadRsa(string keyId, AsymmetricSecretKeyEncoding encoding) =>
        ReadRsa(keyId, encoding, certificate: null);

    private static RsaSecretKey CreateRsaSecretKey(string keyId, AsymmetricAlgorithm key, X509Certificate2? certificate) =>
        SecretKeyFactory.Create<RsaSecretKey>(key, pkcs8PrivateKey =>
            new RsaSecretKey(keyId, key.KeySize, pkcs8PrivateKey, certificate));

    private RsaSecretKey ReadRsa(string keyId, AsymmetricSecretKeyEncoding encoding, X509Certificate2? certificate)
    {
        using var key = ReadAsymmetricKey(RSA.Create, encoding, ImportRsa);
        return CreateRsaSecretKey(keyId, key, certificate);
    }

    private static RsaSecretKey ReadRsa(string keyId, ReadOnlySpan<char> pem, X509Certificate2? certificate)
    {
        using var key = ImportRsa(pem);
        return CreateRsaSecretKey(keyId, key, certificate);
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
