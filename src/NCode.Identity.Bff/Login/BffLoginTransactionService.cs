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
internal class BffLoginTransactionService(BffInstrumentedSessionStore store)
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

        using var lease = Protect(initiatorSecret, key, transaction, out var protectedTransaction);

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

        var transaction = Unprotect(initiatorSecret, key, protectedTransaction.Span);
        return transaction is null
            ? new BffLoginTransactionResult(null, BffLoginFailureReasons.InitiatorMismatch)
            : new BffLoginTransactionResult(transaction, null);
    }

    /// <summary>
    /// Encrypts a transaction into pooled memory, bound to its store key.
    /// </summary>
    /// <param name="initiatorSecret">The secret that the key is derived from.</param>
    /// <param name="key">The store key of the transaction.</param>
    /// <param name="transaction">The transaction to encrypt.</param>
    /// <param name="protectedTransaction">The encrypted transaction, which is valid until the returned rental is disposed.</param>
    /// <returns>The rental that owns <paramref name="protectedTransaction"/>; dispose it after the store has taken the value.</returns>
    private static IDisposable Protect(
        string initiatorSecret,
        string key,
        BffLoginTransaction transaction,
        out ReadOnlyMemory<byte> protectedTransaction
    )
    {
        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            JsonSerializer.Serialize(
                writer,
                transaction,
                BffJsonContext.Default.BffLoginTransaction
            );
            writer.Flush();
        }

        Span<byte> keyBytes = stackalloc byte[BffKeyDerivation.SecretLength];
        var keyLength = SecureEncoding.UTF8.GetBytes(key, keyBytes);

        using var lease = buffer.GetSpanLease(isSensitive: true);
        var rental = BufferFactory.Rent(
            BffPayloadProtector.GetProtectedLength(lease.Span.Length),
            isSensitive: false,
            out Memory<byte> memory
        );

        try
        {
            BffPayloadProtector.Protect(
                initiatorSecret,
                BffKeyDerivation.LoginPayloadPurpose,
                lease.Span,
                keyBytes[..keyLength],
                memory.Span
            );
        }
        catch
        {
            rental.Dispose();
            throw;
        }

        protectedTransaction = memory;
        return rental;
    }

    /// <summary>
    /// Decrypts a transaction, returning <see langword="null"/> when it does not authenticate or is malformed.
    /// </summary>
    /// <param name="initiatorSecret">The secret that the key is derived from.</param>
    /// <param name="key">The store key of the transaction.</param>
    /// <param name="protectedTransaction">The encrypted transaction.</param>
    private static BffLoginTransaction? Unprotect(
        string initiatorSecret,
        string key,
        ReadOnlySpan<byte> protectedTransaction
    )
    {
        Span<byte> keyBytes = stackalloc byte[BffKeyDerivation.SecretLength];
        var keyLength = SecureEncoding.UTF8.GetBytes(key, keyBytes);

        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        var unprotected = BffPayloadProtector.TryUnprotect(
            initiatorSecret,
            BffKeyDerivation.LoginPayloadPurpose,
            protectedTransaction,
            keyBytes[..keyLength],
            buffer
        );
        if (!unprotected)
            return null;

        using var lease = buffer.GetSpanLease(isSensitive: true);
        try
        {
            return JsonSerializer.Deserialize(
                lease.Span,
                BffJsonContext.Default.BffLoginTransaction
            );
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
