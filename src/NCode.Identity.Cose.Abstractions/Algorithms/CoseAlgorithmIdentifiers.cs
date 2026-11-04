// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Algorithms;

/// <summary>
/// Contains the IANA COSE Algorithms registry values of the algorithms this library describes. The values are the known
/// ones, never all of them: an application registers any other algorithm with a <see cref="CoseAlgorithm"/>.
/// </summary>
[PublicAPI]
public static class CoseAlgorithmIdentifiers
{
    /// <summary>
    /// The identifier of ECDSA with SHA-256 over P-256 (RFC 9053 §2.1).
    /// </summary>
    public const int Es256 = -7;

    /// <summary>
    /// The identifier of ECDSA with SHA-384 over P-384 (RFC 9053 §2.1).
    /// </summary>
    public const int Es384 = -35;

    /// <summary>
    /// The identifier of ECDSA with SHA-512 over P-521 (RFC 9053 §2.1).
    /// </summary>
    public const int Es512 = -36;

    /// <summary>
    /// The identifier of ECDSA with SHA-256 over P-256, fully specified (RFC 9864).
    /// </summary>
    public const int Esp256 = -9;

    /// <summary>
    /// The identifier of ECDSA with SHA-384 over P-384, fully specified (RFC 9864).
    /// </summary>
    public const int Esp384 = -51;

    /// <summary>
    /// The identifier of ECDSA with SHA-512 over P-521, fully specified (RFC 9864).
    /// </summary>
    public const int Esp512 = -52;

    /// <summary>
    /// The identifier of ECDSA with SHA-256 over secp256k1 (RFC 8812).
    /// </summary>
    public const int Es256K = -47;

    /// <summary>
    /// The identifier of EdDSA over Ed25519 or Ed448 (RFC 9053 §2.2).
    /// </summary>
    public const int EdDsa = -8;

    /// <summary>
    /// The identifier of EdDSA over Ed25519, fully specified (RFC 9864).
    /// </summary>
    public const int Ed25519 = -19;

    /// <summary>
    /// The identifier of EdDSA over Ed448, fully specified (RFC 9864).
    /// </summary>
    public const int Ed448 = -53;

    /// <summary>
    /// The identifier of RSASSA-PSS with SHA-256 (RFC 8230).
    /// </summary>
    public const int Ps256 = -37;

    /// <summary>
    /// The identifier of RSASSA-PSS with SHA-384 (RFC 8230).
    /// </summary>
    public const int Ps384 = -38;

    /// <summary>
    /// The identifier of RSASSA-PSS with SHA-512 (RFC 8230).
    /// </summary>
    public const int Ps512 = -39;

    /// <summary>
    /// The identifier of ML-DSA-44 (RFC 9964).
    /// </summary>
    public const int MlDsa44 = -48;

    /// <summary>
    /// The identifier of ML-DSA-65 (RFC 9964).
    /// </summary>
    public const int MlDsa65 = -49;

    /// <summary>
    /// The identifier of ML-DSA-87 (RFC 9964).
    /// </summary>
    public const int MlDsa87 = -50;

    /// <summary>
    /// The identifier of RSASSA-PKCS1-v1_5 with SHA-256 (RFC 8812), offered for WebAuthn interoperability and not registered by default.
    /// </summary>
    public const int Rs256 = -257;

    /// <summary>
    /// The identifier of RSASSA-PKCS1-v1_5 with SHA-384 (RFC 8812), offered for WebAuthn interoperability and not registered by default.
    /// </summary>
    public const int Rs384 = -258;

    /// <summary>
    /// The identifier of RSASSA-PKCS1-v1_5 with SHA-512 (RFC 8812), offered for WebAuthn interoperability and not registered by default.
    /// </summary>
    public const int Rs512 = -259;

    /// <summary>
    /// The identifier of HMAC with SHA-256 truncated to 64 bits (RFC 9053 §3.1), not registered by default because of its short tag.
    /// </summary>
    public const int HmacSha256Truncated64 = 4;

    /// <summary>
    /// The identifier of HMAC with SHA-256 and a 256-bit tag (RFC 9053 §3.1).
    /// </summary>
    public const int HmacSha256 = 5;

    /// <summary>
    /// The identifier of HMAC with SHA-384 and a 384-bit tag (RFC 9053 §3.1).
    /// </summary>
    public const int HmacSha384 = 6;

    /// <summary>
    /// The identifier of HMAC with SHA-512 and a 512-bit tag (RFC 9053 §3.1).
    /// </summary>
    public const int HmacSha512 = 7;

    /// <summary>
    /// The identifier of AES-GCM with a 128-bit key (RFC 9053 §4.1).
    /// </summary>
    public const int A128Gcm = 1;

    /// <summary>
    /// The identifier of AES-GCM with a 192-bit key (RFC 9053 §4.1).
    /// </summary>
    public const int A192Gcm = 2;

    /// <summary>
    /// The identifier of AES-GCM with a 256-bit key (RFC 9053 §4.1).
    /// </summary>
    public const int A256Gcm = 3;

    /// <summary>
    /// The identifier of Direct use of the recipient key as the content key (RFC 9053 §6.1.1).
    /// </summary>
    public const int Direct = -6;

    /// <summary>
    /// The identifier of AES key wrap with a 128-bit key (RFC 9053 §6.2.1).
    /// </summary>
    public const int A128Kw = -3;

    /// <summary>
    /// The identifier of AES key wrap with a 192-bit key (RFC 9053 §6.2.1).
    /// </summary>
    public const int A192Kw = -4;

    /// <summary>
    /// The identifier of AES key wrap with a 256-bit key (RFC 9053 §6.2.1).
    /// </summary>
    public const int A256Kw = -5;

    /// <summary>
    /// The identifier of RSAES-OAEP with SHA-1 (RFC 8230), not registered by default because it uses SHA-1.
    /// </summary>
    public const int RsaesOaepSha1 = -40;

    /// <summary>
    /// The identifier of RSAES-OAEP with SHA-256 (RFC 8230).
    /// </summary>
    public const int RsaesOaepSha256 = -41;

    /// <summary>
    /// The identifier of RSAES-OAEP with SHA-512 (RFC 8230).
    /// </summary>
    public const int RsaesOaepSha512 = -42;
}
