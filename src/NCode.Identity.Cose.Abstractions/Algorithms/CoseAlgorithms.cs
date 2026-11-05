// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Algorithms;

namespace NCode.Identity.Cose.Algorithms;

/// <summary>
/// Describes the COSE algorithms this library knows, each bound to the JOSE algorithm that implements it. The
/// instances are data: <c>AddCoseLibrary</c> registers the default set, and an application registers the others, or
/// its own, with <c>AddCoseAlgorithm</c>.
/// </summary>
// See ADR-1409.
[PublicAPI]
public static class CoseAlgorithms
{
    /// <summary>
    /// Gets the descriptor of ECDSA with SHA-256 over P-256 (RFC 9053 §2.1).
    /// </summary>
    public static CoseAlgorithm Es256 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Es256,
            Name = "ES256",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.EcdsaSha256,
        };

    /// <summary>
    /// Gets the descriptor of ECDSA with SHA-384 over P-384 (RFC 9053 §2.1).
    /// </summary>
    public static CoseAlgorithm Es384 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Es384,
            Name = "ES384",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.EcdsaSha384,
        };

    /// <summary>
    /// Gets the descriptor of ECDSA with SHA-512 over P-521 (RFC 9053 §2.1).
    /// </summary>
    public static CoseAlgorithm Es512 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Es512,
            Name = "ES512",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.EcdsaSha512,
        };

    /// <summary>
    /// Gets the descriptor of ECDSA with SHA-256 over P-256, fully specified (RFC 9864).
    /// </summary>
    public static CoseAlgorithm Esp256 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Esp256,
            Name = "ESP256",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.EcdsaSha256,
        };

    /// <summary>
    /// Gets the descriptor of ECDSA with SHA-384 over P-384, fully specified (RFC 9864).
    /// </summary>
    public static CoseAlgorithm Esp384 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Esp384,
            Name = "ESP384",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.EcdsaSha384,
        };

    /// <summary>
    /// Gets the descriptor of ECDSA with SHA-512 over P-521, fully specified (RFC 9864).
    /// </summary>
    public static CoseAlgorithm Esp512 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Esp512,
            Name = "ESP512",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.EcdsaSha512,
        };

    /// <summary>
    /// Gets the descriptor of ECDSA with SHA-256 over secp256k1 (RFC 8812).
    /// </summary>
    public static CoseAlgorithm Es256K { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Es256K,
            Name = "ES256K",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.EcdsaSecp256k1Sha256,
        };

    /// <summary>
    /// Gets the descriptor of EdDSA over Ed25519 or Ed448 (RFC 9053 §2.2).
    /// </summary>
    public static CoseAlgorithm EdDsa { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.EdDsa,
            Name = "EdDSA",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.EdDsa,
        };

    /// <summary>
    /// Gets the descriptor of EdDSA over Ed25519, fully specified (RFC 9864).
    /// </summary>
    public static CoseAlgorithm Ed25519 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Ed25519,
            Name = "Ed25519",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.Ed25519,
        };

    /// <summary>
    /// Gets the descriptor of EdDSA over Ed448, fully specified (RFC 9864).
    /// </summary>
    public static CoseAlgorithm Ed448 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Ed448,
            Name = "Ed448",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.Ed448,
        };

    /// <summary>
    /// Gets the descriptor of RSASSA-PSS with SHA-256 (RFC 8230).
    /// </summary>
    public static CoseAlgorithm Ps256 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Ps256,
            Name = "PS256",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
        };

    /// <summary>
    /// Gets the descriptor of RSASSA-PSS with SHA-384 (RFC 8230).
    /// </summary>
    public static CoseAlgorithm Ps384 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Ps384,
            Name = "PS384",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.RsaSsaPssSha384,
        };

    /// <summary>
    /// Gets the descriptor of RSASSA-PSS with SHA-512 (RFC 8230).
    /// </summary>
    public static CoseAlgorithm Ps512 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Ps512,
            Name = "PS512",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.RsaSsaPssSha512,
        };

    /// <summary>
    /// Gets the descriptor of ML-DSA-44 (RFC 9964).
    /// </summary>
    public static CoseAlgorithm MlDsa44 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.MlDsa44,
            Name = "ML-DSA-44",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.MlDsa44,
        };

    /// <summary>
    /// Gets the descriptor of ML-DSA-65 (RFC 9964).
    /// </summary>
    public static CoseAlgorithm MlDsa65 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.MlDsa65,
            Name = "ML-DSA-65",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.MlDsa65,
        };

    /// <summary>
    /// Gets the descriptor of ML-DSA-87 (RFC 9964).
    /// </summary>
    public static CoseAlgorithm MlDsa87 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.MlDsa87,
            Name = "ML-DSA-87",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.MlDsa87,
        };

    /// <summary>
    /// Gets the descriptor of RSASSA-PKCS1-v1_5 with SHA-256 (RFC 8812), offered for WebAuthn interoperability and not registered by default.
    /// </summary>
    public static CoseAlgorithm Rs256 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Rs256,
            Name = "RS256",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.RsaSha256,
        };

    /// <summary>
    /// Gets the descriptor of RSASSA-PKCS1-v1_5 with SHA-384 (RFC 8812), offered for WebAuthn interoperability and not registered by default.
    /// </summary>
    public static CoseAlgorithm Rs384 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Rs384,
            Name = "RS384",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.RsaSha384,
        };

    /// <summary>
    /// Gets the descriptor of RSASSA-PKCS1-v1_5 with SHA-512 (RFC 8812), offered for WebAuthn interoperability and not registered by default.
    /// </summary>
    public static CoseAlgorithm Rs512 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Rs512,
            Name = "RS512",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.RsaSha512,
        };

    /// <summary>
    /// Gets the descriptor of HMAC with SHA-256 truncated to 64 bits (RFC 9053 §3.1), not registered by default because of its short tag.
    /// </summary>
    public static CoseAlgorithm HmacSha256Truncated64 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.HmacSha256Truncated64,
            Name = "HMAC 256/64",
            Kind = CoseAlgorithmKind.Mac,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.HmacSha256,
            TagSizeBytes = 8,
        };

    /// <summary>
    /// Gets the descriptor of HMAC with SHA-256 and a 256-bit tag (RFC 9053 §3.1).
    /// </summary>
    public static CoseAlgorithm HmacSha256 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.HmacSha256,
            Name = "HMAC 256/256",
            Kind = CoseAlgorithmKind.Mac,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.HmacSha256,
        };

    /// <summary>
    /// Gets the descriptor of HMAC with SHA-384 and a 384-bit tag (RFC 9053 §3.1).
    /// </summary>
    public static CoseAlgorithm HmacSha384 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.HmacSha384,
            Name = "HMAC 384/384",
            Kind = CoseAlgorithmKind.Mac,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.HmacSha384,
        };

    /// <summary>
    /// Gets the descriptor of HMAC with SHA-512 and a 512-bit tag (RFC 9053 §3.1).
    /// </summary>
    public static CoseAlgorithm HmacSha512 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.HmacSha512,
            Name = "HMAC 512/512",
            Kind = CoseAlgorithmKind.Mac,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.HmacSha512,
        };

    /// <summary>
    /// Gets the descriptor of AES-GCM with a 128-bit key (RFC 9053 §4.1).
    /// </summary>
    public static CoseAlgorithm A128Gcm { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.A128Gcm,
            Name = "A128GCM",
            Kind = CoseAlgorithmKind.ContentEncryption,
            JoseAlgorithmCode = AlgorithmCodes.AuthenticatedEncryption.Aes128Gcm,
        };

    /// <summary>
    /// Gets the descriptor of AES-GCM with a 192-bit key (RFC 9053 §4.1).
    /// </summary>
    public static CoseAlgorithm A192Gcm { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.A192Gcm,
            Name = "A192GCM",
            Kind = CoseAlgorithmKind.ContentEncryption,
            JoseAlgorithmCode = AlgorithmCodes.AuthenticatedEncryption.Aes192Gcm,
        };

    /// <summary>
    /// Gets the descriptor of AES-GCM with a 256-bit key (RFC 9053 §4.1).
    /// </summary>
    public static CoseAlgorithm A256Gcm { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.A256Gcm,
            Name = "A256GCM",
            Kind = CoseAlgorithmKind.ContentEncryption,
            JoseAlgorithmCode = AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm,
        };

    /// <summary>
    /// Gets the descriptor of Direct use of the recipient key as the content key (RFC 9053 §6.1.1).
    /// </summary>
    public static CoseAlgorithm Direct { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.Direct,
            Name = "direct",
            Kind = CoseAlgorithmKind.KeyDistribution,
            JoseAlgorithmCode = AlgorithmCodes.KeyManagement.Direct,
        };

    /// <summary>
    /// Gets the descriptor of AES key wrap with a 128-bit key (RFC 9053 §6.2.1).
    /// </summary>
    public static CoseAlgorithm A128Kw { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.A128Kw,
            Name = "A128KW",
            Kind = CoseAlgorithmKind.KeyDistribution,
            JoseAlgorithmCode = AlgorithmCodes.KeyManagement.Aes128,
        };

    /// <summary>
    /// Gets the descriptor of AES key wrap with a 192-bit key (RFC 9053 §6.2.1).
    /// </summary>
    public static CoseAlgorithm A192Kw { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.A192Kw,
            Name = "A192KW",
            Kind = CoseAlgorithmKind.KeyDistribution,
            JoseAlgorithmCode = AlgorithmCodes.KeyManagement.Aes192,
        };

    /// <summary>
    /// Gets the descriptor of AES key wrap with a 256-bit key (RFC 9053 §6.2.1).
    /// </summary>
    public static CoseAlgorithm A256Kw { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.A256Kw,
            Name = "A256KW",
            Kind = CoseAlgorithmKind.KeyDistribution,
            JoseAlgorithmCode = AlgorithmCodes.KeyManagement.Aes256,
        };

    /// <summary>
    /// Gets the descriptor of RSAES-OAEP with SHA-1 (RFC 8230), not registered by default because it uses SHA-1.
    /// </summary>
    public static CoseAlgorithm RsaesOaepSha1 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.RsaesOaepSha1,
            Name = "RSAES-OAEP w/ RFC 8017 default parameters",
            Kind = CoseAlgorithmKind.KeyDistribution,
            JoseAlgorithmCode = AlgorithmCodes.KeyManagement.RsaOaep,
        };

    /// <summary>
    /// Gets the descriptor of RSAES-OAEP with SHA-256 (RFC 8230).
    /// </summary>
    public static CoseAlgorithm RsaesOaepSha256 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.RsaesOaepSha256,
            Name = "RSAES-OAEP w/ SHA-256",
            Kind = CoseAlgorithmKind.KeyDistribution,
            JoseAlgorithmCode = AlgorithmCodes.KeyManagement.RsaOaep256,
        };

    /// <summary>
    /// Gets the descriptor of RSAES-OAEP with SHA-512 (RFC 8230).
    /// </summary>
    public static CoseAlgorithm RsaesOaepSha512 { get; } =
        new()
        {
            Identifier = CoseAlgorithmIdentifiers.RsaesOaepSha512,
            Name = "RSAES-OAEP w/ SHA-512",
            Kind = CoseAlgorithmKind.KeyDistribution,
            JoseAlgorithmCode = AlgorithmCodes.KeyManagement.RsaOaep512,
        };
}
