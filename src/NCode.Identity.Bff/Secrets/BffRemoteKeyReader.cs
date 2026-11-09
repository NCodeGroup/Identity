// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Options;
using NCode.Identity.Jose;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Bff.Secrets;

/// <summary>
/// Reads one <c>Bff:KeyRings:{name}:RemoteKeys:{kid}</c> entry: the public half of a key that a key management
/// service holds, and the proof that the key holds the private half.
/// </summary>
// See ADR-1479.
internal sealed class BffRemoteKeyReader(
    IRemoteKeyResolver remoteKeyResolver,
    IRemoteKeyProver remoteKeyProver
)
{
    private IRemoteKeyResolver RemoteKeyResolver { get; } = remoteKeyResolver;

    private IRemoteKeyProver RemoteKeyProver { get; } = remoteKeyProver;

    /// <summary>
    /// Reads the public half of a remote key and proves that the key management service holds the private half.
    /// </summary>
    /// <param name="keyRing">The name of the key ring within <c>Bff:KeyRings</c>.</param>
    /// <param name="keyId">The id of the key within the key ring.</param>
    /// <param name="options">The configuration of the remote key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="IRemoteSecretKey"/> that holds the public half.</returns>
    /// <exception cref="InvalidOperationException">The key cannot be read or proved. The cause is the inner
    /// exception.</exception>
    public async ValueTask<SecretKey> ReadAsync(
        string keyRing,
        string keyId,
        BffRemoteKeyOptions options,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(keyRing);
        ArgumentException.ThrowIfNullOrEmpty(keyId);
        ArgumentNullException.ThrowIfNull(options);

        SecretKey? key = null;
        try
        {
            var selector = new RemoteKeyAlgorithmSelector
            {
                Provider = options.Provider ?? string.Empty,
                Algorithm = options.Algorithm ?? string.Empty,
                SecretType = options.SecretType ?? string.Empty,
                KeySizeBits = options.KeySizeBits,
                Curve = options.Curve,
            };

            var resolution = RemoteKeyResolver.Resolve(selector);
            key = await RemoteKeyResolver.ReadKeyAsync(
                resolution.Bind(options.KeyId ?? string.Empty),
                new KeyMetadata { KeyId = keyId },
                cancellationToken
            );

            await RemoteKeyProver.ProveAsync(key, cancellationToken);
            return key;
        }
        catch (OperationCanceledException)
        {
            (key as IDisposable)?.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            (key as IDisposable)?.Dispose();
            throw new InvalidOperationException(
                $"The BFF cannot load the remote key '{keyId}' of the key ring '{keyRing}' from the remote key provider '{options.Provider}': {exception.Message}",
                exception
            );
        }
    }
}
