using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NIdentity.OpenId.Cryptography.Keys;

partial struct SecretKeyReader
{
    public RsaSecretKey ReadRsa(string keyId, SecretKeyEncoding encoding) =>
        ReadRsa(keyId, encoding, certificate: null);

    private static RsaSecretKey CreateRsaSecretKey(string keyId, AsymmetricAlgorithm key, X509Certificate2? certificate) =>
        CreateAsymmetricSecretKey<RsaSecretKey>(key, pkcs8PrivateKey =>
            new RsaSecretKey(keyId, key.KeySize, pkcs8PrivateKey, certificate));

    private RsaSecretKey ReadRsa(string keyId, SecretKeyEncoding encoding, X509Certificate2? certificate)
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
