// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Provides the default implementation of <see cref="IRemoteSecretImporter"/>. It imports the supplied public half
/// under the client-key policy of <see cref="IClientKeyImporter"/>, and proves the binding by signing 32 random bytes
/// through the referenced key with <see cref="SignatureAlgorithm.SignAsync"/>, which verifies the key management
/// service's signature with the stored public half. A signature that does not verify is a <c>400</c>; a failure or
/// refusal of the service is logged and is a <c>502</c>.
/// </summary>
// See ADR-1475.
internal class DefaultRemoteSecretImporter(
    IClientKeyImporter keyImporter,
    ISecretSerializer secretSerializer,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ILogger<DefaultRemoteSecretImporter> logger
) : IRemoteSecretImporter
{
    private const int ChallengeSizeBytes = 32;

    private IClientKeyImporter KeyImporter { get; } = keyImporter;

    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ILogger<DefaultRemoteSecretImporter> Logger { get; } = logger;

    private static ManagementError Error(int statusCode, string detail) =>
        new() { StatusCode = statusCode, Detail = detail };

    /// <inheritdoc />
    public bool TryImport(
        RemoteSecretImportRequest request,
        [NotNullWhen(true)] out PersistedSecret? secret,
        [NotNullWhen(false)] out string? error
    )
    {
        if (!KeyImporter.TryImportKey(request.Key, out var publicSecret, out error))
        {
            secret = null;
            return false;
        }

        var key = SecretSerializer.DeserializePublicSecret(publicSecret);
        try
        {
            if (key is not AsymmetricSecretKey asymmetricKey)
            {
                secret = null;
                error = "A key management service holds only asymmetric keys.";
                return false;
            }

            var subjectPublicKeyInfo = ExportSubjectPublicKeyInfo(asymmetricKey);

            secret = publicSecret with
            {
                SecretEncoding = SecretEncodings.Remote,
                EncodedValue = request.Reference.ToString(),
                PublicKey = subjectPublicKeyInfo,
                Origin = SecretOrigins.Supplied,
                ManagedBy = null,
            };
            error = null;
            return true;
        }
        finally
        {
            (key as IDisposable)?.Dispose();
        }
    }

    private static string ExportSubjectPublicKeyInfo(AsymmetricSecretKey key)
    {
        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false, 512);
        IBufferWriter<byte> writer = buffer;
        key.ExportSubjectPublicKeyInfo(ref writer);
        using var lease = buffer.GetSpanLease(isSensitive: false);
        return Base64Url.Encode(lease.Span);
    }

    /// <inheritdoc />
    public async ValueTask<ManagementError?> VerifyBindingAsync(
        PersistedSecret secret,
        CancellationToken cancellationToken
    )
    {
        SecretKey key;
        try
        {
            key = SecretSerializer.DeserializeRemoteSecret(secret);
        }
        catch (Exception exception)
            when (exception
                    is InvalidOperationException
                        or ArgumentException
                        or FormatException
                        or CryptographicException
            )
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }

        try
        {
            if (
                secret.Algorithm is null
                || !AlgorithmCollectionProvider.Collection.TryGetSignatureAlgorithm(
                    secret.Algorithm,
                    out var signatureAlgorithm
                )
            )
            {
                return Error(
                    StatusCodes.Status400BadRequest,
                    $"The algorithm '{secret.Algorithm}' is not a registered signature algorithm."
                );
            }

            if (!RemoteKeyReference.TryParse(secret.EncodedValue, out var reference))
            {
                return Error(
                    StatusCodes.Status400BadRequest,
                    $"The secret '{secret.SecretId}' does not hold a valid remote key reference."
                );
            }

            using var challengeLease = BufferFactory.Rent(
                ChallengeSizeBytes,
                isSensitive: false,
                out Memory<byte> challenge
            );
            RandomNumberGenerator.Fill(challenge.Span);

            using var signatureLease = BufferFactory.Rent(
                signatureAlgorithm.GetSignatureSizeBytes(key.KeySizeBits),
                isSensitive: false,
                out Memory<byte> signature
            );

            try
            {
                await signatureAlgorithm.SignAsync(key, challenge, signature, cancellationToken);
                return null;
            }
            catch (Exception exception) when (exception is JoseException or CryptographicException)
            {
                return Error(
                    StatusCodes.Status400BadRequest,
                    $"The key '{reference.KeyId}' of the remote key provider '{reference.Provider}' does not sign with the private half of the supplied public key."
                );
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Logger.RemoteKeyProviderFailed(reference.Provider, secret.SecretId, exception);
                return Error(
                    StatusCodes.Status502BadGateway,
                    $"The key management service of the remote key provider '{reference.Provider}' did not sign with the key '{reference.KeyId}': it refused the key or the deployment's identity, or it could not be reached."
                );
            }
        }
        finally
        {
            (key as IDisposable)?.Dispose();
        }
    }
}
