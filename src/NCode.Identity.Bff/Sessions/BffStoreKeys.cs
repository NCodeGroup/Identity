// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Globalization;
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

    /// <summary>
    /// Gets the key of the index whose members list the clients that have sessions.
    /// </summary>
    public const string ClientsIndex = "bff:clients";

    /// <summary>
    /// Gets the key of the index of the sessions of a client.
    /// </summary>
    public static string ClientIndex(BffSessionClient client) =>
        "bff:client:" + Digest(client.Frontend, Digest(client.Authority, client.ClientId));

    /// <summary>
    /// Gets the member of <see cref="ClientsIndex"/> that lists a client for the UTC day of a session's absolute expiry.
    /// </summary>
    /// <remarks>
    /// The index holds one member per client and UTC day of absolute expiry, and each member expires at the end of
    /// its day (<see cref="ClientMemberExpiresAt"/>), so adding a session never shortens a client's listing.
    /// </remarks>
    // See ADR-1443.
    public static string ClientMember(BffSessionClient client, DateTimeOffset absoluteExpiresAt) =>
        string.Join(
            '|',
            absoluteExpiresAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            client.Frontend,
            Uri.EscapeDataString(client.Authority),
            Uri.EscapeDataString(client.ClientId)
        );

    /// <summary>
    /// Gets the instant at which the member of <see cref="ClientsIndex"/> for a session's absolute expiry expires:
    /// the end of that expiry's UTC day.
    /// </summary>
    public static DateTimeOffset ClientMemberExpiresAt(DateTimeOffset absoluteExpiresAt) =>
        new DateTimeOffset(absoluteExpiresAt.UtcDateTime.Date, TimeSpan.Zero).AddDays(1);

    /// <summary>
    /// Parses a member of <see cref="ClientsIndex"/> into its client.
    /// </summary>
    /// <returns><see langword="true"/> when the member is well-formed; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseClientMember(string member, out BffSessionClient client)
    {
        var parts = member.Split('|');
        if (parts.Length != 4)
        {
            client = default;
            return false;
        }

        client = new BffSessionClient(
            parts[1],
            Uri.UnescapeDataString(parts[2]),
            Uri.UnescapeDataString(parts[3])
        );
        return true;
    }

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
