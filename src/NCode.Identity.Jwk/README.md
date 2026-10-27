# NCode.Identity.Jwk

First-class **JSON Web Key** (JWK, RFC 7517), **JSON Web Key Set** (JWKS, RFC 7517 §5), and **JWK Thumbprint** /
**Thumbprint URI** (RFC 7638 / RFC 9278) types for the NCode.Identity family — a rich, serializable key model
(`JsonWebKey`, `JsonWebKeySet`) shared by the JOSE and Secrets libraries.

## Why there is no `NCode.Identity.Jwk.Abstractions` package

This is intentional, and it is the one place in the family where a type is deliberately **not** split into a contracts
package. If this comes up again, the decision has already been made — see
[ADR-0119](../../docs/adr/0119-first-class-jwk-jwks-and-thumbprint.md) for the full record. In short:

- **Our `*.Abstractions` packages hold contracts only — no logic.** That rule is what the whole Abstractions/impl split
  exists to enforce.
- **A JWK is a genuinely *used*, behavior-bearing type**, not a dumb DTO. It produces its typed members, computes the
  RFC 7638 canonical thumbprint, and is consumed to verify proofs and assemble a JWKS. That behavior must not live in an
  abstractions package — so a JWK type cannot simply be dropped into one.
- **A "dumb DTO" JWK was tried and rejected as duct tape.** Making `JsonWebKey` a logic-free DTO (typed members +
  `[JsonExtensionData]`) in an abstractions package satisfied the no-logic rule on paper but left every real consumer
  still hand-rolling `JsonObject` / `JsonElement` and converting only at the serialization edges. The type was bolted
  on, not used.
- **It must be first-class in *both* the Secrets and JOSE stacks.** `Secrets.Abstractions` is the lowest shared layer
  (`Jose.Abstractions` depends on it) and is where the descriptor seam that produces JWK members lives. So a single
  shared JWK type cannot live in `Jose.Abstractions` (Secrets could not see it), and putting a behavior-bearing type in
  `Secrets.Abstractions` would violate the no-logic rule.

The resolution is to ship the rich, behavior-bearing JWK model as **its own dedicated, logic-bearing library** —
neither an abstractions package nor folded into Secrets or JOSE. Consumers that need JWK depend on this package
directly; there is no contracts-only companion to depend on, by design.

## Contributing a key type

A `JsonWebKey` carries a `JsonWebKeyTypeDescriptor` that names its thumbprint members and its private members. The IANA
key types (`RSA`, `EC`, `OKP`, `oct` and `AKP`) are built in as `JsonWebKeyTypeDescriptors`. A contributed key type
returns its own descriptor from `ISecretKeyTypeDescriptor.JwkKeyType`, and code that resolved it constructs
`new JsonWebKey(element, descriptor)` so the key uses that type's rules. The standard private member names stay private
for every key type. See [ADR-0271](../../docs/adr/0271-a-key-type-registers-its-json-web-key-rules.md).
