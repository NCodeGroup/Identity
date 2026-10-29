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

using System.Buffers;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.Bff.Serialization;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Login;

/// <summary>
/// The outcome of taking a login transaction: the transaction, or the reason that there is none.
/// </summary>
/// <param name="Transaction">The transaction, when it was taken and decrypted.</param>
/// <param name="FailureReason">The reason that there is no transaction.</param>
internal readonly record struct BffLoginTransactionResult(
    BffLoginTransaction? Transaction,
    string? FailureReason
);

/// <summary>
/// Stores login transactions encrypted under the initiator secret, and takes them back exactly once.
/// </summary>
internal class BffLoginTransactionService(IBffSessionStore store)
{
    /// <summary>
    /// Stores a login transaction.
    /// </summary>
    /// <param name="state">The <c>state</c> of the authorization request.</param>
    /// <param name="initiatorSecret">The secret that the login cookie carries.</param>
    /// <param name="transaction">The transaction to store.</param>
    /// <param name="expiresWhen">The instant after which the transaction is gone.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">The state is not valid or a transaction already exists for it.</exception>
    public virtual async ValueTask StoreAsync(
        string state,
        string initiatorSecret,
        BffLoginTransaction transaction,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (
            !BffKeyDerivation.TryDeriveIdentifier(
                state,
                BffKeyDerivation.LoginIdPurpose,
                out var key
            )
        )
            throw new InvalidOperationException("The login state is not valid.");

        var json = JsonSerializer.SerializeToUtf8Bytes(
            transaction,
            BffJsonContext.Default.BffLoginTransaction
        );
        var keyBytes = SecureEncoding.UTF8.GetBytes(key);
        var protectedTransaction = BffPayloadProtector.Protect(
            initiatorSecret,
            BffKeyDerivation.LoginPayloadPurpose,
            json,
            keyBytes
        );

        var added = await store.TryAddTransactionAsync(
            key,
            protectedTransaction,
            expiresWhen,
            cancellationToken
        );
        if (!added)
            throw new InvalidOperationException(
                "A login transaction already exists for the state."
            );
    }

    /// <summary>
    /// Takes a login transaction, so that no replica can take it again.
    /// </summary>
    /// <param name="state">The <c>state</c> that the callback returned.</param>
    /// <param name="initiatorSecret">The secret that the login cookie carried.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    public virtual async ValueTask<BffLoginTransactionResult> TakeAsync(
        string? state,
        string? initiatorSecret,
        CancellationToken cancellationToken
    )
    {
        if (
            !BffKeyDerivation.TryDeriveIdentifier(
                state,
                BffKeyDerivation.LoginIdPurpose,
                out var key
            )
        )
            return new BffLoginTransactionResult(null, BffLoginFailureReasons.UnknownState);

        // The transaction is taken before it is decrypted, so a wrong initiator cannot probe it repeatedly.
        var taken = await store.TryTakeTransactionAsync(key, cancellationToken);
        if (taken is not { } protectedTransaction)
            return new BffLoginTransactionResult(null, BffLoginFailureReasons.UnknownState);

        if (initiatorSecret is null)
            return new BffLoginTransactionResult(null, BffLoginFailureReasons.InitiatorMismatch);

        var keyBytes = SecureEncoding.UTF8.GetBytes(key);
        var writer = new ArrayBufferWriter<byte>();
        var unprotected = BffPayloadProtector.TryUnprotect(
            initiatorSecret,
            BffKeyDerivation.LoginPayloadPurpose,
            protectedTransaction.Span,
            keyBytes,
            writer
        );
        if (!unprotected)
            return new BffLoginTransactionResult(null, BffLoginFailureReasons.InitiatorMismatch);

        BffLoginTransaction? transaction;
        try
        {
            transaction = JsonSerializer.Deserialize(
                writer.WrittenSpan,
                BffJsonContext.Default.BffLoginTransaction
            );
        }
        catch (JsonException)
        {
            return new BffLoginTransactionResult(null, BffLoginFailureReasons.InitiatorMismatch);
        }

        return transaction is null
            ? new BffLoginTransactionResult(null, BffLoginFailureReasons.InitiatorMismatch)
            : new BffLoginTransactionResult(transaction, null);
    }
}
