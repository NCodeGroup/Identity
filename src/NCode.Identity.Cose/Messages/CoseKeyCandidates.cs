// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Keys;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose.Messages;

/// <summary>
/// Selects the keys that may be tried for a layer of a message that is read, from the keys the caller trusts.
/// </summary>
// See ADR-1409.
internal static class CoseKeyCandidates
{
    /// <summary>
    /// Selects and orders the keys to try. A key is a candidate when it is not expired (allowing for clock skew), is
    /// intended for the operation, is not restricted to another algorithm, and suits the algorithm. When the layer names
    /// a key identifier, the keys whose identifier names the same bytes (see <see cref="CoseKeyIdentifiers"/>)
    /// come first, then the keys with none; a key with another identifier is never tried.
    /// </summary>
    public static List<SecretKey> Select(
        IEnumerable<SecretKey> keys,
        ReadOnlyMemory<byte>? keyId,
        string algorithmCode,
        Func<SecretKey, bool> isCompatible,
        string use,
        DateTimeOffset now,
        TimeSpan clockSkew
    )
    {
        // Saturated, so a clock skew near TimeSpan.MaxValue cannot overflow.
        var expiryCutoff =
            now - DateTimeOffset.MinValue <= clockSkew ? DateTimeOffset.MinValue : now - clockSkew;
        var matching = new List<SecretKey>();
        var unidentified = new List<SecretKey>();
        foreach (var key in keys)
        {
            if (
                key is null
                || key.Metadata.IsExpired(expiryCutoff)
                || (key.Metadata.Use is not null && key.Metadata.Use != use)
                || (key.Metadata.Algorithm is not null && key.Metadata.Algorithm != algorithmCode)
                || !isCompatible(key)
            )
                continue;

            if (string.IsNullOrEmpty(key.KeyId))
            {
                unidentified.Add(key);
                continue;
            }

            if (
                keyId is not { } wanted
                || CoseKeyIdentifiers.GetBytes(key.KeyId).AsSpan().SequenceEqual(wanted.Span)
            )
                matching.Add(key);
        }

        matching.AddRange(unidentified);
        return matching;
    }
}
