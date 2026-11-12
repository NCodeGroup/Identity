// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Management.Logging;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Provides the default implementation of <see cref="IRemoteSecretImporter"/>. It reads the public half of the
/// referenced key from its key management service through <see cref="ISecretGenerator.BindRemoteSecretAsync"/>, and
/// proves the binding with <see cref="IRemoteKeyProver"/>, so a key that does not hold the private half of the public
/// half the service returned is never stored. A refusal of the generator or the prover
/// (<see cref="RemoteKeyRefusedException"/>), a key identifier the provider does not read, a key it refuses, a public
/// half that does not match the request and a signature or decryption that does not prove the key are a <c>400</c>;
/// any other failure is logged and is a <c>502</c>.
/// </summary>
// See ADR-1475.
// See ADR-1476.
// See ADR-1471.
internal class DefaultRemoteSecretImporter(
    ISecretGenerator secretGenerator,
    ISecretSerializer secretSerializer,
    IRemoteKeyProver remoteKeyProver,
    ILogger<DefaultRemoteSecretImporter> logger
) : IRemoteSecretImporter
{
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;

    private ISecretSerializer SecretSerializer { get; } = secretSerializer;

    private IRemoteKeyProver RemoteKeyProver { get; } = remoteKeyProver;

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
        // The generator's refusal and a provider's documented refusals, a key identifier it does not read and a key it refuses, are the caller's to fix; any other exception is a failure of the key management service or its client.
        catch (Exception exception)
            when (exception
                    is RemoteKeyRefusedException
                        or ArgumentException
                        or CryptographicException
            )
        {
            var refusal = Error(
                StatusCodes.Status400BadRequest,
                $"The key '{request.KeyId}' of the remote key provider '{request.Provider}' cannot be bound as a '{request.Algorithm}' key: {exception.Message}"
            );
            return RemoteSecretImportResult.Refused(refusal);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.RemoteKeyProviderFailed(request.Provider, request.SecretId, exception);
            var failure = Error(
                StatusCodes.Status502BadGateway,
                $"The key management service of the remote key provider '{request.Provider}' did not return the public key of the key '{request.KeyId}': it refused the key or the deployment's identity, or it could not be reached."
            );
            return RemoteSecretImportResult.Refused(failure);
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

        if (!RemoteKeyReference.TryParse(secret.EncodedValue, out var reference))
        {
            return Error(
                StatusCodes.Status400BadRequest,
                $"The secret '{secret.SecretId}' does not hold a valid remote key reference."
            );
        }

        try
        {
            await RemoteKeyProver.ProveAsync(key, cancellationToken);
            return null;
        }
        catch (RemoteKeyRefusedException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.RemoteKeyProviderFailed(reference.Provider, secret.SecretId, exception);
            var operation =
                ((IRemoteSecretKey)key).Binding.Algorithm is RemoteSigningAlgorithm
                    ? "sign"
                    : "decrypt";
            return Error(
                StatusCodes.Status502BadGateway,
                $"The key management service of the remote key provider '{reference.Provider}' did not {operation} with the key '{reference.KeyId}': it refused the key or the deployment's identity, or it could not be reached."
            );
        }
    }
}
