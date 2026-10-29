# 290. The BFF keeps each session as an encrypted server-side record under a cookie handle

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

The [backend-for-frontend design](../backend-for-frontend.md) (issue #123) makes an ASP.NET Core application the
confidential OAuth client of its browser frontend: the browser holds only an `HttpOnly` cookie, and the tokens stay on
the server. Phase 2 (issue #394) builds the core of that library on the protocol client
([ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md)) and on the versioned
expiring values and membership indexes of SharedState
([ADR-0267](0267-replicas-share-versioned-expiring-values-and-membership-indexes.md)). The design left open how the
packages are named (§11.5), how a session record is keyed, encrypted and laid out, how the login is bound to the
browser that started it, and what the session principal holds. This ADR records those choices. The refresh, the
cookie acceptance rules and logout have their own ADRs
([ADR-0291](0291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md),
[ADR-0292](0292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md),
[ADR-0293](0293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md)).

## Decision

**A session is an opaque random handle in a `__Host-` cookie; the server keeps the session as a record whose store key
and encryption key are both derived from that handle, so neither the store nor a replacement store can read a session
without the cookie.**

1. **Packages.** `NCode.Identity.Bff.Abstractions` holds the contracts and `NCode.Identity.Bff` the implementation
   (§11.5). `OpenId.*` names protocol concerns, the OP and the protocol client; the BFF is an application pattern that
   spans login, cookie sessions, CSRF and token forwarding, so it is not under `OpenId`. The abstractions reference the
   ASP.NET Core shared framework, because their contracts take `HttpContext` and `BffApiAttribute` derives from
   `AuthorizeAttribute`. `NCode.Identity.Bff` references only abstractions of the family
   ([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)): the host registers the protocol client, the events
   pipeline and the shared-state adapters. `AddBffLibrary(events)` extends `IServiceBuilder<OpenIdClientLibrary>` and
   mints `IServiceBuilder<BffLibrary>`; `AddBffConfiguration(configuration)` binds the `Bff` section strictly. The
   `NCode.Identity.Bff.Server` composition root, the one package that references the implementations, composes all of it
   with `AddIdentityBff(configuration)`, as `AddIdentityServer()` composes the OP.
2. **The handle.** 32 random bytes in unpadded base64url (43 characters), minted afresh at every login. A cookie value of
   any other length is ignored.
3. **The store key** is base64url(HKDF-SHA-256(handle, info `ncode.bff.session.id`, 32 bytes)). The store never sees the
   handle, so a leaked store key does not open the cookie.
4. **The payload** (claims, ID token, refresh token, access tokens, the DPoP `jkt` and the logout guard) is encrypted
   with AES-256-GCM under HKDF(handle, `ncode.bff.session.payload`), stored as a 12-byte nonce, the ciphertext and a
   16-byte tag. The associated data is UTF-8(store key), the record version as 8 bytes big-endian, and the metadata
   JSON. A plaintext metadata field changed in the store, such as an expiry pushed out, therefore fails decryption, and
   so does a record moved to another key or replayed at another version.
5. **The stored value** is format byte `1`, the metadata length as 4 bytes big-endian, the metadata JSON (snake_case,
   with an extension-data bag), then the protected payload. The metadata (frontend, issuer, subject, `sid`, created,
   last-accessed, idle and absolute expiry) stays readable, because back-channel logout and expiry need it without the
   handle.
6. **The store seam.** `IBffSessionStore` is a public, replaceable service. It holds opaque records, the `sid` and
   `sub` membership indexes, login transactions and refresh leases, so a replacement only ever sees ciphertext and
   cannot skip the encryption. The default store prefixes its keys `bff:session:`, `bff:txn:` and `bff:lease:`. Index
   keys are `bff:sid:` or `bff:sub:` plus base64url(SHA-256) of the length-prefixed issuer and value, so a value an
   attacker supplies cannot make a key ambiguous or unbounded. Any adapter exception surfaces as
   `BffStoreUnavailableException`, which is answered with `503`, never `401`
   ([ADR-0292](0292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md)).
7. **The login transaction** (issuer, redirect URI, PKCE verifier, nonce, return URL, the DPoP `jkt` and `max_age`) is
   stored under base64url(HKDF(state, `ncode.bff.login.id`)) and encrypted under HKDF(initiator secret,
   `ncode.bff.login.payload`), with the store key as associated data. The initiator secret is 32 random bytes in the
   `__Host-Http-bff-login` cookie (`SameSite=Lax`, because the callback is a cross-site navigation; ten-minute
   `Max-Age`). The callback **takes** the transaction atomically (`TryTakeAsync`) **before** it decrypts it, so a
   `state` is single-use on every replica and a wrong initiator cannot probe a transaction twice. The callback deletes
   the login cookie on every path, checks `iss` (RFC 9207) before it exchanges the code, and always mints a new handle.
   A provider that cannot be reached or read at login or at the callback (its metadata, its keys or its token
   endpoint, an `OpenIdProviderException`, an `HttpRequestException` or a timeout the caller did not cause) fails the
   login as `provider_unavailable`, the classification the refresh, logout and back-channel logout share.
8. **The session cookie** is `__Host-Http-bff`: `Secure`, `HttpOnly`, `Path=/`, no `Domain`, `SameSite=Strict` by
   default (`Lax` selectable, `None` refused, design D4), and a `Max-Age` that ends with the session.
9. **Lifetimes.** `Session:AbsoluteLifetime` is 8 hours and `Session:IdleTimeout` 1 hour. The stored value expires at
   the earlier of the idle and absolute expiry, and the index members at the absolute expiry. The idle window slides
   only on requests to `[BffApi]` endpoints, never on `/bff/user`, and only once a tenth of `IdleTimeout` has passed
   since the last write, so polling the user endpoint cannot keep a session alive and a busy session does not write on
   every call. Every session write is a read-modify-write with `TryReplaceAsync`, retried up to five times on a
   version conflict.
10. **The session principal** is the ID token's claims as the family's JWT stack maps them
    ([ADR-0122](0122-jwt-claim-values-map-to-json-by-declared-type.md)): the protocol client exposes
    `IdTokenValidationResult.Identity`, so the BFF never maps JSON to claims itself. The BFF drops `nonce`, `at_hash`,
    `c_hash`, `s_hash`, `exp`, `iat`, `nbf`, `aud` and `azp`, stores each remaining claim as its type, value and value
    type, and rebuilds `ClaimsIdentity(claims, "Bff", "name", "role")` per request. `/bff/user` returns `sub`, `name`,
    the claims that `UserClaims` lists, and the session's expiries and logout URL.
11. **Seams.** `IBffLoginGate` is a strategy collection that can only reject a login after the ID token validates, and
    `IBffSessionValidator` one that can only end a session (`ended_by_validator`). `IBffSessionClaimContributor` shapes
    a session's claims once, at login, after the ID token validates and before the gates, so a gate judges the session
    as it will be created; its `BffSessionClaimContext` refuses to add or remove `iss`, `sub`, `sid`, `auth_time`, `acr`
    and `amr`, which anchor the session to the provider's authentication. `IBffUserContributor` adds fields to
    `/bff/user` on each request through `BffUserContext`, which refuses the reserved `sub`, `name`, `claims`, `session`
    and `logoutUrl` and a name already added; the contributors run before the response starts. UserInfo is later work.12. **Audit.** One public record, `BffAuditEvent : AuditEvent, ISupportAuditDetails`, with event source `bff` and the
    names in `BffAuditEventNames`, records logins, session ends, refresh failures, back-channel logouts and CSRF
    refusals ([audit records](../audit-records.md)). No token, handle, cookie value, state, code or secret is ever
    logged or audited.
13. **One frontend.** Phase 2 configures one frontend at the root of `BffOptions`, and every session records
    `Frontend = "default"`, so several frontends (phase 4) need no data migration.

## Options considered

- **An encrypted cookie that carries the tokens (design D1 (b)).** Rejected: two parallel requests hold the same
  refresh token, so it cannot be made correct with rotation and reuse detection, and back-channel logout would need a
  shared deny-list anyway.
- **ASP.NET Core Data Protection for the cookie and the login state (design D2 (b)).** Rejected: it needs a shared,
  persistent, rotated key ring that silently breaks every other replica's cookies when it is missing, and it makes
  single-use `state` a per-replica check instead of an atomic take.
- **The BFF maps the ID token's JSON to claims itself.** Rejected: it would be a second claim mapping beside the JWT
  stack's, and the two would drift.
- **`NCode.Identity.OpenId.Bff`.** Rejected: the BFF is an application pattern, not a protocol concern.
- **A store key equal to the handle, or encryption under a server-wide key.** Rejected: either one lets whoever reads
  the store read or replay every session.

## Consequences

- A store dump, a store operator or a replacement `IBffSessionStore` sees only ciphertext and digests.
- Losing the cookie loses the session: nothing on the server can recover it, by design.
- A host composes the BFF in one call, `AddIdentityBff(configuration)` from `NCode.Identity.Bff.Server`, or composes
  the six libraries (secrets, JOSE, JWT, protocol client, events and shared state) itself before `AddBffLibrary`.
- `IdTokenValidationResult.Identity` is new public surface of the protocol client.

## References

- [Backend-for-frontend design](../backend-for-frontend.md) §5.2–§5.4, §5.7, §5.11, §11.5, D1, D2 and D4
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — abstractions-only dependencies
- [ADR-0122](0122-jwt-claim-values-map-to-json-by-declared-type.md) — claim mapping
- [ADR-0267](0267-replicas-share-versioned-expiring-values-and-membership-indexes.md) — versioned values and indexes
- [ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) — the protocol client
- RFC 5869 (HKDF), RFC 6265bis (`__Host-` prefix, `SameSite`), RFC 9207 (`iss`), RFC 7636 (PKCE)
- Issues #123 and #394
