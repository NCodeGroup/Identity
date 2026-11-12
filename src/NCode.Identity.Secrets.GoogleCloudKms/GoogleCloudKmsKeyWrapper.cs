// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.GoogleCloudKms.Facades;
using NCode.Identity.Secrets.GoogleCloudKms.Options;
using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.GoogleCloudKms;

/// <summary>
/// Provides an implementation of <see cref="IReferencedKeyWrapper"/> that wraps keys with a symmetric encryption key
/// held in Google Cloud Key Management Service, so the root key never leaves the service. It wraps under the
/// configured crypto key, or under the crypto key that the caller names, reaching a named crypto key through the
/// service account that the holder of the key grants when the caller names one.
/// </summary>
/// <remarks>
/// <para>
/// The context goes to the service as the additional authenticated data, so the service refuses to decrypt a wrapped
/// key with any other context. The context holds identifiers only, never key material.
/// </para>
/// <para>
/// Encryption names the configured or the named crypto key, and the service encrypts with its primary version. The
/// identifier of the root key is the resource name of the crypto key version that the service reports, which must be a
/// version of the crypto key that encryption named. Decryption names the crypto key that the recorded version belongs
/// to, so a wrapped key is never decrypted under any other crypto key.
/// </para>
/// <para>
/// Every request and response carries CRC32C checksums, so a value corrupted in transit is detected. A response that
/// fails its check, or that names a version of another crypto key, fails with an <see cref="IOException"/>, because
/// the call can be retried. The service reports a request that fails its check with the <c>InvalidArgument</c>
/// status, the same status as a ciphertext or additional authenticated data that does not match, so decryption is
/// tried at most twice, and a second <c>InvalidArgument</c> fails with a <see cref="CryptographicException"/>. Every
/// other failure, such as throttling, an outage, or a disabled or missing key, propagates unchanged, so that a failure
/// to reach the service is never mistaken for a wrong key.
/// </para>
/// </remarks>
// See ADR-1260, ADR-1414.
internal class GoogleCloudKmsKeyWrapper(
    IGoogleCloudKmsClientFactory clientFactory,
    IOptions<GoogleCloudKmsKeyWrapperOptions> options
) : IReferencedKeyWrapper
{
    private IGoogleCloudKmsClientFactory ClientFactory { get; } = clientFactory;

    private IOptions<GoogleCloudKmsKeyWrapperOptions> RootKeyOptions { get; } = options;

    /// <inheritdoc />
    public string Name => KeyWrapperNames.GoogleCloudKms;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured key name is not the resource name of a Cloud KMS crypto key.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the service does not confirm the integrity of the request and response, or reports a crypto key
    /// version that does not belong to the crypto key.
    /// </exception>
    public async ValueTask<WrappedKey> WrapAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        var keyName = RootKeyOptions.Value.KeyName;
        if (!GoogleCloudKmsKeyName.IsCryptoKeyName(keyName))
        {
            throw new InvalidOperationException(
                "SecretProtection:RootKeyProviders:GoogleCloudKms:KeyName is not a Cloud KMS crypto key name."
            );
        }

        return await WrapUnderAsync(
            new ReferencedKey(keyName, Access: null),
            key,
            context,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public bool TryValidateKeyReference(
        string keyReference,
        [NotNullWhen(false)] out string? failure
    )
    {
        if (GoogleCloudKmsKeyName.IsCryptoKeyName(keyReference))
        {
            failure = null;
            return true;
        }

        failure =
            "must be the resource name of a Cloud KMS crypto key without a version, such as projects/my-project/locations/global/keyRings/tenants/cryptoKeys/tenant-root";
        return false;
    }

    /// <inheritdoc />
    public bool TryValidateAccessIdentity(
        string accessIdentity,
        [NotNullWhen(false)] out string? failure
    )
    {
        if (GoogleServiceAccountEmail.IsValid(accessIdentity))
        {
            failure = null;
            return true;
        }

        failure =
            "must be the email address of a Google Cloud service account, such as ncode-identity@tenant-project.iam.gserviceaccount.com";
        return false;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when the key reference of <paramref name="referencedKey"/> is not a Cloud KMS crypto key name, or its
    /// access identity is set and is not the email address of a service account.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the service does not confirm the integrity of the request and response, or reports a crypto key
    /// version that does not belong to the named crypto key.
    /// </exception>
    public async ValueTask<WrappedKey> WrapAsync(
        ReferencedKey referencedKey,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(referencedKey);

        if (!GoogleCloudKmsKeyName.IsCryptoKeyName(referencedKey.KeyReference))
        {
            throw new ArgumentException(
                "The key reference is not a Cloud KMS crypto key name.",
                nameof(referencedKey)
            );
        }

        if (
            referencedKey.Access is { } access
            && !TryValidateAccessIdentity(access.Identity, out _)
        )
        {
            throw new ArgumentException(
                "The access identity is not the email address of a Google Cloud service account.",
                nameof(referencedKey)
            );
        }

        return await WrapUnderAsync(referencedKey, key, context, cancellationToken);
    }

    private async ValueTask<WrappedKey> WrapUnderAsync(
        ReferencedKey referencedKey,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        var keyName = referencedKey.KeyReference;

        // The call only reads the caller's buffers and they outlive it, so they are wrapped without a copy.
        var request = new EncryptRequest
        {
            Name = keyName,
            Plaintext = UnsafeByteOperations.UnsafeWrap(key),
            AdditionalAuthenticatedData = UnsafeByteOperations.UnsafeWrap(context),
            PlaintextCrc32C = GoogleCloudKmsChecksum.Compute(key.Span),
            AdditionalAuthenticatedDataCrc32C = GoogleCloudKmsChecksum.Compute(context.Span),
        };

        var response = await ClientFactory.ExecuteAsync(
            referencedKey.Access,
            (client, token) => new ValueTask<EncryptResponse>(client.EncryptAsync(request, token)),
            cancellationToken
        );

        if (
            !response.VerifiedPlaintextCrc32C
            || !response.VerifiedAdditionalAuthenticatedDataCrc32C
            || response.CiphertextCrc32C != GoogleCloudKmsChecksum.Compute(response.Ciphertext.Span)
        )
        {
            throw new IOException(
                "Google Cloud KMS did not confirm the integrity of the encrypt request and response; the call can be retried."
            );
        }

        // The service reports the version it encrypted with, which confirms that the intended crypto key was used.
        var versionName = response.Name;
        if (
            !GoogleCloudKmsKeyName.IsCryptoKeyVersionName(versionName)
            || !string.Equals(
                GoogleCloudKmsKeyName.GetCryptoKeyName(versionName),
                keyName,
                StringComparison.Ordinal
            )
        )
        {
            throw new IOException(
                $"Google Cloud KMS reported '{versionName}', which is not a version of the named crypto key; the call can be retried."
            );
        }

        return new WrappedKey
        {
            WrapperName = Name,
            KeyId = versionName,
            Ciphertext = Base64Url.Encode(response.Ciphertext.Span),
        };
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="wrappedKey"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the wrapped key does not name a Cloud KMS crypto key version, is empty or not valid base64url text,
    /// or was rejected twice by the service because its ciphertext or additional authenticated data does not match.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the plaintext that the service returns fails its integrity check.
    /// </exception>
    public ValueTask UnwrapAsync(
        WrappedKey wrappedKey,
        ReadOnlyMemory<byte> context,
        IBufferWriter<byte> destination,
        CancellationToken cancellationToken
    ) => UnwrapCoreAsync(wrappedKey, access: null, context, destination, cancellationToken);

    /// <summary>
    /// Unwraps a key that was wrapped under a crypto key, reaching the crypto key through the service account that the
    /// access names.
    /// </summary>
    /// <param name="wrappedKey">The wrapped key to unwrap.</param>
    /// <param name="access">The service account to impersonate to reach the crypto key.</param>
    /// <param name="context">The context that binds the wrapped key.</param>
    /// <param name="destination">The writer that receives the unwrapped key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="wrappedKey"/> or <paramref name="access"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the wrapped key does not name a Cloud KMS crypto key version, is empty or not valid base64url text,
    /// or was rejected twice by the service because its ciphertext or additional authenticated data does not match.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the plaintext that the service returns fails its integrity check.
    /// </exception>
    public ValueTask UnwrapAsync(
        WrappedKey wrappedKey,
        KeyAccess access,
        ReadOnlyMemory<byte> context,
        IBufferWriter<byte> destination,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(access);

        return UnwrapCoreAsync(wrappedKey, access, context, destination, cancellationToken);
    }

    private async ValueTask UnwrapCoreAsync(
        WrappedKey wrappedKey,
        KeyAccess? access,
        ReadOnlyMemory<byte> context,
        IBufferWriter<byte> destination,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(wrappedKey);

        var keyId = wrappedKey.KeyId;
        if (!GoogleCloudKmsKeyName.IsCryptoKeyVersionName(keyId))
        {
            throw new CryptographicException(
                $"The wrapped key names '{keyId}', which is not the name of a Cloud KMS crypto key version."
            );
        }

        var text = wrappedKey.Ciphertext;

        using var ciphertextLease = BufferFactory.Rent(
            WrappedKeyCiphertext.GetByteCount(text),
            isSensitive: false,
            out Memory<byte> ciphertext
        );

        var written = WrappedKeyCiphertext.Decode(text, ciphertext.Span);

        // The calls only read the buffers and they outlive them, so they are wrapped without a copy.
        var decoded = ciphertext[..written];
        var request = new DecryptRequest
        {
            Name = GoogleCloudKmsKeyName.GetCryptoKeyName(keyId),
            Ciphertext = UnsafeByteOperations.UnsafeWrap(decoded),
            AdditionalAuthenticatedData = UnsafeByteOperations.UnsafeWrap(context),
            CiphertextCrc32C = GoogleCloudKmsChecksum.Compute(decoded.Span),
            AdditionalAuthenticatedDataCrc32C = GoogleCloudKmsChecksum.Compute(context.Span),
        };

        var response = await ClientFactory.ExecuteAsync(
            access,
            (client, token) => DecryptAsync(client, request, token),
            cancellationToken
        );

        CopyPlaintext(response, destination);
    }

    private static async ValueTask<DecryptResponse> DecryptAsync(
        KeyManagementServiceClient client,
        DecryptRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await client.DecryptAsync(request, cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.InvalidArgument)
        {
            // Cloud KMS reports a request whose checksum does not match with the same status as a wrong ciphertext,
            // so one retry tells a corrupted request from a wrong key.
            try
            {
                return await client.DecryptAsync(request, cancellationToken);
            }
            catch (RpcException retryException)
                when (retryException.StatusCode == StatusCode.InvalidArgument)
            {
                // Only a ciphertext or additional authenticated data that the service rejects is a cryptographic
                // failure. Anything else propagates unchanged. An outage must not look like a wrong key: callers
                // delete keys on CryptographicException in Development.
                throw new CryptographicException(
                    $"Google Cloud KMS key '{request.Name}' did not unwrap the master key: the ciphertext or its additional authenticated data does not match.",
                    retryException
                );
            }
        }
    }

    private static void CopyPlaintext(DecryptResponse response, IBufferWriter<byte> destination)
    {
        // The response owns the plaintext and leaves the unwrapped key in it, so it is zeroed once copied.
        var source = MemoryMarshal.AsMemory(response.Plaintext.Memory).Span;
        using var lifetime = source.GetSecureLifetime();

        if (response.PlaintextCrc32C != GoogleCloudKmsChecksum.Compute(source))
        {
            throw new IOException(
                "Google Cloud KMS returned a plaintext that failed its integrity check; the call can be retried."
            );
        }

        var length = source.Length;
        var target = destination.GetSpan(length)[..length];
        source.CopyTo(target);
        destination.Advance(length);
    }
}
