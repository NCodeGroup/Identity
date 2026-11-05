// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Contains the values of the IANA COSE Key Types registry that this library knows. The values are the known ones, never all of them: a key type is registered as a <see cref="CoseKeyTypeDescriptor"/>.
/// </summary>
[PublicAPI]
public static class CoseKeyTypeIdentifiers
{
    /// <summary>
    /// The <c>OKP</c> key type, an octet key pair (RFC 9053 §7.1).
    /// </summary>
    public const int Okp = 1;

    /// <summary>
    /// The <c>EC2</c> key type, a double-coordinate elliptic curve key (RFC 9053 §7.1).
    /// </summary>
    public const int Ec2 = 2;

    /// <summary>
    /// The <c>RSA</c> key type (RFC 8230 §4).
    /// </summary>
    public const int Rsa = 3;

    /// <summary>
    /// The <c>Symmetric</c> key type (RFC 9053 §7.3).
    /// </summary>
    public const int Symmetric = 4;

    /// <summary>
    /// The <c>HSS-LMS</c> key type (RFC 8778).
    /// </summary>
    public const int HssLms = 5;

    /// <summary>
    /// The <c>WalnutDSA</c> key type (RFC 9021).
    /// </summary>
    public const int WalnutDsa = 6;

    /// <summary>
    /// The <c>AKP</c> algorithm key pair key type of the post-quantum signature algorithms.
    /// </summary>
    public const int Akp = 7;
}
