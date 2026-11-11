# 1410. A CWT is validated fail-closed, and its claims are read only after its COSE layer verifies

- **Status:** Accepted (amended by [ADR-1472](1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md):
  `ICwtIssuer.IssueAsync` signs asynchronously)
- **Date:** 2026-11-04
- **Deciders:** NCode Group
- **Refines:** [ADR-1409](1409-cose-messages-are-an-opt-in-package-family-over-system-formats-cbor-and-the-familys-keys-and-algorithms.md), the COSE layer a CWT is issued and validated over

## Context

A CBOR Web Token ([RFC 8392](https://datatracker.ietf.org/doc/html/rfc8392)) is a claims set carried in a COSE message:
`COSE_Sign1` or `COSE_Mac0`, optionally enclosed in a `COSE_Encrypt0` or `COSE_Encrypt` message, with an optional CBOR
tag 61. A verifier of CWTs is at a trust boundary, as a verifier of JWTs is.

The JWT validator validates a token as a signed token under explicit key and algorithm policy, with the algorithm
allow-list checked first
([ADR-1154](1154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)); the SD-JWT verifier
requires its algorithm list at a trust boundary
([ADR-1405](1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator.md)); and `NumericDate` values
are read strictly and range-guarded ([ADR-1123](1123-numeric-date-is-read-strictly-and-range-guarded.md)). A CWT
carries the same claims in CBOR, and its `NumericDate` may be an integer or a floating-point number.

## Decision

1. **Two packages.** `NCode.Identity.Cwt.Abstractions` holds the claims set, the issuer and validator contracts and the
   validation parameters; `NCode.Identity.Cwt` implements them. `AddCwtLibrary()` extends
   `IServiceBuilder<CoseLibrary>`. The implementation references abstractions only.

2. **Accepted shapes.** An authenticated message (`COSE_Sign1`, `COSE_Sign`, `COSE_Mac0` or `COSE_Mac`), optionally
   inside one encryption layer, with an optional leading tag 61. A token is tagged unless the caller names the message
   type. A CWT protected only by encryption is refused: a key transport recipient lets anyone who holds the
   recipient's public key produce one, so encryption alone does not authenticate the issuer, as the JWT validator
   refuses a JWE that does not enclose a JWS.

3. **The policy is stated by the caller and fails closed.** `CwtValidationParameters` requires a non-empty list of
   permitted algorithm identifiers and a non-empty list of issuers, with no empty entry; the keys are given explicitly or
   by a resolver that receives the headers of the authenticated message, and the validator never falls back to the keys
   of the server itself; an encrypted token is refused unless decryption keys and a non-empty list of content
   encryption algorithms are given. A parameter error throws before the token is read.

4. **Claims are read only after the COSE layer verifies.** The order is: the parameters, the CWT tag, `Inspect`, the
   decryption layer, the keys, `Verify`, the token type, then the claims. The registered claims are typed strictly and
   a claim of the wrong form is `invalid_claim`: `iss` and `sub` are text, `aud` is text or a non-empty array of text,
   `exp`, `nbf` and `iat` are integers or finite floating-point numbers without a tag, `cti` is a byte string, `cnf` is
   a map and its `COSE_Key` member a map. Every claim, typed or not, is also available as received.

5. **The audience rule.** When the caller names no audiences, a token that carries `aud` is refused; when the caller
   names audiences, the token must carry one of them (RFC 8392 §3.1.3, RFC 7519 §4.1.3). A token without `aud` is valid
   for a verifier that names none.

6. **The lifetime rule is the JWT validator's.** An expiration time is required unless the caller says otherwise or
   caps the lifetime; `nbf` after `exp` is `invalid_lifetime`; `nbf` or `iat` after now plus the clock skew is
   `not_yet_valid`; `exp` before now minus the clock skew is `expired`; the maximum lifetime is measured from the
   earliest of `iat` and `nbf`.

7. **The type of the token is explicit.** When the caller names a token type, the protected `typ` header parameter
   (RFC 9596) must be text equal to it; `typ` is then understood under `crit`.

8. **Failure codes are shared where the concept is.** `invalid_token`, `invalid_token_type`, `invalid_claim`,
   `missing_claim`, `invalid_issuer`, `invalid_audience`, `expired`, `not_yet_valid` and `invalid_lifetime` use the
   strings of the JWT validator where the meaning is the same; a failure of the COSE layer reports its
   `CoseFailureCodes` code. A failure reason is fixed text that never carries the token.

9. **Issuance.** `ICwtIssuer` writes `iss`, `sub`, `aud`, `exp`, `nbf`, `iat` (the current time when none is given),
   `cti`, `cnf` (the public `COSE_Key` of the confirmation key) and the additional claims, with every `NumericDate` as
   whole seconds, in the deterministic encoding. The additional claims may not use the keys one through eight. The
   authenticated message is `COSE_Sign1` for a signature algorithm and `COSE_Mac0` for a message authentication code
   algorithm; a token type becomes the protected `typ`; encryption credentials or recipients enclose the tagged
   authenticated message, and the CWT tag is written on request. An expiration time that is not after the not-before or
   issued-at time is refused. `Issue` returns the token as a new byte array, as the COSE encoders return a message
   ([ADR-1409](1409-cose-messages-are-an-opt-in-package-family-over-system-formats-cbor-and-the-familys-keys-and-algorithms.md)
   decision 6).

## Options considered

- **Reusing the JWT validator's `null` means unrestricted lists.** Rejected: a CWT verifier is at a trust boundary, as
  [ADR-1405](1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator.md) decides for SD-JWT.
- **A requirement-and-handler pipeline as the JWT validator has.** Rejected: the JWT handlers run over a JSON payload
  and a `ClaimsIdentity`; the CWT result exposes typed claims and every claim for the caller's own rules.
- **Accepting an encryption-only CWT.** Rejected: encryption alone does not authenticate the issuer.
- **Falling back to the keys of the server.** Rejected: a CWT verifier verifies other parties' tokens.
- **Requiring `aud` always.** Rejected: tokens such as status lists carry none; a token that carries one is refused
  unless the verifier names its audiences.

## Consequences

- A caller states the whole policy in `CwtValidationParameters`.
- Failures are machine-readable codes shared with the JWT validator where the concept is the same.
- No claim is read before the COSE layer verifies.
- `AddCwtLibrary()` can be called once per service collection, like the other library builders.

## References

- [RFC 8392](https://datatracker.ietf.org/doc/html/rfc8392), [RFC 8747](https://datatracker.ietf.org/doc/html/rfc8747),
  [RFC 9596](https://datatracker.ietf.org/doc/html/rfc9596), [RFC 7519](https://datatracker.ietf.org/doc/html/rfc7519)
- [ADR-1123](1123-numeric-date-is-read-strictly-and-range-guarded.md),
  [ADR-1154](1154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md),
  [ADR-1405](1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator.md),
  [ADR-1409](1409-cose-messages-are-an-opt-in-package-family-over-system-formats-cbor-and-the-familys-keys-and-algorithms.md)
- Issue [#76](https://github.com/NCodeGroup/identity/issues/76)
- `NCode.Identity.Cwt.Abstractions`: `ICwtValidator`, `CwtValidationParameters`, `ICwtIssuer`;
  `NCode.Identity.Cwt`: `DefaultCwtValidator`, `DefaultCwtIssuer`, `CwtClaimsReader`
