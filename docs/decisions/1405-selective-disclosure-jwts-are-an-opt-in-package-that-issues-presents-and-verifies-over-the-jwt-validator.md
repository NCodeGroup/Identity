# 1405. Selective Disclosure JWTs are an opt-in package that issues, presents and verifies over the JWT validator

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** NCode Group

## Context

[RFC 9901](https://datatracker.ietf.org/doc/html/rfc9901), Selective Disclosure for JWTs (SD-JWT), lets an issuer
sign a JWT whose claims are replaced by salted digests, so the holder discloses a chosen subset to a verifier by
handing over the disclosures that match those digests, and binds the presentation to its own key with a Key Binding
JWT (KB-JWT). It is the credential format of the EUDI wallet and of SD-JWT-based Verifiable Credentials
(draft-ietf-oauth-sd-jwt-vc), which profile the format: the issuer-signed JWT is typed `dc+sd-jwt`, carries the
credential type in `vct`, and keeps `iss`, `nbf`, `exp`, `cnf`, `vct`, `vct#integrity` and `status` in its payload,
never in a disclosure. Other profiles exist and more will, so a profile must be data an application can define, not
a switch over a name.

The format is layered on JWS: the issuer-signed JWT and the KB-JWT are compact JWS, and the disclosures are
base64url-encoded JSON arrays whose digests are hashed over their encoding as issued. Four facts shape how the family
adds it:

- The JOSE library signs and verifies compact JWS tokens, and the JWT library validates a received JWT under one
  policy: the keys, the signature algorithms, the `typ`, the issuer and the lifetime
  ([ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md)).
  An SD-JWT verifier that verified either token itself would copy that policy.
- An implementation package references only `*.Abstractions` packages
  ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)),
  and an opt-in capability extends the marker of the library it consumes
  ([ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md)).
- Every vocabulary is open unless provably closed
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). The `_sd_alg` names come from the IANA Named
  Information Hash Algorithm Registry and the `cnf` methods (RFC 7800) from the IANA JWT Confirmation Methods
  registry; both grow without this library.
- A verifier reads disclosures from the holder, so the number of disclosures and the depth of the resolved claims
  bound the work a presentation can demand.

## Decision

1. **`NCode.Identity.Jose.SdJwt` is an opt-in package over the JWT library.** `AddSdJwt()` extends
   `IServiceBuilder<JwtLibrary>`, because the verifier validates the issuer-signed JWT and the KB-JWT through
   `IJwtValidator`, so a host cannot register it without the JWT library and the JOSE library beneath it. It
   references the JOSE, JWT and Secrets abstractions, `NCode.Identity.Json` and `NCode.Identity.Time`, and no
   implementation package.

2. **Three roles, three services.**
   - `ISdJwtIssuer.IssueAsync` takes the claims and the JSON Pointers (RFC 6901) of the claims the holder may disclose
     selectively, object members and array elements at any depth. It walks the claims depth first, so a disclosable
     claim inside another is replaced by its digest before the outer claim moves into its own disclosure, which is the
     recursive structure of RFC 9901 §5.2.4. Each `_sd` array is sorted ordinally so its order says nothing about the
     claims, and decoy digests are added where asked. The holder key is written to `cnf` as a public JSON Web Key,
     read under its registered key type so that type's own private members are refused as well as the standard ones,
     and the payload is signed with an asymmetric key. `iss`, `nbf`, `exp` and `cnf` are never made disclosable, in
     whole or in part, under any profile: the verifier judges them on the issuer-signed payload before it resolves a
     disclosure, so an SD-JWT that disclosed one would never verify.
   - `ISdJwtHolder` parses the combined format and resolves each disclosure to the claim it discloses and to the
     disclosure that carries its digest (`DisclosedClaim`). `CreatePresentationAsync` builds a presentation of a
     chosen subset that includes the disclosures a chosen one depends on and, when the verifier asked for one, a
     `kb+jwt` KB-JWT over `iat`, `aud`, `nonce` and the `sd_hash` of the presentation.
   - `ISdJwtVerifier.VerifyAsync` verifies a presentation and returns the resolved claims as one JSON object with
     every disclosed value in place and the `_sd`, `_sd_alg` and `...` members removed, or the code of the rule the
     presentation failed.

3. **The JWT validator owns the signature policy.** The caller supplies the whole `JwtValidationParameters` of the
   issuer-signed JWT, whose `ValidSignatureAlgorithms` must be set because an unset list permits nothing at a trust
   boundary. The validator judges the issuer, lifetime and claim requirements on the signed payload before any
   disclosure is resolved, and the holder key is read from that payload, so a disclosure never supplies it. The
   verifier refuses a signing key that is not asymmetric whatever the list names, and refuses a Nested JWT whatever
   decryption keys the parameters name, because RFC 9901 §4.1 defines the issuer-signed JWT as a compact JWS.

4. **Resolution follows RFC 9901 §7.1 and refuses what it cannot resolve unambiguously.** Every digest is matched to at
   most one disclosure. A digest that matches none is a decoy and is dropped. Each of these refuses the presentation
   with its own `SdJwtVerificationFailureCodes` code: a digest that repeats anywhere, a disclosure that two presented
   disclosures share, an object digest that names an array element disclosure or the reverse, a disclosed claim that
   collides with a member already present, a disclosure referenced by nothing, a reserved name (`_sd`, `...`,
   `_sd_alg`) where it may not appear, and an `_sd` or `...` member that is not of its shape. A disclosure that does
   not decode to a JSON array of a non-empty string salt, an optional non-empty string claim name that is not
   reserved, and a value is refused when it is parsed.

5. **The hash algorithms and the confirmation methods are registered data.** `SdJwtDigestAlgorithm` names an `_sd_alg`
   value and the platform hash that computes it; `AddSdJwt()` registers `sha-256`, `sha-384` and `sha-512` as
   instances, and `ISdJwtDigestAlgorithmCatalog` validates the registered set when it is composed. An
   `IConfirmationKeyResolver` resolves the holder key from one `cnf` member. Each member has exactly one resolver, so
   a duplicate fails composition and an application's resolver never loses silently to a built-in one. The built-in
   `JwkConfirmationKeyResolver` reads `jwk` through the registered secret key types, so every asymmetric key type the
   JOSE stack supports binds a presentation.

6. **Salts are random and the limits are validated at start.** A salt is the base64url encoding of
   `SdJwtOptions.SaltSizeBytes` bytes, at least 128 bits, from the platform's cryptographically secure random number
   generator. `MaxDisclosureCount` bounds the disclosures an SD-JWT may carry, checked before any disclosure is
   decoded, and `MaxNestingDepth` bounds how deep the resolved claims may nest; both are validated when the host
   starts. The issuer refuses claims beyond either limit, so every SD-JWT it makes resolves within them.

7. **A presentation's KB-JWT is verified only under a requirement.** With no `SdJwtKeyBindingRequirement`, a KB-JWT
   the presentation carries is not verified and the result reports no key binding; with one, a presentation without a
   KB-JWT is refused. A requirement whose audience or nonce is empty, whose signature algorithms are unset, whose
   maximum age is not positive or whose clock skew is negative is refused before the presentation is read, because a
   requirement built from a missing value would let any captured KB-JWT replay. The KB-JWT is validated as `kb+jwt`
   with the holder key, the requirement's algorithms and the verifier's audience. `nonce` and `sd_hash` are compared in
   fixed time, and `sd_hash` is computed over the presentation exactly as received, up to and including the last `~`.

8. **A profile is data the issuer and the verifier apply.**
   - `SdJwtProfile` names the `typ` of the issuer-signed JWT, the claims that must be present as non-empty strings, and
     the top-level claims that are never selectively disclosable. A required claim is judged on the signed payload
     before any disclosure is resolved, so it is always protected, whether or not the profile lists it.
   - `SdJwtProfiles.SdJwtVc` is `dc+sd-jwt`, requires `vct`, and protects `iss`, `nbf`, `exp`, `cnf`, `vct`,
     `vct#integrity` and `status`. An application defines another profile by constructing one.
   - `SdJwtIssuanceOptions.Profile` types the issuer-signed JWT with the profile's `typ`, refuses a disclosable path
     under a protected top-level claim, and refuses claims that lack a required claim.
     `SdJwtVerificationParameters.Profile` refuses a `typ` that is not the profile's, compared by the media-type rules
     of RFC 7515 §4.1.9, and a payload that lacks a required claim.
   - A protected claim is plaintext in its entirety: any `_sd` array or `...` placeholder beneath it refuses the
     presentation whether or not a disclosure matches, because a withheld `status_list` would otherwise be dropped as a
     decoy and the credential read as unrevoked. The check happens as the disclosures are resolved, so a protected name
     nested inside another claim is an ordinary claim.
   - Without a profile the issuer-signed JWT is typed `JWT`, and the verifier requires nothing of its `typ` beyond the
     caller's JWT validation parameters.
   - `status` passes through unresolved, as the issuer wrote it, for a status checker the application brings.

## Options considered

- **Verifying through the JOSE serializer alone.** Rejected: the package would copy the JWT library's algorithm
  allow-list, key filtering, lifetime and `typ` requirements, and the two copies would drift.
- **Extending `IServiceBuilder<JoseLibrary>`, as the EdDSA and secp256k1 packages do.** Rejected: those packages add
  algorithms and curves and consume nothing above the JOSE library; this package consumes the JWT validator.
- **A builder instead of pointers for the disclosable claims.** Rejected: a pointer names any member or element of an
  existing claims document without a parallel model, and a nested pointer expresses the recursion directly.
- **Refusing a presentation that carries a KB-JWT the verifier did not require.** Rejected: RFC 9901 §7.3 leaves an
  unrequired KB-JWT to the verifier, and a holder that always binds would be refused by every verifier that does not
  ask for binding.
- **Fixing the hashes to SHA-2.** Rejected: the `_sd_alg` registry is open, and an application registers SHA-3 or
  another platform hash by name.
- **A separate SD-JWT VC service pair.** Rejected: the profile adds rules to the format, and a second pair would copy
  the format's own rules.
- **A profile registry resolved by name.** Rejected: a profile is passed where it is used, so there is no name to
  resolve and no catalog to validate.
- **Accepting `vc+sd-jwt` beside `dc+sd-jwt`.** Rejected: the draft registers `dc+sd-jwt`, and a profile that accepts
  two types is an application's own `SdJwtProfile`.
- **Resolving status in the verifier.** Rejected: a Token Status List is fetched and cached over HTTP under the
  application's own policy, which the verifier does not own.
- **Chosen:** an opt-in package over the JWT validator, with profiles as data.

## Consequences

- An application issues, presents and verifies SD-JWTs and SD-JWT VCs with the same services, using every key type the
  JOSE stack serves, including EdDSA and ES256K when their packages are registered.
- A verifier states its whole policy in one place: the JWT validation parameters, the key binding requirement and the
  profile.
- A presentation that violates a rule of RFC 9901 §7.1 is refused with a machine-readable code, so a caller never
  matches on message text. A failure reason never carries the token: a JWT the validator could not decode is reported
  by its failure code and fixed text.
- A profile cannot express a rule beyond `typ`, required string claims and protected top-level claims; a rule such as a
  required `cnf` when key binding is used is the verifier's key binding requirement.
- The package holds the `21000`-`21999` event-ID band of the family's registry.

## References

- [RFC 9901](https://datatracker.ietf.org/doc/html/rfc9901), [RFC 7800](https://datatracker.ietf.org/doc/html/rfc7800),
  [RFC 6901](https://datatracker.ietf.org/doc/html/rfc6901),
  [draft-ietf-oauth-sd-jwt-vc](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-sd-jwt-vc)
- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md),
  [ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md)
- Issue [#73](https://github.com/NCodeGroup/identity/issues/73)
- `NCode.Identity.Jose.SdJwt`: `DefaultSdJwtIssuer`, `DefaultSdJwtHolder`, `DefaultSdJwtVerifier`,
  `SdJwtPayloadProcessor`, `JwkConfirmationKeyResolver`, `SdJwtProfile`, `SdJwtProfiles`; `JoseTokenTypes.KeyBindingJwt`,
  `JoseTokenTypes.SdJwtVc` and the `_sd`, `_sd_alg`, `...`, `sd_hash`, `vct`, `vct#integrity` and `status` names in
  `JoseClaimNames.Payload` of `NCode.Identity.Jose.Abstractions`
