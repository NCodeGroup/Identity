// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.SdJwt.Presentation;

/// <summary>
/// The holder's side of Selective Disclosure JWTs (RFC 9901 §6): reads an SD-JWT, resolves where each of its
/// disclosures lands, and builds a presentation that reveals a chosen subset, bound to the holder's key when the
/// verifier asks. The holder trusts the issuer-signed JWT it received; a holder that checks it on receipt verifies it
/// as a verifier does.
/// </summary>
[PublicAPI]
public interface ISdJwtHolder
{
    /// <summary>
    /// Reads an SD-JWT in the combined format, under the configured disclosure limit.
    /// </summary>
    /// <param name="serialized">The combined format, as issued or as presented.</param>
    /// <returns>The parsed SD-JWT.</returns>
    /// <exception cref="SdJwtException">The value is not in the combined format, carries more disclosures than the
    /// limit, or carries a disclosure that is not of the expected shape.</exception>
    SelectiveDisclosureJwt Parse(string serialized);

    /// <summary>
    /// Resolves each disclosure of an SD-JWT to the claim it discloses and to the disclosure that carries its digest,
    /// so the holder can choose what to reveal by claim.
    /// </summary>
    /// <param name="sdJwt">The SD-JWT.</param>
    /// <returns>One entry per disclosure.</returns>
    /// <exception cref="SdJwtException">The issuer-signed JWT or its disclosures do not satisfy the rules of
    /// RFC 9901 §7.1, or the issuer-signed JWT is not a compact JWS.</exception>
    IReadOnlyList<DisclosedClaim> ResolveDisclosures(SelectiveDisclosureJwt sdJwt);

    /// <summary>
    /// Builds a presentation that reveals the chosen disclosures, and every disclosure that carries one of their
    /// digests, with a Key Binding JWT when the options ask for one.
    /// </summary>
    /// <param name="options">The SD-JWT, the disclosures to reveal and the key binding inputs.</param>
    /// <returns>The presentation in the combined format.</returns>
    /// <exception cref="ArgumentException">A disclosure is not one of the SD-JWT's, the key binding key is not
    /// asymmetric, or its audience or nonce is empty.</exception>
    /// <exception cref="SdJwtException">The issuer-signed JWT or its disclosures do not satisfy the rules of
    /// RFC 9901 §7.1.</exception>
    string CreatePresentation(SdJwtPresentationOptions options);
}
