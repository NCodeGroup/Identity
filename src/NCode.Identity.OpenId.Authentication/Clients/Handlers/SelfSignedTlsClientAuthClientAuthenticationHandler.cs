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
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Clients.Handlers;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticationHandler"/> for the <c>self_signed_tls_client_auth</c>
/// client-authentication method (<see href="https://datatracker.ietf.org/doc/html/rfc8705">RFC 8705</see> §2.2): the
/// presented mutual-TLS client certificate must match one of the certificates the client registered, compared by
/// SHA-256 thumbprint.
/// </summary>
internal sealed class SelfSignedTlsClientAuthClientAuthenticationHandler(
    IOpenIdClientResolver clientResolver,
    IOpenIdClientFactory clientFactory,
    IAuditEventRecorder auditEventRecorder,
    ILogger<SelfSignedTlsClientAuthClientAuthenticationHandler> logger,
    IClientCertificateResolver clientCertificateResolver
)
    : MutualTlsClientAuthenticationHandler(
        clientResolver,
        clientFactory,
        auditEventRecorder,
        logger,
        clientCertificateResolver
    )
{
    /// <inheritdoc />
    public override string AuthenticationMethod =>
        OpenIdConstants.ClientAuthenticationMethods.SelfSignedTlsClientAuth;

    /// <inheritdoc />
    protected override SecretKey? MatchCertificate(
        OpenIdClient openIdClient,
        X509Certificate2 certificate
    )
    {
        // SHA-256 is a fixed 32 bytes, so both thumbprints live on the stack and RawDataMemory avoids copying the
        // presented certificate's DER into a new array.
        Span<byte> presentedThumbprint = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(certificate.RawDataMemory.Span, presentedThumbprint);

        Span<byte> registeredThumbprint = stackalloc byte[SHA256.HashSizeInBytes];
        foreach (var secretKey in openIdClient.SecretKeys.OfType<AsymmetricSecretKey>())
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
