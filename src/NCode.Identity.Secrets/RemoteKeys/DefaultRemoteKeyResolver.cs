// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Provides the default implementation of <see cref="IRemoteKeyResolver"/>. It finds the descriptor of a secret type
/// that a key management service can hold, the registered provider and the provider's row without calling the
/// service, reads the public half of a key through <see cref="IRemoteKeyProvider.GetPublicKeyAsync"/>, and creates
/// the <see cref="IRemoteSecretKey"/> through the secret type's <see cref="IRemoteSecretKeyTypeDescriptor"/>.
/// </summary>
/// <param name="secretKeyTypeCatalog">The catalog of secret key-type descriptors.</param>
/// <param name="remoteKeyProviderCatalog">The catalog of registered remote key providers.</param>
/// <param name="remoteKeyDescriptorCatalog">The catalog of the rows of the mapping vocabulary.</param>
// See ADR-1476.
// See ADR-1479.
internal sealed class DefaultRemoteKeyResolver(
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    IRemoteKeyProviderCatalog remoteKeyProviderCatalog,
    IRemoteKeyDescriptorCatalog remoteKeyDescriptorCatalog
) : IRemoteKeyResolver
{
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;

    private IRemoteKeyProviderCatalog RemoteKeyProviderCatalog { get; } = remoteKeyProviderCatalog;

    private IRemoteKeyDescriptorCatalog RemoteKeyDescriptorCatalog { get; } =
        remoteKeyDescriptorCatalog;

    /// <inheritdoc />
    public RemoteKeyResolution Resolve(RemoteKeyAlgorithmSelector selector)
    {
        _ = GetDescriptor(selector.SecretType);

        if (!RemoteKeyProviderCatalog.TryGetProvider(selector.Provider, out var provider))
        {
            throw new RemoteKeyRefusedException(
                $"The remote key provider '{selector.Provider}' is not registered."
            );
        }

        if (!RemoteKeyDescriptorCatalog.TryGetAlgorithm(selector, out var algorithm))
        {
            throw new RemoteKeyRefusedException(
                $"The remote key provider '{selector.Provider}' has no algorithm '{selector.Algorithm}' "
                    + $"for the '{selector.SecretType}' secret type, key size {selector.KeySizeBits} and curve '{selector.Curve}'."
            );
        }

        return new RemoteKeyResolution { Provider = provider, Algorithm = algorithm };
    }

    /// <inheritdoc />
    public async ValueTask<SecretKey> ReadKeyAsync(
        RemoteKeyBinding binding,
        KeyMetadata metadata,
        CancellationToken cancellationToken
    )
    {
        using var publicKeyBuffer = BufferFactory.CreatePooledBufferWriter(
            isSensitive: false,
            binding.Algorithm.KeySizeBits >> 3
        );

        await binding.Provider.GetPublicKeyAsync(
            new RemotePublicKeyRequest
            {
                KeyId = binding.Reference.KeyId,
                Algorithm = binding.Algorithm,
            },
            publicKeyBuffer,
            cancellationToken
        );

        // The span lease is read after the last await.
        using var publicKeyLease = publicKeyBuffer.GetSpanLease(isSensitive: false);
        return CreateKey(binding, metadata, publicKeyLease.Span);
    }

    /// <inheritdoc />
    public SecretKey CreateKey(
        RemoteKeyBinding binding,
        KeyMetadata metadata,
        ReadOnlySpan<byte> subjectPublicKeyInfo
    )
    {
        var descriptor = GetDescriptor(binding.Algorithm.SecretType);
        var keyMetadata = metadata with
        {
            Use = binding.Algorithm.Use,
            Algorithm = binding.Algorithm.Algorithm,
        };

        try
        {
            return descriptor.CreateRemoteKey(keyMetadata, subjectPublicKeyInfo, binding);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            throw new RemoteKeyRefusedException(
                $"The remote key provider '{binding.Reference.Provider}' returned a public key for the key '{binding.Reference.KeyId}' that cannot be read as a "
                    + $"'{binding.Algorithm.SecretType}' key.",
                exception
            );
        }
        catch (ArgumentException exception)
        {
            throw new RemoteKeyRefusedException(
                $"The key '{binding.Reference.KeyId}' of the remote key provider '{binding.Reference.Provider}' does not have the key size "
                    + $"{binding.Algorithm.KeySizeBits} and curve '{binding.Algorithm.Curve}' of its row '{binding.Algorithm.Algorithm}'.",
                exception
            );
        }
    }

    // Finds, without calling the key management service, the descriptor of a secret type a service can hold.
    private IRemoteSecretKeyTypeDescriptor GetDescriptor(string secretType)
    {
        if (
            !SecretKeyTypeCatalog.TryGet(secretType, out var descriptor)
            || descriptor is not IRemoteSecretKeyTypeDescriptor remoteDescriptor
        )
        {
            throw new RemoteKeyRefusedException(
                $"The '{secretType}' secret type cannot be held in a key management service."
            );
        }

        return remoteDescriptor;
    }
}
