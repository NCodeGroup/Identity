// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides the name of the stable key in a key management service whose versions are one owner's successive keys for
/// one algorithm and key specification.
/// </summary>
// See ADR-1474 and ADR-1478.
internal static class RemoteKeyGroupName
{
    private const string Prefix = "ncid-";
    private const int HashBytes = 16;

    /// <summary>
    /// Creates the group name for a deployment, an owner and a row of the mapping vocabulary. It is <c>ncid-</c>
    /// followed by the lowercase hexadecimal of the first 16 bytes of the SHA-256 hash of the deployment, owner,
    /// provider, algorithm, key specification, key size and curve, which makes it 37 characters and a valid key name
    /// for every provider.
    /// </summary>
    /// <param name="deployment">The name of the deployment that creates the key.</param>
    /// <param name="ownerId">The identifier of the owner, such as a tenant, whose key to create.</param>
    /// <param name="algorithm">The row of the mapping vocabulary that selects the key's type, size and curve.</param>
    /// <returns>The group name.</returns>
    public static string Create(string deployment, string ownerId, RemoteKeyAlgorithm algorithm)
    {
        var text =
            $"{deployment}\n{ownerId}\n{algorithm.Provider}\n{algorithm.Algorithm}\n{algorithm.KeySpec}\n{algorithm.KeySizeBits}\n{algorithm.Curve}";

        using var lease = BufferFactory.Rent(
            SecureEncoding.UTF8.GetByteCount(text),
            isSensitive: false,
            out Span<byte> bytes
        );
        SecureEncoding.UTF8.GetBytes(text, bytes);

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(bytes, hash);

        return Prefix + Convert.ToHexStringLower(hash[..HashBytes]);
    }
}
