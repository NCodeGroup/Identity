# 294. A BFF client is registered with explicit metadata, not a bff security profile

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

The [backend-for-frontend design](../backend-for-frontend.md) §11.1 asked whether the NCode OP should offer a
registered `bff` security profile that provisions a BFF client in one step: confidential `private_key_jwt`, PAR
required, DPoP required, `backchannel_logout_session_required`, rotation, and a refresh-token absolute lifetime aligned
with the BFF session. In this family a security profile is a **ceiling** over security settings, merged down the
settings cascade ([ADR-0108](0108-fapi-security-profile-merge-lattice-ceiling-injection.md),
[ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md)): it forbids weaker values, it does not
provision a client. The BFF itself must work against any standards-compliant provider (design goal G3). Phase 2 (issue
#394) settles the question.

## Decision

**There is no `bff` security profile. A BFF client is an ordinary confidential client that registers the metadata
below; a deployment that wants the OP to enforce a ceiling over such clients selects the existing `fapi2` profile.**

A BFF client registers:

- `token_endpoint_auth_method` `private_key_jwt`, with `jwks_uri` pointing at the BFF's public client keys (the BFF's
  `Bff:ClientAuthentication`); `tls_client_auth` or `self_signed_tls_client_auth` are equally acceptable
  ([ADR-0288](0288-the-protocol-client-authenticates-with-a-tls-client-certificate-from-its-key-ring.md)).
- `redirect_uris` with exactly the BFF's callback, `https://<app>/bff/callback` (exact match).
- `post_logout_redirect_uris` with the absolute forms of the allow-listed `Bff:ReturnUrls` it logs out to.
- `backchannel_logout_uri` `https://<app>/bff/backchannel-logout` and `backchannel_logout_session_required=true`
  ([ADR-0293](0293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md)).
- `require_pushed_authorization_requests=true`; the protocol client pushes whenever the provider offers PAR
  ([ADR-0275](0275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md)).
- `dpop_bound_access_tokens=true` when the BFF runs `Bff:DPoP:Mode` `required`
  ([ADR-0291](0291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md)).
- `id_token_signed_response_alg`, `userinfo_signed_response_alg` and their `_encrypted_response_alg` and `_enc`,
  when set, the same as the BFF's `Bff:IdToken` and `Bff:UserInfo` name
  ([ADR-0301](0301-a-signed-userinfo-response-is-validated-like-an-id-token-under-the-clients-registered-algorithms.md)).
- The `authorization_code` and `refresh_token` grant types and the `offline_access` scope, which the BFF always
  requests.
- A refresh-token absolute lifetime no shorter than the BFF's `Session:AbsoluteLifetime`, so the session does not end
  early on `invalid_grant`.

The reasons:

1. **A profile is a ceiling, not a template.** Everything a `bff` profile would set is either the client's own
   registration metadata (redirect, logout and back-channel URIs, authentication method, key set, lifetimes) or a
   security floor the `fapi2` profile already enforces (confidential clients, PAR, sender-constrained tokens, strong
   algorithms). A `bff` profile would duplicate `fapi2`'s ceiling and add a provisioning concept profiles do not have.
2. **Goal G3.** The BFF works against any OP that publishes standard metadata. A profile at the NCode OP would make a
   BFF deployment look as if it needed the NCode OP, and other providers would have nothing equivalent.
3. **Open vocabularies.** A deployment that wants a stricter ceiling for its BFF clients can still register its own
   profile ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)); the family does not need to ship one.

## Options considered

- **A `bff` security profile (design §11.1).** Rejected for the reasons above.
- **A provisioning template in the management API.** Deferred: a one-step "create a BFF client" flow is a management
  console feature, and it would set the same metadata this ADR lists; it is not needed for the library.
- **Leaving the registration undocumented.** Rejected: a BFF client with `backchannel_logout_session_required=false` or a
  refresh-token lifetime shorter than the session works but misbehaves, so the metadata is part of the contract.

## Consequences

- The BFF documentation lists this metadata as the registration a BFF client needs, at any provider.
- The NCode OP needs no new profile, setting or descriptor for the BFF.
- Making the NCode OP return `refresh_token_expires_in`, which lets the BFF cap a session up front (design §11.2), is a
  separate OP change.

## References

- [Backend-for-frontend design](../backend-for-frontend.md) §3.1 (G3), §5.1 and §11.1
- [ADR-0108](0108-fapi-security-profile-merge-lattice-ceiling-injection.md) — profiles as a merge-lattice ceiling
- [ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md) — profiles down the cascade
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- [ADR-0275](0275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md) — PAR
- [ADR-0288](0288-the-protocol-client-authenticates-with-a-tls-client-certificate-from-its-key-ring.md) — mTLS client authentication
- RFC 7591 (dynamic client registration metadata), RFC 9126 (PAR), RFC 9449 §5.2 (`dpop_bound_access_tokens`)
- Issues #123 and #394
