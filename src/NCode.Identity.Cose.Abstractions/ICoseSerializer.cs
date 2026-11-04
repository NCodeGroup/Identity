// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Reads and writes CBOR Object Signing and Encryption (COSE) messages (RFC 9052). Reading is strict and bounded, and
/// writing is deterministic.
/// </summary>
// See ADR-1409.
[PublicAPI]
public interface ICoseSerializer
{
    /// <summary>
    /// Reads the type and the headers of a message without verifying it. The result carries no content.
    /// </summary>
    /// <param name="message">The encoded message.</param>
    /// <param name="untaggedMessageType">The type of the message when it carries no tag, or <c>null</c> to refuse an
    /// untagged message.</param>
    /// <returns>The type and headers of the message.</returns>
    /// <exception cref="CoseException">The message is malformed or exceeds a limit.</exception>
    CoseMessageInfo Inspect(ReadOnlyMemory<byte> message, CoseMessageType? untaggedMessageType);

    /// <summary>
    /// Signs content as a <c>COSE_Sign1</c> message.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="credentials">The signing key and its signature algorithm.</param>
    /// <param name="options">The header parameters and the encoding choices.</param>
    /// <returns>The encoded message.</returns>
    /// <exception cref="ArgumentException">The algorithm is not a registered signature algorithm, the key does not suit
    /// it, or a header parameter is owned by the library or invalid.</exception>
    byte[] EncodeSign1(
        ReadOnlySpan<byte> content,
        CoseCredentials credentials,
        CoseEncodingOptions options
    );

    /// <summary>
    /// Signs content as a <c>COSE_Sign</c> message with one signature per signer.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="signers">The signers; at least one.</param>
    /// <param name="options">The header parameters and the encoding choices.</param>
    /// <returns>The encoded message.</returns>
    /// <exception cref="ArgumentException">There is no signer or too many, an algorithm is not a registered signature
    /// algorithm, a key does not suit its algorithm, or a header parameter is owned by the library or invalid.</exception>
    byte[] EncodeSign(
        ReadOnlySpan<byte> content,
        IReadOnlyList<CoseSigner> signers,
        CoseEncodingOptions options
    );

    /// <summary>
    /// Verifies a signed or authenticated message and returns its content. The algorithm must be one the caller permits
    /// and must be protected, and the signature or authentication code must verify with one of the caller's keys.
    /// </summary>
    /// <param name="message">The encoded message.</param>
    /// <param name="parameters">What the verifier accepts.</param>
    /// <returns>The verified message.</returns>
    /// <exception cref="ArgumentException">A parameter is invalid.</exception>
    /// <exception cref="CoseException">The message is malformed, exceeds a limit, or does not verify.</exception>
    CoseVerifiedMessage Verify(ReadOnlyMemory<byte> message, CoseVerificationParameters parameters);

    /// <summary>
    /// Authenticates content as a <c>COSE_Mac0</c> message with a key the recipient already knows.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="credentials">The symmetric key and its message authentication code algorithm.</param>
    /// <param name="options">The header parameters and the encoding choices.</param>
    /// <returns>The encoded message.</returns>
    /// <exception cref="ArgumentException">The algorithm is not a registered message authentication code algorithm,
    /// the key does not suit it, or a header parameter is owned by the library or invalid.</exception>
    byte[] EncodeMac0(
        ReadOnlySpan<byte> content,
        CoseCredentials credentials,
        CoseEncodingOptions options
    );

    /// <summary>
    /// Authenticates content as a <c>COSE_Mac</c> message whose key reaches the recipients directly or wrapped.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="recipients">The message authentication code algorithm and the recipients.</param>
    /// <param name="options">The header parameters and the encoding choices.</param>
    /// <returns>The encoded message.</returns>
    /// <exception cref="ArgumentException">An algorithm is not registered for its use, a key does not suit its
    /// algorithm, a direct recipient is not alone, or a header parameter is owned by the library or invalid.</exception>
    byte[] EncodeMac(
        ReadOnlySpan<byte> content,
        CoseRecipients recipients,
        CoseEncodingOptions options
    );

    /// <summary>
    /// Encrypts content as a <c>COSE_Encrypt0</c> message with a key the recipient already knows.
    /// </summary>
    /// <param name="content">The plaintext.</param>
    /// <param name="credentials">The symmetric key and its content encryption algorithm.</param>
    /// <param name="options">The header parameters and the encoding choices.</param>
    /// <returns>The encoded message.</returns>
    /// <exception cref="ArgumentException">The algorithm is not a registered content encryption algorithm, the key is
    /// not exactly the size the algorithm needs, or a header parameter is owned by the library or invalid.</exception>
    byte[] EncodeEncrypt0(
        ReadOnlySpan<byte> content,
        CoseCredentials credentials,
        CoseEncodingOptions options
    );

    /// <summary>
    /// Encrypts content as a <c>COSE_Encrypt</c> message whose content key reaches the recipients directly or wrapped.
    /// </summary>
    /// <param name="content">The plaintext.</param>
    /// <param name="recipients">The content encryption algorithm and the recipients.</param>
    /// <param name="options">The header parameters and the encoding choices.</param>
    /// <returns>The encoded message.</returns>
    /// <exception cref="ArgumentException">An algorithm is not registered for its use, a key does not suit its
    /// algorithm, a direct recipient is not alone, or a header parameter is owned by the library or invalid.</exception>
    byte[] EncodeEncrypt(
        ReadOnlySpan<byte> content,
        CoseRecipients recipients,
        CoseEncodingOptions options
    );

    /// <summary>
    /// Decrypts an encrypted message and returns its plaintext. The algorithm must be one the caller permits and must
    /// be protected, and the authentication tag must verify with one of the caller's keys; the plaintext is returned
    /// only when it does.
    /// </summary>
    /// <param name="message">The encoded message.</param>
    /// <param name="parameters">What the decrypting party accepts.</param>
    /// <returns>The decrypted message.</returns>
    /// <exception cref="ArgumentException">A parameter is invalid.</exception>
    /// <exception cref="CoseException">The message is malformed, exceeds a limit, or does not decrypt.</exception>
    CoseVerifiedMessage Decrypt(ReadOnlyMemory<byte> message, CoseDecryptionParameters parameters);

    /// <summary>
    /// Decodes a CBOR map with integer and text labels, such as a CBOR Web Token claims set, under the size, depth and
    /// strictness rules of the library.
    /// </summary>
    /// <param name="encodedMap">The encoded map.</param>
    /// <returns>The entries of the map.</returns>
    /// <exception cref="CoseException">The map is malformed or exceeds a limit.</exception>
    IReadOnlyDictionary<CoseLabel, CoseValue> DecodeMap(ReadOnlyMemory<byte> encodedMap);

    /// <summary>
    /// Encodes a CBOR map with its entries in deterministic order (RFC 8949 §4.2.1).
    /// </summary>
    /// <param name="map">The entries.</param>
    /// <returns>The encoded map.</returns>
    /// <exception cref="ArgumentException">A value is not a definite-length item within the nesting limit.</exception>
    byte[] EncodeMap(IReadOnlyDictionary<CoseLabel, CoseValue> map);
}
