// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jwt.Validation;

namespace NCode.Identity.Jose.SdJwt.Verification;

/// <summary>
/// The known codes of a Selective Disclosure JWT (SD-JWT) verification failure, carried on
/// <see cref="SdJwtVerificationResult.FailureCode"/> and <see cref="SdJwtException.FailureCode"/>. A failure of the
/// issuer-signed JWT itself carries the <see cref="JwtValidationFailureCodes"/> code of that validation.
/// </summary>
/// <remarks>
/// The codes are an open vocabulary: a confirmation-key resolver or a profile that refuses a presentation for its own
/// cause names a code of its own. Codes are compared ordinally.
/// </remarks>
[PublicAPI]
public static class SdJwtVerificationFailureCodes
{
    /// <summary>
    /// The SD-JWT is not in the combined format <c>&lt;Issuer-signed JWT&gt;~&lt;Disclosure 1&gt;~...~[&lt;KB-JWT&gt;]</c>,
    /// or the issuer-signed JWT payload is not a JSON object.
    /// </summary>
    public const string InvalidSdJwt = "invalid_sd_jwt";

    /// <summary>
    /// A disclosure is not the base64url encoding of a JSON array of a salt, an optional claim name and a value, or
    /// its claim name is reserved.
    /// </summary>
    public const string InvalidDisclosure = "invalid_disclosure";

    /// <summary>
    /// The SD-JWT carries more disclosures than the configured limit.
    /// </summary>
    public const string DisclosureLimitExceeded = "disclosure_limit_exceeded";

    /// <summary>
    /// The claims nest deeper than the configured limit once the disclosures are resolved.
    /// </summary>
    public const string NestingLimitExceeded = "nesting_limit_exceeded";

    /// <summary>
    /// The <c>_sd_alg</c> claim names a hash algorithm that is not registered.
    /// </summary>
    public const string UnsupportedDigestAlgorithm = "unsupported_digest_algorithm";

    /// <summary>
    /// Two presented disclosures have the same digest.
    /// </summary>
    public const string DuplicateDisclosure = "duplicate_disclosure";

    /// <summary>
    /// A digest appears more than once in the issuer-signed JWT payload or the disclosures it references.
    /// </summary>
    public const string DuplicateDigest = "duplicate_digest";

    /// <summary>
    /// A presented disclosure is referenced by no digest of the issuer-signed JWT payload or the disclosures it
    /// references.
    /// </summary>
    public const string UnreferencedDisclosure = "unreferenced_disclosure";

    /// <summary>
    /// A disclosed claim name is already present in the object that discloses it.
    /// </summary>
    public const string DuplicateClaim = "duplicate_claim";

    /// <summary>
    /// A reserved claim name (<c>_sd</c>, <c>...</c> or <c>_sd_alg</c>) is used where RFC 9901 forbids it.
    /// </summary>
    public const string ReservedClaimName = "reserved_claim_name";

    /// <summary>
    /// An <c>_sd</c> claim is not an array of strings, or an array element placeholder is not an object whose one member
    /// <c>...</c> is a string.
    /// </summary>
    public const string InvalidDigestStructure = "invalid_digest_structure";

    /// <summary>
    /// An object digest references an array element disclosure, or an array element digest references an object
    /// property disclosure.
    /// </summary>
    public const string DisclosureMismatch = "disclosure_mismatch";

    /// <summary>
    /// A digest discloses a protected top-level claim or a part of one: <c>iss</c>, <c>nbf</c>, <c>exp</c> and
    /// <c>cnf</c> under every profile, and the claims the profile keeps in the issuer-signed JWT payload.
    /// </summary>
    public const string ProtectedClaimDisclosed = "protected_claim_disclosed";

    /// <summary>
    /// A claim the profile requires is missing from the issuer-signed JWT payload or is not a non-empty string.
    /// </summary>
    public const string MissingRequiredClaim = "missing_required_claim";

    /// <summary>
    /// Key binding is required and the presentation carries no Key Binding JWT.
    /// </summary>
    public const string KeyBindingRequired = "key_binding_required";

    /// <summary>
    /// Key binding is required and the issuer-signed JWT payload carries no <c>cnf</c> claim that a registered
    /// confirmation-key resolver can turn into a key.
    /// </summary>
    public const string HolderKeyNotFound = "holder_key_not_found";

    /// <summary>
    /// The Key Binding JWT failed validation: its structure, signature, <c>typ</c>, <c>aud</c> or algorithm.
    /// </summary>
    public const string InvalidKeyBinding = "invalid_key_binding";

    /// <summary>
    /// The Key Binding JWT's <c>nonce</c> claim is missing or is not the expected nonce.
    /// </summary>
    public const string InvalidNonce = "invalid_nonce";

    /// <summary>
    /// The Key Binding JWT's <c>sd_hash</c> claim is missing or is not the digest of the presentation it accompanies.
    /// </summary>
    public const string InvalidSdHash = "invalid_sd_hash";

    /// <summary>
    /// The Key Binding JWT's <c>iat</c> claim is missing, malformed, in the future or older than the maximum age.
    /// </summary>
    public const string KeyBindingNotFresh = "key_binding_not_fresh";
}
