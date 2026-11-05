// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
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
/// Provides an implementation of <see cref="IReferencedKeyWrapper"/> that wraps keys with an RSA key held in Azure Key
/// Vault or Managed HSM, so the root key never leaves the service. It wraps under the configured key, or under the key
/// that the caller names, reaching a named key through the Microsoft Entra directory that the holder of the key names
/// when the caller names one.
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
/// a version, or with the version of another key, is refused. Unwrapping names the recorded key version, so a
/// configured identifier without a version wraps with the key's current version, and rotating the key in Key Vault
/// leaves older master keys unwrapping under their own version. A wrapped key shorter than
/// <see cref="MinimumWrappedKeySizeBytes"/> bytes, which an RSA key under 3072 bits produces, is refused when wrapping.
/// </para>
/// <para>
/// A wrapped key that the service rejects with an HTTP 400 response when unwrapping fails with a
/// <see cref="CryptographicException"/>. Every other failure, such as throttling, an outage, denied access, or a
/// disabled or missing key, propagates unchanged, so that a failure to reach the service is never mistaken for a wrong
/// key.
/// </para>
/// </remarks>
// See ADR-1260, ADR-1415 and ADR-1429.
internal class AzureKeyVaultKeyWrapper(
    IAzureKeyVaultClientFactory clientFactory,
    IOptions<AzureKeyVaultKeyWrapperOptions> options
) : IReferencedKeyWrapper
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
    /// a key that is not a version of the key, or that is shorter than 3072 bits.
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

        return await WrapUnderAsync(keyId, access: null, key, cancellationToken);
    }

    /// <inheritdoc />
    public bool TryValidateKeyReference(
        string keyReference,
        [NotNullWhen(false)] out string? failure
    )
    {
        if (!AzureKeyVaultKeyId.IsKeyId(keyReference))
        {
            failure =
                "must be the identifier of a Key Vault key, such as https://tenant-vault.vault.azure.net/keys/tenant-root";
            return false;
        }

        // A reference that a caller names must stay on a Key Vault or Managed HSM host, so that the server's identity is
        // never presented to another endpoint. The configured root key is not restricted.
        // See ADR-1415.
        var hostSuffixes = RootKeyOptions.Value.ReferencedKeyHostSuffixes;
        if (!AzureKeyVaultKeyId.IsOnHostEndingWith(keyReference, hostSuffixes))
        {
            failure =
                "must name a key in an Azure Key Vault or Managed HSM, whose host ends with one of "
                + string.Join(", ", hostSuffixes);
            return false;
        }

        failure = null;
        return true;
    }

    /// <inheritdoc />
    public bool TryValidateAccessIdentity(
        string accessIdentity,
        [NotNullWhen(false)] out string? failure
    )
    {
        if (IsDirectoryId(accessIdentity))
        {
            failure = null;
            return true;
        }

        failure = "must be a Microsoft Entra tenant ID, a GUID";
        return false;
    }

    private static bool IsDirectoryId(string? value) =>
        Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty;

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when the key reference of <paramref name="referencedKey"/> is not a valid Key Vault key identifier, or
    /// its access identity is set and is not a Microsoft Entra tenant ID.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the service reports a key that is not a version of the named key, or that is shorter than 3072 bits.
    /// </exception>
    public async ValueTask<WrappedKey> WrapAsync(
        ReferencedKey referencedKey,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(referencedKey);

        if (!TryValidateKeyReference(referencedKey.KeyReference, out var failure))
        {
            throw new ArgumentException($"The key reference {failure}.", nameof(referencedKey));
        }

        if (
            referencedKey.Access is { } access
            && !TryValidateAccessIdentity(access.Identity, out var accessFailure)
        )
        {
            throw new ArgumentException(
                $"The access identity {accessFailure}.",
                nameof(referencedKey)
            );
        }

        return await WrapUnderAsync(
            referencedKey.KeyReference,
            referencedKey.Access,
            key,
            cancellationToken
        );
    }

    // Key Vault's RSA-OAEP takes no associated data, so the context is not sent, and the key-check value of the master
    // key binds the wrapped key to its owner.
    private async ValueTask<WrappedKey> WrapUnderAsync(
        string keyId,
        KeyAccess? access,
        ReadOnlyMemory<byte> key,
        CancellationToken cancellationToken
    )
    {
        var plaintext = GC.AllocateArray<byte>(key.Length, pinned: true);

        WrapResult result;
        try
        {
            // The SDK demands an array of exactly the key's length. Key Vault's RSA-OAEP takes no associated data, so
            // the context is not sent, and the key-check value of the master key binds it to its owner.
            key.CopyTo(plaintext);
            var client = ClientFactory.GetClient(keyId, access);
            result = await client.WrapKeyAsync(
                KeyWrapAlgorithm.RsaOaep256,
                plaintext,
                cancellationToken
            );
        }
        catch (Exception exception)
            when (access is not null && exception is not OperationCanceledException)
        {
            // An identity that fails must leave no credential cached.
            ClientFactory.Release(access);
            throw;
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

        if (!AzureKeyVaultKeyId.IsVersionOf(result.KeyId, keyId))
        {
            throw new InvalidOperationException(
                $"Azure Key Vault reported '{result.KeyId}', which is not a version of the key '{keyId}'."
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
    /// <exception cref="CryptographicException">
    /// Thrown when the wrapped key does not name a Key Vault key version, is empty or not valid base64url text, or was
    /// rejected by the service with an HTTP 400 response.
    /// </exception>
    public ValueTask UnwrapAsync(
        WrappedKey wrappedKey,
        ReadOnlyMemory<byte> context,
        IBufferWriter<byte> destination,
        CancellationToken cancellationToken
    ) => UnwrapCoreAsync(wrappedKey, access: null, destination, cancellationToken);

    /// <summary>
    /// Unwraps a key that was wrapped under a Key Vault key, reaching the key through the Microsoft Entra directory
    /// that the access names.
    /// </summary>
    /// <param name="wrappedKey">The wrapped key to unwrap.</param>
    /// <param name="access">The Microsoft Entra directory that tokens for the key are requested from.</param>
    /// <param name="context">The context that binds the wrapped key.</param>
    /// <param name="destination">The writer that receives the unwrapped key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="wrappedKey"/> or <paramref name="access"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the wrapped key does not name a Key Vault key version, is empty or not valid base64url text, or was
    /// rejected by the service with an HTTP 400 response.
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

        return UnwrapCoreAsync(wrappedKey, access, destination, cancellationToken);
    }

    private async ValueTask UnwrapCoreAsync(
        WrappedKey wrappedKey,
        KeyAccess? access,
        IBufferWriter<byte> destination,
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

        // The SDK demands an array of exactly the ciphertext's length.
        var text = wrappedKey.Ciphertext;
        var ciphertext = new byte[WrappedKeyCiphertext.GetByteCount(text)];
        WrappedKeyCiphertext.Decode(text, ciphertext);
        var client = ClientFactory.GetClient(keyId, access);

        UnwrapResult result;
        try
        {
            result = await client.UnwrapKeyAsync(
                KeyWrapAlgorithm.RsaOaep256,
                ciphertext,
                cancellationToken
            );
        }
        catch (Exception exception)
        {
            // An identity that fails must leave no credential cached.
            if (access is not null && exception is not OperationCanceledException)
            {
                ClientFactory.Release(access);
            }

            // Only a ciphertext that the service rejects is a cryptographic failure. Anything else propagates
            // unchanged. An outage must not look like a wrong key: callers delete keys on CryptographicException in
            // Development.
            if (exception is RequestFailedException { Status: 400 })
            {
                throw new CryptographicException(
                    $"Azure Key Vault key '{keyId}' did not unwrap the master key: the service rejected the ciphertext.",
                    exception
                );
            }

            throw;
        }

        CopyPlaintext(result.Key, destination);
    }

    private static void CopyPlaintext(byte[] plaintext, IBufferWriter<byte> destination)
    {
        // The SDK hands over an array that nothing else references and leaves the unwrapped key in it, so it is
        // zeroed once copied.
        var source = plaintext.AsSpan();
        using var lifetime = source.GetSecureLifetime();

        var length = source.Length;
        source.CopyTo(destination.GetSpan(length)[..length]);
        destination.Advance(length);
    }
}
