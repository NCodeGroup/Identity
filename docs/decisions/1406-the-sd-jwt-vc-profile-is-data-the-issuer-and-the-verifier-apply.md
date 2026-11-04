# 1406. The SD-JWT VC profile is data the issuer and the verifier apply

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** NCode Group
- **Refines:** [ADR-1405](1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator.md), the
  profile the issuer types a credential with and the verifier requires

## Context

SD-JWT-based Verifiable Credentials (draft-ietf-oauth-sd-jwt-vc) profile the SD-JWT format
([ADR-1405](1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator.md)) for credentials: the
issuer-signed JWT is typed `dc+sd-jwt`, carries the credential type in `vct` as a string, and keeps `iss`, `nbf`,
`exp`, `cnf`, `vct`, `vct#integrity` and `status` in its payload, never in a disclosure, while `sub` and `iat` may
be selectively disclosable. The `status` claim carries the credential's status mechanisms, such as a Token Status
List reference, which the verifier reads from the verified claims.

The profile is a set of rules over the format, not a second format: the same issuer, holder and verifier serve it.
Other profiles exist and more will, so the rules must be data an application can define, not a switch over a name.

## Decision

1. **A profile is data.** `SdJwtProfile` names a profile, the `typ` of its issuer-signed JWT, the claims that must be
   present in the payload as non-empty strings, and the top-level claims that are never selectively disclosable.
   `SdJwtProfiles.SdJwtVc` is the SD-JWT VC profile: `dc+sd-jwt`, `vct` required, and `iss`, `nbf`, `exp`, `cnf`,
   `vct`, `vct#integrity` and `status` protected. An application defines another profile by constructing one.

2. **The issuer and the verifier apply the same profile.** `SdJwtIssuanceOptions.Profile` types the issuer-signed JWT
   with the profile's `typ`, refuses a disclosable path whose top-level claim the profile protects, and refuses
   claims that lack a required claim. `SdJwtVerificationParameters.Profile` refuses an issuer-signed JWT whose `typ`
   is not the profile's, by the media-type rules of RFC 7515 §4.1.9, a payload that lacks a required claim, and a
   disclosure that discloses a protected top-level claim; the check happens as the disclosures are resolved, so a
   protected name nested inside another claim is an ordinary claim. Without a profile the issuer-signed JWT is typed
   `JWT`, and the verifier requires nothing of its `typ` beyond what the caller's JWT validation parameters say.

3. **`status` passes through.** The verifier neither reads nor resolves a status mechanism; the claim reaches the
   verified claims as the issuer wrote it, for a status checker the application brings.

## Options considered

- **A separate SD-JWT VC issuer and verifier.** Rejected: the profile adds rules to the format, and a second service
  pair would copy the format's own rules.
- **A profile registry resolved by name.** Rejected: a profile is passed where it is used, so there is no name to
  resolve and no catalog to validate; an application constructs the profile it needs.
- **Accepting `vc+sd-jwt` beside `dc+sd-jwt`.** Rejected: the draft registers `dc+sd-jwt`, and a profile that accepts
  two types is an application's own `SdJwtProfile`.
- **Resolving the status in the verifier.** Rejected: a Token Status List is fetched and cached over HTTP under the
  application's own policy, which the verifier does not own.

## Consequences

- An SD-JWT VC is issued and verified with the same services as a plain SD-JWT, under one profile object.
- A verifier of credentials from several issuers names the profile once in its parameters.
- A profile cannot express a rule beyond `typ`, required string claims and protected top-level claims; a rule such
  as a required `cnf` when key binding is used is the verifier's key binding requirement, not the profile's.

## References

- [draft-ietf-oauth-sd-jwt-vc](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-sd-jwt-vc),
  [RFC 9901](https://datatracker.ietf.org/doc/html/rfc9901)
- [ADR-1405](1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator.md)
- Issue [#73](https://github.com/NCodeGroup/Identity/issues/73)
- `SdJwtProfile` and `SdJwtProfiles` in `NCode.Identity.Jose.SdJwt`; `JoseTokenTypes.SdJwtVc` and the `vct`,
  `vct#integrity` and `status` names in `JoseClaimNames.Payload` of `NCode.Identity.Jose.Abstractions`
