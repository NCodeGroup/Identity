// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.AspNetCore.TransportSecurity;

/// <summary>
/// Judges the certificate an HTTPS endpoint serves: it must be inside its validity period, carry an RSA key of at
/// least 2048 bits or an ECDSA key of at least 256 bits, be signed with SHA-256 or stronger, and not be the ASP.NET
/// Core HTTPS development certificate. A self-signed certificate passes, because trusting the certificate is the
/// client's and the platform's job.
/// </summary>
internal static class TransportCertificateInspector
{
    /// <summary>
    /// The smallest RSA key, in bits, that an endpoint's certificate may carry.
    /// </summary>
    internal const int MinimumRsaKeySizeInBits = 2048;

    /// <summary>
    /// The smallest ECDSA key, in bits, that an endpoint's certificate may carry.
    /// </summary>
    internal const int MinimumEcdsaKeySizeInBits = 256;

    /// <summary>
    /// The extension OID that marks the ASP.NET Core HTTPS development certificate.
    /// </summary>
    internal const string AspNetCoreDevelopmentCertificateOid = "1.3.6.1.4.1.311.84.1.1";

    // RSASSA-PSS, RSA PKCS#1 with SHA-256, SHA-384 and SHA-512, and ECDSA with SHA-256, SHA-384 and SHA-512.
    private static FrozenSet<string> AcceptedSignatureAlgorithms { get; } =
        new[]
        {
            "1.2.840.113549.1.1.10",
            "1.2.840.113549.1.1.11",
            "1.2.840.113549.1.1.12",
            "1.2.840.113549.1.1.13",
            "1.2.840.10045.4.3.2",
            "1.2.840.10045.4.3.3",
            "1.2.840.10045.4.3.4",
        }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Returns every rule the certificate breaks, each as a sentence that continues "The certificate served at
    /// '{endpoint}' …", or an empty list when the certificate is usable.
    /// </summary>
    internal static IReadOnlyList<string> FindFailures(
        X509Certificate2 certificate,
        DateTimeOffset now
    )
    {
        var failures = new List<string>();

        if (certificate.Extensions[AspNetCoreDevelopmentCertificateOid] is not null)
        {
            failures.Add(
                "is the ASP.NET Core HTTPS development certificate, which only a developer's machine trusts. Supply a certificate issued for this deployment."
            );
        }

        if (now < certificate.NotBefore)
        {
            failures.Add($"is not valid before {certificate.NotBefore.ToUniversalTime():O}.");
        }

        if (now > certificate.NotAfter)
        {
            failures.Add($"expired at {certificate.NotAfter.ToUniversalTime():O}.");
        }

        AddKeyFailure(certificate, failures);

        if (
            !AcceptedSignatureAlgorithms.Contains(
                certificate.SignatureAlgorithm.Value ?? string.Empty
            )
        )
        {
            var algorithm =
                certificate.SignatureAlgorithm.FriendlyName ?? certificate.SignatureAlgorithm.Value;
            failures.Add(
                $"is signed with {algorithm}; only SHA-256 or stronger with RSA or ECDSA is accepted."
            );
        }

        return failures;
    }

    private static void AddKeyFailure(X509Certificate2 certificate, List<string> failures)
    {
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is not null)
        {
            if (rsa.KeySize < MinimumRsaKeySizeInBits)
            {
                failures.Add(
                    $"has an RSA key of {rsa.KeySize} bits; at least {MinimumRsaKeySizeInBits} bits are required."
                );
            }

            return;
        }

        using var ecdsa = certificate.GetECDsaPublicKey();
        if (ecdsa is not null)
        {
            if (ecdsa.KeySize < MinimumEcdsaKeySizeInBits)
            {
                failures.Add(
                    $"has an ECDSA key of {ecdsa.KeySize} bits; at least {MinimumEcdsaKeySizeInBits} bits are required."
                );
            }

            return;
        }

        var keyAlgorithm =
            certificate.PublicKey.Oid.FriendlyName ?? certificate.PublicKey.Oid.Value;
        failures.Add($"has a {keyAlgorithm} public key; only RSA and ECDSA keys are accepted.");
    }
}
