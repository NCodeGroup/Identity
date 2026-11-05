// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Contains the machine-readable codes of <see cref="CoseException.FailureCode"/> that this library reports. The codes
/// are the known ones, never all of them. A code that names the same concept as one of the JSON Web Token validation
/// codes uses the same string.
/// </summary>
[PublicAPI]
public static class CoseFailureCodes
{
    /// <summary>
    /// The bytes are not a well-formed COSE message, or use a CBOR form this library refuses, such as an
    /// indefinite-length item or trailing bytes.
    /// </summary>
    public const string InvalidMessage = "invalid_message";

    /// <summary>
    /// The message exceeds a configured limit: its size, its nesting depth, its signatures or its recipients.
    /// </summary>
    public const string LimitExceeded = "limit_exceeded";

    /// <summary>
    /// The message carries no COSE tag and the caller did not name its type.
    /// </summary>
    public const string UntaggedMessage = "untagged_message";

    /// <summary>
    /// The message carries a tag that is not a COSE message tag, or a message type the operation does not take.
    /// </summary>
    public const string UnsupportedMessageType = "unsupported_message_type";

    /// <summary>
    /// A header parameter has a form that RFC 9052 does not allow.
    /// </summary>
    public const string InvalidHeader = "invalid_header";

    /// <summary>
    /// The algorithm of a layer is not in its protected header bucket.
    /// </summary>
    public const string UnprotectedAlgorithm = "unprotected_algorithm";

    /// <summary>
    /// The <c>crit</c> parameter lists a label that the verifier does not understand.
    /// </summary>
    public const string CriticalHeaderNotUnderstood = "critical_header_not_understood";

    /// <summary>
    /// The algorithm of the message is not one the caller permits.
    /// </summary>
    public const string AlgorithmNotPermitted = "algorithm_not_permitted";

    /// <summary>
    /// The algorithm of the message is permitted but not registered, or not of the kind the layer needs.
    /// </summary>
    public const string UnsupportedAlgorithm = "unsupported_algorithm";

    /// <summary>
    /// The content is detached and the caller supplied none, or the content is present and the caller supplied it too.
    /// </summary>
    public const string DetachedContent = "detached_content";

    /// <summary>
    /// No key the caller supplied can be tried for the message.
    /// </summary>
    public const string KeyNotFound = "key_not_found";

    /// <summary>
    /// No candidate key verifies the signature or the message authentication code.
    /// </summary>
    public const string InvalidSignature = "invalid_signature";

    /// <summary>
    /// No candidate key decrypts the message.
    /// </summary>
    public const string DecryptionFailed = "decryption_failed";

    /// <summary>
    /// A COSE_Key is malformed, carries private key material or cannot be imported.
    /// </summary>
    public const string InvalidKey = "invalid_key";

    /// <summary>
    /// The key type or the curve of a COSE_Key is not registered.
    /// </summary>
    public const string UnsupportedKeyType = "unsupported_key_type";
}
