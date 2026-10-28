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

using System.Security.Cryptography;
using Azure;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Facades;
using NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Options;

namespace NCode.Identity.Secrets.KeyWrapping.AzureKeyVault;

/// <summary>
/// Provides an implementation of <see cref="IKeyWrapper"/> that wraps keys with an RSA key held in Azure Key Vault
/// or Managed HSM, so the root key never leaves the service.
/// </summary>
/// <remarks>
/// <para>
/// Keys are wrapped with RSA-OAEP using SHA-256 (<c>RSA-OAEP-256</c>), which every Key Vault tier supports and which is
/// the only RSA wrap algorithm Microsoft recommends. Key Vault's RSA-OAEP takes no associated data, so the context is
/// not sent to the service; the key-check value of the master key binds the wrapped key to its owner and version
/// instead. The SDK demands an array of exactly the key's length, so the key is copied into a pinned array that is
/// zeroed once the call returns.
/// </para>
/// <para>
/// The identifier of the root key is the identifier of the key version that the service reports, and a result without
/// a version is refused. Unwrapping names the recorded key version, so a configured identifier without a version wraps
/// with the key's current version, and rotating the key in Key Vault leaves older master keys unwrapping under their
/// own version. A wrapped key shorter than <see cref="MinimumWrappedKeySizeBytes"/> bytes, which an RSA key under 3072
/// bits produces, is refused when wrapping.
/// </para>
/// <para>
/// A wrapped key that the service rejects with an HTTP 400 response when unwrapping fails with a
/// <see cref="CryptographicException"/>. Every other failure, such as throttling, an outage, denied access, or a
/// disabled or missing key, propagates unchanged, so that a failure to reach the service is never mistaken for a wrong
/// key.
/// </para>
/// </remarks>
// See ADR-0260.
internal class AzureKeyVaultKeyWrapper(
    IAzureKeyVaultClientFactory clientFactory,
    IOptions<AzureKeyVaultKeyWrapperOptions> options
) : IKeyWrapper
{
    /// <summary>
    /// The minimum size, in bytes, of a wrapped key, which is the size of an RSA modulus of 3072 bits.
    /// </summary>
    internal const int MinimumWrappedKeySizeBytes = 384;

    private IAzureKeyVaultClientFactory ClientFactory { get; } = clientFactory;

    private IOptions<AzureKeyVaultKeyWrapperOptions> RootKeyOptions { get; } = options;

    /// <inheritdoc />
    public string Name => KeyWrapperNames.AzureKeyVault;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured key identifier is not the identifier of a Key Vault key, or when the service reports
    /// a key that is not a key version or that is shorter than 3072 bits.
    /// </exception>
    public async ValueTask<WrappedKey> WrapAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        var keyId = RootKeyOptions.Value.KeyId;
        if (!AzureKeyVaultKeyId.IsKeyId(keyId))
        {
            throw new InvalidOperationException(
                "SecretProtection:RootKeyProviders:AzureKeyVault:KeyId is not a Key Vault key identifier."
            );
        }

        var plaintext = GC.AllocateArray<byte>(key.Length, pinned: true);

        WrapResult result;
        try
        {
            // The SDK demands an array of exactly the key's length. Key Vault's RSA-OAEP takes no associated data, so
            // the context is not sent, and the key-check value of the master key binds it to its owner.
            key.CopyTo(plaintext);
            var client = ClientFactory.GetClient(keyId);
            result = await client.WrapKeyAsync(
                KeyWrapAlgorithm.RsaOaep256,
                plaintext,
                cancellationToken
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        if (!AzureKeyVaultKeyId.IsKeyVersionId(result.KeyId))
        {
            throw new InvalidOperationException(
                $"Azure Key Vault reported '{result.KeyId}', which is not the identifier of a key version."
            );
        }

        if (result.EncryptedKey.Length < MinimumWrappedKeySizeBytes)
        {
            throw new InvalidOperationException(
                $"Azure Key Vault key '{result.KeyId}' is shorter than 3072 bits; the root key must be an RSA key of 3072 bits or more."
            );
        }

        return new WrappedKey
        {
            WrapperName = Name,
            KeyId = result.KeyId,
            Ciphertext = Base64Url.Encode(result.EncryptedKey),
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
    /// Thrown when the wrapped key does not name a Key Vault key version, is not valid base64url text, or was rejected
    /// by the service with an HTTP 400 response.
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
        if (!AzureKeyVaultKeyId.IsKeyVersionId(keyId))
        {
            throw new CryptographicException(
                $"The wrapped key names '{keyId}', which is not the identifier of a Key Vault key version."
            );
        }

        var ciphertext = DecodeCiphertext(wrappedKey.Ciphertext);
        var client = ClientFactory.GetClient(keyId);

        UnwrapResult result;
        try
        {
            result = await client.UnwrapKeyAsync(
                KeyWrapAlgorithm.RsaOaep256,
                ciphertext,
                cancellationToken
            );
        }
        catch (RequestFailedException exception) when (exception.Status == 400)
        {
            // Only a ciphertext that the service rejects is a cryptographic failure. Anything else propagates
            // unchanged. An outage must not look like a wrong key: callers delete keys on CryptographicException in
            // Development.
            throw new CryptographicException(
                $"Azure Key Vault key '{keyId}' did not unwrap the master key: the service rejected the ciphertext.",
                exception
            );
        }

        return CopyPlaintext(result.Key, destination.Span);
    }

    private static byte[] DecodeCiphertext(string text)
    {
        // A remainder of one character is not a valid base64url length, and counting its bytes throws.
        if (text.Length == 0 || text.Length % 4 == 1)
        {
            throw new CryptographicException("The wrapped key is not valid base64url text.");
        }

        try
        {
            return Base64Url.Decode(text);
        }
        catch (FormatException exception)
        {
            throw new CryptographicException(
                "The wrapped key is not valid base64url text.",
                exception
            );
        }
    }

    private static int CopyPlaintext(byte[] plaintext, Span<byte> destination)
    {
        // The SDK hands over an array that nothing else references and leaves the unwrapped key in it, so it is
        // zeroed once copied.
        var source = plaintext.AsSpan();
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
}
