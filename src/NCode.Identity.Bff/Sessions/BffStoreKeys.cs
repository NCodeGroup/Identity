// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Encoders;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Builds the keys that the default session store uses in the shared-state sets.
/// </summary>
internal static class BffStoreKeys
{
    /// <summary>
    /// Gets the key of a session value.
    /// </summary>
    public static string Session(string storeKey) => "bff:session:" + storeKey;

    /// <summary>
    /// Gets the key of a login transaction.
    /// </summary>
    public static string Transaction(string transactionKey) => "bff:txn:" + transactionKey;

    /// <summary>
    /// Gets the key of a refresh lease.
    /// </summary>
    public static string Lease(string leaseKey) => "bff:lease:" + leaseKey;

    /// <summary>
    /// Gets the key of the index of the sessions of an OP session identifier.
    /// </summary>
    public static string SessionIdIndex(string issuer, string sessionId) =>
        "bff:sid:" + Digest(issuer, sessionId);

    /// <summary>
    /// Gets the key of the index of the sessions of a subject.
    /// </summary>
    public static string SubjectIndex(string issuer, string subject) =>
        "bff:sub:" + Digest(issuer, subject);

    // Each value is length-prefixed, so a value cannot make a key ambiguous or unbounded.
    private static string Digest(string first, string second)
    {
        var firstLength = SecureEncoding.UTF8.GetByteCount(first);
        var secondLength = SecureEncoding.UTF8.GetByteCount(second);

        var secondOffset = sizeof(int) + firstLength;
        using var lease = BufferFactory.Rent(
            secondOffset + sizeof(int) + secondLength,
            isSensitive: false,
            out Span<byte> buffer
        );
        BinaryPrimitives.WriteInt32BigEndian(buffer[..sizeof(int)], firstLength);
        SecureEncoding.UTF8.GetBytes(first, buffer.Slice(sizeof(int), firstLength));
        BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(secondOffset, sizeof(int)), secondLength);
        SecureEncoding.UTF8.GetBytes(
            second,
            buffer.Slice(secondOffset + sizeof(int), secondLength)
        );

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(buffer, hash);
        return Base64Url.Encode(hash);
    }
}
