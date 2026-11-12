# 1497. A registered key set names a key without a kid by its thumbprint

- **Status:** Accepted (amends [ADR-1161](1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md) decision 3)
- **Refines:** [ADR-1161](1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md)
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

A client registers its public keys by value in the `jwks` member of a dynamic client registration request (RFC 7591,
[ADR-1192](1192-dynamic-client-registration-rides-the-client-and-grant-stores-authorized-by-single-use-initial-access-tokens.md)). OpenID Connect Dynamic Client Registration 1.0 and RFC 7517 §4.5 make
the `kid` of a key optional, and a client selects among the keys of its set by `kid` when it has more than one.
[ADR-1161](1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md) decision 3 requires a `kid`
on every key of a registered or published set, so a registration whose key has none is refused with
`invalid_client_metadata`.

- **The conformance suite registers a key without a `kid`.** The OpenID Foundation conformance suite's registration
  request carries an RSA key that has no `kid`; the suite drops it from its own configured key. The Basic OP plan
  registers a client for each module, so every registration is refused and the plan fails before its first test.
- **A published set may serve other parties.** A set a client publishes at its `jwks_uri` can carry keys meant for
  parties other than the server, and RFC 7517 §5 has a consumer ignore a key it cannot use, so a published key without a
  `kid` is ignored
  ([ADR-1161](1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md) decision 5). A registered
  set is addressed to the server alone, so every key in it is the client's to name.
- **A stored key needs a stable name.** A stored key is selected by its `KeyId`, and without one by its secret
  identifier, an opaque value the server generates and a client never sees.

## Decision

**A key of a registered key set that declares no `kid`, or an empty one, is named by its RFC 7638 thumbprint.**

1. **The thumbprint is the name.** A key is unnamed when its `kid` is absent, JSON `null` or an empty string. The server
   computes the SHA-256 thumbprint of an unnamed key's public members, base64url encoded (RFC 7638), and stores it as
   the key's `KeyId`. Any holder of the key computes the same value, so the client selects the key by a name it can
   derive. The registration response and the management API render the thumbprint as the key's `kid`.
2. **A key that declares a `kid` keeps it.** A `kid` that is not a string is left to the member validation, which
   reports it. A key of a `kty` the server has no rules for is refused as unsupported and never named.
3. **The set stays all or nothing and its `kid`s stay unique.** Two keys that name the same `kid`, explicitly or by
   thumbprint (the same key twice, or a key and another key whose declared `kid` is its thumbprint), are refused with
   the duplicate-`kid` error, and one refused key refuses the set.
4. **The other entry points keep their rules.** A published set ignores a key without a `kid`, and the management API's
   single-key import keeps the JWK's `kid` or selects a key from PEM or a certificate by its secret identifier.

## Options considered

- **Accept a key without a `kid` and store none.** Rejected: a stored key then has no stable name, and selection falls
  back to the opaque secret identifier, which a client never sees.
- **An opaque server-generated `kid`.** Rejected: the client cannot derive it, and the server would invent a name for a
  key the client did not name.
- **A `kid` added by the harness.** Rejected: the conformance suite removes the `kid` from its registration request, so
  a harness cannot supply one.

## Consequences

- A registration whose `jwks` has a key without a `kid` succeeds, and the response's `jwks` shows the key with its
  thumbprint as `kid`.
- A client that registers the same key without a `kid` twice, or alongside a key that declares its thumbprint as its
  `kid`, is refused with `invalid_client_metadata`.
- A key's thumbprint is its name for as long as the key keeps the members the thumbprint covers; a client that names its
  keys itself is unaffected.
- The published-set path and the management import keep their `kid` rules.

## References

- Issue [#124](https://github.com/NCodeGroup/identity/issues/124)
- Issue [#129](https://github.com/NCodeGroup/identity/issues/129)
- [ADR-1161](1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md)
- [RFC 7638](https://www.rfc-editor.org/rfc/rfc7638)
- [RFC 7517](https://www.rfc-editor.org/rfc/rfc7517)
