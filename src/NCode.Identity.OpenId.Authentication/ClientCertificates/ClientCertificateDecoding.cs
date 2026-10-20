#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides the decoding shared by the forwarded-certificate formats: a list of certificates, the client's own first,
/// becomes a <see cref="PresentedClientCertificate"/>, and a value that is not entirely certificates is rejected.
/// </summary>
internal static class ClientCertificateDecoding
{
    private const string CertificateLabel = "CERTIFICATE";

    /// <summary>
    /// Decodes PEM text holding one or more <c>CERTIFICATE</c> blocks separated only by whitespace, the client's own
    /// certificate first.
    /// </summary>
    public static bool TryDecodePem(
        ReadOnlySpan<char> pem,
        [NotNullWhen(true)] out PresentedClientCertificate? certificate
    )
    {
        var certificates = new List<X509Certificate2>();
        try
        {
            var remaining = pem.Trim();
            while (!remaining.IsEmpty)
            {
                if (
                    !PemEncoding.TryFind(remaining, out var fields)
                    || fields.Location.Start.Value != 0
                    || !remaining[fields.Label].SequenceEqual(CertificateLabel)
                )
                {
                    return Fail(certificates, out certificate);
                }

                var der = new byte[fields.DecodedDataLength];
                if (!Convert.TryFromBase64Chars(remaining[fields.Base64Data], der, out _))
                    return Fail(certificates, out certificate);

                certificates.Add(X509CertificateLoader.LoadCertificate(der));
                remaining = remaining[fields.Location.End..].TrimStart();
            }
        }
        catch (CryptographicException)
        {
            return Fail(certificates, out certificate);
        }

        return TryCreate(certificates, out certificate);
    }

    /// <summary>
    /// Decodes a comma-separated list of base64-encoded DER certificates, the client's own certificate first.
    /// </summary>
    public static bool TryDecodeBase64DerList(
        string value,
        [NotNullWhen(true)] out PresentedClientCertificate? certificate
    )
    {
        var certificates = new List<X509Certificate2>();
        try
        {
            foreach (var part in value.Split(',', StringSplitOptions.TrimEntries))
            {
                if (part.Length == 0)
                    return Fail(certificates, out certificate);

                var der = Convert.FromBase64String(part);
                certificates.Add(X509CertificateLoader.LoadCertificate(der));
            }
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return Fail(certificates, out certificate);
        }

        return TryCreate(certificates, out certificate);
    }

    /// <summary>
    /// Disposes every certificate a decoded <see cref="PresentedClientCertificate"/> holds.
    /// </summary>
    public static void DisposeAll(PresentedClientCertificate certificate)
    {
        certificate.Certificate.Dispose();
        foreach (var intermediate in certificate.Intermediates)
            intermediate.Dispose();
    }

    private static bool TryCreate(
        List<X509Certificate2> certificates,
        [NotNullWhen(true)] out PresentedClientCertificate? certificate
    )
    {
        if (certificates.Count == 0)
        {
            certificate = null;
            return false;
        }

        certificate = new PresentedClientCertificate(certificates[0])
        {
            Intermediates = [.. certificates.Skip(1)],
        };
        return true;
    }

    private static bool Fail(
        List<X509Certificate2> certificates,
        out PresentedClientCertificate? certificate
    )
    {
        foreach (var loaded in certificates)
            loaded.Dispose();

        certificate = null;
        return false;
    }
}
