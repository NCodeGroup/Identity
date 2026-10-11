#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Clients.Handlers;

/// <summary>
/// Provides a common base for <see cref="IClientAuthenticationHandler"/> implementations.
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
    /// Authenticates the client using the specified <paramref name="clientId"/> and <paramref name="clientSecret"/>.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="clientId">Contains the client identifier.</param>
    /// <param name="clientSecret">Contains the client secret.</param>
    /// <param name="hasClientSecret">Indicates whether the client secret was specified.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ClientAuthenticationResult"/>.</returns>
    protected async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        string clientId,
        ReadOnlyMemory<char> clientSecret,
        bool hasClientSecret,
        CancellationToken cancellationToken
    )
    {
        var tenantId = openIdContext.Tenant.TenantId;

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
            // A stored credential could not be deserialized (for example a secret protected by a
            // data-protection key that is no longer available); surface it as invalid_client with a diagnostic.
            Logger.ClientAuthenticationCredentialDeserializationFailed(clientId, exception);
            await AuditEventRecorder.RecordClientAuthenticationFailedAsync(
                openIdContext,
                clientId,
                "credential_deserialization_failed",
                cancellationToken
            );
            return new ClientAuthenticationResult(
                openIdContext
                    .ErrorFactory.InvalidClient()
                    .WithStatusCode(StatusCodes.Status400BadRequest)
            );
        }

        if (publicClient is null)
        {
            Logger.ClientAuthenticationClientNotFound(clientId, tenantId);
            await AuditEventRecorder.RecordClientAuthenticationFailedAsync(
                openIdContext,
                clientId,
                "client_not_found",
                cancellationToken
            );
            return new ClientAuthenticationResult(
                openIdContext
                    .ErrorFactory.InvalidClient()
                    .WithStatusCode(StatusCodes.Status400BadRequest)
            );
        }

        if (!hasClientSecret)
            return new ClientAuthenticationResult(publicClient);

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

        return await AuthenticateClientAsync(
            openIdContext,
            publicClient,
            clientSecretBytes,
            cancellationToken
        );
    }

    /// <summary>
    /// Authenticates the client using the specified <paramref name="publicClient"/> and <paramref name="clientSecretBytes"/>.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="publicClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="clientSecretBytes">Contains the client secret as a byte buffer.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ClientAuthenticationResult"/>.</returns>
    protected async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        OpenIdClient publicClient,
        ReadOnlyMemory<byte> clientSecretBytes,
        CancellationToken cancellationToken
    )
    {
        foreach (var secretKey in publicClient.SecretKeys.OfType<SymmetricSecretKey>())
        {
            if (!IsSecretEqual(secretKey, clientSecretBytes.Span))
                continue;

            var confidentialClient = await ClientFactory.CreateConfidentialClientAsync(
                openIdContext,
                publicClient,
                AuthenticationMethod,
                secretKey,
                confirmation: null,
                cancellationToken
            );

            return new ClientAuthenticationResult(confidentialClient);
        }

        // client secret was specified but failed to verify
        Logger.ClientAuthenticationSecretMismatch(publicClient.ClientId);
        await AuditEventRecorder.RecordClientAuthenticationFailedAsync(
            openIdContext,
            publicClient.ClientId,
            "invalid_client_secret",
            cancellationToken
        );
        return new ClientAuthenticationResult(
            openIdContext
                .ErrorFactory.InvalidClient()
                .WithStatusCode(StatusCodes.Status400BadRequest)
        );
    }

    /// <summary>
    /// Determines whether the specified symmetric <paramref name="secretKey"/> is equal to the specified <paramref name="clientSecretBytes"/>.
    /// </summary>
    /// <param name="secretKey">The <see cref="SymmetricSecretKey"/> instance to compare.</param>
    /// <param name="clientSecretBytes">The client secret byte array to compare.</param>
    /// <returns><c>true</c> if the specified symmetric <paramref name="secretKey"/> is equal to the specified <paramref name="clientSecretBytes"/>; otherwise, <c>false</c>.</returns>
    protected static bool IsSecretEqual(
        SymmetricSecretKey secretKey,
        ReadOnlySpan<byte> clientSecretBytes
    )
    {
        // increase our chances for a single-segment buffer
        using var privateKeyBuffer = BufferFactory.CreatePooledBufferWriter(
            isSensitive: true,
            clientSecretBytes.Length
        );
        IBufferWriter<byte> privateKeyWriter = privateKeyBuffer;

        secretKey.ExportPrivateKey(ref privateKeyWriter);

        using var privateKeySpanLease = privateKeyBuffer.GetSpanLease(isSensitive: true);
        var privateKeySpan = privateKeySpanLease.Span;

        return CryptographicOperations.FixedTimeEquals(privateKeySpan, clientSecretBytes);
    }
}
