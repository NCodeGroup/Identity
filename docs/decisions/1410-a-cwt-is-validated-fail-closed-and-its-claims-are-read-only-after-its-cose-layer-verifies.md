# 1410. A CWT is validated fail-closed, and its claims are read only after its COSE layer verifies

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** NCode Group

## Context

A CBOR Web Token ([RFC 8392](https://datatracker.ietf.org/doc/html/rfc8392)) is a claims set carried in a COSE message:
`COSE_Sign1` or `COSE_Mac0`, optionally enclosed in a `COSE_Encrypt0` or `COSE_Encrypt` message, with an optional CBOR
tag 61. A verifier of CWTs is at a trust boundary, as a verifier of JWTs is.

The JWT validator validates a token as a signed token under explicit key and algorithm policy, with the algorithm
allow-list checked first
([ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md)); the SD-JWT verifier
requires its algorithm list at a trust boundary
([ADR-1405](1405-selective-disclosure-jwts-are-an-opt-in-package-that-issues-presents-and-verifies-over-the-jwt-validator.md)); and `NumericDate` values
are read strictly and range-guarded ([ADR-1123](1123-a-numericdate-and-a-whole-number-a-remote-party-sends-are-read-strictly-through-shared-readers.md)). A CWT
carries the same claims in CBOR, and its `NumericDate` may be an integer or a floating-point number.

The COSE layer a CWT is issued and validated over is the opt-in COSE package family
([ADR-1409](1409-cose-messages-are-an-opt-in-package-family-over-system-formats-cbor-and-the-familys-keys-and-algorithms.md)).

## Decision

A CWT is validated fail-closed under a policy the caller states, and no claim is read before the COSE layer verifies
the message.

1. **Two packages.** `NCode.Identity.Cwt.Abstractions` holds `CwtClaimsSet`, `ICwtValidator`, `ICwtIssuer`,
   `CwtIssuanceParameters`, `CwtValidationParameters` and `CwtValidationFailureCodes`; `NCode.Identity.Cwt` implements
   them over the COSE layer and references abstractions only. `AddCwtLibrary()` extends
   `IServiceBuilder<CoseLibrary>`.

2. **Accepted shapes.** An authenticated message (`COSE_Sign1`, `COSE_Sign`, `COSE_Mac0` or `COSE_Mac`), optionally
   inside one encryption layer, with an optional leading tag 61. A token is tagged unless the caller names the message
   type (`UntaggedMessageType`). A CWT protected only by encryption is refused: a key transport recipient lets anyone
   who holds the recipient's public key produce one, so encryption alone does not authenticate the issuer, as the JWT
   validator refuses a JWE that does not enclose a JWS.

3. **The policy is stated by the caller and fails closed.** `CwtValidationParameters` requires non-empty
   `ValidAlgorithms` and `ValidIssuers`, with no empty entry. The keys are `ValidationKeys` or a
   `ResolveValidationKeysAsync` resolver that receives the headers of the authenticated message; the validator never
   falls back to the keys of the server itself. An encrypted token requires `DecryptionKeys` and a non-empty
   `ValidContentEncryptionAlgorithms`, with `ValidKeyDistributionAlgorithms` for recipients. A parameter error throws
   before the token is read.

4. **Claims are read only after the COSE layer verifies.** The order is: the parameters, the CWT tag, `Inspect`, the
   decryption layer, the keys, `Verify`, the token type, then the claims. The registered claims are typed strictly and
   a claim of the wrong form is `invalid_claim`: `iss` and `sub` are text, `aud` is text or a non-empty array of text,
   `exp`, `nbf` and `iat` are integers or finite floating-point numbers without a tag, `cti` is a byte string, `cnf` is
   a map and its `COSE_Key` member a map. Every claim, typed or not, is also available as received. A missing
   `RequiredClaims` entry is `missing_claim`.

5. **The audience rule.** When the caller names no `ValidAudiences`, a token that carries `aud` is refused; when the
   caller names some, the token must carry one of them (RFC 8392 §3.1.3, RFC 7519 §4.1.3). A token without `aud` is
   valid for a verifier that names none.

6. **The lifetime rule is the JWT validator's.** `exp` is required unless `RequireExpirationTime` is false or
   `MaxLifetime` caps the lifetime; `nbf` after `exp` is `invalid_lifetime`; `nbf` or `iat` after now plus
   `ClockSkew` is `not_yet_valid`; `exp` before now minus the skew is `expired`; the maximum lifetime is measured from
   the earliest of `iat` and `nbf`.

7. **The type of the token is explicit.** When the caller names a `TokenType`, the protected `typ` header parameter
   (RFC 9596) must be text equal to it; `typ` is then understood under `crit`.

8. **Failure codes are shared where the concept is.** `CwtValidationFailureCodes` uses the JWT validator's strings
   where the meaning is the same (`invalid_token`, `invalid_token_type`, `invalid_claim`, `invalid_issuer`,
   `invalid_audience`, `expired`, `not_yet_valid`, `invalid_lifetime`) and adds `missing_claim`. A failure of the COSE
   layer reports its `CoseFailureCodes` code. A failure reason is fixed text that never carries the token.

9. **Issuance.** `ICwtIssuer.IssueAsync` writes `iss`, `sub`, `aud`, `exp`, `nbf`, `iat` (the current time when none
   is given), `cti`, `cnf` (a public `COSE_Key`) and the additional claims, with every `NumericDate` as whole seconds,
   in the deterministic encoding. The additional claims may not use the keys one through eight. An `exp` that is not
   after `nbf` and `iat` is refused. The authenticated message is `COSE_Sign1` for a signature algorithm and
   `COSE_Mac0` for a message authentication code algorithm; a token type becomes the protected `typ`; an encryption
   wrapper encloses the tagged authenticated message, and the CWT tag is written on request. The token is returned as a
   new `byte[]`.

## Options considered

- **Reusing the JWT validator's `null` means unrestricted lists.** Rejected: a CWT verifier is at a trust boundary, as
  [ADR-1405](1405-selective-disclosure-jwts-are-an-opt-in-package-that-issues-presents-and-verifies-over-the-jwt-validator.md) decides for SD-JWT.
- **A requirement-and-handler pipeline as the JWT validator has.** Rejected: the JWT handlers run over a JSON payload
  and a `ClaimsIdentity`; the CWT result exposes typed claims and every claim for the caller's own rules.
- **Accepting an encryption-only CWT.** Rejected: encryption alone does not authenticate the issuer.
- **Falling back to the keys of the server.** Rejected: a CWT verifier verifies other parties' tokens.
- **Requiring `aud` always.** Rejected: tokens such as status lists carry none; a token that carries one is refused
  unless the verifier names its audiences.
- **Chosen:** a caller-stated, fail-closed policy with strictly typed claims read after verification.

## Consequences

- A caller states the whole policy in `CwtValidationParameters`.
- Failures are machine-readable codes shared with the JWT validator where the concept is the same.
- No claim is read before the COSE layer verifies.
- `AddCwtLibrary()` can be called once per service collection, like the other library builders.

## References

- [RFC 8392](https://datatracker.ietf.org/doc/html/rfc8392), [RFC 8747](https://datatracker.ietf.org/doc/html/rfc8747),
  [RFC 9596](https://datatracker.ietf.org/doc/html/rfc9596), [RFC 7519](https://datatracker.ietf.org/doc/html/rfc7519)
- [ADR-1123](1123-a-numericdate-and-a-whole-number-a-remote-party-sends-are-read-strictly-through-shared-readers.md),
  [ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md),
  [ADR-1405](1405-selective-disclosure-jwts-are-an-opt-in-package-that-issues-presents-and-verifies-over-the-jwt-validator.md),
  [ADR-1409](1409-cose-messages-are-an-opt-in-package-family-over-system-formats-cbor-and-the-familys-keys-and-algorithms.md)
- Issue [#76](https://github.com/NCodeGroup/identity/issues/76)
- `NCode.Identity.Cwt.Abstractions`: `ICwtValidator`, `CwtValidationParameters`, `ICwtIssuer`;
  `NCode.Identity.Cwt`: `DefaultCwtValidator`, `DefaultCwtIssuer`, `CwtClaimsReader`
