#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
/// Provides an implementation of <see cref="IKeyWrapper"/> that wraps keys with a symmetric encryption key held
/// in AWS Key Management Service, so the root key never leaves the service.
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
// See ADR-0235.
internal class AwsKmsKeyWrapper(
    IAwsKmsClientFactory clientFactory,
    IOptions<AwsKmsKeyWrapperOptions> options
) : IKeyWrapper
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

        var client = ClientFactory.GetClient(AwsKmsKeyArn.GetRegion(keyArn));
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
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destination"/> is too small to hold the unwrapped key.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the wrapped key does not name a KMS key, is not valid base64url text, or was rejected by the
    /// service as altered or as wrapped under another key or context.
    /// </exception>
    public async ValueTask<int> UnwrapAsync(
        WrappedKey wrappedKey,
        ReadOnlyMemory<byte> context,
        Memory<byte> destination,
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

        // A remainder of one character is not a valid base64url length, and counting its bytes throws.
        if (text.Length == 0 || text.Length % 4 == 1)
        {
            throw new CryptographicException("The wrapped key is not valid base64url text.");
        }

        using var ciphertextLease = BufferFactory.Rent(
            Base64Url.GetByteCountForDecode(text.Length),
            isSensitive: false,
            out Memory<byte> ciphertext
        );

        int written;
        try
        {
            if (!Base64Url.TryDecode(text, ciphertext.Span, out written))
            {
                throw new CryptographicException("The wrapped key is not valid base64url text.");
            }
        }
        catch (FormatException exception)
        {
            throw new CryptographicException(
                "The wrapped key is not valid base64url text.",
                exception
            );
        }

        using var ciphertextStream = CreateStream(ciphertext, written);
        var request = new DecryptRequest
        {
            KeyId = keyId,
            CiphertextBlob = ciphertextStream,
            EncryptionAlgorithm = EncryptionAlgorithmSpec.SYMMETRIC_DEFAULT,
            EncryptionContext = CreateEncryptionContext(context),
        };

        var client = ClientFactory.GetClient(AwsKmsKeyArn.GetRegion(keyId));

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

        return CopyPlaintext(response.Plaintext, destination.Span);
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

    private static int CopyPlaintext(MemoryStream plaintext, Span<byte> destination)
    {
        if (plaintext.TryGetBuffer(out var segment))
        {
            // The SDK owns the segment and leaves the unwrapped key in it, so it is zeroed once copied.
            var source = segment.AsSpan();
            using var lifetime = source.GetSecureLifetime();

            if (source.Length > destination.Length)
            {
                throw new ArgumentException(
                    "The destination is too small to hold the unwrapped key.",
                    nameof(destination)
                );
            }

            source.CopyTo(destination);
            return source.Length;
        }

        // The SDK did not expose its buffer, so the key is read straight into the destination.
        plaintext.Position = 0;
        if (plaintext.Length > destination.Length)
        {
            throw new ArgumentException(
                "The destination is too small to hold the unwrapped key.",
                nameof(destination)
            );
        }

        var length = (int)plaintext.Length;
        plaintext.ReadExactly(destination[..length]);
        return length;
    }
}
