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
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.Persistence.GoogleCloudKms.Facades;
using NCode.Identity.Secrets.Persistence.GoogleCloudKms.Options;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence.GoogleCloudKms;

/// <summary>
/// Provides an implementation of <see cref="IRootKeyProvider"/> that wraps keys with a symmetric encryption key held
/// in Google Cloud Key Management Service, so the root key never leaves the service.
/// </summary>
/// <remarks>
/// <para>
/// The context goes to the service as the additional authenticated data, so the service refuses to decrypt a wrapped
/// key with any other context. The context holds identifiers only, never key material.
/// </para>
/// <para>
/// Encryption names the configured crypto key, and the service encrypts with its primary version. The identifier of
/// the root key is the resource name of the crypto key version that the service reports, which must be a version of
/// the configured crypto key. Decryption names the crypto key that the recorded version belongs to, so a wrapped key
/// is never decrypted under any other crypto key.
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
// See ADR-0238.
internal class GoogleCloudKmsRootKeyProvider(
    IGoogleCloudKmsClientFactory clientFactory,
    IOptions<GoogleCloudKmsRootKeyOptions> options
) : IRootKeyProvider
{
    private IGoogleCloudKmsClientFactory ClientFactory { get; } = clientFactory;

    private IOptions<GoogleCloudKmsRootKeyOptions> RootKeyOptions { get; } = options;

    /// <inheritdoc />
    public string ProviderName => RootKeyProviderNames.GoogleCloudKms;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured key name is not the resource name of a Cloud KMS crypto key.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the service does not confirm the integrity of the request and response, or reports a crypto key
    /// version that does not belong to the configured crypto key.
    /// </exception>
    public async ValueTask<RootWrappedKey> WrapAsync(
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

        // The call only reads the caller's buffers and they outlive it, so they are wrapped without a copy.
        var request = new EncryptRequest
        {
            Name = keyName,
            Plaintext = UnsafeByteOperations.UnsafeWrap(key),
            AdditionalAuthenticatedData = UnsafeByteOperations.UnsafeWrap(context),
            PlaintextCrc32C = GoogleCloudKmsChecksum.Compute(key.Span),
            AdditionalAuthenticatedDataCrc32C = GoogleCloudKmsChecksum.Compute(context.Span),
        };

        var client = await ClientFactory.GetClientAsync(cancellationToken);
        var response = await client.EncryptAsync(request, cancellationToken);

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
                $"Google Cloud KMS reported '{versionName}', which is not a version of the configured crypto key; the call can be retried."
            );
        }

        return new RootWrappedKey
        {
            ProviderName = ProviderName,
            KeyId = versionName,
            Ciphertext = Base64Url.Encode(response.Ciphertext.Span),
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
    /// Thrown when the wrapped key does not name a Cloud KMS crypto key version, is not valid base64url text, or was
    /// rejected twice by the service because its ciphertext or additional authenticated data does not match.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the plaintext that the service returns fails its integrity check.
    /// </exception>
    public async ValueTask<int> UnwrapAsync(
        RootWrappedKey wrappedKey,
        ReadOnlyMemory<byte> context,
        Memory<byte> destination,
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

        var client = await ClientFactory.GetClientAsync(cancellationToken);
        var response = await DecryptAsync(client, request, cancellationToken);

        return CopyPlaintext(response, destination.Span);
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

    private static int CopyPlaintext(DecryptResponse response, Span<byte> destination)
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
}
