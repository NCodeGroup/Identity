# 74. End-user sessions, the `sid` claim, and RP-Initiated + Back-Channel Logout

- **Status:** Accepted (the logout revocation scope is refined by [ADR-0077](0077-session-scoped-grant-revocation-on-logout.md):
  logout revokes the terminating session's grants, not the whole subject's). The outright decline of the two
  iframe-based mechanisms below is **superseded by [ADR-0088](0088-opt-in-front-channel-logout-and-session-management.md)**,
  which implements Front-Channel Logout 1.0 and Session Management 1.0 as opt-in capabilities that are available but
  disabled by default.
- **Date:** 2026-10-11
- **Deciders:** NCode Group

## Context

An OpenID Provider (OP) that only issues tokens is only half of a login system; the other half is **ending** a
login. Four OpenID Connect specifications cover the logout surface, and they are **not** equal in long-term quality for
a greenfield 2026 OP:

- **RP-Initiated Logout 1.0** — a relying party (RP) redirects the user agent to the OP's `end_session_endpoint` with
  an `id_token_hint`, optional `post_logout_redirect_uri`, and `state`; the OP clears its own login and redirects back.
  Robust, user-agent-mediated, no cross-site cookies.
- **Back-Channel Logout 1.0** — on logout the OP `POST`s a signed `logout_token` (a JWT carrying a `sid` and/or `sub`
  and the `http://schemas.openid.net/event/backchannel-logout` event) **server-to-server** to each participating RP's
  `backchannel_logout_uri`. Robust, cookie-independent, and the modern best practice for terminating sessions across RPs.
- **Front-Channel Logout 1.0** — the OP renders one hidden `<iframe>` per RP so each RP clears its own cookie in the
  user agent. Depends on the browser sending each RP's **third-party cookie** inside an iframe.
- **Session Management 1.0** (`check_session_iframe` / `session_state`) — an RP polls an OP-hosted iframe via
  `postMessage` to detect session changes. Also depends on third-party cookies in an iframe.

The forcing function is the browser platform: Safari (ITP) blocks third-party cookies outright, Firefox partitions
them, and Chrome is deprecating them. Both iframe-based mechanisms (Front-Channel Logout and Session Management) are
therefore unreliable by construction going forward, while Back-Channel Logout — a direct server-to-server POST — is
unaffected.

None of this exists yet. There is no `end_session_endpoint`, no session record, no logout notification, and the
`sid` claim — already defined as `JoseClaimNames.Payload.Sid` and documented as "requires a session-management
subsystem" by the token claims handlers — is never emitted. The interactive sign-in path mints a cookie principal
(`sub`, `auth_time`, `tid`, …) via `SignInAsync`, and the only logout today is a bare `SignOutAsync` that clears the OP
cookie with no server-side invalidation and no RP notification. Server-side revocation already exists for tokens:
`IGrantStore.RevokeWhereAsync(subjectId, clientId, …)` bulk-revokes a subject's grants, and `IPersistedGrantService`
persists "any type of grant and its payload" as a tenant-scoped, hash-keyed, expiring, status-bearing `PersistedGrant`
envelope.

## Decision

**Introduce a first-class end-user _session_ as a `PersistedGrant` payload keyed by an opaque `sid`, emit the `sid`
claim from it, and build the two cookie-independent logout mechanisms — RP-Initiated Logout (the `end_session_endpoint`)
and Back-Channel Logout (the signed `logout_token` POST). Deliberately _decline_ the two iframe/third-party-cookie
mechanisms (Front-Channel Logout and Session Management `check_session_iframe`), behind a clean extensibility seam, and
record that omission as an evaluated deferral rather than a silent gap.** No new persistence table, store, or EF
migration is introduced — the session rides the existing grant store, consistent with
[ADR-0059](0059-device-authorization-grant.md).

Concretely:

- **A session is a persisted grant.** A login is a `PersistedGrant` of type `PersistedGrantTypes.Session`, keyed by an
  opaque, 256-bit `sid`, whose JSON payload (`SessionGrant`) carries the `SubjectId`, `auth_time`, the authentication
  method references (`amr`), the identity provider (`idp`), and the **set of participating client ids** (the RPs the
  session has issued tokens to — the back-channel logout notification list). The row is tenant-scoped, hash-keyed, and
  expiring exactly like every other grant, so it reuses the entire grant store, EF entity/configuration, concurrency
  handling, and tenant scoping unchanged. The session is created at interactive sign-in and its `sid` is minted into the
  OP cookie principal (as the `sid` claim and on the `AuthenticationProperties`) so every subsequent authorize request
  resolves the same session.
- **`sid` is emitted from the session.** The ID-token payload claims handler reads the `sid` from the authenticated
  subject and emits `JoseClaimNames.Payload.Sid`, replacing the standing `// TODO: sid` marker; the back-channel
  `logout_token` carries the same `sid`. It is **not** emitted into access tokens, so a resource server never receives
  the OP session identifier. A session with no `sid` (non-interactive issuance) simply omits the claim.
- **RP-Initiated Logout is an endpoint family.** A self-advertising `end_session_endpoint`
  (`EndpointNames.EndSession`) validates the `id_token_hint` (to identify the subject/session and the requesting
  client), validates `post_logout_redirect_uri` against the client's registered `post_logout_redirect_uris`, clears the
  OP cookie (`SignOutAsync`), revokes the session and the subject's grants, fires back-channel notifications, and
  redirects to the validated `post_logout_redirect_uri` with the echoed `state` (or renders a confirmation otherwise).
- **The OP's own account page is a first-party entry point into the same pipeline.** The interactive surface's account
  ("me") page offers an end-user sign-out that `POST`s (antiforgery-protected) to the interactive `/account/logout`,
  which runs the identical `EndSessionCommand` pipeline but supplies **no `id_token_hint`**: the end-user is already
  authenticated by the OP login cookie, so the session resolves from the cookie (the pipeline's documented fallback)
  rather than a relying party's token. One termination pipeline, two entry points — an RP's `id_token_hint` at the
  `end_session_endpoint`, or the first-party login cookie at `/account/logout`. This replaces the earlier bare
  cookie-clearing (`SignOutAsync`-only) logout, so a first-party sign-out now also revokes the session and grants and
  fires back-channel notifications. The logout verb is hardened from `GET` to `POST` (state-changing, antiforgery-bound,
  matching login/consent); the spec-facing `end_session_endpoint` keeps its `GET`/`POST` support.
- **Back-Channel Logout is a pluggable notifier.** On logout the OP builds a signed `logout_token` per participating RP
  (carrying `iss`, `aud`, `iat`, `jti`, `sid`/`sub`, and the `events` claim) and `POST`s it to each client's
  `backchannel_logout_uri` through an injectable `IBackChannelLogoutNotifier` (default implementation uses a named
  `HttpClient`). The transport is an abstraction because delivery policy (retries, fan-out, out-of-band queuing) is
  deployment-specific.
- **The iframe mechanisms are declined, not stubbed.** Front-Channel Logout and Session Management are **not**
  implemented because they depend on third-party cookies the browser platform is removing. The session model and the
  logout pipeline leave the seam open (a session knows its participating clients; the logout command is a mediator
  pipeline others can extend), so a deployment that still needs front-channel behavior can add it without a redesign.
  The omission is recorded as a deferred item in [`BACKLOG.md`](../../BACKLOG.md), with this ADR as the rationale.
  **(Superseded by [ADR-0088](0088-opt-in-front-channel-logout-and-session-management.md): both mechanisms are now
  implemented as opt-in capabilities, available but disabled by default. The seam this ADR left open is exactly what
  made that an additive, migration-free change.)**

## Options considered

- **A dedicated `PersistedSession` table + store + EF migration.** A first-class schema is the "textbook" model, but it
  duplicates the grant store's tenant scoping, concurrency tokens, hash-keying, expiry, and bulk-revocation, and forces
  a new host-owned EF migration ([ADR-0041](0041-host-owned-ef-migrations-deliberate-outside-development.md)) that the
  model-drift gate ([ADR-0072](0072-self-healing-dev-database-and-model-drift-gate.md)) would police — all for a row
  shape the grant store already provides. Riding the grant store (as the device flow does) is the cohesive home for "a
  grant and its payload" and keeps the subsystem migration-free. Chosen.
- **Stateless `sid` (no server-side session).** Deriving `sid` from the cookie alone avoids storage but makes
  server-side revocation and back-channel fan-out impossible (there is nothing to look up a session's participating RPs
  from, and nothing to invalidate independently of the cookie). Rejected — it cannot support the logout surface.
- **Implement all four specs ("maximum coverage").** Front-Channel Logout and Session Management add real public surface
  (per-RP `frontchannel_logout_uri`, an OP-hosted session iframe, `session_state` computation) whose correctness is
  gated on third-party cookies that the browser platform is eliminating — i.e. maximum surface for a mechanism that
  degrades to unreliable. For a greenfield 2026 OP the higher-quality choice is the cookie-independent pair plus a
  documented seam. Rejected in favor of the chosen scope.

## Consequences

- **Easier:** `sid` is emitted and stable; sessions can be revoked server-side independently of the cookie; logout
  notifies RPs over a channel browsers cannot break; no new table or migration; the model reuses the audited grant
  revocation path (`RevokeWhereAsync`).
- **Harder / follow-ups:** The OP must track participating clients on the session as tokens are issued (a write on the
  issuance path). Back-channel delivery is now **retried out of band** via the generic job-dispatch port
  ([ADR-0090](0090-durable-outbound-delivery-dispatch-adapter.md)) \u2014 the default notifier builds the signed
  `logout_token` and dispatches a `BackChannelLogoutDeliveryJob` rather than `POST`ing inline, so a failed delivery is
  retried (and becomes crash-durable under a durable dispatcher adapter, cf.
  [ADR-0048](0048-durable-audit-delivery-outbox.md)). `max_age` / re-authentication semantics and
  the `auth_time` conditionality ([`BACKLOG.md`](../../BACKLOG.md) `F11`, `F13`) interact with the session's recorded
  `auth_time`. Front-Channel Logout / Session Management remain unavailable **by default** (now implementable as an
  opt-in capability via [ADR-0088](0088-opt-in-front-channel-logout-and-session-management.md)); a deployment that
  requires them enables the corresponding setting.
- **You-chose-wrong smells:** an RP that only supports front-channel logout and cannot be migrated to back-channel; a
  deployment that needs cross-RP _session-change detection_ (not just logout). Either would reopen the iframe question
  under its own ADR.

## References

- RP-Initiated Logout 1.0 — <https://openid.net/specs/openid-connect-rpinitiated-1_0.html>
- Back-Channel Logout 1.0 — <https://openid.net/specs/openid-connect-backchannel-1_0.html>
- Front-Channel Logout 1.0 — <https://openid.net/specs/openid-connect-frontchannel-1_0.html>
- Session Management 1.0 — <https://openid.net/specs/openid-connect-session-1_0.html>
- [ADR-0059](0059-device-authorization-grant.md) — the persisted-grant-as-stateful-artifact precedent.
- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — service vs strategy vs mediator extensibility.
- [ADR-0009](0009-endpoint-families-own-their-route-group.md) — endpoints self-register and self-advertise.
- [ADR-0028](0028-token-revocation-refresh-tokens-only.md), [ADR-0022](0022-grant-filters-and-bulk-revocation.md) —
  existing grant revocation surface reused by logout.
