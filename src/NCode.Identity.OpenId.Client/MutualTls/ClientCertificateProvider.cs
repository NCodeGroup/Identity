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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Client.MutualTls;

/// <summary>
/// Selects the TLS client certificate of a client from its key ring and materializes it with its private key.
/// </summary>
/// <remarks>
/// Each materialized certificate is cached by its SHA-256 thumbprint, with its issuers' certificates and its TLS
/// context, for the life of the provider, because a live handler may still use it. The selection runs before
/// <c>IHttpClientFactory</c> is asked for a client, because the factory caches an exception thrown while it builds a
/// handler.
/// </remarks>
internal class ClientCertificateProvider(
    IClientKeyRingProvider keyRingProvider,
    TimeProvider timeProvider
) : IDisposable
{
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    private IClientKeyRingProvider KeyRingProvider { get; } = keyRingProvider;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ConcurrentDictionary<string, SelectedCertificate> Certificates { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Selects the first qualifying certificate of the client's key ring, caches it with its private key, and returns
    /// the name of the <c>IHttpClientFactory</c> client that presents it.
    /// </summary>
    /// <param name="registration">The client registration.</param>
    /// <exception cref="InvalidOperationException">The client has no usable certificate and private key.</exception>
    public virtual string SelectHttpClientName(ClientRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var clientId = registration.ClientId;
        var keyRing = registration.KeyRing;
        if (string.IsNullOrEmpty(keyRing))
        {
            throw new InvalidOperationException(
                $"The client '{clientId}' has no key ring for its TLS client certificate."
            );
        }

        var now = TimeProvider.GetUtcNow();
        foreach (var key in KeyRingProvider.GetKeyRing(keyRing).OfType<AsymmetricSecretKey>())
        {
            if (key is not (RsaSecretKey or EccSecretKey) || !key.HasCertificate)
                continue;

            if (string.Equals(key.Metadata.Use, "enc", StringComparison.Ordinal))
                continue;

            var thumbprint = Convert.ToHexString(
                SHA256.HashData(key.CertificateRawData.Value.Span)
            );

            if (!Certificates.TryGetValue(thumbprint, out var entry))
            {
                using var publicCertificate = X509CertificateLoader.LoadCertificate(
                    key.CertificateRawData.Value.Span
                );

                if (!AllowsClientAuthentication(publicCertificate) || !HasPrivateKey(key))
                    continue;

                var certificate = Materialize(key, publicCertificate, clientId, keyRing);
                X509Certificate2Collection? issuers = null;
                try
                {
                    issuers = key.ExportIssuerCertificates();
                    entry = new SelectedCertificate(certificate, issuers);
                }
                catch
                {
                    certificate.Dispose();
                    if (issuers is not null)
                        DisposeCertificates(issuers);
                    throw;
                }

                if (!Certificates.TryAdd(thumbprint, entry))
                {
                    entry.Dispose();
                    entry = Certificates[thumbprint];
                }
            }

            if (
                now < new DateTimeOffset(entry.Certificate.NotBefore)
                || now > new DateTimeOffset(entry.Certificate.NotAfter)
            )
            {
                continue;
            }

            return OpenIdClientDefaults.MutualTlsHttpClientNamePrefix + thumbprint;
        }

        throw new InvalidOperationException(
            $"The key ring '{keyRing}' of the client '{clientId}' has no RSA or EC key with a currently valid client certificate and its private key."
        );
    }

    /// <summary>
    /// Gets the certificate previously selected for the specified SHA-256 thumbprint.
    /// </summary>
    /// <param name="thumbprint">The uppercase hexadecimal SHA-256 hash of the certificate's DER encoding.</param>
    /// <param name="certificate">The certificate with its private key, when found.</param>
    public bool TryGetCertificate(
        string thumbprint,
        [NotNullWhen(true)] out X509Certificate2? certificate
    )
    {
        if (Certificates.TryGetValue(thumbprint, out var entry))
        {
            certificate = entry.Certificate;
            return true;
        }

        certificate = null;
        return false;
    }

    /// <summary>
    /// Creates the primary HTTP handler that presents the previously selected certificate in the TLS handshake.
    /// </summary>
    /// <param name="thumbprint">The uppercase hexadecimal SHA-256 hash of the certificate's DER encoding.</param>
    /// <exception cref="InvalidOperationException">No certificate has been selected for the thumbprint.</exception>
    public virtual SocketsHttpHandler CreatePrimaryHandler(string thumbprint)
    {
        if (!Certificates.TryGetValue(thumbprint, out var entry))
        {
            throw new InvalidOperationException(
                $"No client certificate with the SHA-256 thumbprint '{thumbprint}' has been selected."
            );
        }

        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            SslOptions = new SslClientAuthenticationOptions
            {
                ClientCertificateContext = entry.Context,
            },
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var thumbprint in Certificates.Keys)
        {
            if (Certificates.TryRemove(thumbprint, out var entry))
                entry.Dispose();
        }
    }

    private static bool AllowsClientAuthentication(X509Certificate2 certificate)
    {
        foreach (var extension in certificate.Extensions)
        {
            switch (extension)
            {
                case X509EnhancedKeyUsageExtension eku:
                    var found = false;
                    foreach (var oid in eku.EnhancedKeyUsages)
                    {
                        if (
                            string.Equals(
                                oid.Value,
                                ClientAuthenticationOid,
                                StringComparison.Ordinal
                            )
                        )
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        return false;
                    break;

                case X509KeyUsageExtension keyUsage:
                    if (!keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
                        return false;
                    break;
            }
        }

        return true;
    }

    private static bool HasPrivateKey(AsymmetricSecretKey key)
    {
        try
        {
            switch (key)
            {
                case RsaSecretKey rsaKey:
                    using (var rsa = rsaKey.ExportRSA())
                    {
                        var parameters = rsa.ExportParameters(includePrivateParameters: true);
                        ZeroMemory(
                            parameters.D,
                            parameters.P,
                            parameters.Q,
                            parameters.DP,
                            parameters.DQ,
                            parameters.InverseQ
                        );
                    }
                    return true;

                case EccSecretKey eccKey:
                    using (var ecdsa = eccKey.ExportECDsa())
                    {
                        var parameters = ecdsa.ExportParameters(includePrivateParameters: true);
                        ZeroMemory(parameters.D);
                    }
                    return true;

                default:
                    return false;
            }
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static void DisposeCertificates(X509Certificate2Collection certificates)
    {
        foreach (var certificate in certificates)
        {
            certificate.Dispose();
        }
    }

    private static void ZeroMemory(params byte[]?[] buffers)
    {
        foreach (var buffer in buffers)
        {
            if (buffer is not null)
                CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private static X509Certificate2 Materialize(
        AsymmetricSecretKey key,
        X509Certificate2 publicCertificate,
        string clientId,
        string keyRing
    )
    {
        X509Certificate2 withKey;
        try
        {
            switch (key)
            {
                case RsaSecretKey rsaKey:
                    using (var rsa = rsaKey.ExportRSA())
                        withKey = publicCertificate.CopyWithPrivateKey(rsa);
                    break;

                case EccSecretKey eccKey:
                    using (var ecdsa = eccKey.ExportECDsa())
                        withKey = publicCertificate.CopyWithPrivateKey(ecdsa);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"The key '{key.Metadata.KeyId}' is not an RSA or EC key."
                    );
            }
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"The certificate of the key '{key.Metadata.KeyId}' in the key ring '{keyRing}' of the client '{clientId}' does not match its private key.",
                exception
            );
        }

        if (!OperatingSystem.IsWindows())
            return withKey;

        // SChannel cannot use an ephemeral private key for TLS, so re-import the key as a persisted-style key set.
        using (withKey)
        {
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var pfx = withKey.Export(X509ContentType.Pkcs12, password);
            try
            {
                return X509CertificateLoader.LoadPkcs12(
                    pfx,
                    password,
                    X509KeyStorageFlags.DefaultKeySet
                );
            }
            finally
            {
                CryptographicOperations.ZeroMemory(pfx);
            }
        }
    }

    // The certificates of the authorities that issued the client's certificate travel with it in the TLS handshake, so a
    // server that trusts only the root builds the chain (RFC 8446 §4.4.2); offline, nothing is downloaded. See ADR-0300.
    private sealed class SelectedCertificate(
        X509Certificate2 certificate,
        X509Certificate2Collection issuers
    ) : IDisposable
    {
        public X509Certificate2 Certificate { get; } = certificate;

        public X509Certificate2Collection Issuers { get; } = issuers;

        public SslStreamCertificateContext Context { get; } =
            SslStreamCertificateContext.Create(
                certificate,
                additionalCertificates: issuers.Count > 0 ? issuers : null,
                offline: true
            );

        public void Dispose()
        {
            Certificate.Dispose();
            DisposeCertificates(Issuers);
        }
    }
}
