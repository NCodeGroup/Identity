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
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides the default implementation of <see cref="IClientCertificateChainValidator"/>: it builds the presented
/// certificate's chain with the platform chain engine in custom-root-trust mode, so only the client's configured trust
/// anchors are trusted, never the machine's certificate store, and missing issuers are never downloaded. The platform
/// engine never fetches anything: when revocation is checked, every certificate below the trust anchor is checked
/// through the revocation checker, which retrieves revocation data through the server's outbound-request guard. A
/// certificate whose revocation status cannot be determined fails the chain unless the client's revocation mode is
/// the soft-fail mode; a revoked certificate always fails it.
/// </summary>
// See ADR-0173.
internal sealed class DefaultClientCertificateChainValidator(
    TimeProvider timeProvider,
    IClientCertificateRevocationChecker revocationChecker,
    ILogger<DefaultClientCertificateChainValidator> logger
) : IClientCertificateChainValidator
{
    // id-kp-clientAuth (RFC 5280 §4.2.1.12).
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    private TimeProvider TimeProvider { get; } = timeProvider;

    private IClientCertificateRevocationChecker RevocationChecker { get; } = revocationChecker;

    private ILogger<DefaultClientCertificateChainValidator> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<bool> ValidateAsync(
        OpenIdClient openIdClient,
        PresentedClientCertificate presentedCertificate,
        CancellationToken cancellationToken
    )
    {
        var settings = openIdClient.Settings;
        var trustAnchors = settings.TryGetValue(
            OpenIdSettingKeys.TlsClientAuthTrustAnchors,
            out var configured
        )
            ? configured
            : [];

        if (trustAnchors.Count == 0)
        {
            Logger.ClientCertificateTrustAnchorsMissing(openIdClient.ClientId);
            return false;
        }

        var revocationMode = settings.TryGetValue(
            OpenIdSettingKeys.TlsClientAuthRevocationMode,
            out var configuredMode
        )
            ? configuredMode
            : OpenIdConstants.ClientCertificateRevocationMode.Online;

        // The soft-fail mode accepts a certificate whose revocation status cannot be determined; it never accepts
        // one that is revoked. See ADR-0252.
        var acceptsUnknownStatus = string.Equals(
            revocationMode,
            OpenIdConstants.ClientCertificateRevocationMode.OnlineSoftFail,
            StringComparison.Ordinal
        );

        var loaded = new X509Certificate2Collection();
        using var chain = new X509Chain();
        try
        {
            var policy = chain.ChainPolicy;
            policy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            policy.DisableCertificateDownloads = true;
            policy.RevocationMode = X509RevocationMode.NoCheck;
            policy.VerificationTime = TimeProvider.GetUtcNow().UtcDateTime;
            policy.VerificationTimeIgnored = false;
            policy.ApplicationPolicy.Add(new Oid(ClientAuthenticationOid));

            foreach (var pem in trustAnchors)
                loaded.ImportFromPem(pem);

            foreach (var certificate in loaded)
            {
                var isSelfIssued = certificate
                    .SubjectName.RawData.AsSpan()
                    .SequenceEqual(certificate.IssuerName.RawData);
                if (isSelfIssued)
                    policy.CustomTrustStore.Add(certificate);
                else
                    policy.ExtraStore.Add(certificate);
            }

            foreach (var intermediate in presentedCertificate.Intermediates)
                policy.ExtraStore.Add(intermediate);

            if (!chain.Build(presentedCertificate.Certificate))
            {
                var chainStatus = string.Join(
                    ", ",
                    chain.ChainStatus.Select(status => status.Status.ToString()).Distinct()
                );
                Logger.ClientCertificateUntrusted(openIdClient.ClientId, chainStatus);
                return false;
            }

            if (
                string.Equals(
                    revocationMode,
                    OpenIdConstants.ClientCertificateRevocationMode.None,
                    StringComparison.Ordinal
                )
            )
            {
                return true;
            }

            // Every element except the trust anchor is checked against its issuer, the next element in the chain.
            var elements = chain.ChainElements;
            var trustAnchor = elements[elements.Count - 1].Certificate;
            X509Certificate2[] intermediates = [.. policy.ExtraStore];
            for (var i = 0; i < elements.Count - 1; i++)
            {
                var certificate = elements[i].Certificate;
                var context = new ClientCertificateRevocationContext
                {
                    Certificate = certificate,
                    Issuer = elements[i + 1].Certificate,
                    TrustAnchor = trustAnchor,
                    Intermediates = intermediates,
                };
                var status = await RevocationChecker.CheckAsync(context, cancellationToken);
                if (status == ClientCertificateRevocationStatus.Good)
                    continue;

                if (status == ClientCertificateRevocationStatus.Unknown && acceptsUnknownStatus)
                {
                    Logger.ClientCertificateRevocationUnknownAccepted(
                        openIdClient.ClientId,
                        certificate.Subject
                    );
                    continue;
                }

                var revocationStatus = status.ToString();
                Logger.ClientCertificateRevocationFailed(
                    openIdClient.ClientId,
                    certificate.Subject,
                    revocationStatus
                );
                return false;
            }

            return true;
        }
        catch (CryptographicException exception)
        {
            // A stored trust anchor that no longer parses, or a chain the platform cannot build, trusts nothing.
            Logger.ClientCertificateUntrusted(openIdClient.ClientId, exception.Message);
            return false;
        }
        finally
        {
            foreach (var element in chain.ChainElements)
                element.Certificate.Dispose();
            foreach (var certificate in loaded)
                certificate.Dispose();
        }
    }
}
