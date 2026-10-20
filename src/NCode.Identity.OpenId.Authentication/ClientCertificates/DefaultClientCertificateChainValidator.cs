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
/// anchors are trusted, never the machine's certificate store, and missing issuers are never downloaded.
/// </summary>
internal sealed class DefaultClientCertificateChainValidator(
    TimeProvider timeProvider,
    ILogger<DefaultClientCertificateChainValidator> logger
) : IClientCertificateChainValidator
{
    // id-kp-clientAuth (RFC 5280 §4.2.1.12).
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    // Bounds the time an online revocation check may hold the token request.
    private static readonly TimeSpan RevocationRetrievalTimeout = TimeSpan.FromSeconds(5);

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<DefaultClientCertificateChainValidator> Logger { get; } = logger;

    /// <inheritdoc />
    public ValueTask<bool> ValidateAsync(
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
            return ValueTask.FromResult(false);
        }

        var revocationMode = settings.TryGetValue(
            OpenIdSettingKeys.TlsClientAuthRevocationMode,
            out var configuredMode
        )
            ? configuredMode
            : OpenIdConstants.ClientCertificateRevocationMode.Online;

        var loaded = new X509Certificate2Collection();
        using var chain = new X509Chain();
        try
        {
            var policy = chain.ChainPolicy;
            policy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            policy.DisableCertificateDownloads = true;
            policy.RevocationMode = ToRevocationMode(revocationMode);
            policy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            policy.UrlRetrievalTimeout = RevocationRetrievalTimeout;
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

            if (chain.Build(presentedCertificate.Certificate))
                return ValueTask.FromResult(true);

            var chainStatus = string.Join(
                ", ",
                chain.ChainStatus.Select(status => status.Status.ToString()).Distinct()
            );
            Logger.ClientCertificateUntrusted(openIdClient.ClientId, chainStatus);
            return ValueTask.FromResult(false);
        }
        finally
        {
            foreach (var element in chain.ChainElements)
                element.Certificate.Dispose();
            foreach (var certificate in loaded)
                certificate.Dispose();
        }
    }

    private static X509RevocationMode ToRevocationMode(string revocationMode) =>
        revocationMode switch
        {
            OpenIdConstants.ClientCertificateRevocationMode.None => X509RevocationMode.NoCheck,
            OpenIdConstants.ClientCertificateRevocationMode.Offline => X509RevocationMode.Offline,
            _ => X509RevocationMode.Online,
        };
}
