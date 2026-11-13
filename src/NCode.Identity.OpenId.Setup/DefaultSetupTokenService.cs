// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Setup.Logging;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Provides a default implementation of <see cref="ISetupTokenService"/>. A configured <see cref="SetupOptions.Token"/>
/// wins: only its SHA-256 hash is held, and it is never logged. Without one, an uninitialized instance generates a
/// 256-bit token, stores only its SHA-256 hash with its issue and expiry instants in the instance setup row that every
/// replica reads, and logs the token once at Warning on the replica that stored it.
/// </summary>
internal sealed class DefaultSetupTokenService(
    IStoreManagerFactory storeManagerFactory,
    IOptions<SetupOptions> options,
    TimeProvider timeProvider,
    SetupProcessClock processClock,
    ILogger<DefaultSetupTokenService> logger
) : ISetupTokenService
{
    /// <summary>
    /// Specifies the number of random bytes in a generated setup token.
    /// </summary>
    internal const int GeneratedTokenByteLength = 32;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private SetupProcessClock ProcessClock { get; } = processClock;

    private ILogger<DefaultSetupTokenService> Logger { get; } = logger;

    private TimeSpan GeneratedTokenLifetime { get; } = options.Value.GeneratedTokenLifetime;

    private byte[]? ConfiguredTokenHash { get; } = HashConfiguredToken(options.Value.Token);

    /// <inheritdoc />
    public string TokenSource =>
        ConfiguredTokenHash is null ? SetupTokenSources.Generated : SetupTokenSources.Configured;

    private static byte[]? HashConfiguredToken(string? token) =>
        string.IsNullOrWhiteSpace(token) ? null : HashToken(token);

    private static byte[] HashToken(string token) =>
        SHA256.HashData(SecureEncoding.UTF8.GetBytes(token));

    /// <inheritdoc />
    public async ValueTask EnsureTokenAsync(CancellationToken cancellationToken)
    {
        if (ConfiguredTokenHash is not null)
            return;

        var setup = await InstanceSetupRecords.GetOrAddAsync(
            StoreManagerFactory,
            cancellationToken
        );
        if (setup.IsInitialized)
            return;

        // A token issued at or after this process started is this process's or that of a replica of the same rollout.
        if (setup.TokenIssuedAt is { } storedIssuedAt && storedIssuedAt >= ProcessClock.StartedAt)
            return;

        var token = Base64Url.EncodeToString(
            RandomNumberGenerator.GetBytes(GeneratedTokenByteLength)
        );
        var issuedAt = TimeProvider.GetUtcNow();
        var expiresAt = issuedAt + GeneratedTokenLifetime;

        setup.TokenHash = HashToken(token);
        setup.TokenIssuedAt = issuedAt;
        setup.TokenExpiresAt = expiresAt;

        try
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<IInstanceSetupStore>();

            await store.UpdateAsync(setup, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // Another replica stored a token between the read above and this write, so that replica logs its token.
            var concurrent = await InstanceSetupRecords.GetOrAddAsync(
                StoreManagerFactory,
                cancellationToken
            );
            Logger.SetupTokenIssuedByAnotherReplica(concurrent.TokenIssuedAt);
            return;
        }

        Logger.SetupTokenIssued(token, expiresAt);
    }

    /// <inheritdoc />
    public async ValueTask<bool> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        var presentedHash = HashToken(token);

        if (ConfiguredTokenHash is not null)
            return CryptographicOperations.FixedTimeEquals(presentedHash, ConfiguredTokenHash);

        var setup = await FindAsync(cancellationToken);
        if (setup?.TokenHash is not { } storedHash)
            return false;

        var isMatch = CryptographicOperations.FixedTimeEquals(presentedHash, storedHash);

        return isMatch
            && !setup.IsInitialized
            && setup.TokenExpiresAt is { } expiresAt
            && expiresAt > TimeProvider.GetUtcNow();
    }

    private async ValueTask<PersistedInstanceSetup?> FindAsync(CancellationToken cancellationToken)
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IInstanceSetupStore>();
        return await store.GetOrDefaultAsync(cancellationToken);
    }
}
