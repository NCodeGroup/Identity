# 286. The protocol client binds tokens with DPoP whenever the provider supports it

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

The backend-for-frontend ([design](../backend-for-frontend.md) §5.1, §5.5 and D7, issue #123) binds its tokens with
DPoP (RFC 9449): one key ring is shared by the whole BFF, the `jkt` is stored per session, and a `use_dpop_nonce`
challenge is retried. [ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md)
and [ADR-0275](0275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md) left DPoP
to phase 1b. The JOSE stack already provides `IDPoPProofService`
([ADR-0176](0176-dpop-proofs-are-a-jose-building-block.md)), and the OP keeps a refresh token's
binding continuous across rotation ([ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md)). This ADR records
how `NCode.Identity.OpenId.Client` creates the proofs, pins the keys, sends `dpop_jkt` and handles nonces.

## Decision

**The protocol client signs a DPoP proof for every token request, and sends `dpop_jkt` with every authorization request,
whenever the provider supports DPoP and the client has a key for it; it requires DPoP when the registration or a pinned
key says so, and it never accepts a downgrade.**

1. **A dedicated, rotatable key ring.** `ClientRegistration.DPoPKeyRing` names a keyed `ISecretKeyCollectionProvider`,
   as `KeyRing` does, and only its `AsymmetricSecretKey`s are candidates. The ring is shared by the whole BFF rather
   than created per session (D7), so the key lives apart from the tokens. The ring may be the same as `KeyRing`, but a
   separate ring is recommended. With no `DPoPKeyRing`, the client sends no proofs, unless DPoP is required.
2. **`when_supported`, unless required.** DPoP applies when the provider advertises `dpop_signing_alg_values_supported`
   and the ring has a key that can sign one of those algorithms. `ClientRegistration.DPoPBoundAccessTokens` mirrors the
   client metadata `dpop_bound_access_tokens` (RFC 9449 §5.2), as `RequirePushedAuthorizationRequests` mirrors its
   metadata, and makes DPoP required when `true`. A pinned thumbprint (point 4) also makes it required. Required with no
   ring, an empty ring or an unknown pinned key throws `InvalidOperationException`, a configuration error
   ([ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md)). Required while the
   provider advertises no usable algorithm throws `OpenIdProviderException`. In every failure nothing is sent.
3. **Algorithm choice.** The client's order is `ES256`, `PS256`, `ES384`, `PS384`, `ES512`, `PS512`, filtered to the
   provider's advertised list and followed by the provider's other values except `none`, as `PrivateKeyJwtClientAuthenticator`
   does. Only asymmetric keys are candidates, so a MAC is never chosen. No option is added on `OpenIdClientOptions`: the
   ring's key type decides.
4. **A key is pinned by its `jkt`.** The `jkt` is the RFC 7638 SHA-256 thumbprint that the OP binds as `cnf.jkt`.
   `TokenRequest.DPoPJwkThumbprint` pins the key, so a session keeps the key it was bound with across rotation (design
   §5.5, [ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md)). `TokenResult.DPoPJwkThumbprint` and
   `AuthorizationRequestResult.DPoPJwkThumbprint` report the `jkt` used, and are `null` when no proof or `dpop_jkt` was
   sent. Thumbprints are computed per call, which is cheap for the few keys a ring holds.
5. **Token endpoint.** Every token request carries a fresh proof whenever DPoP applies: `htm` is `POST`, `htu` is the
   endpoint, and there is no `ath`. A successful response must carry `token_type` `DPoP`, compared case-insensitively
   (RFC 6749 §5.1); anything else is a downgrade and throws `OpenIdProviderException`.
6. **Authorization request.** Whenever DPoP applies, the client writes `dpop_jkt` (RFC 9449 §10), on the front channel
   and in the PAR body alike, after the `resource` values and before the additional parameters. `dpop_jkt` becomes a
   reserved name in `AuthorizationRequest.AdditionalParameters`. The PAR push carries no `DPoP` header: RFC 9449 §10.1
   allows either one, and `dpop_jkt` works on both channels.
7. **Nonces** (RFC 9449 §8). A `DPoP-Nonce` header on any response to a request with a proof is recorded for that
   request's origin (`Uri.GetLeftPart(UriPartial.Authority)`). The cache is in-process and holds at most 1024 origins;
   when a new origin would exceed that, the cache is cleared first. It is not shared across replicas: a stale nonce
   costs only one retry. A response carrying the `use_dpop_nonce` OAuth error together with a nonce is retried exactly
   once, with a new proof. The retry rebuilds the form from a fresh copy of the caller's parameters and runs client
   authentication again, so a `private_key_jwt` assertion is never replayed.
8. **Resource proofs** (RFC 9449 §7). The public `IDPoPProofFactory` serves the BFF's resource calls.
   `CreateResourceProofAsync(registration, jwkThumbprint, method, requestUri, accessToken, ct)` returns a proof with
   `ath` and the cached nonce for the request's origin, with the key pinned and the algorithm chosen as above.
   `RecordNonce(requestUri, nonce)` stores a resource server's `DPoP-Nonce`. The retry on `use_dpop_nonce` belongs to
   the caller.
9. **No DPoP on revocation.** Neither RFC 7009 nor RFC 9449 defines a proof there.
10. **Rename.** `OpenIdProviderMetadata.DpopSigningAlgValuesSupported` becomes `DPoPSigningAlgValuesSupported`, to match
    `OpenIdProtocol` and `IDPoPProofService`.

## Options considered

- **A key per session.** Rejected: D7 forbids it, because it stores the key next to the tokens.
- **Reusing `KeyRing` without a separate name.** Rejected: it mixes key purposes and blocks independent rotation.
- **Retrying `use_dpop_nonce` more than once, or replaying the same form.** Rejected: it would replay a client assertion.
- **Sharing nonces through SharedState.** Rejected: a stale nonce costs only one retry.
- **Accepting a `Bearer` response to a proof.** Rejected: it is a silent downgrade.
- **A proof on the PAR push instead of `dpop_jkt`.** Rejected: `dpop_jkt` covers both channels.

## Consequences

- mTLS client authentication remains.
- The BFF's handler and forwarder (phases 2 and 3) use `IDPoPProofFactory` and retry resource-server nonces.
- `AuthorizationRequest.AdditionalParameters` no longer accepts `dpop_jkt`; the client writes it.

## References

- [ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md) — refresh-token binding
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- [ADR-0176](0176-dpop-proofs-are-a-jose-building-block.md) — the JOSE DPoP service
- [ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) — the protocol client
- [ADR-0275](0275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md) — PAR
- RFC 9449 §4, §5, §5.2, §7, §8, §10 and §10.1 (DPoP)
- RFC 7638 (JWK thumbprint)
- RFC 6749 §5.1 (token response)
- Issue #123
