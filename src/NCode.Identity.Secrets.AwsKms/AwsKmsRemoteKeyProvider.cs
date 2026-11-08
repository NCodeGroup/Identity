// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.AwsKms.Facades;
using NCode.Identity.Secrets.AwsKms.Options;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.AwsKms;

/// <summary>
/// Provides an implementation of <see cref="IRemoteKeyProvider"/> that creates signing keys in AWS Key Management
/// Service, signs with them, and schedules them for deletion, so the private half of a key never leaves the service.
/// The provider reaches the service with the credentials of the deployment.
/// </summary>
/// <remarks>
/// <para>
/// AWS KMS has no versions of an asymmetric key, so every key the provider creates is a new KMS key in the configured
/// region, tagged with its owner, secret, algorithm and group. The identifier of a key is its ARN, and every call that
/// uses the key goes to the region that the ARN names.
/// </para>
/// <para>
/// The provider refuses a public key that is not a signing key of the requested specification for the requested
/// algorithm with a <see cref="CryptographicException"/>, and a response that names another key or another algorithm
/// than requested with an <see cref="IOException"/>, because the call can be retried. Logs and messages carry
/// identifiers only, never the signing input, a digest or a signature.
/// </para>
/// </remarks>
// See ADR-1471.
internal class AwsKmsRemoteKeyProvider(
    IAwsKmsClientFactory clientFactory,
    IOptions<AwsKmsRemoteKeyProviderOptions> options
) : IRemoteKeyProvider
{
    /// <summary>
    /// The key of the tag that carries the identifier of the owner of a key.
    /// </summary>
    internal const string OwnerTagKey = "ncode-identity:owner";

    /// <summary>
    /// The key of the tag that carries the identifier of the secret of a key.
    /// </summary>
    internal const string SecretTagKey = "ncode-identity:secret";

    /// <summary>
    /// The key of the tag that carries the JOSE algorithm of a key.
    /// </summary>
    internal const string AlgorithmTagKey = "ncode-identity:algorithm";

    /// <summary>
    /// The key of the tag that carries the group of a key, the name of the stable key whose versions are one owner's
    /// successive keys.
    /// </summary>
    internal const string GroupTagKey = "ncode-identity:group";

    private IAwsKmsClientFactory ClientFactory { get; } = clientFactory;

    private IOptions<AwsKmsRemoteKeyProviderOptions> ProviderOptions { get; } = options;

    /// <inheritdoc />
    public string Name => RemoteKeyProviderNames.AwsKms;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured region is not set.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the service creates a key whose identifier is not a KMS key ARN.
    /// </exception>
    public async ValueTask<RemoteKeyCreationResult> CreateKeyAsync(
        RemoteKeyCreationRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var region = ProviderOptions.Value.Region;
        if (string.IsNullOrEmpty(region))
        {
            throw new InvalidOperationException(
                "SecretProtection:RemoteKeyProviders:AwsKms:Region is not set."
            );
        }

        var row = request.Algorithm;
        var createRequest = new CreateKeyRequest
        {
            KeySpec = KeySpec.FindValue(row.KeySpec),
            KeyUsage = KeyUsageType.SIGN_VERIFY,
            Description = $"NCode.Identity signing key {request.SecretId} of {request.OwnerId}",
            Tags =
            [
                new Tag { TagKey = OwnerTagKey, TagValue = request.OwnerId },
                new Tag { TagKey = SecretTagKey, TagValue = request.SecretId },
                new Tag { TagKey = AlgorithmTagKey, TagValue = row.Algorithm },
                new Tag { TagKey = GroupTagKey, TagValue = request.GroupName },
            ],
        };

        var created = await ClientFactory.ExecuteAsync(
            region,
            access: null,
            (client, token) =>
                new ValueTask<CreateKeyResponse>(client.CreateKeyAsync(createRequest, token)),
            cancellationToken
        );

        var keyArn = created.KeyMetadata?.Arn;
        if (!AwsKmsKeyArn.IsValid(keyArn))
        {
            throw new CryptographicException(
                "AWS KMS created a key whose identifier is not a KMS key ARN."
            );
        }

        return new RemoteKeyCreationResult { KeyId = keyArn };
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when the key identifier of <paramref name="request"/> is not a KMS key ARN.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the service reports a public key of another key than requested.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the service reports a public key that is missing or is not a signing key of the requested
    /// specification for the requested algorithm.
    /// </exception>
    public async ValueTask GetPublicKeyAsync(
        RemotePublicKeyRequest request,
        IBufferWriter<byte> subjectPublicKeyInfo,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(subjectPublicKeyInfo);

        var keyId = request.KeyId;
        if (!AwsKmsKeyArn.IsValid(keyId))
        {
            throw new ArgumentException(
                "The key identifier is not the ARN of a KMS key.",
                nameof(request)
            );
        }

        var row = request.Algorithm;
        var region = AwsKmsKeyArn.GetRegion(keyId);
        var publicKeyRequest = new GetPublicKeyRequest { KeyId = keyId };

        var publicKey = await ClientFactory.ExecuteAsync(
            region,
            access: null,
            (client, token) =>
                new ValueTask<GetPublicKeyResponse>(
                    client.GetPublicKeyAsync(publicKeyRequest, token)
                ),
            cancellationToken
        );

        if (!string.Equals(publicKey.KeyId, keyId, StringComparison.Ordinal))
        {
            throw new IOException(
                $"AWS KMS key '{keyId}' was asked for its public key, but the service reports the public key of another key."
            );
        }

        if (publicKey.KeyUsage != KeyUsageType.SIGN_VERIFY)
        {
            throw new CryptographicException($"AWS KMS key '{keyId}' is not a signing key.");
        }

        if (publicKey.KeySpec?.Value != row.KeySpec)
        {
            throw new CryptographicException(
                $"AWS KMS key '{keyId}' does not have the key specification '{row.KeySpec}'."
            );
        }

        if (
            publicKey.SigningAlgorithms is not { } signingAlgorithms
            || !signingAlgorithms.Contains(row.SigningAlgorithm, StringComparer.Ordinal)
        )
        {
            throw new CryptographicException(
                $"AWS KMS key '{keyId}' does not support the signing algorithm '{row.SigningAlgorithm}'."
            );
        }

        if (publicKey.PublicKey is not { Length: > 0 } stream)
        {
            throw new CryptographicException($"AWS KMS key '{keyId}' has no public key.");
        }

        var length = checked((int)stream.Length);
        stream.ReadExactly(subjectPublicKeyInfo.GetSpan(length)[..length]);
        subjectPublicKeyInfo.Advance(length);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when the key identifier of <paramref name="request"/> is not a KMS key ARN.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the service reports that another key or another algorithm made the signature.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the signature does not fit in the buffer.
    /// </exception>
    public async ValueTask<int> SignAsync(
        RemoteSignatureRequest request,
        Memory<byte> signature,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var keyId = request.KeyId;
        if (!AwsKmsKeyArn.IsValid(keyId))
        {
            throw new ArgumentException(
                "The key identifier is not the ARN of a KMS key.",
                nameof(request)
            );
        }

        var row = request.Algorithm;
        var region = AwsKmsKeyArn.GetRegion(keyId);

        // The SDK takes the message as a stream, so an array-backed input is wrapped without a copy.
        var message =
            MemoryMarshal.TryGetArray(request.Data, out var segment) && segment.Array is { } array
                ? new MemoryStream(array, segment.Offset, segment.Count, writable: false)
                : new MemoryStream(request.Data.ToArray());

        // A row that names no digest has the service receive the whole signing input.
        var signRequest = new SignRequest
        {
            KeyId = keyId,
            Message = message,
            MessageType = row.DigestAlgorithm is null ? MessageType.RAW : MessageType.DIGEST,
            SigningAlgorithm = SigningAlgorithmSpec.FindValue(row.SigningAlgorithm),
        };

        using var messageToDispose = message;

        var response = await ClientFactory.ExecuteAsync(
            region,
            access: null,
            (client, token) => new ValueTask<SignResponse>(client.SignAsync(signRequest, token)),
            cancellationToken
        );

        if (!string.Equals(response.KeyId, keyId, StringComparison.Ordinal))
        {
            throw new IOException(
                $"AWS KMS key '{keyId}' was asked to sign, but the service reports another key made the signature."
            );
        }

        if (
            !string.Equals(
                response.SigningAlgorithm?.Value,
                row.SigningAlgorithm,
                StringComparison.Ordinal
            )
        )
        {
            throw new IOException(
                $"AWS KMS key '{keyId}' was asked to sign with '{row.SigningAlgorithm}', but the service reports another algorithm made the signature."
            );
        }

        var length = checked((int)response.Signature.Length);
        if (length > signature.Length)
        {
            throw new CryptographicException(
                $"The signature of AWS KMS key '{keyId}' does not fit in the buffer."
            );
        }

        response.Signature.ReadExactly(signature.Span[..length]);
        return length;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="keyId"/> is not a KMS key ARN.
    /// </exception>
    public async ValueTask DestroyKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keyId);

        if (!AwsKmsKeyArn.IsValid(keyId))
        {
            throw new ArgumentException(
                "The key identifier is not the ARN of a KMS key.",
                nameof(keyId)
            );
        }

        var region = AwsKmsKeyArn.GetRegion(keyId);
        var deletionRequest = new ScheduleKeyDeletionRequest
        {
            KeyId = keyId,
            PendingWindowInDays = ProviderOptions.Value.PendingWindowInDays,
        };

        try
        {
            await ClientFactory.ExecuteAsync(
                region,
                access: null,
                (client, token) =>
                    new ValueTask<ScheduleKeyDeletionResponse>(
                        client.ScheduleKeyDeletionAsync(deletionRequest, token)
                    ),
                cancellationToken
            );
        }
        catch (NotFoundException)
        {
            // The key is already deleted.
        }
        catch (KMSInvalidStateException)
        {
            // A key that is already pending deletion is destroyed. Any other state propagates.
            if (!await IsPendingDeletionAsync(region, keyId, cancellationToken))
            {
                throw;
            }
        }
    }

    private async ValueTask<bool> IsPendingDeletionAsync(
        string region,
        string keyId,
        CancellationToken cancellationToken
    )
    {
        var describeRequest = new DescribeKeyRequest { KeyId = keyId };

        var response = await ClientFactory.ExecuteAsync(
            region,
            access: null,
            (client, token) =>
                new ValueTask<DescribeKeyResponse>(client.DescribeKeyAsync(describeRequest, token)),
            cancellationToken
        );

        return response.KeyMetadata?.KeyState == KeyState.PendingDeletion;
    }
}
