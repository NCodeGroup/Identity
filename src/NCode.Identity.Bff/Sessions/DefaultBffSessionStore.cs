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
            var value = BffSessionRecordSerializer.Serialize(
                metadata,
                record.ProtectedPayload.Span
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

            return true;
        }
        catch (Exception exception) when (IsStoreFailure(exception))
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
        catch (Exception exception) when (IsStoreFailure(exception))
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
            var value = BffSessionRecordSerializer.Serialize(
                metadata,
                record.ProtectedPayload.Span
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
        catch (Exception exception) when (IsStoreFailure(exception))
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
        }
        catch (Exception exception) when (IsStoreFailure(exception))
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
        catch (Exception exception) when (IsStoreFailure(exception))
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
        catch (Exception exception) when (IsStoreFailure(exception))
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
        catch (Exception exception) when (IsStoreFailure(exception))
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
        catch (Exception exception) when (IsStoreFailure(exception))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryAcquireRefreshLeaseAsync(
        string leaseKey,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var key = BffStoreKeys.Lease(leaseKey);
            var marker = "1"u8.ToArray();
            return await values.TryAddAsync(key, marker, expiresWhen, cancellationToken);
        }
        catch (Exception exception) when (IsStoreFailure(exception))
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
        catch (Exception exception) when (IsStoreFailure(exception))
        {
            throw Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask ReleaseRefreshLeaseAsync(
        string leaseKey,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var key = BffStoreKeys.Lease(leaseKey);
            await values.RemoveAsync(key, cancellationToken);
        }
        catch (Exception exception) when (IsStoreFailure(exception))
        {
            throw Unavailable(exception);
        }
    }

    private static DateTimeOffset GetExpiresWhen(BffSessionMetadata metadata) =>
        metadata.IdleExpiresAt < metadata.AbsoluteExpiresAt
            ? metadata.IdleExpiresAt
            : metadata.AbsoluteExpiresAt;

    private static bool IsStoreFailure(Exception exception) =>
        exception is not (OperationCanceledException or BffStoreUnavailableException);

    private static BffStoreUnavailableException Unavailable(Exception exception) =>
        new("The BFF session store is unavailable.", exception);
}
