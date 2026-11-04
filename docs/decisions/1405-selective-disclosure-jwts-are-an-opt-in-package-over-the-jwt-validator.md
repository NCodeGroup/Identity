# 1405. Selective Disclosure JWTs are an opt-in package that issues, presents and verifies over the JWT validator

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** NCode Group

## Context

[RFC 9901](https://datatracker.ietf.org/doc/html/rfc9901), Selective Disclosure for JWTs (SD-JWT), lets an issuer
sign a JWT whose claims are replaced by salted digests, so the holder discloses a chosen subset to a verifier by
handing over the disclosures that match those digests, and binds the presentation to its own key with a Key Binding
JWT (KB-JWT). It is the credential format of the EUDI wallet and of SD-JWT-based Verifiable Credentials
([ADR-1406](1406-the-sd-jwt-vc-profile-is-data-the-issuer-and-the-verifier-apply.md)).

The format is layered on JWS: the issuer-signed JWT is a compact JWS, the KB-JWT is a compact JWS, and the
disclosures are base64url-encoded JSON arrays whose digests are hashed over their encoding as issued. Three facts
shape how the family adds it:

- The JOSE library signs and verifies compact JWS tokens, and the JWT library validates a received JWT under one
  policy — the keys, the signature algorithms it may use, its `typ`, issuer and lifetime
  ([ADR-1154](1154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)). An SD-JWT
  verifier that verified the issuer-signed JWT or the KB-JWT itself would copy that policy.
- An implementation package references only `*.Abstractions` packages
  ([ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md)), and an opt-in capability extends the
  marker of the library it consumes ([ADR-1069](1069-registration-library-compile-time-marker-nesting.md)).
- Every vocabulary is open unless provably closed ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
  The `_sd_alg` hash algorithm names come from the IANA Named Information Hash Algorithm Registry, and the `cnf`
  confirmation methods (RFC 7800) from the IANA JWT Confirmation Methods registry; both grow without this library.
- A verifier reads disclosures from the holder, so the number of disclosures and the depth the resolved claims reach
  bound the work a presentation can demand.

## Decision

1. **`NCode.Identity.Jose.SdJwt` is an opt-in package over the JWT library.** `AddSdJwt()` extends
   `IServiceBuilder<JwtLibrary>`, because the verifier validates the issuer-signed JWT and the KB-JWT through
   `IJwtValidator`, so a host cannot register it without the JWT library and the JOSE library beneath it. It
   references the JOSE, JWT and Secrets abstractions, `NCode.Json` and `NCode.Time`, and no implementation package.

2. **Three roles, three services.** `ISdJwtIssuer.Issue` takes a claims object and the JSON Pointers (RFC 6901) of
   the claims the holder may disclose selectively — object members and array elements at any depth — and walks the
   claims depth first, so a disclosable claim inside another disclosable claim is replaced by its digest before the
   outer claim is moved into its own disclosure, which is the recursive structure of RFC 9901 §5.2.4. Each `_sd`
   array is sorted ordinally so its order says nothing about the claims, decoy digests are added where asked, the
   holder key is written to `cnf` as a public JSON Web Key, read under its registered key type so that type's own
   private members are refused as well as the standard ones, and the payload is signed by the JOSE serializer with
   an asymmetric key. `iss`, `nbf`, `exp` and `cnf` are never made disclosable, in whole or in part, under any
   profile: the verifier judges them on the issuer-signed payload before it resolves a disclosure (decision 3), so
   an SD-JWT that disclosed one would never verify. `ISdJwtHolder` parses the combined format, resolves each
   disclosure to the claim it discloses and to the disclosure that carries its digest (`DisclosedClaim`), and builds
   a presentation of a chosen subset that always includes the disclosures a chosen one depends on, with a `kb+jwt`
   KB-JWT over `iat`, `aud`, `nonce` and the `sd_hash` of the presentation when the verifier asked for one.
   `ISdJwtVerifier.VerifyAsync` verifies a presentation and returns the resolved claims as one JSON object with every
   disclosed value in place and the `_sd`, `_sd_alg` and `...` members removed, or the code of the rule the
   presentation failed.

3. **The JWT validator owns the signature policy.** The caller supplies the whole `JwtValidationParameters` of the
   issuer-signed JWT, whose `ValidSignatureAlgorithms` must be set because an unset list permits nothing at a trust
   boundary; the validator judges the issuer, lifetime and claim requirements on the issuer-signed payload, before
   any disclosure is resolved. The verifier refuses a signing key that is not asymmetric whatever the list names,
   and refuses a Nested JWT whatever decryption keys the parameters name, because RFC 9901 §4.1 defines the
   issuer-signed JWT as a compact JWS. The KB-JWT is
   validated with parameters the verifier builds from the holder key, the algorithms the requirement names, the
   `kb+jwt` token type and the verifier's audience; its `nonce`, `sd_hash` and `iat` are checked by the SD-JWT
   verifier, and `sd_hash` is computed over the presentation exactly as received, up to and including the last `~`.

4. **Resolution follows RFC 9901 §7.1 and refuses what it cannot resolve unambiguously.** Every digest is matched to
   at most one disclosure; a digest that matches none is a decoy and is dropped; a digest that repeats anywhere, a
   disclosure that two presented disclosures share, an object digest that names an array element disclosure or the
   reverse, a disclosed claim that collides with a member already present, a disclosure referenced by nothing, a
   reserved name (`_sd`, `...`, `_sd_alg`) where it may not appear, and an `_sd` or `...` member that is not of its
   shape each refuse the presentation with its own `SdJwtVerificationFailureCodes` code. A disclosure that does not
   decode to a JSON array of a non-empty string salt, an optional non-empty string claim name that is not reserved,
   and a value is refused when it is parsed. The holder key is read from the issuer-signed payload before any
   disclosure is resolved into it, so a disclosure never supplies it.

5. **The hash algorithms and the confirmation methods are registered data.** `SdJwtDigestAlgorithm` names an
   `_sd_alg` value and the platform hash that computes it; `AddSdJwt()` registers `sha-256`, `sha-384` and `sha-512`
   as instances, and `ISdJwtDigestAlgorithmCatalog` validates the registered set when it is composed. An
   `IConfirmationKeyResolver` resolves the holder key from one `cnf` member; the built-in one reads `jwk` through the
   registered secret key types, so every asymmetric key type the JOSE stack supports binds a presentation, and a
   resolver for another method is registered beside it. Each `cnf` member has exactly one resolver: the verifier
   validates the registered set when it is composed and refuses a second resolver for a member, so an application's
   resolver never loses silently to a built-in one.

6. **Salts are random and the limits are configured and validated at start.** A salt is the base64url encoding of
   `SdJwtOptions.SaltSizeBytes` bytes from the platform's cryptographically secure random number generator, at least
   128 bits. `MaxDisclosureCount` bounds the disclosures an SD-JWT may carry, checked before any disclosure is decoded,
   and `MaxNestingDepth` bounds how deep the resolved claims may nest; both are validated when the host starts.

7. **A presentation's KB-JWT is verified only when the verifier requires it.** With no
   `SdJwtKeyBindingRequirement`, a KB-JWT the presentation carries is not verified and the result reports no key
   binding; with one, a presentation without a KB-JWT is refused.

## Options considered

- **Verifying the issuer-signed JWT in the SD-JWT package through the JOSE serializer alone.** Rejected: the package
  would copy the JWT library's algorithm allow-list, key filtering, lifetime and `typ` requirements, and the two
  copies would drift.
- **Extending `IServiceBuilder<JoseLibrary>`, as the EdDSA and secp256k1 packages do.** Rejected: those packages add
  algorithms and curves to the JOSE library and consume nothing above it; this package consumes the JWT validator, so
  its prerequisite is the JWT library.
- **Describing the disclosable claims with a builder instead of JSON Pointers.** Rejected: a pointer names any member
  or element of an existing claims document without a parallel model of it, and a nested pointer expresses the
  recursive structure directly.
- **Refusing a presentation that carries a KB-JWT the verifier did not require.** Rejected: RFC 9901 §7.3 leaves an
  unrequired KB-JWT to the verifier, and a holder that always binds its presentations would be refused by every
  verifier that does not ask for binding.
- **Fixing the hash algorithms to SHA-2.** Rejected: the `_sd_alg` registry is open, and an application can register
  SHA-3 or another platform hash by name without a change here.

## Consequences

- An application issues, presents and verifies SD-JWTs with the keys, algorithms and key types the JOSE stack already
  serves, including EdDSA and ES256K once their packages are registered.
- A verifier states its whole policy in one place: the JWT validation parameters of the issuer-signed JWT, the key
  binding requirement and the profile.
- A presentation that violates a rule of RFC 9901 §7.1 is refused with a machine-readable code, so a caller never
  matches on message text.
- The package holds the `21000`–`21999` event-ID band of the family's registry.

## References

- [RFC 9901](https://datatracker.ietf.org/doc/html/rfc9901), [RFC 7800](https://datatracker.ietf.org/doc/html/rfc7800),
  [RFC 6901](https://datatracker.ietf.org/doc/html/rfc6901)
- [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md),
  [ADR-1069](1069-registration-library-compile-time-marker-nesting.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1154](1154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md),
  [ADR-1406](1406-the-sd-jwt-vc-profile-is-data-the-issuer-and-the-verifier-apply.md)
- Issue [#73](https://github.com/NCodeGroup/Identity/issues/73)
- `NCode.Identity.Jose.SdJwt`: `DefaultSdJwtIssuer`, `DefaultSdJwtHolder`, `DefaultSdJwtVerifier`,
  `SdJwtPayloadProcessor`, `JwkConfirmationKeyResolver`; `JoseTokenTypes.KeyBindingJwt` and the `_sd`, `_sd_alg`,
  `...` and `sd_hash` names in `JoseClaimNames.Payload` of `NCode.Identity.Jose.Abstractions`
