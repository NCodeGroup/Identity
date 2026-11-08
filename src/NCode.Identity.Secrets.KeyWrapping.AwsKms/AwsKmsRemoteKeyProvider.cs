// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.KeyWrapping.AwsKms.Facades;
using NCode.Identity.Secrets.KeyWrapping.AwsKms.Options;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms;

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
/// algorithm, and a signature that the service reports as made by another key or with another algorithm, each with a
/// <see cref="CryptographicException"/>. Logs and messages carry identifiers only, never the signing input, a digest or
/// a signature.
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
    /// Thrown when the service creates a key whose identifier is not a KMS key ARN, or reports a public key that is not
    /// a signing key of the requested specification for the requested algorithm.
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

        var publicKeyRequest = new GetPublicKeyRequest { KeyId = keyArn };

        var publicKey = await ClientFactory.ExecuteAsync(
            region,
            access: null,
            (client, token) =>
                new ValueTask<GetPublicKeyResponse>(
                    client.GetPublicKeyAsync(publicKeyRequest, token)
                ),
            cancellationToken
        );

        if (publicKey.KeyUsage != KeyUsageType.SIGN_VERIFY)
        {
            throw new CryptographicException($"AWS KMS key '{keyArn}' is not a signing key.");
        }

        if (publicKey.KeySpec?.Value != row.KeySpec)
        {
            throw new CryptographicException(
                $"AWS KMS key '{keyArn}' does not have the key specification '{row.KeySpec}'."
            );
        }

        if (
            publicKey.SigningAlgorithms is not { } signingAlgorithms
            || !signingAlgorithms.Contains(row.SigningAlgorithm, StringComparer.Ordinal)
        )
        {
            throw new CryptographicException(
                $"AWS KMS key '{keyArn}' does not support the signing algorithm '{row.SigningAlgorithm}'."
            );
        }

        return new RemoteKeyCreationResult
        {
            KeyId = keyArn,
            SubjectPublicKeyInfo = publicKey.PublicKey.ToArray(),
        };
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when the key identifier of <paramref name="request"/> is not a KMS key ARN.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the service reports that another key or another algorithm made the signature.
    /// </exception>
    public async ValueTask<byte[]> SignAsync(
        RemoteSignatureRequest request,
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

        // A row that names no digest has the service receive the whole signing input.
        var signRequest = new SignRequest
        {
            KeyId = keyId,
            Message = new MemoryStream(request.Data.ToArray()),
            MessageType = row.DigestAlgorithm is null ? MessageType.RAW : MessageType.DIGEST,
            SigningAlgorithm = SigningAlgorithmSpec.FindValue(row.SigningAlgorithm),
        };

        using var message = signRequest.Message;

        var response = await ClientFactory.ExecuteAsync(
            region,
            access: null,
            (client, token) => new ValueTask<SignResponse>(client.SignAsync(signRequest, token)),
            cancellationToken
        );

        if (!string.Equals(response.KeyId, keyId, StringComparison.Ordinal))
        {
            throw new CryptographicException(
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
            throw new CryptographicException(
                $"AWS KMS key '{keyId}' was asked to sign with '{row.SigningAlgorithm}', but the service reports another algorithm made the signature."
            );
        }

        return response.Signature.ToArray();
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
