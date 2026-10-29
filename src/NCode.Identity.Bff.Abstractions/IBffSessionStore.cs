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

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The replaceable store of BFF sessions, login transactions and refresh leases. It holds only opaque ciphertext, so a
/// replacement never sees a token and cannot skip the encryption. Every member throws
/// <see cref="BffStoreUnavailableException"/> when the underlying store fails.
/// </summary>
[PublicAPI]
public interface IBffSessionStore
{
    /// <summary>
    /// Adds the record at version one, together with its <c>sid</c> and <c>sub</c> index members.
    /// </summary>
    /// <param name="record">The record to add.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> if the record was added; or <see langword="false"/> if the key already exists.</returns>
    ValueTask<bool> TryCreateAsync(BffSessionRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the record stored under <paramref name="storeKey"/>.
    /// </summary>
    /// <param name="storeKey">The key of the record.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The record; or <see langword="null"/> if it is absent or expired.</returns>
    ValueTask<BffSessionRecord?> GetAsync(string storeKey, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the record when its stored version is still <paramref name="expectedVersion"/>.
    /// </summary>
    /// <param name="record">The replacement record.</param>
    /// <param name="expectedVersion">The version the caller read, which must still be current.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> if the record was replaced; or <see langword="false"/> on a version conflict or when it is absent.</returns>
    ValueTask<bool> TryReplaceAsync(
        BffSessionRecord record,
        long expectedVersion,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes the record and its index members.
    /// </summary>
    /// <param name="storeKey">The key of the record.</param>
    /// <param name="metadata">The metadata of the record, which names its index members.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the record is removed.</returns>
    ValueTask RemoveAsync(
        string storeKey,
        BffSessionMetadata metadata,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Finds the sessions of an OP session identifier.
    /// </summary>
    /// <param name="issuer">The issuer of the OP.</param>
    /// <param name="sessionId">The OP <c>sid</c>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The store keys of the matching sessions.</returns>
    ValueTask<IReadOnlyList<string>> FindBySessionIdAsync(
        string issuer,
        string sessionId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Finds the sessions of a subject.
    /// </summary>
    /// <param name="issuer">The issuer of the OP.</param>
    /// <param name="subject">The <c>sub</c> of the user.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The store keys of the matching sessions.</returns>
    ValueTask<IReadOnlyList<string>> FindBySubjectAsync(
        string issuer,
        string subject,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Adds a login transaction when none exists under the key.
    /// </summary>
    /// <param name="transactionKey">The key of the transaction.</param>
    /// <param name="protectedTransaction">The encrypted transaction.</param>
    /// <param name="expiresWhen">The instant after which the transaction is gone.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> if the transaction was added; or <see langword="false"/> if the key exists.</returns>
    ValueTask<bool> TryAddTransactionAsync(
        string transactionKey,
        ReadOnlyMemory<byte> protectedTransaction,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Atomically reads and removes a login transaction, so it is single-use on every replica.
    /// </summary>
    /// <param name="transactionKey">The key of the transaction.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The encrypted transaction; or <see langword="null"/> if it is absent, expired or already taken.</returns>
    ValueTask<ReadOnlyMemory<byte>?> TryTakeTransactionAsync(
        string transactionKey,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Acquires a refresh lease when none is held under the key.
    /// </summary>
    /// <param name="leaseKey">The key of the lease.</param>
    /// <param name="expiresWhen">The instant after which the lease is gone.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> if the lease was acquired; or <see langword="false"/> if it is held.</returns>
    ValueTask<bool> TryAcquireRefreshLeaseAsync(
        string leaseKey,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Determines whether a refresh lease is held.
    /// </summary>
    /// <param name="leaseKey">The key of the lease.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> if an unexpired lease is held; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> IsRefreshLeaseHeldAsync(string leaseKey, CancellationToken cancellationToken);

    /// <summary>
    /// Releases a refresh lease. Releasing a lease that is not held does nothing.
    /// </summary>
    /// <param name="leaseKey">The key of the lease.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the lease is released.</returns>
    ValueTask ReleaseRefreshLeaseAsync(string leaseKey, CancellationToken cancellationToken);
}
