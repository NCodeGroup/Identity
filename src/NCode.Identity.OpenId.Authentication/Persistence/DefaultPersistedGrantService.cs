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
    // The attempts that a write which must not lose a race makes, each in a new unit of work. See ADR-0215.
    private const int MaxSaveAttempts = 3;

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
    public async ValueTask<bool> SetRevokedOnceAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        for (var attempt = 1; ; attempt++)
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<IGrantStore>();

            var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
            if (envelope is null)
                return false;

            if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
                return false;

            if (envelope.RevokedWhen is not null)
                return false;

            envelope.RevokedWhen = revokedWhen;

            try
            {
                await store.UpdateAsync(envelope, cancellationToken);

                await storeManager.SaveChangesAsync(cancellationToken);

                return true;
            }
            catch (StoreConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                // A concurrent write changed the grant after it was read, such as a sliding-expiration update.
                // A revocation must win, so it reads the grant again. See ADR-0215.
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> RevokeSessionOnceAsync(
        OpenIdContext openIdContext,
        PersistedGrantId grantId,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        for (var attempt = 1; ; attempt++)
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<IGrantStore>();

            var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
            if (envelope is null)
                return false;

            if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
                return false;

            if (envelope.RevokedWhen is not null)
                return false;

            envelope.RevokedWhen = revokedWhen;

            try
            {
                await store.UpdateAsync(envelope, cancellationToken);

                if (envelope.SessionId is { Length: > 0 } sessionId)
                {
                    // The grants issued under the session are revoked in the unit of work that revokes the session,
                    // so a failure after the claim leaves none of them active. See ADR-0223.
                    await store.RevokeWhereAsync(
                        new GrantFilter { SessionId = sessionId },
                        revokedWhen,
                        cancellationToken
                    );
                }

                await storeManager.SaveChangesAsync(cancellationToken);

                return true;
            }
            catch (StoreConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                // A concurrent write changed the session or a grant issued under it after it was read. The
                // revocation reads them again, and a concurrent revocation of the session ends it with false.
                // See ADR-0215.
            }
        }
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

        for (var attempt = 1; ; attempt++)
        {
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

                return true;
            }
            catch (StoreConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                // Another request changed the grant after it was read. A spend or a revocation leaves it inactive,
                // and the next read refuses it; a write that left it active, such as a retry within the reuse
                // interval that marks its lineage, is read again. See ADR-0211 and ADR-0217.
            }
        }
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

        var replacedGrantType = replacedGrantId.GrantType;
        var replacedHashedKey = GetHashedKey(replacedGrantId);

        for (var attempt = 1; ; attempt++)
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<IGrantStore>();

            var replaced = await store.GetOrDefaultAsync(
                replacedGrantType,
                replacedHashedKey,
                cancellationToken
            );
            if (replaced is null)
                return null;

            if (
                !string.Equals(
                    replaced.TenantId,
                    replacedGrantId.TenantId,
                    StringComparison.Ordinal
                )
            )
                return null;

            if (GetStatus(utcNow, replaced) != PersistedGrantStatus.Active)
                return null;

            // A rotation marks the grant it spends consumed, which tells a rotated refresh token from one revoked
            // otherwise. See ADR-0217.
            replaced.RevokedWhen = createdWhen;
            replaced.ConsumedWhen = createdWhen;

            var envelope = CreateEnvelope(openIdContext, grantId, grant, createdWhen, lifetime);

            try
            {
                await store.UpdateAsync(replaced, cancellationToken);
                await store.AddAsync(envelope, cancellationToken);

                await storeManager.SaveChangesAsync(cancellationToken);

                return envelope.GrantId;
            }
            catch (StoreConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                // Another request changed the grant after it was read. A spend or a revocation leaves it inactive,
                // and the next read refuses it; a write that left it active, such as a retry within the reuse
                // interval that marks its lineage, is read again. See ADR-0211 and ADR-0217.
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<string?> AddToRefreshTokenLineageOrDefault<TPayload>(
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

        var refreshTokenLineageId = grant.RefreshTokenLineageId;
        if (string.IsNullOrEmpty(refreshTokenLineageId))
            throw new ArgumentException(
                "The grant must name a refresh-token rotation family.",
                nameof(grant)
            );

        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        for (var attempt = 1; ; attempt++)
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<IGrantStore>();

            var lineage = await store.GetActiveByRefreshTokenLineageAsync(
                refreshTokenLineageId,
                cancellationToken
            );

            var active = new List<PersistedGrant>(lineage.Count);
            foreach (var member in lineage)
            {
                if (!string.Equals(member.TenantId, grant.TenantId, StringComparison.Ordinal))
                    continue;

                if (GetStatus(utcNow, member) != PersistedGrantStatus.Active)
                    continue;

                active.Add(member);
            }

            if (active.Count == 0)
                return null;

            var envelope = CreateEnvelope(openIdContext, grantId, grant, createdWhen, lifetime);

            try
            {
                // Updating each active grant unchanged gives it a new concurrency token, so a concurrent revocation
                // of the family conflicts with this store whichever commits first.
                foreach (var member in active)
                {
                    await store.UpdateAsync(member, cancellationToken);
                }

                await store.AddAsync(envelope, cancellationToken);

                await storeManager.SaveChangesAsync(cancellationToken);

                return envelope.GrantId;
            }
            catch (StoreConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                // Another request changed a grant of the family after it was read. The next read sees which grants
                // are still active, and a family with none stores nothing. See ADR-0217.
            }
        }
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
            catch (StoreConcurrencyException) when (attempt < MaxSaveAttempts)
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
    public async ValueTask<long> RevokeWhereAsync(
        OpenIdContext openIdContext,
        GrantFilter filter,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        if (filter.IsEmpty)
            throw new ArgumentException(
                "The filter must select grants by at least one criterion.",
                nameof(filter)
            );

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await RevokeWhereOnceAsync(filter, revokedWhen, cancellationToken);
            }
            catch (StoreConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                // A selected grant changed after it was read, such as a sliding-expiration update or a rotation that
                // committed in between. A revocation must win, so it reads the grants again. See ADR-0215.
            }
        }
    }

    private async ValueTask<long> RevokeWhereOnceAsync(
        GrantFilter filter,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var count = await store.RevokeWhereAsync(filter, revokedWhen, cancellationToken);

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
        Func<PersistedGrant<TPayload>, PersistedGrantUpdate<TPayload>?> update,
        CancellationToken cancellationToken
    )
    {
        var grantType = grantId.GrantType;
        var hashedKey = GetHashedKey(grantId);

        for (var attempt = 1; ; attempt++)
        {
            var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();

            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<IGrantStore>();

            var envelope = await store.GetOrDefaultAsync(grantType, hashedKey, cancellationToken);
            if (envelope is null)
                return false;

            if (!string.Equals(envelope.TenantId, grantId.TenantId, StringComparison.Ordinal))
                return false;

            if (GetStatus(utcNow, envelope) != PersistedGrantStatus.Active)
                return false;

            var jsonSerializerOptions = openIdContext.Environment.JsonSerializerOptions;

            var storedPayload = PayloadSerializer.Deserialize<TPayload>(
                envelope.PayloadJson,
                jsonSerializerOptions
            );

            var stored = new PersistedGrant<TPayload>
            {
                Status = PersistedGrantStatus.Active,
                TenantId = envelope.TenantId,
                ClientId = envelope.ClientId,
                SubjectId = envelope.SubjectId,
                SessionId = envelope.SessionId,
                RefreshTokenLineageId = envelope.RefreshTokenLineageId,
                ExpiresWhen = envelope.ExpiresWhen,
                ConsumedWhen = envelope.ConsumedWhen,
                Payload = storedPayload,
            };

            if (update(stored) is not { } change)
                return false;

            envelope.PayloadJson = PayloadSerializer.Serialize(
                change.Payload,
                jsonSerializerOptions
            );

            if (change.ExpiresWhen is { } expiresWhen)
                envelope.ExpiresWhen = expiresWhen;

            try
            {
                await store.UpdateAsync(envelope, cancellationToken);

                await storeManager.SaveChangesAsync(cancellationToken);

                return true;
            }
            catch (StoreConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                // Another request changed the grant after it was read, so the update is applied to the grant as it is
                // stored now. See ADR-0222.
            }
        }
    }
}
