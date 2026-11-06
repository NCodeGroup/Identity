// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Buffers;
using NCode.Identity.SharedState;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// The default <see cref="IBffSessionStore"/>, which keeps sessions, login transactions and refresh leases in the
/// shared-state sets that every replica sees.
/// </summary>
internal class DefaultBffSessionStore(IExpiringValueSet values, IExpiringIndexSet indexes)
    : IBffSessionStore
{
    /// <inheritdoc />
    public async ValueTask<bool> TryCreateAsync(
        BffSessionRecord record,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var metadata = record.Metadata;
            var key = BffStoreKeys.Session(record.StoreKey);
            using var lease = BffSessionRecordSerializer.Serialize(
                metadata,
                record.ProtectedPayload.Span,
                out var value
            );
            var expiresWhen = GetExpiresWhen(metadata);

            var added = await values.TryAddAsync(key, value, expiresWhen, cancellationToken);
            if (!added)
                return false;

            if (metadata.SessionId is { Length: > 0 } sessionId)
            {
                var sessionIdIndex = BffStoreKeys.SessionIdIndex(metadata.Issuer, sessionId);
                await indexes.AddMemberAsync(
                    sessionIdIndex,
                    record.StoreKey,
                    metadata.AbsoluteExpiresAt,
                    cancellationToken
                );
            }

            var subjectIndex = BffStoreKeys.SubjectIndex(metadata.Issuer, metadata.Subject);
            await indexes.AddMemberAsync(
                subjectIndex,
                record.StoreKey,
                metadata.AbsoluteExpiresAt,
                cancellationToken
            );

            var client = new BffSessionClient(
                metadata.Frontend,
                metadata.Authority,
                metadata.ClientId
            );
            await indexes.AddMemberAsync(
                BffStoreKeys.ClientIndex(client),
                record.StoreKey,
                metadata.AbsoluteExpiresAt,
                cancellationToken
            );
            await indexes.AddMemberAsync(
                BffStoreKeys.ClientsIndex,
                BffStoreKeys.ClientMember(client, metadata.AbsoluteExpiresAt),
                BffStoreKeys.ClientMemberExpiresAt(metadata.AbsoluteExpiresAt),
                cancellationToken
            );

            return true;
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<BffSessionRecord?> GetAsync(
        string storeKey,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var key = BffStoreKeys.Session(storeKey);
            var stored = await values.GetAsync(key, cancellationToken);
            if (stored is not { } found)
                return null;

            if (
                !BffSessionRecordSerializer.TryDeserialize(
                    found.Value,
                    out var metadata,
                    out var protectedPayload
                )
            )
                return null;

            return new BffSessionRecord(storeKey, found.Version, metadata, protectedPayload);
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryReplaceAsync(
        BffSessionRecord record,
        long expectedVersion,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var metadata = record.Metadata;
            var key = BffStoreKeys.Session(record.StoreKey);
            using var lease = BffSessionRecordSerializer.Serialize(
                metadata,
                record.ProtectedPayload.Span,
                out var value
            );
            var expiresWhen = GetExpiresWhen(metadata);

            return await values.TryReplaceAsync(
                key,
                expectedVersion,
                value,
                expiresWhen,
                cancellationToken
            );
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(
        string storeKey,
        BffSessionMetadata metadata,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var key = BffStoreKeys.Session(storeKey);
            await values.RemoveAsync(key, cancellationToken);

            if (metadata.SessionId is { Length: > 0 } sessionId)
            {
                var sessionIdIndex = BffStoreKeys.SessionIdIndex(metadata.Issuer, sessionId);
                await indexes.RemoveMemberAsync(sessionIdIndex, storeKey, cancellationToken);
            }

            var subjectIndex = BffStoreKeys.SubjectIndex(metadata.Issuer, metadata.Subject);
            await indexes.RemoveMemberAsync(subjectIndex, storeKey, cancellationToken);

            var client = new BffSessionClient(
                metadata.Frontend,
                metadata.Authority,
                metadata.ClientId
            );
            await indexes.RemoveMemberAsync(
                BffStoreKeys.ClientIndex(client),
                storeKey,
                cancellationToken
            );
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> FindBySessionIdAsync(
        string issuer,
        string sessionId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var indexKey = BffStoreKeys.SessionIdIndex(issuer, sessionId);
            return await indexes.GetMembersAsync(indexKey, cancellationToken);
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> FindBySubjectAsync(
        string issuer,
        string subject,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var indexKey = BffStoreKeys.SubjectIndex(issuer, subject);
            return await indexes.GetMembersAsync(indexKey, cancellationToken);
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<BffSessionClient>> FindClientsAsync(
        CancellationToken cancellationToken
    )
    {
        try
        {
            var members = await indexes.GetMembersAsync(
                BffStoreKeys.ClientsIndex,
                cancellationToken
            );

            var clients = new List<BffSessionClient>();
            foreach (var member in members)
            {
                if (BffStoreKeys.TryParseClientMember(member, out var client))
                    clients.Add(client);
            }

            return clients.Distinct().ToList();
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> FindByClientAsync(
        BffSessionClient client,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var indexKey = BffStoreKeys.ClientIndex(client);
            return await indexes.GetMembersAsync(indexKey, cancellationToken);
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryAddTransactionAsync(
        string transactionKey,
        ReadOnlyMemory<byte> protectedTransaction,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var key = BffStoreKeys.Transaction(transactionKey);
            return await values.TryAddAsync(
                key,
                protectedTransaction,
                expiresWhen,
                cancellationToken
            );
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>?> TryTakeTransactionAsync(
        string transactionKey,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var key = BffStoreKeys.Transaction(transactionKey);
            var taken = await values.TryTakeAsync(key, cancellationToken);
            if (taken is not { } found)
                return null;

            return found.Value;
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<BffRefreshLease?> TryAcquireRefreshLeaseAsync(
        string leaseKey,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var key = BffStoreKeys.Lease(leaseKey);
            var owner = BffKeyDerivation.CreateSecret();
            using var lease = RentOwnerBytes(owner, out var value);
            var added = await values.TryAddAsync(key, value, expiresWhen, cancellationToken);
            return added ? new BffRefreshLease(leaseKey, owner) : null;
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> IsRefreshLeaseHeldAsync(
        string leaseKey,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var key = BffStoreKeys.Lease(leaseKey);
            var stored = await values.GetAsync(key, cancellationToken);
            return stored is not null;
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask ReleaseRefreshLeaseAsync(
        BffRefreshLease lease,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var key = BffStoreKeys.Lease(lease.LeaseKey);
            using var ownerLease = RentOwnerBytes(lease.Owner, out var owner);
            await values.TryRemoveAsync(key, owner, cancellationToken);
        }
        catch (Exception exception) when (IsStoreFailure(exception, cancellationToken))
        {
            throw Unavailable(exception);
        }
    }

    /// <summary>
    /// Encodes a refresh lease's owner token as UTF-8 into sensitive pooled memory that is zeroed when the returned
    /// lease is disposed. The bytes live in <see cref="Memory{T}"/> because they cross an <c>await</c>.
    /// </summary>
    /// <param name="owner">The owner token to encode.</param>
    /// <param name="bytes">The owner token's UTF-8 bytes.</param>
    /// <returns>The lease that returns the memory to the pool.</returns>
    private static IDisposable RentOwnerBytes(string owner, out Memory<byte> bytes)
    {
        var byteCount = SecureEncoding.UTF8.GetByteCount(owner);
        var lease = BufferFactory.Rent(byteCount, isSensitive: true, out bytes);
        SecureEncoding.UTF8.GetBytes(owner, bytes.Span);
        return lease;
    }

    private static DateTimeOffset GetExpiresWhen(BffSessionMetadata metadata) =>
        metadata.IdleExpiresAt < metadata.AbsoluteExpiresAt
            ? metadata.IdleExpiresAt
            : metadata.AbsoluteExpiresAt;

    private static bool IsStoreFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is not BffStoreUnavailableException
        && (
            exception is not OperationCanceledException
            || !cancellationToken.IsCancellationRequested
        );

    private static BffStoreUnavailableException Unavailable(Exception exception) =>
        new("The BFF session store is unavailable.", exception);
}
