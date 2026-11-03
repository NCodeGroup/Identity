// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Clients.Handlers;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticationHandler"/> for the <c>self_signed_tls_client_auth</c>
/// client-authentication method (<see href="https://datatracker.ietf.org/doc/html/rfc8705">RFC 8705</see> §2.2): the
/// presented mutual-TLS client certificate must match one of the certificates among the client's keys, compared by
/// SHA-256 thumbprint. The client's keys are resolved through <see cref="IClientKeyResolver"/>, so a client that
/// publishes its keys at a <c>jwks_uri</c> is matched against those rather than any stored certificate; a JSON Web Key
/// carries its certificate as the first entry of its <c>x5c</c>.
/// </summary>
internal sealed class SelfSignedTlsClientAuthClientAuthenticationHandler(
    IOpenIdClientResolver clientResolver,
    IOpenIdClientFactory clientFactory,
    IAuditEventRecorder auditEventRecorder,
    IClientAuthenticationAttemptLimiter attemptLimiter,
    ILogger<SelfSignedTlsClientAuthClientAuthenticationHandler> logger,
    IClientCertificateResolver clientCertificateResolver,
    IClientKeyResolver clientKeyResolver
)
    : MutualTlsClientAuthenticationHandler(
        clientResolver,
        clientFactory,
        auditEventRecorder,
        attemptLimiter,
        logger,
        clientCertificateResolver
    )
{
    private IClientKeyResolver ClientKeyResolver { get; } = clientKeyResolver;

    /// <inheritdoc />
    public override string AuthenticationMethod =>
        OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth;

    /// <inheritdoc />
    protected override async ValueTask<SecretKey?> MatchCertificateAsync(
        OpenIdClient openIdClient,
        PresentedClientCertificate certificate,
        CancellationToken cancellationToken
    )
    {
        var clientKeys = await ClientKeyResolver.ResolveSecretKeysAsync(
            openIdClient,
            cancellationToken
        );

        return MatchCertificate(clientKeys, certificate.Certificate);
    }

    private static AsymmetricSecretKey? MatchCertificate(
        IEnumerable<SecretKey> clientKeys,
        X509Certificate2 certificate
    )
    {
        // SHA-256 is a fixed 32 bytes, so both thumbprints live on the stack and RawDataMemory avoids copying the
        // presented certificate's DER into a new array.
        Span<byte> presentedThumbprint = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(certificate.RawDataMemory.Span, presentedThumbprint);

        Span<byte> registeredThumbprint = stackalloc byte[SHA256.HashSizeInBytes];
        foreach (var secretKey in clientKeys.OfType<AsymmetricSecretKey>())
        {
            if (!secretKey.HasCertificate)
                continue;

            // Hash the key's stored DER directly rather than materializing an X509Certificate2 per registered key.
            SHA256.HashData(secretKey.CertificateRawData.Value.Span, registeredThumbprint);

            if (CryptographicOperations.FixedTimeEquals(presentedThumbprint, registeredThumbprint))
                return secretKey;
        }

        return null;
    }
}
