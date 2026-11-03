// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.OpenId.Authentication.Clients.Handlers;

/// <summary>
/// Provides a common base for <see cref="IClientAuthenticationHandler"/> implementations. A handler authenticates only
/// a client registered to use its <see cref="AuthenticationMethod"/> whose ceiling permits it, so the credential a client
/// presents never authenticates it with a method other than the one it is registered for.
/// </summary>
internal abstract class CommonClientAuthenticationHandler(
    IOpenIdClientResolver clientResolver,
    IOpenIdClientFactory clientFactory,
    IAuditEventRecorder auditEventRecorder,
    ILogger logger
) : IClientAuthenticationHandler
{
    /// <summary>
    /// Gets the <see cref="IOpenIdClientResolver"/> instance.
    /// </summary>
    protected IOpenIdClientResolver ClientResolver { get; } = clientResolver;

    /// <summary>
    /// Gets the <see cref="IOpenIdClientFactory"/> instance.
    /// </summary>
    protected IOpenIdClientFactory ClientFactory { get; } = clientFactory;

    /// <summary>
    /// Gets the <see cref="IAuditEventRecorder"/> instance.
    /// </summary>
    protected IAuditEventRecorder AuditEventRecorder { get; } = auditEventRecorder;

    /// <summary>
    /// Gets the <see cref="ILogger"/> instance.
    /// </summary>
    protected ILogger Logger { get; } = logger;

    /// <inheritdoc />
    public abstract string AuthenticationMethod { get; }

    /// <inheritdoc />
    public abstract ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Creates the <see cref="ClientAuthenticationResult"/> returned when client authentication fails with the
    /// <c>invalid_client</c> error. The base implementation responds with <c>400 Bad Request</c>; handlers whose
    /// credentials arrive in the HTTP <c>Authorization</c> request header override this to respond with
    /// <c>401 Unauthorized</c> and a matching <c>WWW-Authenticate</c> challenge, as required by
    /// <see href="https://datatracker.ietf.org/doc/html/rfc6749#section-5.2">RFC 6749 §5.2</see>.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <returns>The <see cref="ClientAuthenticationResult"/> representing the <c>invalid_client</c> failure.</returns>
    protected virtual ClientAuthenticationResult CreateInvalidClientResult(
        OpenIdContext openIdContext
    ) =>
        new(
            openIdContext
                .ErrorFactory.InvalidClient()
                .WithStatusCode(StatusCodes.Status400BadRequest)
        );

    /// <summary>
    /// Resolves the client named by <paramref name="clientId"/>. The result contains the client, or the conclusive
    /// <c>invalid_client</c> failure when no enabled client with that identifier belongs to the tenant or its stored
    /// credentials cannot be read.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="clientId">Contains the client identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ClientAuthenticationResult"/>
    /// with the resolved client or the failure.</returns>
    protected async ValueTask<ClientAuthenticationResult> ResolveClientAsync(
        OpenIdContext openIdContext,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        OpenIdClient? client;
        try
        {
            client = await ClientResolver.ResolveClientOrDefaultAsync(
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
            // A stored credential could not be deserialized (for example a secret protected by a master key or root
            // key that is no longer available); surface it as invalid_client with a diagnostic.
            Logger.ClientAuthenticationCredentialDeserializationFailed(clientId, exception);
            return await FailAsync(
                openIdContext,
                clientId,
                "credential_deserialization_failed",
                cancellationToken
            );
        }

        if (client is null)
        {
            Logger.ClientAuthenticationClientNotFound(clientId, openIdContext.Tenant.TenantId);
            return await FailAsync(openIdContext, clientId, "client_not_found", cancellationToken);
        }

        return new ClientAuthenticationResult(client);
    }

    /// <summary>
    /// Rejects the <paramref name="client"/> unless it may authenticate with this handler's
    /// <see cref="AuthenticationMethod"/>: the method must be the one the client is registered to use, and the client's
    /// <c>token_endpoint_auth_methods_supported</c> ceiling must permit it.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="client">The client presenting this handler's credentials.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation, containing the <c>invalid_client</c>
    /// failure, or <see langword="null"/> when the client may authenticate with this handler's method.</returns>
    // See ADR-1145.
    protected async ValueTask<ClientAuthenticationResult?> RejectUnlessRegisteredAsync(
        OpenIdContext openIdContext,
        OpenIdClient client,
        CancellationToken cancellationToken
    )
    {
        var registeredMethod = client.RegisteredAuthenticationMethod;
        if (!string.Equals(registeredMethod, AuthenticationMethod, StringComparison.Ordinal))
        {
            Logger.ClientAuthenticationMethodNotRegistered(
                client.ClientId,
                AuthenticationMethod,
                registeredMethod
            );
            return await FailAsync(
                openIdContext,
                client.ClientId,
                "authentication_method_not_registered",
                cancellationToken
            );
        }

        return await RejectUnlessPermittedAsync(openIdContext, client, cancellationToken);
    }

    /// <summary>
    /// Rejects the <paramref name="client"/> unless its <c>token_endpoint_auth_methods_supported</c> ceiling permits the
    /// client-authentication method it is registered to use. A handler that owns a request only for the clients
    /// registered for its method checks the registration itself and the ceiling with this.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="client">The client registered for this handler's method.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation, containing the <c>invalid_client</c>
    /// failure, or <see langword="null"/> when the ceiling permits the client's registered method.</returns>
    protected async ValueTask<ClientAuthenticationResult?> RejectUnlessPermittedAsync(
        OpenIdContext openIdContext,
        OpenIdClient client,
        CancellationToken cancellationToken
    )
    {
        if (client.IsRegisteredAuthenticationMethodPermitted)
            return null;

        Logger.ClientAuthenticationMethodNotPermitted(
            client.ClientId,
            client.RegisteredAuthenticationMethod
        );
        return await FailAsync(
            openIdContext,
            client.ClientId,
            "authentication_method_not_permitted",
            cancellationToken
        );
    }

    /// <summary>
    /// Records a failed client authentication and creates the <c>invalid_client</c> result for it.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="clientId">Contains the identifier of the client that failed to authenticate.</param>
    /// <param name="reason">Contains the audited reason for the failure.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation, containing the <c>invalid_client</c>
    /// <see cref="ClientAuthenticationResult"/>.</returns>
    protected async ValueTask<ClientAuthenticationResult> FailAsync(
        OpenIdContext openIdContext,
        string clientId,
        string reason,
        CancellationToken cancellationToken
    )
    {
        await AuditEventRecorder.RecordClientAuthenticationFailedAsync(
            openIdContext,
            clientId,
            AuthenticationMethod,
            reason,
            cancellationToken
        );
        return CreateInvalidClientResult(openIdContext);
    }

    /// <summary>
    /// Authenticates the client identified by <paramref name="clientId"/> with the shared
    /// <paramref name="clientSecret"/> it presented: the client must be registered for this handler's method, and the
    /// secret must match one of the client's one-way verifiers, the form every secret of a method that compares its
    /// secret is stored in.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="clientId">Contains the client identifier.</param>
    /// <param name="clientSecret">Contains the client secret.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ClientAuthenticationResult"/>.</returns>
    protected async ValueTask<ClientAuthenticationResult> AuthenticateClientSecretAsync(
        OpenIdContext openIdContext,
        string clientId,
        ReadOnlyMemory<char> clientSecret,
        CancellationToken cancellationToken
    )
    {
        var resolution = await ResolveClientAsync(openIdContext, clientId, cancellationToken);
        if (!resolution.HasClient)
            return resolution;

        var client = resolution.Client;

        var rejection = await RejectUnlessRegisteredAsync(openIdContext, client, cancellationToken);
        if (rejection is { } rejected)
            return rejected;

        var clientSecretChars = clientSecret.Span;
        var byteCount = SecureEncoding.UTF8.GetByteCount(clientSecretChars);
        using var _ = BufferFactory.Rent(
            byteCount,
            isSensitive: true,
            out Memory<byte> clientSecretBytes
        );

        var decodeResult = SecureEncoding.UTF8.TryGetBytes(
            clientSecretChars,
            clientSecretBytes.Span,
            out var bytesWritten
        );
        Debug.Assert(decodeResult && bytesWritten == byteCount);

        return await VerifyClientSecretAsync(
            openIdContext,
            client,
            clientSecretBytes,
            cancellationToken
        );
    }

    // A presented secret is compared only with the client's verifiers: a method that compares its secret stores nothing
    // else. A verifier with a work factor waits for its turn, so unauthenticated requests cannot occupy every
    // processor, and one refused a turn answers 503 instead of invalid_client, because the secret was never compared.
    // See ADR-1391.
    private async ValueTask<ClientAuthenticationResult> VerifyClientSecretAsync(
        OpenIdContext openIdContext,
        OpenIdClient publicClient,
        ReadOnlyMemory<byte> clientSecretBytes,
        CancellationToken cancellationToken
    )
    {
        var match = await FindMatchingSecretAsync(
            publicClient,
            clientSecretBytes,
            cancellationToken
        );

        switch (match.Result)
        {
            case SecretVerificationResult.Success:
                var confidentialClient = await ClientFactory.CreateConfidentialClientAsync(
                    openIdContext,
                    publicClient,
                    AuthenticationMethod,
                    match.CredentialId,
                    confirmation: null,
                    cancellationToken
                );
                return new ClientAuthenticationResult(confidentialClient);

            case SecretVerificationResult.Unavailable:
                Logger.ClientAuthenticationSecretVerificationUnavailable(publicClient.ClientId);
                await AuditEventRecorder.RecordClientAuthenticationFailedAsync(
                    openIdContext,
                    publicClient.ClientId,
                    AuthenticationMethod,
                    "secret_verification_unavailable",
                    cancellationToken
                );
                return new ClientAuthenticationResult(
                    CreateVerificationUnavailableError(openIdContext)
                );

            default:
                // client secret was specified but failed to verify
                Logger.ClientAuthenticationSecretMismatch(publicClient.ClientId);
                return await FailAsync(
                    openIdContext,
                    publicClient.ClientId,
                    "invalid_client_secret",
                    cancellationToken
                );
        }
    }

    // The verifiers without a work factor run first, so a presented secret that matches a generated secret's SHA-256
    // verifier never waits for a turn, and a wrong one costs a derivation only for a client that holds a supplied
    // secret. Each comparison is constant-time; which one matched, if any, is what the caller learns anyway.
    private static async ValueTask<ClientSecretMatch> FindMatchingSecretAsync(
        OpenIdClient client,
        ReadOnlyMemory<byte> clientSecretBytes,
        CancellationToken cancellationToken
    )
    {
        foreach (var verifier in client.SecretVerifiers)
        {
            if (
                !verifier.HasWorkFactor
                && await verifier.VerifyAsync(clientSecretBytes, cancellationToken)
                    == SecretVerificationResult.Success
            )
            {
                return ClientSecretMatch.Matched(verifier.KeyId);
            }
        }

        foreach (var verifier in client.SecretVerifiers)
        {
            if (!verifier.HasWorkFactor)
            {
                continue;
            }

            var result = await verifier.VerifyAsync(clientSecretBytes, cancellationToken);
            if (result == SecretVerificationResult.Success)
            {
                return ClientSecretMatch.Matched(verifier.KeyId);
            }

            if (result == SecretVerificationResult.Unavailable)
            {
                return ClientSecretMatch.Unavailable;
            }
        }

        return ClientSecretMatch.Failed;
    }

    // The server is saturated, not the client's credential wrong: 503 with temporarily_unavailable, as the rate limits
    // answer temporarily_unavailable, and a Retry-After of one second. See ADR-1391.
    private static IOpenIdError CreateVerificationUnavailableError(OpenIdContext openIdContext) =>
        openIdContext
            .ErrorFactory.Create(OpenIdProtocol.ErrorCodes.TemporarilyUnavailable)
            .WithDescription("The client secret could not be verified; retry later.")
            .WithStatusCode(StatusCodes.Status503ServiceUnavailable)
            .WithResponseHeader(HeaderNames.RetryAfter, "1")
            .WithResponseHeader(HeaderNames.CacheControl, "no-store");
}
