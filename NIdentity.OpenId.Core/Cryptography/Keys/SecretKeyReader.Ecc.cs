using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

// ReSharper disable InconsistentNaming

namespace NIdentity.OpenId.Cryptography.Keys;

partial struct SecretKeyReader
{
    public EccSecretKey ReadEcc(string keyId, SecretKeyEncoding encoding) =>
        ReadECDiffieHellman(keyId, encoding, certificate: null);

    private static EccSecretKey CreateEccSecretKey(string keyId, AsymmetricAlgorithm key, X509Certificate2? certificate) =>
        CreateAsymmetricSecretKey<EccSecretKey>(key, pkcs8PrivateKey =>
            new EccSecretKey(keyId, key.KeySize, pkcs8PrivateKey, certificate));

    private EccSecretKey ReadECDiffieHellman(string keyId, SecretKeyEncoding encoding, X509Certificate2? certificate)
    {
        using var key = ReadAsymmetricKey(ECDiffieHellman.Create, encoding, ImportECDiffieHellman);
        return CreateEccSecretKey(keyId, key, certificate);
    }

    private static EccSecretKey ReadECDiffieHellman(string keyId, ReadOnlySpan<char> pem, X509Certificate2? certificate)
    {
        using var key = ImportECDiffieHellman(pem);
        return CreateEccSecretKey(keyId, key, certificate);
    }

    private static ECDiffieHellman ImportECDiffieHellman(ReadOnlySpan<char> pem) =>
        ImportAsymmetricKeyPem(ECDiffieHellman.Create, pem, label => label switch
        {
            PemLabels.EcPrivateKey =>
                (ECDiffieHellman key, ReadOnlySpan<byte> source, out int bytesRead) =>
                    key.ImportECPrivateKey(source, out bytesRead),

            PemLabels.Pkcs8PrivateKey =>
                (ECDiffieHellman key, ReadOnlySpan<byte> source, out int bytesRead) =>
                    key.ImportPkcs8PrivateKey(source, out bytesRead),

            PemLabels.SpkiPublicKey =>
                (ECDiffieHellman key, ReadOnlySpan<byte> source, out int bytesRead) =>
                    key.ImportSubjectPublicKeyInfo(source, out bytesRead),

            _ => null
        });

    private static EccSecretKey ReadECDsa(string keyId, ReadOnlySpan<char> pem, X509Certificate2? certificate)
    {
        using var key = ImportECDsa(pem);
        return CreateEccSecretKey(keyId, key, certificate);
    }

    private static ECDsa ImportECDsa(ReadOnlySpan<char> pem) =>
        ImportAsymmetricKeyPem(ECDsa.Create, pem, label => label switch
        {
            PemLabels.EcPrivateKey =>
                (ECDsa key, ReadOnlySpan<byte> source, out int bytesRead) =>
                    key.ImportECPrivateKey(source, out bytesRead),

            PemLabels.Pkcs8PrivateKey =>
                (ECDsa key, ReadOnlySpan<byte> source, out int bytesRead) =>
                    key.ImportPkcs8PrivateKey(source, out bytesRead),

            PemLabels.SpkiPublicKey =>
                (ECDsa key, ReadOnlySpan<byte> source, out int bytesRead) =>
                    key.ImportSubjectPublicKeyInfo(source, out bytesRead),

            _ => null
        });
}
