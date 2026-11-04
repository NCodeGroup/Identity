// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Jose.SdJwt.Digests;

/// <summary>
/// The hash algorithms that <c>AddSdJwt()</c> registers for the <c>_sd_alg</c> claim, named as the IANA Named
/// Information Hash Algorithm Registry names them (RFC 9901 §4.1.1). These are the known algorithms, not all of them:
/// an application registers another <see cref="SdJwtDigestAlgorithm"/> beside them.
/// </summary>
[PublicAPI]
public static class SdJwtDigestAlgorithms
{
    /// <summary>
    /// The name of the SHA-256 algorithm, <c>sha-256</c>, which is the default when an SD-JWT carries no
    /// <c>_sd_alg</c> claim.
    /// </summary>
    public const string Sha256Name = "sha-256";

    /// <summary>
    /// The name of the SHA-384 algorithm, <c>sha-384</c>.
    /// </summary>
    public const string Sha384Name = "sha-384";

    /// <summary>
    /// The name of the SHA-512 algorithm, <c>sha-512</c>.
    /// </summary>
    public const string Sha512Name = "sha-512";

    /// <summary>
    /// The name of the algorithm an SD-JWT without an <c>_sd_alg</c> claim uses: <see cref="Sha256Name"/>.
    /// </summary>
    public const string DefaultName = Sha256Name;

    /// <summary>
    /// Gets the SHA-256 algorithm, <c>sha-256</c>.
    /// </summary>
    public static SdJwtDigestAlgorithm Sha256 { get; } =
        new() { Name = Sha256Name, HashAlgorithmName = HashAlgorithmName.SHA256 };

    /// <summary>
    /// Gets the SHA-384 algorithm, <c>sha-384</c>.
    /// </summary>
    public static SdJwtDigestAlgorithm Sha384 { get; } =
        new() { Name = Sha384Name, HashAlgorithmName = HashAlgorithmName.SHA384 };

    /// <summary>
    /// Gets the SHA-512 algorithm, <c>sha-512</c>.
    /// </summary>
    public static SdJwtDigestAlgorithm Sha512 { get; } =
        new() { Name = Sha512Name, HashAlgorithmName = HashAlgorithmName.SHA512 };
}
