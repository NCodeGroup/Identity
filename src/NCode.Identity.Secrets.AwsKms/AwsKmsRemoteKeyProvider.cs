// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Runtime.CompilerServices;
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
/// Provides an implementation of <see cref="IRemoteKeyProvider"/> that creates signing, decryption and key agreement
/// keys in AWS Key Management Service, signs, decrypts and agrees with them, and schedules them for deletion, so the
/// private half of a key never leaves the service. The provider reaches the service with the credentials of the
/// deployment.
/// </summary>
/// <remarks>
/// <para>
/// AWS KMS has no versions of an asymmetric key, so every key the provider creates is a new KMS key in the configured
/// region, tagged with its owner, secret, algorithm, group and deployment. The identifier of a key is its ARN, and every call that
/// uses the key goes to the region that the ARN names.
/// </para>
/// <para>
/// The provider refuses a public key that is not a key of the requested usage and specification for the requested
/// algorithm with a <see cref="CryptographicException"/>, and a response that names another key or another algorithm
/// than requested with an <see cref="IOException"/>, because the call can be retried. A ciphertext that the service
/// refuses is a <see cref="CryptographicException"/>. Logs and messages carry identifiers only, never the signing
/// input, a digest, a signature, a ciphertext, a plaintext or a shared secret.
/// </para>
/// <para>
/// The call that creates a key is made once, by a client that never retries; the calls that read, sign, decrypt, agree
/// and schedule deletion keep the retry of the SDK.
/// </para>
/// </remarks>
// See ADR-1471.
// See ADR-1471.
// See ADR-1478.
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

    /// <summary>
    /// The key of the tag that carries the name of the deployment that created a key.
    /// </summary>
    internal const string DeploymentTagKey = "ncode-identity:deployment";

    // ListKeys returns at most 1,000 keys and ListResourceTags at most 50 tags per page, the service's maximum.
    private const int ListKeysPageSize = 1000;
    private const int ListResourceTagsPageSize = 50;

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
            KeyUsage = GetKeyUsage(row),
            Description = $"NCode.Identity key {request.SecretId} of {request.OwnerId}",
            Tags =
            [
                new Tag { TagKey = OwnerTagKey, TagValue = request.OwnerId },
                new Tag { TagKey = SecretTagKey, TagValue = request.SecretId },
                new Tag { TagKey = AlgorithmTagKey, TagValue = row.Algorithm },
                new Tag { TagKey = GroupTagKey, TagValue = request.GroupName },
                new Tag { TagKey = DeploymentTagKey, TagValue = request.Deployment },
            ],
        };

        // Creating a key is not repeatable: a retry after a lost response would create a second key that nothing records.
        var created = await ClientFactory.ExecuteWithoutRetryAsync(
            region,
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
    /// Thrown when the service reports a public key that is missing or is not a key of the usage and specification of
    /// the requested row, or that does not support the row's algorithm.
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

        var expectedUsage = GetKeyUsage(row);
        if (publicKey.KeyUsage != expectedUsage)
        {
            throw new CryptographicException(
                $"AWS KMS key '{keyId}' is not a {GetKindName(row)} key."
            );
        }

        if (publicKey.KeySpec?.Value != row.KeySpec)
        {
            throw new CryptographicException(
                $"AWS KMS key '{keyId}' does not have the key specification '{row.KeySpec}'."
            );
        }

        var (supported, serviceAlgorithm) = row switch
        {
            RemoteSigningAlgorithm signing => (
                publicKey.SigningAlgorithms,
                signing.SigningAlgorithm
            ),
            RemoteDecryptionAlgorithm decryption => (
                publicKey.EncryptionAlgorithms,
                decryption.EncryptionAlgorithm
            ),
            RemoteKeyAgreementAlgorithm agreement => (
                publicKey.KeyAgreementAlgorithms,
                agreement.KeyAgreementAlgorithm
            ),
            _ => throw new ArgumentException(
                "The mapping row is not a signing, decryption or key agreement row.",
                nameof(request)
            ),
        };

        if (supported is null || !supported.Contains(serviceAlgorithm, StringComparer.Ordinal))
        {
            throw new CryptographicException(
                $"AWS KMS key '{keyId}' does not support the {GetKindName(row)} algorithm '{serviceAlgorithm}'."
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
    /// Thrown when the key identifier of <paramref name="request"/> is not a KMS key ARN.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the service reports that another key or another algorithm decrypted the ciphertext.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the service refuses the ciphertext or the plaintext does not fit in the buffer.
    /// </exception>
    public async ValueTask<int> DecryptAsync(
        RemoteDecryptionRequest request,
        Memory<byte> plaintext,
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

        using var ciphertext = CreateReadOnlyStream(request.Ciphertext);
        var decryptRequest = new DecryptRequest
        {
            KeyId = keyId,
            CiphertextBlob = ciphertext,
            EncryptionAlgorithm = EncryptionAlgorithmSpec.FindValue(row.EncryptionAlgorithm),
        };

        DecryptResponse response;
        try
        {
            response = await ClientFactory.ExecuteAsync(
                region,
                access: null,
                (client, token) =>
                    new ValueTask<DecryptResponse>(client.DecryptAsync(decryptRequest, token)),
                cancellationToken
            );
        }
        catch (InvalidCiphertextException exception)
        {
            throw new CryptographicException(
                $"AWS KMS key '{keyId}' refused the ciphertext.",
                exception
            );
        }

        try
        {
            if (!string.Equals(response.KeyId, keyId, StringComparison.Ordinal))
            {
                throw new IOException(
                    $"AWS KMS key '{keyId}' was asked to decrypt, but the service reports another key decrypted the ciphertext."
                );
            }

            if (
                !string.Equals(
                    response.EncryptionAlgorithm?.Value,
                    row.EncryptionAlgorithm,
                    StringComparison.Ordinal
                )
            )
            {
                throw new IOException(
                    $"AWS KMS key '{keyId}' was asked to decrypt with '{row.EncryptionAlgorithm}', but the service reports another algorithm decrypted the ciphertext."
                );
            }

            return CopyResult(
                response.Plaintext,
                plaintext,
                $"The plaintext of AWS KMS key '{keyId}'"
            );
        }
        finally
        {
            ZeroStream(response.Plaintext);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when the key identifier of <paramref name="request"/> is not a KMS key ARN.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the service reports that another key or another algorithm derived the shared secret.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the service refuses the peer public key or the shared secret does not fit in the buffer.
    /// </exception>
    public async ValueTask<int> DeriveSharedSecretAsync(
        RemoteKeyAgreementRequest request,
        Memory<byte> sharedSecret,
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

        using var peerPublicKey = CreateReadOnlyStream(request.PeerSubjectPublicKeyInfo);
        var deriveRequest = new DeriveSharedSecretRequest
        {
            KeyId = keyId,
            KeyAgreementAlgorithm = KeyAgreementAlgorithmSpec.FindValue(row.KeyAgreementAlgorithm),
            PublicKey = peerPublicKey,
        };

        DeriveSharedSecretResponse response;
        try
        {
            response = await ClientFactory.ExecuteAsync(
                region,
                access: null,
                (client, token) =>
                    new ValueTask<DeriveSharedSecretResponse>(
                        client.DeriveSharedSecretAsync(deriveRequest, token)
                    ),
                cancellationToken
            );
        }
        catch (InvalidKeyUsageException exception)
        {
            throw new CryptographicException(
                $"AWS KMS key '{keyId}' refused the peer public key.",
                exception
            );
        }

        try
        {
            if (!string.Equals(response.KeyId, keyId, StringComparison.Ordinal))
            {
                throw new IOException(
                    $"AWS KMS key '{keyId}' was asked to agree a key, but the service reports another key derived the shared secret."
                );
            }

            if (
                !string.Equals(
                    response.KeyAgreementAlgorithm?.Value,
                    row.KeyAgreementAlgorithm,
                    StringComparison.Ordinal
                )
            )
            {
                throw new IOException(
                    $"AWS KMS key '{keyId}' was asked to agree a key with '{row.KeyAgreementAlgorithm}', but the service reports another algorithm derived the shared secret."
                );
            }

            return CopyResult(
                response.SharedSecret,
                sharedSecret,
                $"The shared secret of AWS KMS key '{keyId}'"
            );
        }
        finally
        {
            ZeroStream(response.SharedSecret);
        }
    }

    private static MemoryStream CreateReadOnlyStream(ReadOnlyMemory<byte> data) =>
        MemoryMarshal.TryGetArray(data, out var segment) && segment.Array is { } array
            ? new MemoryStream(array, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(data.ToArray());

    private static int CopyResult(MemoryStream? source, Memory<byte> destination, string subject)
    {
        if (source is null)
        {
            throw new CryptographicException($"{subject} is missing.");
        }

        var length = checked((int)source.Length);
        if (length > destination.Length)
        {
            throw new CryptographicException($"{subject} does not fit in the buffer.");
        }

        source.Position = 0;
        source.ReadExactly(destination.Span[..length]);
        return length;
    }

    private static void ZeroStream(MemoryStream? stream)
    {
        if (stream is not null && stream.TryGetBuffer(out var buffer))
        {
            CryptographicOperations.ZeroMemory(buffer.AsSpan());
        }
    }

    private static KeyUsageType GetKeyUsage(RemoteKeyAlgorithm row) =>
        row switch
        {
            RemoteSigningAlgorithm => KeyUsageType.SIGN_VERIFY,
            RemoteDecryptionAlgorithm => KeyUsageType.ENCRYPT_DECRYPT,
            RemoteKeyAgreementAlgorithm => KeyUsageType.KEY_AGREEMENT,
            _ => throw new ArgumentException(
                "The mapping row is not a signing, decryption or key agreement row.",
                nameof(row)
            ),
        };

    private static string GetKindName(RemoteKeyAlgorithm row) =>
        row switch
        {
            RemoteSigningAlgorithm => "signing",
            RemoteDecryptionAlgorithm => "decryption",
            RemoteKeyAgreementAlgorithm => "key agreement",
            _ => throw new ArgumentException(
                "The mapping row is not a signing, decryption or key agreement row.",
                nameof(row)
            ),
        };

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

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured region is not set.
    /// </exception>
    public async IAsyncEnumerable<RemoteKeyListing> ListKeysAsync(
        RemoteKeyListRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken
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

        string? marker = null;
        do
        {
            var listRequest = new ListKeysRequest { Limit = ListKeysPageSize, Marker = marker };
            var page = await ClientFactory.ExecuteAsync(
                region,
                access: null,
                (client, token) =>
                    new ValueTask<ListKeysResponse>(client.ListKeysAsync(listRequest, token)),
                cancellationToken
            );

            if (page.Keys is not null)
            {
                foreach (var entry in page.Keys)
                {
                    var listing = await ReadListingAsync(
                        region,
                        entry.KeyArn,
                        request.Deployment,
                        cancellationToken
                    );

                    if (listing is not null)
                    {
                        yield return listing;
                    }
                }
            }

            marker = page.Truncated == true ? page.NextMarker : null;
        } while (marker is not null);
    }

    private async ValueTask<RemoteKeyListing?> ReadListingAsync(
        string region,
        string? keyArn,
        string deployment,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(keyArn))
        {
            return null;
        }

        var tagsRequest = new ListResourceTagsRequest
        {
            KeyId = keyArn,
            Limit = ListResourceTagsPageSize,
        };

        ListResourceTagsResponse tagsResponse;
        try
        {
            tagsResponse = await ClientFactory.ExecuteAsync(
                region,
                access: null,
                (client, token) =>
                    new ValueTask<ListResourceTagsResponse>(
                        client.ListResourceTagsAsync(tagsRequest, token)
                    ),
                cancellationToken
            );
        }
        catch (NotFoundException)
        {
            return null;
        }
        catch (AmazonKeyManagementServiceException exception)
            when (string.Equals(
                    exception.ErrorCode,
                    "AccessDeniedException",
                    StringComparison.Ordinal
                )
            )
        {
            return null;
        }

        var tags = tagsResponse.Tags ?? [];
        var groupName = FindTag(tags, GroupTagKey);
        if (
            groupName is null
            || !groupName.StartsWith(RemoteKeyGroupNames.Prefix, StringComparison.Ordinal)
        )
        {
            return null;
        }

        if (!string.Equals(FindTag(tags, DeploymentTagKey), deployment, StringComparison.Ordinal))
        {
            return null;
        }

        var describeRequest = new DescribeKeyRequest { KeyId = keyArn };
        var describeResponse = await ClientFactory.ExecuteAsync(
            region,
            access: null,
            (client, token) =>
                new ValueTask<DescribeKeyResponse>(client.DescribeKeyAsync(describeRequest, token)),
            cancellationToken
        );

        var metadata = describeResponse.KeyMetadata;
        if (
            metadata is not { CreationDate: { } creationDate }
            || metadata.KeyManager != KeyManagerType.CUSTOMER
            || metadata.KeyState != KeyState.Enabled
        )
        {
            return null;
        }

        return new RemoteKeyListing
        {
            KeyId = keyArn,
            GroupName = groupName,
            CreatedWhen = new DateTimeOffset(DateTime.SpecifyKind(creationDate, DateTimeKind.Utc)),
            OwnerId = FindTag(tags, OwnerTagKey),
            SecretId = FindTag(tags, SecretTagKey),
        };
    }

    private static string? FindTag(List<Tag> tags, string key) =>
        tags.FirstOrDefault(x => string.Equals(x.TagKey, key, StringComparison.Ordinal))?.TagValue;
}
