using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

// ReSharper disable InconsistentNaming

namespace NIdentity.OpenId.Cryptography.Keys;

partial struct SecretKeyReader
{
    /// <summary>
    /// Reads an <see cref="EccSecretKey"/> from the source buffer.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="encoding">Specifies the type of encoding to use when reading the asymmetric key.</param>
    /// <returns>The <see cref="EccSecretKey"/> that was read.</returns>
    public EccSecretKey ReadEcc(string keyId, AsymmetricSecretKeyEncoding encoding) =>
        ReadECDiffieHellman(keyId, encoding, certificate: null);

    private static EccSecretKey CreateEccSecretKey(string keyId, AsymmetricAlgorithm key, X509Certificate2? certificate) =>
        CreateAsymmetricSecretKey<EccSecretKey>(key, pkcs8PrivateKey =>
            new EccSecretKey(keyId, key.KeySize, pkcs8PrivateKey, certificate));

    private EccSecretKey ReadECDiffieHellman(string keyId, AsymmetricSecretKeyEncoding encoding, X509Certificate2? certificate)
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
