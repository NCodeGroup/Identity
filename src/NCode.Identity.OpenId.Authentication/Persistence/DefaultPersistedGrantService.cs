#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Provides a default implementation for the <see cref="IPersistedGrantService"/> abstraction.
/// </summary>
internal class DefaultPersistedGrantService(
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IStoreManagerFactory storeManagerFactory,
    IPersistedPayloadSerializer payloadSerializer
) : IPersistedGrantService
{
    private const int MaxRevocationAttempts = 3;

    // This uses C# compiler's ability to refer to static data directly.
    // For more information see https://vcsjones.dev/2019/02/01/csharp-readonly-span-bytes-static
    private static ReadOnlySpan<char> TenantDelimiter => "~~";

    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPersistedPayloadSerializer PayloadSerializer { get; } = payloadSerializer;

    private static string GetHashInput(PersistedGrantId grantId) =>
        string.Concat(grantId.TenantId.AsSpan(), TenantDelimiter, grantId.GrantKey.AsSpan());

    private string GetHashedKey(PersistedGrantId grantId) =>
        CryptoService.HashValue(
            GetHashInput(grantId),
            HashAlgorithmName.SHA256,
            BinaryEncodings.Base64
        );

    /// <inheritdoc />
    public PersistedGrantId CreateGrantId(string tenantId, string grantType, string grantKey) =>
        new()
        {
            TenantId = tenantId,
            GrantType = grantType,
            GrantKey = grantKey,
        };

    /// <inheritdoc />
    public async ValueTask<string> AddAsync<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        PersistedGrant<TPayload> grant,
        DateTimeOffset createdWhen,
        TimeSpan? lifetime,
        CancellationToken cancellationToken
    )
    {
        if (grant.Status != PersistedGrantStatus.Active)
            throw new InvalidOperationException("The grant must be active.");

        var envelope = CreateEnvelope(openIdContext, grantId, grant, createdWhen, lifetime);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        await store.AddAsync(envelope, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return envelope.GrantId;
    }

    private PersistedGrant CreateEnvelope<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        PersistedGrant<TPayload> grant,
        DateTimeOffset createdWhen,
        TimeSpan? lifetime
    )
    {
        var openIdEnvironment = openIdContext.Environment;

        var hashedKey = GetHashedKey(grantId);

        var expiresWhen = createdWhen + lifetime;

        var payloadJson = PayloadSerializer.Serialize(
            grant.Payload,
            openIdEnvironment.JsonSerializerOptions
        );

        return new PersistedGrant
        {
            GrantType = grantId.GrantType,
            HashedKey = hashedKey,
            GrantId = CryptoService.GenerateResourceId(),
            TenantId = grant.TenantId,
            ClientId = grant.ClientId,
            SubjectId = grant.SubjectId,
            SessionId = grant.SessionId,
            RefreshTokenLineageId = grant.RefreshTokenLineageId,
            CreatedWhen = createdWhen,
            ExpiresWhen = expiresWhen,
            RevokedWhen = null,
            ConsumedWhen = null,
            PayloadJson = payloadJson,
        };
    }

    private static PersistedGrantStatus GetStatus(DateTimeOffset utcNow, PersistedGrant envelope)
    {
        if (envelope.RevokedWhen is not null)
            return PersistedGrantStatus.Revoked;

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (envelope.ExpiresWhen <= utcNow)
            return PersistedGrantStatus.Expired;

        return PersistedGrantStatus.Active;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant<TPayload>?> GetOrDefaultAsync<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        CancellationToken cancellationToken
    )
    {
        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var openIdEnvironment = openIdContext.Environment;

        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
        if (envelope == null)
            return null;

        if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
            return null;

        var payload = PayloadSerializer.Deserialize<TPayload>(
            envelope.PayloadJson,
            openIdEnvironment.JsonSerializerOptions
        );

        var status = GetStatus(utcNow, envelope);
        var persistedGrant = new PersistedGrant<TPayload>
        {
            Status = status,
            TenantId = envelope.TenantId,
            ClientId = envelope.ClientId,
            SubjectId = envelope.SubjectId,
            SessionId = envelope.SessionId,
            RefreshTokenLineageId = envelope.RefreshTokenLineageId,
            ExpiresWhen = envelope.ExpiresWhen,
            ConsumedWhen = envelope.ConsumedWhen,
            Payload = payload,
        };

        return persistedGrant;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedGrant<TPayload>?> ConsumeOnceOrDefault<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        CancellationToken cancellationToken
    )
    {
        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var openIdEnvironment = openIdContext.Environment;

        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
        if (envelope == null)
            return null;

        if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
            return null;

        var status = GetStatus(utcNow, envelope);
        var isConsumed = envelope.ConsumedWhen is not null;
        if (status != PersistedGrantStatus.Active || isConsumed)
            return null;

        var payload = PayloadSerializer.Deserialize<TPayload>(
            envelope.PayloadJson,
            openIdEnvironment.JsonSerializerOptions
        );

        envelope.ConsumedWhen = utcNow;

        try
        {
            await store.UpdateAsync(envelope, cancellationToken);

            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // Another request spent the grant after it was read, so this one is answered as a second spend.
            // See ADR-0208.
            return null;
        }

        var persistedGrant = new PersistedGrant<TPayload>
        {
            Status = status,
            TenantId = envelope.TenantId,
            ClientId = envelope.ClientId,
            SubjectId = envelope.SubjectId,
            SessionId = envelope.SessionId,
            RefreshTokenLineageId = envelope.RefreshTokenLineageId,
            ExpiresWhen = envelope.ExpiresWhen,
            ConsumedWhen = utcNow,
            Payload = payload,
        };

        return persistedGrant;
    }

    /// <inheritdoc />
    public async ValueTask SetRevokedOnceAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
        if (envelope is null)
            return;

        if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
            return;

        if (envelope.RevokedWhen is not null)
            return;

        envelope.RevokedWhen = revokedWhen;

        await store.UpdateAsync(envelope, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryRevokeOnceAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
        if (envelope is null)
            return false;

        if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
            return false;

        if (GetStatus(utcNow, envelope) != PersistedGrantStatus.Active)
            return false;

        envelope.RevokedWhen = revokedWhen;

        try
        {
            await store.UpdateAsync(envelope, cancellationToken);

            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public async ValueTask<string?> ReplaceOnceOrDefault<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId replacedGrantId,
        PersistedGrantId grantId,
        PersistedGrant<TPayload> grant,
        DateTimeOffset createdWhen,
        TimeSpan? lifetime,
        CancellationToken cancellationToken
    )
    {
        if (grant.Status != PersistedGrantStatus.Active)
            throw new InvalidOperationException("The grant must be active.");

        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var replaced = await store.GetOrDefaultAsync(
            replacedGrantId.GrantType,
            GetHashedKey(replacedGrantId),
            cancellationToken
        );
        if (replaced is null)
            return null;

        if (!string.Equals(replaced.TenantId, replacedGrantId.TenantId, StringComparison.Ordinal))
            return null;

        if (GetStatus(utcNow, replaced) != PersistedGrantStatus.Active)
            return null;

        replaced.RevokedWhen = createdWhen;

        var envelope = CreateEnvelope(openIdContext, grantId, grant, createdWhen, lifetime);

        try
        {
            await store.UpdateAsync(replaced, cancellationToken);
            await store.AddAsync(envelope, cancellationToken);

            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // Another request revoked or rotated the replaced grant after it was read, so nothing is stored.
            // See ADR-0211.
            return null;
        }

        return envelope.GrantId;
    }

    /// <inheritdoc />
    public async ValueTask<long> RevokeByRefreshTokenLineageAsync(
        OpenIdContext openIdContext,
        string refreshTokenLineageId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await RevokeLineageOnceAsync(
                    refreshTokenLineageId,
                    revokedWhen,
                    cancellationToken
                );
            }
            catch (StoreConcurrencyException) when (attempt < MaxRevocationAttempts)
            {
                // A grant of the lineage changed after it was read, such as a rotation that committed in between.
                // A revocation must win, so it reads the lineage again. See ADR-0211.
            }
        }
    }

    private async ValueTask<long> RevokeLineageOnceAsync(
        string refreshTokenLineageId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var count = await store.RevokeByRefreshTokenLineageAsync(
            refreshTokenLineageId,
            revokedWhen,
            cancellationToken
        );

        await storeManager.SaveChangesAsync(cancellationToken);

        return count;
    }

    /// <inheritdoc />
    public async ValueTask<long> RevokeBySessionAsync(
        OpenIdContext openIdContext,
        string sessionId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var count = await store.RevokeWhereAsync(
            new GrantFilter { SessionId = sessionId },
            revokedWhen,
            cancellationToken
        );

        await storeManager.SaveChangesAsync(cancellationToken);

        return count;
    }

    /// <inheritdoc />
    public async ValueTask UpdateExpirationAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
        if (envelope == null)
            return;

        if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
            return;

        envelope.ExpiresWhen = expiresWhen;

        try
        {
            await store.UpdateAsync(envelope, cancellationToken);

            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // Another request changed the grant after it was read, such as a concurrent refresh that extended it,
            // and its change stands. See ADR-0211.
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> UpdatePayloadAsync<TPayload>(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        TPayload payload,
        CancellationToken cancellationToken
    )
    {
        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var openIdEnvironment = openIdContext.Environment;

        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
        if (envelope is null)
            return false;

        if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
            return false;

        if (GetStatus(utcNow, envelope) != PersistedGrantStatus.Active)
            return false;

        envelope.PayloadJson = PayloadSerializer.Serialize(
            payload,
            openIdEnvironment.JsonSerializerOptions
        );

        await store.UpdateAsync(envelope, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        return true;
    }
}
