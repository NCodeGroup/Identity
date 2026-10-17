# 119. First-class JWK / JWKS / thumbprint live in a dedicated logic-bearing package

- **Status:** Accepted
- **Date:** 2026-10-16
- **Deciders:** NCode.Identity maintainers

## Context

Backlog item `F33` asked for first-class **JSON Web Key** (RFC 7517), **JSON Web Key Set** (RFC 7517 §5), and
**JWK Thumbprint** / **Thumbprint URI** (RFC 7638 / RFC 9278) types. Before this, a key's JWK form existed only as a
raw `System.Text.Json.Nodes.JsonObject` produced by the secret-key-type descriptor
([ADR-0105](0105-secret-key-type-descriptor-first-class-seam.md)), the JWKS endpoint hand-assembled a result object of
`JsonObject` keys, and the DPoP proof validator computed the `jkt` thumbprint from a **hand-rolled canonical JSON
string** (`$$"""{"e":"…","kty":"RSA","n":"…"}"""`) — three separate, duplicative treatments of the same concept.

Two constraints shaped the design, and they pull against each other:

1. **`*.Abstractions` packages contain contracts only — no logic.** A JWK that is genuinely _used_ by the codebase
   (verify a proof, produce members, hash a canonical form) needs behavior, and behavior must not live in an
   abstractions package.
2. **JWK must be first-class in _both_ the Secrets and JOSE stacks.** `Secrets.Abstractions` is the lowest shared
   layer (`Jose.Abstractions` depends on it), and the descriptor seam that produces JWK members lives in Secrets. So a
   single shared JWK type cannot live in `Jose.Abstractions` (Secrets can't see it) — and putting a _behavior-bearing_
   type in `Secrets.Abstractions` violates constraint 1.

A first attempt made `JsonWebKey` a "dumb DTO" (typed members + `[JsonExtensionData]`) in `Secrets.Abstractions` with a
separate `IJsonWebKeyThumbprintCalculator` service. That satisfied the no-logic rule but produced **duct tape**: a dumb
DTO is not a usable working type, so the descriptors kept building `JsonObject` and the DPoP validator kept parsing a
`JsonElement` by hand, converting to the DTO only at the serialization edges. The type was bolted on, not used.

## Decision

**Ship the rich, behavior-bearing JWK model as its own dedicated library, `NCode.Identity.Jwk`** — not an abstractions
package, not folded into Secrets or JOSE.

1. **Dedicated package.** `NCode.Identity.Jwk` holds `JsonWebKey`, `JsonWebKeySet`, and `JsonWebKeyParameterNames`.
   JWK/JWKS/thumbprint is a self-contained RFC domain (7517 / 7638 / 9278), distinct from JOSE signing/encryption
   (7515 / 7516) and from key _storage_ (Secrets). Both `Secrets.Abstractions` and `Jose.Abstractions` **reference**
   it. Referencing a model library is fine — the rule is that no abstractions package _contains_ logic, and none does;
   the logic lives in `NCode.Identity.Jwk`. This is the layering the constraints actually require, and it keeps each
   existing package focused.

2. **Backed by an immutable `JsonElement`, not a `JsonObject`.** `JsonElement.Parse` / `JsonElement.ParseValue`
   (net9+) return a **detached, immutable** element that owns no `JsonDocument` — nothing to dispose, nothing to clone.
   That is the natural backing store for an immutable model; the earlier `JsonObject` + defensive `DeepClone` pattern
   is gone.

3. **The thumbprint is a method on the type, not a service.** `JsonWebKey.ComputeThumbprint()` /
   `ComputeThumbprintUri()` compute the RFC 7638 / RFC 9278 fingerprint from the key type's required members (RSA
   `e,kty,n`; EC `crv,kty,x,y`; OKP `crv,kty,x`; oct `k,kty`; AKP `kty,pub`). Because the type is reachable everywhere
   (both abstractions stacks reference the package), no DI seam is needed; the `IJsonWebKeyThumbprintCalculator` service
   from the first attempt is deleted.

4. **Cohesive construction surface.** In-memory nodes construct via **public constructors** —
   `JsonWebKey(JsonElement)` and `JsonWebKey(JsonObject)`, `JsonWebKeySet(JsonElement)` and
   `JsonWebKeySet(IEnumerable<JsonWebKey>)`; JSON **text** parses via the static `Parse(string)`. (The verb split is
   the rule: a constructor adopts an in-memory value, `Parse` deserializes text.)

5. **The descriptor seam is unchanged; `JsonWebKey` is a generic wire model, not a per-family converter.** ADR-0105
   deliberately removed a per-key-type `IJsonWebKeyConverter` / typed `RsaJsonWebKey`/`EccJsonWebKey` registry. This
   does **not** reintroduce it: the descriptor still owns per-key-type member production (it assembles the `JsonObject`
   of members), and `JsonWebKey` is a single generic type that merely wraps those members and adds parse / serialize /
   thumbprint. `ExportedPublicKey.Jwk` becomes a `JsonWebKey`, the JWKS endpoint returns a `JsonWebKeySet` (the old
   `JsonWebKeySetResult` is retired), the management public-key read path exposes the element directly
   (`JsonWebKey.AsElement()`), and the DPoP validator parses the embedded proof key into a `JsonWebKey` and calls
   `ComputeThumbprint()` — one representation, no hand-rolled canonical form.

## Consequences

- A new published package (`NCode.Identity.Jwk`) and its test project join the family; governance/versioning follow
  the lockstep model ([ADR-0003](0003-central-package-management-and-lockstep-versioning.md)).
- The JWK concept has one home and one representation used end-to-end, eliminating the three prior duplicative
  treatments and the fragile string-interpolated DPoP thumbprint.
- Adding a new key type's thumbprint support is a one-line entry in the required-members table (or, for a custom key
  type, falls out of the same generic path once its members are present).
