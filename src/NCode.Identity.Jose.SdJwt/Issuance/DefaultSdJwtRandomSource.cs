// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using NCode.Encoders;
using NCode.Identity.Jose.SdJwt.Options;

namespace NCode.Identity.Jose.SdJwt.Issuance;

/// <summary>
/// Provides the default implementation of <see cref="ISdJwtRandomSource"/> over
/// <see cref="RandomNumberGenerator"/>, the platform's cryptographically secure random number generator.
/// </summary>
/// <param name="optionsAccessor">The accessor of the options that size a salt.</param>
internal sealed class DefaultSdJwtRandomSource(IOptions<SdJwtOptions> optionsAccessor)
    : ISdJwtRandomSource
{
    private const int StackAllocLimit = 256;

    private int SaltSizeBytes { get; } = optionsAccessor.Value.SaltSizeBytes;

    /// <inheritdoc />
    public string NextSalt()
    {
        Span<byte> salt =
            SaltSizeBytes <= StackAllocLimit
                ? stackalloc byte[SaltSizeBytes]
                : new byte[SaltSizeBytes];
        RandomNumberGenerator.Fill(salt);
        return Base64Url.Encode(salt);
    }

    /// <inheritdoc />
    public int NextIndex(int maxExclusive) => RandomNumberGenerator.GetInt32(maxExclusive);
}
