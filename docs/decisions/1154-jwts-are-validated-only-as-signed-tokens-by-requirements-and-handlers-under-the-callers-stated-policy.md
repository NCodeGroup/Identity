# 1154. JWTs are validated only as signed tokens, by requirements and handlers under the caller's stated policy

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

`IJwtValidator` is the one entry point for every JWT the server consumes: the access tokens presented to its own
endpoints (UserInfo, introspection, token exchange, the management API), client assertions (`private_key_jwt`,
`client_secret_jwt`), request objects (RFC 9101), signed CIBA authentication requests, and the `id_token_hint` of an
end-session request. Each caller supplies `JwtValidationParameters` (the keys, the issuer, the audience, the lifetime
rules) and the validator decides whether the token is authentic.

Two forces shape the design.

- **Who owns the rules.** Validation rules are many and open-ended: issuer, audience, token type, lifetimes, certificate
  lifetimes and bespoke checks. A consumer that only injects `IJwtValidator` and configures a validation must not need
  the implementation package, and a third-party rule must be addable without editing the library
  ([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)).
- **What counts as authentic.** A validator that decodes whatever the JOSE serializer can decode with the keys it is
  given leaves four gaps. An unsecured JWS (`alg` `none`) must never be accepted where a signed one is expected (RFC 8725
  §3.2). A recipient must accept only the algorithms it expects and check them before any cryptographic operation (RFC
  8725 §3.1). The keys that decrypt a token and the keys that verify it are different roles: an encrypted request object
  is encrypted to the server's key and signed with the client's. A Nested JWT (RFC 7519 §5.2) signs inside a JWE with
  `cty` `JWT`, and an encrypted token without an enclosed signature authenticates nothing, because anyone holding the
  recipient's public key can produce it.

The JSON serializations carry several signatures or recipients with their own algorithms, and compressed (`zip`)
payloads are controlled by a token's producer and discouraged with encryption by RFC 8725 §3.6.

## Decision

**Validation is a set of declarative requirements judged by registered handlers, and the validator accepts only a signed
JWT, decoded under the algorithm lists and key roles the caller states, with every header checked before any
cryptographic operation.**

### Service shape and packages

The service has two sides: `IJwtEncoder` (`EncodeAsync`, [ADR-1472](1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md))
and `IJwtValidator` (`ValidateAsync`), implemented by `DefaultJwtEncoder` and `DefaultJwtValidator`. No combined facade
exists, because no consumer needs both.

The contracts, the requirement records, the fluent builders, the exceptions, `IClaimsIdentityFactory` and
`JsonClaimValueTypes` live in `NCode.Identity.Jwt.Abstractions` under `NCode.Identity.Jwt.Encoding`,
`NCode.Identity.Jwt.Validation` and `NCode.Identity.Jwt.Claims`. The handlers, the evaluator, the key resolver and the
claims factory live in `NCode.Identity.Jwt`. `NCode.Identity.OpenId.Authentication` references only the abstractions.

### Requirements and handlers

1. **Requirements are pure data.** Each is an `IJwtValidationRequirement` record on `JwtValidationParameters.Requirements`:
   `ClaimValueRequirement` (with `ValidateIssuer` and `ValidateAudience`), `TokenTypeRequirement`,
   `TokenLifetimeRequirement`, `CertificateLifetimeRequirement` and `AssertionRequirement`. The fluent builders in
   `JwtValidationParametersExtensions` only construct and append records.
2. **Handlers judge them.** A handler implements `IJwtValidationHandler` or `JwtValidationHandler<TRequirement>`.
   Handlers form a strategy collection, and external code registers its own with `AddJwtValidationHandler<T>()`.
3. **Handlers vote.** A handler calls `Succeed(requirement)` or `Fail(failureCode, reason)` on `JwtValidationContext`. A
   token is valid only when every requirement succeeded and nothing failed. Failures aggregate into one
   `JwtValidationException` on the result; the codes are
   [ADR-1278](1278-token-validation-failures-carry-a-machine-readable-code-set-where-the-failure-is-detected.md)'s.
4. **Two seams are delegates.** The key resolver and the claims-identity creation are reached through the nullable
   delegates `ResolveValidationKeysAsync` and `CreateClaimsIdentityAsync` on the parameters. When a delegate is null the
   validator uses the built-in default; claim mapping is
   [ADR-1122](1122-jwt-claim-values-map-to-and-from-json-by-their-declared-type-alone.md)'s.

### What the validator accepts

1. **Signed only.** A token is a compact JWS whose `alg` is not `none`, or a compact JWE whose `cty` is `JWT` (compared
   as a media type, RFC 7515 §4.1.10) enclosing such a JWS, one level deep. An unsecured JWS is refused at every level,
   with no parameter that opts into it. A JWE that encloses no JWS is refused. The JOSE library implements no `none`
   algorithm either ([ADR-1295](1295-the-jose-library-does-not-implement-the-none-algorithm.md)).
2. **Compact serialization only.** RFC 7519 §1 represents a JWT in the compact serializations; the JSON serializations
   are refused.
3. **Key roles are explicit.** `ValidationKeys` (`UseValidationKeys`) verify signatures and `DecryptionKeys`
   (`UseDecryptionKeys`) decrypt. An encrypted token without decryption keys is refused; they are never inferred from
   the verification keys. A key's declared `use` and `alg` restrict what it serves (RFC 7517 §4.2, §4.4); a key that
   declares neither may serve any.
4. **Algorithms are an allow-list checked first.** `ValidSignatureAlgorithms`, `ValidKeyManagementAlgorithms`,
   `ValidContentEncryptionAlgorithms` and `ValidCompressionAlgorithms` name the accepted algorithms: `null` is
   unrestricted and an empty list accepts nothing. Every header, the enclosing JWE's and the enclosed JWS's, is checked
   before a key is selected or a byte is decrypted (RFC 8725 §3.1, §3.3).
5. **The decoded token reports what protected it.** `DecodedJwt` exposes the `SigningKey`, and for a Nested JWT the
   `EncryptionHeader` and the `DecryptionKey`.
6. **The encoder marks a Nested JWT.** A token that is both signed and encrypted is a JWE with `cty` `JWT` enclosing
   the JWS.

### Defaults

The library's defaults are permissive only where it cannot know the caller's policy (`null` lists), and never unsafe: no
default accepts an unsigned token, decrypts with a verification key, or skips a check a caller states. Every OpenID
caller states its policy: a caller at a trust boundary passes the algorithm lists its settings permit and names its
decryption keys ([ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata-within-its-effective-ceilings.md)),
and the server's own tokens follow [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md).

## Options considered

- **A fluent DSL of closures.** Rejected: closures put validation logic in the abstractions and make a validation an
  opaque set of delegates instead of an inspectable description.
- **Moving only the DTOs into the abstractions.** Rejected: the handlers and the evaluator would stay behind the
  implementation package, so an abstractions-only consumer could not extend validation by registering a handler.
- **A combined encoder-validator facade.** Rejected: no consumer needs both sides, and the facade would couple them.
- **Let a caller opt into unsecured tokens.** Rejected: no caller needs one. OpenID Connect Core §6.1 lets a server
  decline unsigned request objects, and an opt-in is one mistaken flag away from accepting forged tokens.
- **Check algorithms in each caller after validation.** Rejected: a check after decoding runs after the cryptographic
  operation RFC 8725 §3.1 says it must precede, and each caller that forgets it reopens the gap.
- **Fall back to the verification keys for decryption.** Rejected: the signer and the recipient are different parties,
  so the fallback either decrypts with the wrong party's key or hides a missing decryption policy. Failing closed makes
  the missing policy visible.
- **Accept an encryption-only token.** Rejected: encryption to a public key authenticates nothing about the sender.
- **Arbitrary nesting depth.** Rejected: no specification nests more than once, and an unbounded depth is unbounded work
  for an unauthenticated input.
- **Chosen: requirement records judged by registered handlers, signed tokens only, explicit key roles and an
  algorithm allow-list checked first.** It keeps abstractions free of logic, extensible by registration, and closed
  against unsigned and unauthenticated input.

## Consequences

- A forged `alg:none` token is refused everywhere the validator is used: UserInfo, introspection, token exchange and the
  management API.
- A caller states its algorithm and key policy in one place, and the validator enforces it uniformly across signed and
  nested tokens.
- A third-party rule is a requirement record plus a handler registered through DI. A consumer of the abstractions alone
  can inject `IJwtValidator`, configure a validation with the fluent builders and read the result.
- `AssertionRequirement` is an escape hatch usable from the abstractions: its delegate operates only on the abstractions'
  `JwtValidationContext`, so bespoke one-off validation needs no implementation reference.
- An encrypted request object is decrypted with the tenant's keys and verified with the client's.
- A smell: a caller that needs a handler's logic inside a requirement record, or that passes `null` algorithm lists at a
  trust boundary, has stepped outside the design.

## References

- [RFC 8725](https://www.rfc-editor.org/rfc/rfc8725) (JSON Web Token Best Current Practices): §3.1 algorithm
  verification, §3.2 appropriate algorithms, §3.3 validate all cryptographic operations, §3.6 avoid compression.
- [RFC 7519](https://www.rfc-editor.org/rfc/rfc7519) §5.2 (Nested JWT), [RFC 7517](https://www.rfc-editor.org/rfc/rfc7517)
  §4.2 and §4.4 (`use` and `alg`).
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md): service,
  strategy collection and mediator.
- [ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata-within-its-effective-ceilings.md) and
  [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md): the policies the OpenID callers state.
- [ADR-1187](1187-a-client-signed-jwt-is-short-lived-its-lifetime-capped-from-its-earliest-asserted-start.md): the
  lifetime requirements the validator evaluates.
- [ADR-1278](1278-token-validation-failures-carry-a-machine-readable-code-set-where-the-failure-is-detected.md): failure codes.
- Code: `NCode.Identity.Jwt.Validation.DefaultJwtValidator`, `JwtValidationParameters`,
  `JwtValidationParametersExtensions`, `JwtValidationContext`, `NCode.Identity.Jwt.DecodedJwt`,
  `NCode.Identity.Jwt.Encoding.DefaultJwtEncoder`.
