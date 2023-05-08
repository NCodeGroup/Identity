using System.Security.Cryptography.X509Certificates;

namespace NCode.Cryptography.Keys;

partial struct SecretKeyReader
{
    /// <summary>
    /// Reads a <see cref="SecretKey"/> from the source buffer along with its corresponding certificate.
    /// The certificate's <see cref="X509Certificate2.Thumbprint"/> is used as the <c>Key ID (KID)</c>.
    /// </summary>
    /// <returns>The <see cref="SecretKey"/> that was read.</returns>
    /// <remarks>
    /// The data for the certificate must be encoded using <c>PEM</c> rules.
    /// The currently supported PKI algorithms are RSA and EcPublicKey (either ECDsa or ECDiffieHellman).
    /// </remarks>
    public SecretKey ReadCertificate() =>
        ReadPem(pem => ReadCertificate(keyId: null, pem));

    /// <summary>
    /// Reads a <see cref="SecretKey"/> from the source buffer along with its corresponding certificate.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <returns>The <see cref="SecretKey"/> that was read.</returns>
    /// <remarks>
    /// The data for the certificate must be encoded using <c>PEM</c> rules.
    /// The currently supported PKI algorithms are RSA and EcPublicKey (either ECDsa or ECDiffieHellman).
    /// </remarks>
    public SecretKey ReadCertificate(string keyId) =>
        ReadPem(pem => ReadCertificate(keyId, pem));

    private static SecretKey ReadCertificate(string? keyId, ReadOnlySpan<char> pem)
    {
        var certificate = X509Certificate2.CreateFromPem(pem);
        try
        {
            keyId ??= certificate.Thumbprint;
            return certificate.GetKeyAlgorithm() switch
            {
                RsaSecretKey.Oid => ReadRsa(keyId, pem, certificate),
                EccSecretKey.Oid when IsECDsa(certificate) => ReadECDsa(keyId, pem, certificate),
                EccSecretKey.Oid when IsECDiffieHellman(certificate) => ReadECDiffieHellman(keyId, pem, certificate),
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
