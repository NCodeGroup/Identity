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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Clients.Handlers;

/// <summary>
/// Provides a common base for the mutual-TLS client-authentication handlers (<c>tls_client_auth</c> and
/// <c>self_signed_tls_client_auth</c>) defined by <see href="https://datatracker.ietf.org/doc/html/rfc8705">RFC 8705</see>
/// §2. The client presents its <c>client_id</c> in the token request and a client certificate on the mutual-TLS
/// connection; the handler owns the request only when the resolved client is registered for this handler's method,
/// then verifies the presented certificate against the client's registration and promotes the client to a confidential
/// client on success. The two concrete handlers differ only in how they match the certificate: <c>tls_client_auth</c>
/// matches the certificate's subject DN / SAN against the client's registered metadata (PKI); whereas
/// <c>self_signed_tls_client_auth</c> matches the certificate against one of the client's registered certificates by
/// thumbprint.
/// </summary>
internal abstract class MutualTlsClientAuthenticationHandler(
    IOpenIdClientResolver clientResolver,
    IOpenIdClientFactory clientFactory,
    IAuditEventRecorder auditEventRecorder,
    ILogger logger,
    IClientCertificateResolver clientCertificateResolver
) : CommonClientAuthenticationHandler(clientResolver, clientFactory, auditEventRecorder, logger)
{
    private IClientCertificateResolver ClientCertificateResolver { get; } =
        clientCertificateResolver;

    /// <summary>
    /// Determines whether the presented client <paramref name="certificate"/> is trusted as a credential at all, before
    /// it is matched against the client's registration. The default trusts it, since the self-signed method's match is
    /// itself the proof; a PKI method validates the chain to the client's trust anchors.
    /// </summary>
    protected virtual ValueTask<bool> IsCertificateTrustedAsync(
        OpenIdClient openIdClient,
        PresentedClientCertificate certificate,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(true);

    /// <summary>
    /// Matches the presented client <paramref name="certificate"/> against the client's registration, returning the
    /// <see cref="SecretKey"/> that represents the authenticating credential when it matches, or <see langword="null"/>
    /// when it does not.
    /// </summary>
    protected abstract ValueTask<SecretKey?> MatchCertificateAsync(
        OpenIdClient openIdClient,
        PresentedClientCertificate certificate,
        CancellationToken cancellationToken
    );

    /// <inheritdoc />
    public override async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    )
    {
        var httpRequest = openIdContext.Http.Request;

        if (!HttpMethods.IsPost(httpRequest.Method) || !httpRequest.HasFormContentType)
            return ClientAuthenticationResult.Undefined;

        var form = await httpRequest.ReadFormAsync(cancellationToken);

        if (
            !form.TryGetValue(OpenIdConstants.Parameters.ClientId, out var clientIdValues)
            || string.IsNullOrEmpty(clientIdValues.ToString())
        )
        {
            // No client_id present; this handler does not apply.
            return ClientAuthenticationResult.Undefined;
        }

        var clientId = clientIdValues.ToString();

        OpenIdClient? publicClient;
        try
        {
            publicClient = await ClientResolver.ResolveClientOrDefaultAsync(
                openIdContext,
                clientId,
                cancellationToken
            );
        }
        catch (Exception exception)
            when (exception
                    is CryptographicException
                        or FormatException
                        or InvalidOperationException
            )
        {
            Logger.ClientAuthenticationCredentialDeserializationFailed(clientId, exception);
            return ClientAuthenticationResult.Undefined;
        }

        if (publicClient is null)
            return ClientAuthenticationResult.Undefined;

        // This handler owns the request only when the client is registered for its method: the request carries only a
        // client_id, as a public client's does, so the registration is what tells the two apart.
        if (
            !string.Equals(
                publicClient.RegisteredAuthenticationMethod,
                AuthenticationMethod,
                StringComparison.Ordinal
            )
        )
        {
            return ClientAuthenticationResult.Undefined;
        }

        // From here the request is owned by this handler: any failure is a conclusive invalid_client.
        var rejection = await RejectUnlessPermittedAsync(
            openIdContext,
            publicClient,
            cancellationToken
        );
        if (rejection is { } rejected)
            return rejected;

        var certificate = await ClientCertificateResolver.ResolveCertificateAsync(
            openIdContext,
            cancellationToken
        );
        if (certificate is null)
            return await FailAsync(
                openIdContext,
                clientId,
                "client_certificate_missing",
                cancellationToken
            );

        var isTrusted = await IsCertificateTrustedAsync(
            publicClient,
            certificate,
            cancellationToken
        );
        if (!isTrusted)
            return await FailAsync(
                openIdContext,
                clientId,
                "client_certificate_untrusted",
                cancellationToken
            );

        var credentialKey = await MatchCertificateAsync(
            publicClient,
            certificate,
            cancellationToken
        );
        if (credentialKey is null)
            return await FailAsync(
                openIdContext,
                clientId,
                "client_certificate_mismatch",
                cancellationToken
            );

        var confidentialClient = await ClientFactory.CreateConfidentialClientAsync(
            openIdContext,
            publicClient,
            AuthenticationMethod,
            credentialKey,
            confirmation: null,
            cancellationToken
        );

        return new ClientAuthenticationResult(confidentialClient);
    }
}
