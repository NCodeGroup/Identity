# 1294. A BFF client is registered with explicit metadata, not a bff security profile

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

The [backend-for-frontend design](../backend-for-frontend.md) §11.1 asks whether the NCode OP should offer a
registered `bff` security profile that provisions a BFF client in one step: confidential `private_key_jwt`, PAR
required, DPoP required, `backchannel_logout_session_required`, rotation, and a refresh-token absolute lifetime aligned
with the BFF session. In this family a security profile is a ceiling over security settings, merged down the settings
cascade ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)):
it forbids weaker values, it does not provision a client. The BFF itself must work against any standards-compliant
provider (design goal G3).

## Decision

**There is no `bff` security profile. A BFF client is an ordinary confidential client that registers the metadata
below; a deployment that wants the OP to enforce a ceiling over such clients selects the `fapi2` profile or registers
its own profile.**

Each frontend registers its own confidential client, with these values:

- `token_endpoint_auth_method` is `private_key_jwt`, and `jwks_uri` is `<PublicOrigin><PathPrefix>/jwks`, which the
  frontend serves when `Jwks:Enabled` is set. A provider that cannot reach that URI gets the frontend's keys by value.
  `tls_client_auth` and `self_signed_tls_client_auth` are equally acceptable
  ([ADR-1300](1300-the-protocol-client-authenticates-with-a-key-rings-tls-client-certificate-and-its-issuers-certificates.md)).
- `redirect_uris` holds exactly the frontend's callback, `PublicOrigin + PathPrefix + "/callback"`, compared exactly.
- `post_logout_redirect_uris` holds the absolute forms of the frontend's `ReturnUrls`.
- `backchannel_logout_uri` is the frontend's `/backchannel-logout`, and `backchannel_logout_session_required` is
  `true` ([ADR-1293](1293-the-bff-ends-sessions-by-a-guarded-logout-form-post-and-by-back-channel-logout-through-indexes.md)).
- `require_pushed_authorization_requests` is `true`; the protocol client pushes whenever the provider offers PAR
  ([ADR-1275](1275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md)).
- `dpop_bound_access_tokens` is `true` when the frontend's `DPoP:Mode` is `required`
  ([ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md)).
- The ID token and UserInfo signing and encryption algorithms (`id_token_signed_response_alg`,
  `userinfo_signed_response_alg` and their `_encrypted_response_alg` and `_enc`), when set, match the frontend's
  `IdToken` and `UserInfo` settings
  ([ADR-1301](1301-the-protocol-client-reads-userinfo-as-json-or-a-registered-jwt-under-the-familys-claim-mapping.md)).
- The grant types are `authorization_code` and `refresh_token`. The `offline_access` scope is registered only when the
  frontend lists it in `Scopes`.
- The refresh-token absolute lifetime is no shorter than the frontend's `Session:AbsoluteLifetime`, so the session does
  not end early on `invalid_grant`.

The reasons:

1. **A profile is a ceiling, not a template.** Everything a `bff` profile would set is either the client's own
   registration metadata (redirect, logout and back-channel URIs, authentication method, key set, lifetimes) or a
   security floor the `fapi2` profile already enforces (confidential clients, PAR, sender-constrained tokens, strong
   algorithms). A `bff` profile would duplicate `fapi2`'s ceiling and add a provisioning concept profiles do not have.
2. **Goal G3.** The BFF works against any standards-compliant provider, so the NCode OP needs no BFF-specific profile,
   setting or descriptor. A profile at the NCode OP would make a BFF deployment look as if it needed the NCode OP, and
   other providers would have nothing equivalent.
3. **Open vocabularies.** A deployment that wants a stricter ceiling for its BFF clients can register its own profile
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)); the family does not need to ship one.

## Options considered

- **A `bff` security profile (design §11.1).** Rejected: it would duplicate `fapi2` and imply the NCode OP.
- **A provisioning template in the library.** Rejected: a one-step "create a BFF client" flow is a console feature that
  would set this same metadata, and the library does not need it.
- **Leaving the registration undocumented.** Rejected: a BFF client with `backchannel_logout_session_required=false` or
  a refresh-token lifetime shorter than the session works but misbehaves, so the metadata is part of the contract.
- **Explicit metadata, documented as the contract (chosen).** Each frontend registers the values above at any provider.

## Consequences

- The BFF documentation lists this metadata as the registration a BFF client needs, at any provider.
- The NCode OP needs no profile, setting or descriptor for the BFF.
- A deployment that wants an OP-enforced ceiling over BFF clients selects `fapi2` or registers its own profile.
- The OP announces the refresh-token family's horizon in the token response
  ([ADR-1297](1297-a-refresh-token-family-expires-at-a-fixed-horizon-that-the-token-response-announces.md)).

## References

- [Backend-for-frontend design](../backend-for-frontend.md) §3.1 (G3), §5.1 and §11.1
- [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md): profiles as ceilings down the cascade
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md): open vocabularies
