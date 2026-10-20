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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using NCode.Registration;

namespace NCode.Identity.Server.KeyRingProtection;

/// <summary>
/// Applies the configured <see cref="KeyRingProtectionOptions"/> to the Data Protection key ring.
/// </summary>
internal static class KeyRingProtectionRegistration
{
    /// <summary>
    /// Binds <see cref="KeyRingProtectionOptions"/> from <paramref name="configuration"/> and, when a certificate is
    /// configured, protects new keys with it and decrypts existing keys with it or any previous certificate. The
    /// certificates are loaded here rather than when the host starts, because Data Protection takes certificate
    /// instances, so a missing file or a wrong password stops the host during composition.
    /// </summary>
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> whose
    /// <see cref="IIdentityServerBuilder.DataProtectionBuilder"/> composes the key ring.</param>
    /// <param name="configuration">The <see cref="IConfiguration"/> that holds the
    /// <see cref="KeyRingProtectionOptions.DefaultSectionName"/> section.</param>
    public static void AddKeyRingProtection(
        IIdentityServerBuilder builder,
        IConfiguration configuration
    )
    {
        var section = configuration.GetSection(KeyRingProtectionOptions.DefaultSectionName);
        var options = new KeyRingProtectionOptions();
        section.BindStrictly(options);

        if (options.Certificate is null)
        {
            if (options.PreviousCertificates.Length > 0)
                throw new InvalidOperationException(
                    $"The configuration section '{section.Path}' lists previous certificates but no current "
                        + $"'{nameof(KeyRingProtectionOptions.Certificate)}'. Configure the certificate that "
                        + "protects new keys."
                );

            return;
        }

        var certificate = LoadCertificate(
            options.Certificate,
            $"{section.Path}:{nameof(KeyRingProtectionOptions.Certificate)}"
        );
        var previousCertificates = options
            .PreviousCertificates.Select(
                (previous, index) =>
                    LoadCertificate(
                        previous,
                        $"{section.Path}:{nameof(KeyRingProtectionOptions.PreviousCertificates)}:{index}"
                    )
            )
            .ToArray();

        var dataProtectionBuilder = builder.DataProtectionBuilder;
        dataProtectionBuilder.ProtectKeysWithCertificate(certificate);
        if (previousCertificates.Length > 0)
            dataProtectionBuilder.UnprotectKeysWithAnyCertificate(previousCertificates);
    }

    /// <summary>
    /// Loads the certificate that <paramref name="options"/> locates and checks that it can protect a key ring.
    /// </summary>
    /// <param name="options">The <see cref="KeyRingCertificateOptions"/> that locate the certificate.</param>
    /// <param name="configurationPath">The configuration path of <paramref name="options"/>, named in errors.</param>
    /// <returns>The loaded <see cref="X509Certificate2"/>, with its RSA private key.</returns>
    /// <exception cref="InvalidOperationException">The certificate is not configured, cannot be loaded, or does not
    /// carry an RSA private key.</exception>
    internal static X509Certificate2 LoadCertificate(
        KeyRingCertificateOptions options,
        string configurationPath
    )
    {
        if (string.IsNullOrWhiteSpace(options.CertificatePath))
            throw new InvalidOperationException(
                $"'{configurationPath}:{nameof(KeyRingCertificateOptions.CertificatePath)}' is not set."
            );

        X509Certificate2 certificate;
        try
        {
            certificate =
                string.IsNullOrEmpty(options.PrivateKeyPath)
                    ? X509CertificateLoader.LoadPkcs12FromFile(
                        options.CertificatePath,
                        options.Password
                    )
                : string.IsNullOrEmpty(options.Password)
                    ? X509Certificate2.CreateFromPemFile(
                        options.CertificatePath,
                        options.PrivateKeyPath
                    )
                : X509Certificate2.CreateFromEncryptedPemFile(
                    options.CertificatePath,
                    options.Password,
                    options.PrivateKeyPath
                );
        }
        catch (Exception exception)
            when (exception is CryptographicException or IOException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"The key ring protection certificate '{configurationPath}' cannot be loaded: {exception.Message}",
                exception
            );
        }

        using var privateKey = certificate.GetRSAPrivateKey();
        if (privateKey is not null)
            return certificate;

        certificate.Dispose();
        throw new InvalidOperationException(
            $"The key ring protection certificate '{configurationPath}' has no RSA private key. Data Protection "
                + "encrypts keys with RSA, so the certificate must carry an RSA key pair."
        );
    }
}
