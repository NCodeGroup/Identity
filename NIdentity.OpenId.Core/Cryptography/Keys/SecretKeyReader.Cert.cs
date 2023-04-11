using System.Security.Cryptography.X509Certificates;

namespace NIdentity.OpenId.Cryptography.Keys;

partial struct SecretKeyReader
{
    /// <summary>
    /// Reads a <see cref="SecretKey"/> from the source buffer along with its corresponding certificate.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <returns>The <see cref="SecretKey"/> that was read.</returns>
    /// <remarks>
    /// The data for the certificate must be encoded using <c>PEM</c> rules.
    /// </remarks>
    public SecretKey ReadCertificate(string keyId) =>
        ReadPem(pem => ReadCertificate(keyId, pem));

    private static SecretKey ReadCertificate(string keyId, ReadOnlySpan<char> pem)
    {
        var certificate = X509Certificate2.CreateFromPem(pem);
        try
        {
            return certificate.GetKeyAlgorithm() switch
            {
                Oids.Rsa => ReadRsa(keyId, pem, certificate),
                Oids.Ecc when IsECDsa(certificate) => ReadECDsa(keyId, pem, certificate),
                Oids.Ecc when IsECDiffieHellman(certificate) => ReadECDiffieHellman(keyId, pem, certificate),
                _ => throw new InvalidOperationException()
            };
        }
        catch
        {
            certificate.Dispose();
            throw;
        }
    }

    private static bool IsECDsa(X509Certificate2 certificate)
    {
        // the easiest implementation without copying the logic from HasECDsaKeyUsage
        using var key = certificate.GetECDsaPublicKey();
        return key is not null;
    }

    private static bool IsECDiffieHellman(X509Certificate2 certificate)
    {
        // the easiest implementation without copying the logic from HasECDiffieHellmanKeyUsage
        using var key = certificate.GetECDiffieHellmanPublicKey();
        return key is not null;
    }
}
