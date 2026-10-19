# 154. JWTs are validated as signed tokens under explicit algorithm and key policy

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

`IJwtValidator` is the one entry point for every JWT the server consumes: the access tokens presented to its own
endpoints (UserInfo, introspection, token exchange, the management API), client assertions (`private_key_jwt`,
`client_secret_jwt`), request objects (RFC 9101) and signed CIBA authentication requests, and the `id_token_hint` of an
end-session request. Each caller supplies `JwtValidationParameters` — the keys, the issuer, the audience, the lifetime
rules — and the validator decides whether the token is authentic.

The validator decoded whatever the JOSE serializer could decode with the keys it was given, and that left four gaps:

- **An unsecured JWS was authentic.** The `none` signature algorithm verified successfully with any key, so a token
  whose header named `alg` `none` and carried an empty signature validated against any key collection. A forged access
  token was accepted by every endpoint that authenticates a bearer token. RFC 8725 §3.2 requires that an unsecured JWS
  never be accepted where a signed one is expected.
- **No caller could restrict the algorithms.** RFC 8725 §3.1 requires a recipient to accept only the algorithms it
  expects and to check them before using the token in any cryptographic operation. The parameters had no algorithm
  lists, so callers either checked the header by hand after validation (the CIBA loader) or not at all (the
  authorization endpoint checked a request object only against `none`).
- **The keys that decrypt a token and the keys that verify it were one collection.** An encrypted request object is
  encrypted to the server's key and signed with the client's; one collection cannot say which key serves which role, so
  the validator decrypted with whatever verification keys it was given, and a key could be used for a role or an
  algorithm its metadata does not declare.
- **A Nested JWT was not supported, and an encrypted token without a signature was authentic.** RFC 7519 §5.2 nests a
  signed JWT inside a JWE (`cty` `JWT`) when a token must be both signed and encrypted; the validator could not decode
  one, and it accepted a JWE that enclosed no signature — which anyone holding the recipient's public key can produce.
  The encoder, for its part, did not mark the JWE it wraps around a signed JWT with `cty` `JWT`.

The validator also accepted the JSON serializations, which carry several signatures or recipients with their own
algorithms, and compressed (`zip`) payloads, which a token's producer controls and which RFC 8725 §3.6 says not to use
with encryption.

## Decision

**The validator accepts only a signed JWT, decodes it under the algorithm lists and the key roles the caller states,
and checks every header against them before any cryptographic operation.**

1. **Signed only.** A token is a compact JWS whose `alg` is not `none`, or a compact JWE whose `cty` is `JWT` (compared
   as a media type, RFC 7515 §4.1.10) enclosing such a JWS — a Nested JWT. One level of nesting is supported. An
   unsecured JWS is rejected at every level, with no parameter that opts into it; a JWE that encloses anything but a
   JWS is rejected. Independently, the `none` signature algorithm verifies only with an empty key, so a real key never
   verifies an unsecured token anywhere the JOSE library is used.
2. **Compact serialization only.** RFC 7519 §1 represents a JWT in the compact serializations; the JSON
   serializations are rejected.
3. **Key roles are explicit.** `SecretKeys` (`UseValidationKeys`) verify signatures; `DecryptionKeys`
   (`UseDecryptionKeys`) decrypt. An encrypted token is rejected when the caller names no decryption keys — they are
   never inferred from the verification keys. A key whose metadata declares a `use` serves only that use, and one that
   declares an `alg` only that algorithm (RFC 7517 §4.2, §4.4); a key that declares neither may serve any.
4. **Algorithms are an allow-list checked first.** `ValidSignatureAlgorithms`, `ValidKeyManagementAlgorithms`,
   `ValidContentEncryptionAlgorithms`, and `ValidCompressionAlgorithms` name the algorithms the caller accepts; `null`
   is unrestricted and an empty list accepts none. Every header — the enclosing JWE's and the enclosed JWS's — is
   checked against them before a key is selected or a byte is decrypted (RFC 8725 §3.1, §3.3).
5. **The decoded token reports what protected it.** `DecodedJwt` exposes the `SigningKey` that verified the signature
   and, for a Nested JWT, the `EncryptionHeader` and the `DecryptionKey`.
6. **The encoder marks a Nested JWT.** A token that is both signed and encrypted is a JWE with `cty` `JWT` enclosing
   the JWS, so a recipient can tell it from an encrypted claims set.

The library's defaults stay permissive where they cannot know the caller's policy (`null` lists), but they are never
unsafe: no default accepts an unsigned token, decrypts with a verification key, or skips the algorithm check a caller
states. Every OpenID caller states its policy: a caller at a trust boundary passes the algorithm lists its settings
permit and names its decryption keys ([ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)).

## Options considered

- **Let a caller opt into unsecured tokens.** Rejected: no caller needs one. OpenID Connect Core §6.1 lets a server
  decline unsigned request objects, and the authorization endpoint answers one with `request_not_supported` before
  validation. An opt-in would be one mistaken flag away from accepting forged tokens.
- **Keep checking algorithms in each caller after validation.** Rejected: a check after decoding runs after the
  cryptographic operation RFC 8725 §3.1 says the check must precede, and each caller that forgets it reopens the gap.
- **Fall back to the verification keys when no decryption keys are named.** Rejected: the signer and the recipient are
  different parties, so the fallback either decrypts with the wrong party's key or hides a missing decryption policy.
  Failing closed makes the missing policy visible.
- **Accept an encrypted token without an enclosed signature.** Rejected: encryption to a public key authenticates
  nothing about the sender.
- **Support arbitrary nesting depth.** Rejected: no specification nests more than once, and an unbounded depth is
  unbounded work for an unauthenticated input.

## Consequences

- An unsigned token is rejected everywhere: the forged-`alg:none` access token no longer authenticates at UserInfo,
  introspection, token exchange, or the management API.
- A caller states its algorithm and key policy in one place, and the validator enforces it uniformly across signed and
  nested tokens; the CIBA loader's hand-written algorithm checks are gone.
- An encrypted request object is decrypted with the tenant's keys and verified with the client's.
- **Breaking:** `DecodedJwt.SecretKey` is `DecodedJwt.SigningKey`; `JwtValidationParameters` gains the decryption keys
  and the four algorithm lists; the validator rejects an unsigned token, a JSON-serialized token, and an encrypted
  token without decryption keys or an enclosed signature.
- The server cannot yet validate an access token it encrypted itself, because the callers that authenticate its own
  access tokens name no decryption keys and no algorithm lists (backlog `F67`).

## References

- [RFC 8725](https://www.rfc-editor.org/rfc/rfc8725) — JSON Web Token Best Current Practices (§3.1 algorithm
  verification, §3.2 appropriate algorithms, §3.3 validate all cryptographic operations, §3.6 avoid compression).
- [RFC 7519](https://www.rfc-editor.org/rfc/rfc7519) §5.2 (Nested JWT), [RFC 7517](https://www.rfc-editor.org/rfc/rfc7517)
  §4.2 / §4.4 (`use` / `alg`).
- [ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md) — the policies the OpenID callers
  state.
- [ADR-0124](0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md) — the lifetime requirements the validator
  evaluates.
- Code: `NCode.Identity.Jwt.Validation.DefaultJwtValidator`, `JwtValidationParameters`,
  `JwtValidationParametersExtensions.UseDecryptionKeys`, `NCode.Identity.Jwt.DecodedJwt`,
  `NCode.Identity.Jwt.Encoding.DefaultJwtEncoder`, `NCode.Identity.Jose.Algorithms.Signature.NoneSignatureAlgorithm`.
