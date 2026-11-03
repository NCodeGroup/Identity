// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Verifiers;

/// <summary>
/// Contains the names of the schemes a <see cref="SecretVerifier"/> is made with. A stored verifier names its scheme
/// first, as <c>$&lt;scheme&gt;$…</c>, so a verifier made with any scheme keeps verifying when the scheme chosen for
/// new secrets changes.
/// </summary>
// See ADR-1391.
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class SecretVerifierSchemes
{
    /// <summary>
    /// Indicates a SHA-256 digest of the secret, <c>$ncode-sha256$&lt;digest&gt;</c> with the digest base64url-encoded.
    /// It verifies a secret the server generated with at least 256 bits of entropy, which no search can guess.
    /// </summary>
    public const string Sha256 = "ncode-sha256";

    /// <summary>
    /// Indicates a PBKDF2-HMAC-SHA256 derivation of the secret,
    /// <c>$ncode-pbkdf2-sha256$&lt;iterations&gt;$&lt;salt&gt;$&lt;derived key&gt;</c> with the salt and the derived key
    /// base64url-encoded: the format of the family's password hashes. It verifies a secret someone chose, whose entropy
    /// is unknown.
    /// </summary>
    public const string Pbkdf2Sha256 = "ncode-pbkdf2-sha256";
}
