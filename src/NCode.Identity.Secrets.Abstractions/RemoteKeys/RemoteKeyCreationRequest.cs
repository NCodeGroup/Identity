// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Buffers;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes a key for a <see cref="IRemoteKeyProvider"/> to create.
/// </summary>
// See ADR-1471.
[PublicAPI]
public sealed class RemoteKeyCreationRequest
{
    private const string GroupNamePrefix = "ncid-";
    private const int GroupNameHashBytes = 16;

    /// <summary>
    /// Gets the identifier of the owner, such as a tenant, whose key to create.
    /// </summary>
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets the identifier of the secret that the key belongs to.
    /// </summary>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the row of the mapping vocabulary that selects the key's type, size and curve.
    /// </summary>
    public required RemoteSigningAlgorithm Algorithm { get; init; }

    /// <summary>
    /// Gets the name of the stable key in the key management service whose versions are one owner's successive keys
    /// for one algorithm and key specification. It is <c>ncid-</c> followed by the lowercase hexadecimal of the first
    /// 16 bytes of the SHA-256 hash of the owner, provider, algorithm, key specification, key size and curve, which
    /// makes it 37 characters and a valid key name for every provider.
    /// </summary>
    public string GroupName
    {
        get
        {
            var algorithm = Algorithm;
            var text =
                $"{OwnerId}\n{algorithm.Provider}\n{algorithm.Algorithm}\n{algorithm.KeySpec}\n{algorithm.KeySizeBits}\n{algorithm.Curve}";

            Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(SecureEncoding.UTF8.GetBytes(text), hash);

            return GroupNamePrefix + Convert.ToHexStringLower(hash[..GroupNameHashBytes]);
        }
    }
}
