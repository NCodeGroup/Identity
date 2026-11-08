// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Provides the default implementation of <see cref="IRemoteSecretImporter"/>. It reads the public half of the
/// referenced key from its key management service through <see cref="ISecretGenerator.BindRemoteSecretAsync"/>, and
/// proves the binding by signing 32 random bytes through the referenced key with
/// <see cref="SignatureAlgorithm.SignAsync"/>, which verifies the key management service's signature with the stored
/// public half. A key the provider refuses, a public half that does not match the request and a signature that does
/// not verify are a <c>400</c>; a failure or refusal of the service is logged and is a <c>502</c>.
/// </summary>
// See ADR-1475.
// See ADR-1476.
internal class DefaultRemoteSecretImporter(
    ISecretGenerator secretGenerator,
    ISecretSerializer secretSerializer,
    IAlgorithmCollectionProvider algorithmCollectionProvider,
    ILogger<DefaultRemoteSecretImporter> logger
) : IRemoteSecretImporter
{
    private const int ChallengeSizeBytes = 32;

    private ISecretGenerator SecretGenerator { get; } = secretGenerator;

    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    private ILogger<DefaultRemoteSecretImporter> Logger { get; } = logger;

    private static ManagementError Error(int statusCode, string detail) =>
        new() { StatusCode = statusCode, Detail = detail };

    /// <inheritdoc />
    public async ValueTask<RemoteSecretImportResult> ImportAsync(
        BindRemoteSecretRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var secret = await SecretGenerator.BindRemoteSecretAsync(request, cancellationToken);
            return RemoteSecretImportResult.Bound(secret);
        }
        catch (Exception exception)
            when (exception
                    is InvalidOperationException
                        or ArgumentException
                        or CryptographicException
            )
        {
            return RemoteSecretImportResult.Refused(
                Error(
                    StatusCodes.Status400BadRequest,
                    $"The key '{request.KeyId}' of the remote key provider '{request.Provider}' cannot be bound as a '{request.Algorithm}' key: {exception.Message}"
                )
            );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.RemoteKeyProviderFailed(request.Provider, request.SecretId, exception);
            return RemoteSecretImportResult.Refused(
                Error(
                    StatusCodes.Status502BadGateway,
                    $"The key management service of the remote key provider '{request.Provider}' did not return the public key of the key '{request.KeyId}': it refused the key or the deployment's identity, or it could not be reached."
                )
            );
        }
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

        using var keyDisposal = key as IDisposable;

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
                $"The key '{reference.KeyId}' of the remote key provider '{reference.Provider}' does not sign with the private half of the public key the service returned."
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
}
