# 295. The JOSE library does not implement the none algorithm

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

RFC 7518 §3.6 defines `none`, the unsecured JWS: its signature is empty and proves nothing about who produced the
content. RFC 8725 §3.2 tells applications to reject it unless an unsecured token is the explicit intent, and
draft-ietf-jose-deprecate-none-rsa15 asks libraries to deprecate it and applications to disable it by default. The
same draft deprecates `RSA1_5`, which this library no longer implements
([ADR-0282](0282-the-jose-library-does-not-implement-rsa1-5.md)); that ADR left `none` as a separate decision.

The default algorithm registry still registered a `none` signature algorithm. It verified only against the empty key
(a key of zero bits), so a caller verifying with its own keys never accepted an unsigned token by accident
([ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)). Every consumer in the
family already refuses `none` by name: `SettingAlgorithms`, `DefaultJwtValidator`,
`ClientAssertionClientAuthenticationHandler`, `DefaultLoadAuthorizationRequestHandler`, `ProviderSignaturePolicy`, the
protocol client's `ClientRegistration` and `OpenIdClientOptionsValidator`. No component of the family produces or
consumes an unsecured JWS. A registered `none` therefore served no caller of the family and remained one empty-key call
away from accepting a forged token for a direct consumer of `IJoseSerializer`.

## Decision

**The JOSE library implements no `none` signature algorithm.**

1. `DefaultAlgorithmDataSource` does not register `none`, and no registration method adds it. A JWS whose `alg` is
   `none` fails like any unregistered algorithm, with a `JoseInvalidAlgorithmException`: "The `none` algorithm is not
   supported for digital signatures."
2. The name stays. `AlgorithmCodes.DigitalSignature.None` (`"none"`) is the IANA-registered `alg` value that policies
   refuse by name.
3. The guards stay. Every component listed above keeps refusing `none` by name, so a host that registers its own
   unsecured algorithm through `ICollectionDataSource<Algorithm>` never makes the provider advertise or accept it, and
   never makes the JWT validator or the protocol client accept an unsigned token. The JWT validator's tests register a
   viable `none`, so its refusal passes only because of the name rule; the other guards compare the name alone.
4. A consumer that genuinely needs unsecured JWSs implements its own `SignatureAlgorithm` and registers it; the
   vocabulary stays open ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)), and the choice is that
   consumer's explicit risk.

## Options considered

- **Keep it registered, verifying only against the empty key.** The family is safe, but every direct consumer of the
  JOSE packages keeps a registered algorithm that proves nothing and that the draft asks to disable by default.
  Rejected.
- **An opt-in registration method.** The default is safe, but a registration method is a global switch, and nothing
  in the family needs one. Rejected.
- **Remove it.** The default and every composition are safe, the guards keep refusing a host's own `none`, and the
  open vocabulary leaves a legacy need to the consumer. Chosen.

## Consequences

- An unsecured JWS fails to decode with an unsupported-algorithm error instead of a signature failure.
- `ISecretKeyFactory.Empty` stays: it is a general primitive, not part of the algorithm.
- Both algorithms the draft deprecates are absent from the library.

## References

- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)
- [ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)
- [ADR-0282](0282-the-jose-library-does-not-implement-rsa1-5.md)
- RFC 7518 §3.6 (using the algorithm `none`)
- RFC 8725 §3.2 (use appropriate algorithms)
- draft-ietf-jose-deprecate-none-rsa15 (deprecating `none` and `RSA1_5`)
- Issue #379
