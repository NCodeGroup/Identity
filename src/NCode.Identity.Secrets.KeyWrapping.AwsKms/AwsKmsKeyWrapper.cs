// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.KeyWrapping.AwsKms.Facades;
using NCode.Identity.Secrets.KeyWrapping.AwsKms.Options;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms;

/// <summary>
/// Provides an implementation of <see cref="IReferencedKeyWrapper"/> that wraps keys with a symmetric encryption key
/// held in AWS Key Management Service, so the root key never leaves the service. It wraps under the configured key, or
/// under the key or alias ARN that the caller names, reaching a named key through the IAM role that the holder of the
/// key grants when the caller names one.
/// </summary>
/// <remarks>
/// <para>
/// The context goes to the service as the encryption context, under the key <see cref="EncryptionContextKey"/>, so the
/// service refuses to decrypt a wrapped key with any other context, CloudTrail records the context, and an IAM policy
/// can require it. The context holds identifiers only, never key material.
/// </para>
/// <para>
/// The identifier of the root key is the ARN that the service reports when it encrypts, which is the ARN of the key
/// even when the configured key is an alias. Decryption names that ARN, so a wrapped key is never decrypted under any
/// other key. The region of the client is the region in the ARN.
/// </para>
/// <para>
/// A wrapped key that the service rejects as altered, or that was wrapped under another key or context, fails with a
/// <see cref="CryptographicException"/>. Every other failure, such as throttling, an outage, or a disabled or missing
/// key, propagates unchanged, so that a failure to reach the service is never mistaken for a wrong key.
/// </para>
/// </remarks>
// See ADR-1235 and ADR-1415.
internal class AwsKmsKeyWrapper(
    IAwsKmsClientFactory clientFactory,
    IOptions<AwsKmsKeyWrapperOptions> options
) : IReferencedKeyWrapper
{
    /// <summary>
    /// The key of the encryption context entry that carries the context of a wrapped key.
    /// </summary>
    internal const string EncryptionContextKey = "ncode-identity:root-context";

    private IAwsKmsClientFactory ClientFactory { get; } = clientFactory;

    private IOptions<AwsKmsKeyWrapperOptions> RootKeyOptions { get; } = options;

    /// <inheritdoc />
    public string Name => KeyWrapperNames.AwsKms;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured key ARN is not the ARN of a KMS key or alias.
    /// </exception>
    public async ValueTask<WrappedKey> WrapAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        var keyArn = RootKeyOptions.Value.KeyArn;
        if (!AwsKmsKeyArn.IsValid(keyArn))
        {
            throw new InvalidOperationException(
                "SecretProtection:RootKeyProviders:AwsKms:KeyArn is not a KMS key or alias ARN."
            );
        }

        return await WrapUnderAsync(keyArn, access: null, key, context, cancellationToken);
    }

    /// <inheritdoc />
    public bool TryValidateKeyReference(
        string keyReference,
        [NotNullWhen(false)] out string? failure
    )
    {
        if (AwsKmsKeyArn.IsValid(keyReference))
        {
            failure = null;
            return true;
        }

        failure =
            "must be the ARN of a KMS key or alias, such as arn:aws:kms:us-west-2:111122223333:alias/tenant-root";
        return false;
    }

    /// <summary>
    /// Validates the syntax of an access identity: the ARN of the IAM role that the wrapper assumes to reach the key.
    /// </summary>
    /// <param name="accessIdentity">The access identity to validate.</param>
    /// <param name="failure">
    /// When this method returns <see langword="false"/>, a description of what the access identity must be, phrased to
    /// follow the name of the value (for example, "must be …"); otherwise, <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> if the access identity is an IAM role ARN; otherwise, <see langword="false"/>.</returns>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Part of the instance API alongside the key reference validation."
    )]
    public bool TryValidateAccessIdentity(
        string accessIdentity,
        [NotNullWhen(false)] out string? failure
    )
    {
        if (AwsIamRoleArn.IsValid(accessIdentity))
        {
            failure = null;
            return true;
        }

        failure =
            "must be the ARN of an IAM role, such as arn:aws:iam::111122223333:role/ncode-identity-tenant-root";
        return false;
    }

    /// <inheritdoc />
    public async ValueTask<WrappedKey> WrapAsync(
        string keyReference,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        if (!AwsKmsKeyArn.IsValid(keyReference))
        {
            throw new ArgumentException(
                "The key reference is not the ARN of a KMS key or alias.",
                nameof(keyReference)
            );
        }

        return await WrapUnderAsync(keyReference, access: null, key, context, cancellationToken);
    }

    /// <summary>
    /// Wraps a key under the KMS key or alias that the referenced key names, reaching it through the IAM role that the
    /// access of the referenced key names, if any.
    /// </summary>
    /// <param name="referencedKey">The KMS key or alias ARN, and the optional IAM role to assume to reach it.</param>
    /// <param name="key">The key to wrap.</param>
    /// <param name="context">The context that binds the wrapped key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The wrapped key.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the key reference of <paramref name="referencedKey"/> is not the ARN of a KMS key or alias, or its
    /// access identity is set and is not the ARN of an IAM role.
    /// </exception>
    public async ValueTask<WrappedKey> WrapAsync(
        ReferencedKey referencedKey,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(referencedKey);

        if (!AwsKmsKeyArn.IsValid(referencedKey.KeyReference))
        {
            throw new ArgumentException(
                "The key reference is not the ARN of a KMS key or alias.",
                nameof(referencedKey)
            );
        }

        if (
            referencedKey.Access is { } access
            && !TryValidateAccessIdentity(access.Identity, out _)
        )
        {
            throw new ArgumentException(
                "The access identity is not the ARN of an IAM role.",
                nameof(referencedKey)
            );
        }

        return await WrapUnderAsync(
            referencedKey.KeyReference,
            referencedKey.Access,
            key,
            context,
            cancellationToken
        );
    }

    private async ValueTask<WrappedKey> WrapUnderAsync(
        string keyArn,
        KeyAccess? access,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        // The SDK demands a stream, so the key is copied into a sensitive rental that the stream reads from; the
        // rental is zeroed when it is disposed.
        using var plaintextLease = BufferFactory.Rent(
            key.Length,
            isSensitive: true,
            out Memory<byte> plaintext
        );
        key.CopyTo(plaintext);

        using var plaintextStream = CreateStream(plaintext, key.Length);
        var request = new EncryptRequest
        {
            KeyId = keyArn,
            Plaintext = plaintextStream,
            EncryptionAlgorithm = EncryptionAlgorithmSpec.SYMMETRIC_DEFAULT,
            EncryptionContext = CreateEncryptionContext(context),
        };

        var client = ClientFactory.GetClient(AwsKmsKeyArn.GetRegion(keyArn), access);
        var response = await client.EncryptAsync(request, cancellationToken);

        return new WrappedKey
        {
            WrapperName = Name,
            KeyId = response.KeyId,
            Ciphertext = EncodeCiphertext(response.CiphertextBlob),
        };
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="wrappedKey"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the wrapped key does not name a KMS key, is empty or not valid base64url text, or was rejected by
    /// the service as altered or as wrapped under another key or context.
    /// </exception>
    public ValueTask UnwrapAsync(
        WrappedKey wrappedKey,
        ReadOnlyMemory<byte> context,
        IBufferWriter<byte> destination,
        CancellationToken cancellationToken
    ) => UnwrapCoreAsync(wrappedKey, access: null, context, destination, cancellationToken);

    /// <summary>
    /// Unwraps a key that was wrapped under a KMS key, reaching the KMS key through the IAM role that the access names.
    /// </summary>
    /// <param name="wrappedKey">The wrapped key to unwrap.</param>
    /// <param name="access">The IAM role to assume to reach the KMS key, and the external ID to assume it with.</param>
    /// <param name="context">The context that binds the wrapped key.</param>
    /// <param name="destination">The writer that receives the unwrapped key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="wrappedKey"/> or <paramref name="access"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the wrapped key does not name a KMS key, is empty or not valid base64url text, or was rejected by
    /// the service as altered or as wrapped under another key or context.
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
        if (!AwsKmsKeyArn.IsValid(keyId))
        {
            throw new CryptographicException(
                $"The wrapped key names '{keyId}', which is not the ARN of a KMS key."
            );
        }

        var text = wrappedKey.Ciphertext;

        using var ciphertextLease = BufferFactory.Rent(
            WrappedKeyCiphertext.GetByteCount(text),
            isSensitive: false,
            out Memory<byte> ciphertext
        );

        var written = WrappedKeyCiphertext.Decode(text, ciphertext.Span);

        using var ciphertextStream = CreateStream(ciphertext, written);
        var request = new DecryptRequest
        {
            KeyId = keyId,
            CiphertextBlob = ciphertextStream,
            EncryptionAlgorithm = EncryptionAlgorithmSpec.SYMMETRIC_DEFAULT,
            EncryptionContext = CreateEncryptionContext(context),
        };

        var client = ClientFactory.GetClient(AwsKmsKeyArn.GetRegion(keyId), access);

        DecryptResponse response;
        try
        {
            response = await client.DecryptAsync(request, cancellationToken);
        }
        catch (Exception exception)
            when (exception is InvalidCiphertextException or IncorrectKeyException)
        {
            // Only a ciphertext or key that the service rejects is a cryptographic failure. Anything else
            // propagates unchanged. An outage must not look like a wrong key: callers delete keys on
            // CryptographicException in Development.
            throw new CryptographicException(
                $"AWS KMS key '{keyId}' did not unwrap the master key: the ciphertext or its encryption context does not match.",
                exception
            );
        }

        CopyPlaintext(response.Plaintext, destination);
    }

    private static Dictionary<string, string> CreateEncryptionContext(
        ReadOnlyMemory<byte> context
    ) =>
        new(StringComparer.Ordinal)
        {
            // The value is not secret, and the SDK requires a string.
            [EncryptionContextKey] = Base64Url.Encode(context.Span),
        };

    private static MemoryStream CreateStream(Memory<byte> rental, int length)
    {
        if (
            !MemoryMarshal.TryGetArray<byte>(rental, out var segment)
            || segment.Array is not { } array
        )
        {
            throw new InvalidOperationException(
                "The rented buffer is not backed by an array, which the AWS SDK requires."
            );
        }

        return new MemoryStream(array, segment.Offset, length, writable: false);
    }

    private static string EncodeCiphertext(MemoryStream ciphertext)
    {
        if (ciphertext.TryGetBuffer(out var segment))
        {
            return Base64Url.Encode(segment.AsSpan());
        }

        ciphertext.Position = 0;
        using var lease = BufferFactory.Rent(
            checked((int)ciphertext.Length),
            isSensitive: false,
            out Span<byte> buffer
        );
        ciphertext.ReadExactly(buffer);
        return Base64Url.Encode(buffer);
    }

    private static void CopyPlaintext(MemoryStream plaintext, IBufferWriter<byte> destination)
    {
        if (plaintext.TryGetBuffer(out var segment))
        {
            // The SDK owns the segment and leaves the unwrapped key in it, so it is zeroed once copied.
            var source = segment.AsSpan();
            using var lifetime = source.GetSecureLifetime();

            source.CopyTo(destination.GetSpan(source.Length));
            destination.Advance(source.Length);
            return;
        }

        // The SDK did not expose its buffer, so the key is read straight into the destination.
        plaintext.Position = 0;
        var length = checked((int)plaintext.Length);
        plaintext.ReadExactly(destination.GetSpan(length)[..length]);
        destination.Advance(length);
    }
}
