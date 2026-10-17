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

using System.Formats.Asn1;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Clients.Handlers;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticationHandler"/> for the <c>tls_client_auth</c>
/// client-authentication method (<see href="https://datatracker.ietf.org/doc/html/rfc8705">RFC 8705</see> §2.1): the
/// presented mutual-TLS client certificate is PKI-validated by the TLS layer, and its subject distinguished name or a
/// subject alternative name must match the single expected identity the client registered
/// (<c>tls_client_auth_subject_dn</c> or one of the <c>tls_client_auth_san_*</c> values).
/// </summary>
internal sealed class TlsClientAuthClientAuthenticationHandler(
    IOpenIdClientResolver clientResolver,
    IOpenIdClientFactory clientFactory,
    IAuditEventRecorder auditEventRecorder,
    ILogger<TlsClientAuthClientAuthenticationHandler> logger,
    IClientCertificateResolver clientCertificateResolver,
    ISecretKeyFactory secretKeyFactory
)
    : MutualTlsClientAuthenticationHandler(
        clientResolver,
        clientFactory,
        auditEventRecorder,
        logger,
        clientCertificateResolver
    )
{
    private const string SubjectAlternativeNameOid = "2.5.29.17";

    // GeneralName CHOICE context-specific tag numbers (RFC 5280 §4.2.1.6).
    private const int Rfc822NameTag = 1;
    private const int UniformResourceIdentifierTag = 6;

    private ISecretKeyFactory SecretKeyFactory { get; } = secretKeyFactory;

    /// <inheritdoc />
    public override string AuthenticationMethod =>
        OpenIdConstants.ClientAuthenticationMethods.TlsClientAuth;

    /// <inheritdoc />
    protected override SecretKey? MatchCertificate(
        OpenIdClient openIdClient,
        X509Certificate2 certificate
    )
    {
        var settings = openIdClient.Settings;

        if (!IsExpectedIdentityPresented(settings, certificate))
            return null;

        var metadata = new KeyMetadata
        {
            KeyId = certificate.Thumbprint,
            ExpiresWhen = certificate.NotAfter,
        };
        return SecretKeyFactory.Create(metadata, certificate);
    }

    private static bool IsExpectedIdentityPresented(
        NCode.Identity.Settings.IReadOnlySettingCollection settings,
        X509Certificate2 certificate
    )
    {
        var expectedSubjectDn = GetValueOrEmpty(settings, OpenIdSettingKeys.TlsClientAuthSubjectDn);
        if (!string.IsNullOrEmpty(expectedSubjectDn))
            return SubjectDnMatches(expectedSubjectDn, certificate);

        var expectedDns = GetValueOrEmpty(settings, OpenIdSettingKeys.TlsClientAuthSanDns);
        var expectedUri = GetValueOrEmpty(settings, OpenIdSettingKeys.TlsClientAuthSanUri);
        var expectedIp = GetValueOrEmpty(settings, OpenIdSettingKeys.TlsClientAuthSanIp);
        var expectedEmail = GetValueOrEmpty(settings, OpenIdSettingKeys.TlsClientAuthSanEmail);

        if (
            string.IsNullOrEmpty(expectedDns)
            && string.IsNullOrEmpty(expectedUri)
            && string.IsNullOrEmpty(expectedIp)
            && string.IsNullOrEmpty(expectedEmail)
        )
        {
            // The client is registered for tls_client_auth but configured no expected identity: never match.
            return false;
        }

        var generic = certificate.Extensions.FirstOrDefault(e =>
            string.Equals(e.Oid?.Value, SubjectAlternativeNameOid, StringComparison.Ordinal)
        );
        if (generic is null)
            return false;

        var san =
            generic as X509SubjectAlternativeNameExtension
            ?? new X509SubjectAlternativeNameExtension(generic.RawData, generic.Critical);

        if (
            !string.IsNullOrEmpty(expectedDns)
            && san.EnumerateDnsNames().Contains(expectedDns, StringComparer.OrdinalIgnoreCase)
        )
            return true;

        if (
            !string.IsNullOrEmpty(expectedIp)
            && IPAddress.TryParse(expectedIp, out var expectedAddress)
            && san.EnumerateIPAddresses().Any(address => address.Equals(expectedAddress))
        )
            return true;

        // The BCL's X509SubjectAlternativeNameExtension surfaces only DNS names and IP addresses; the
        // uniformResourceIdentifier and rfc822Name (email) GeneralName choices have no typed accessor, so those two —
        // and only those two — are decoded from the extension's ASN.1 directly.
        if (!string.IsNullOrEmpty(expectedUri) || !string.IsNullOrEmpty(expectedEmail))
            return MatchesUriOrEmailSan(generic.RawData, expectedUri, expectedEmail);

        return false;
    }

    private static bool SubjectDnMatches(string expectedSubjectDn, X509Certificate2 certificate)
    {
        try
        {
            // Compare canonical DER encodings so formatting/whitespace differences do not matter.
            var expected = new X500DistinguishedName(expectedSubjectDn);
            return CryptographicOperations.FixedTimeEquals(
                expected.RawData,
                certificate.SubjectName.RawData
            );
        }
        catch (CryptographicException)
        {
            // A malformed configured DN cannot match any certificate.
            return false;
        }
    }

    private static bool MatchesUriOrEmailSan(
        byte[] sanRawData,
        string expectedUri,
        string expectedEmail
    )
    {
        try
        {
            var reader = new AsnReader(sanRawData, AsnEncodingRules.DER);
            var sequence = reader.ReadSequence();
            while (sequence.HasData)
            {
                var tag = sequence.PeekTag();
                if (
                    tag.TagClass != TagClass.ContextSpecific
                    || tag.TagValue is not (Rfc822NameTag or UniformResourceIdentifierTag)
                )
                {
                    sequence.ReadEncodedValue();
                    continue;
                }

                // [1] rfc822Name (email) and [6] uniformResourceIdentifier are both IA5String GeneralName choices.
                var value = sequence.ReadCharacterString(UniversalTagNumber.IA5String, tag);

                if (
                    tag.TagValue == UniformResourceIdentifierTag
                    && !string.IsNullOrEmpty(expectedUri)
                    && string.Equals(value, expectedUri, StringComparison.Ordinal)
                )
                    return true;

                if (
                    tag.TagValue == Rfc822NameTag
                    && !string.IsNullOrEmpty(expectedEmail)
                    && string.Equals(value, expectedEmail, StringComparison.OrdinalIgnoreCase)
                )
                    return true;
            }
        }
        catch (AsnContentException)
        {
            // A malformed SAN extension yields no matchable names.
        }

        return false;
    }

    private static string GetValueOrEmpty(
        NCode.Identity.Settings.IReadOnlySettingCollection settings,
        NCode.Identity.Settings.SettingKey<string> key
    ) => settings.TryGetValue(key, out var value) ? value : string.Empty;
}
