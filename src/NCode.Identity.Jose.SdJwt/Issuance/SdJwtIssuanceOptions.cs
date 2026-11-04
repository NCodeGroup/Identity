// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Profiles;
using NCode.Identity.Jwk;

namespace NCode.Identity.Jose.SdJwt.Issuance;

/// <summary>
/// The inputs of issuing a Selective Disclosure JWT (RFC 9901 §5): the claims, which of them the holder may disclose
/// selectively, and the key the issuer signs with.
/// </summary>
[PublicAPI]
public sealed class SdJwtIssuanceOptions
{
    /// <summary>
    /// Gets the asymmetric key and signature algorithm that sign the issuer-signed JWT.
    /// </summary>
    public required JoseSigningCredentials SigningCredentials { get; init; }

    /// <summary>
    /// Gets the claims of the credential, a JSON object. No member at any depth is named <c>_sd</c>, <c>...</c> or
    /// <c>_sd_alg</c>, and no top-level member is named <c>cnf</c> when <see cref="HolderKey"/> is set.
    /// </summary>
    public required JsonElement Claims { get; init; }

    /// <summary>
    /// Gets the JSON Pointers (RFC 6901) of the claims that the holder may disclose selectively: an object member such
    /// as <c>/given_name</c> or <c>/address/street_address</c>, or an array element such as <c>/nationalities/1</c>.
    /// Every pointer names an existing member or element. A pointer within another disclosable claim makes the inner
    /// claim disclosable on its own, so its digest travels in the outer disclosure's value, as RFC 9901 §5.2.4
    /// describes. No pointer names <c>iss</c>, <c>nbf</c>, <c>exp</c> or <c>cnf</c>, a claim the
    /// <see cref="Profile"/> protects, or anything beneath one, because the verifier judges those claims on the
    /// issuer-signed payload before it resolves a disclosure. The default value is empty.
    /// </summary>
    public IReadOnlyCollection<string> DisclosablePaths { get; init; } = [];

    /// <summary>
    /// Gets the number of decoy digests (RFC 9901 §4.2.5) added to each <c>_sd</c> array and to each array that has
    /// a disclosable element, so a verifier cannot count the claims the holder withheld. The default value is zero.
    /// </summary>
    public int DecoyDigestCount { get; init; }

    /// <summary>
    /// Gets the name of the registered hash algorithm that computes the digests, written to the <c>_sd_alg</c> claim.
    /// The default value is <see cref="SdJwtDigestAlgorithms.DefaultName"/>.
    /// </summary>
    public string DigestAlgorithm { get; init; } = SdJwtDigestAlgorithms.DefaultName;

    /// <summary>
    /// Gets the public key of the holder, written to the <c>cnf</c> claim as a JSON Web Key so a presentation can be
    /// bound to it with a Key Binding JWT (RFC 9901 §4.3), or <c>null</c> for a credential without key binding. The
    /// key is of a registered asymmetric key type and carries no private member, by that type's own JWK rules.
    /// </summary>
    public JsonWebKey? HolderKey { get; init; }

    /// <summary>
    /// Gets the profile that types the issuer-signed JWT and names the claims it requires and the claims that are
    /// never disclosable, such as <see cref="SdJwtProfiles.SdJwtVc"/>, or <c>null</c> for a plain SD-JWT, whose
    /// <c>typ</c> is <c>JWT</c>.
    /// </summary>
    public SdJwtProfile? Profile { get; init; }
}
