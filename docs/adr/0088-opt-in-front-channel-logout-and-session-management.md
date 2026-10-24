# 88. Opt-in Front-Channel Logout and Session Management (available, disabled by default)

- **Status:** Accepted (revises the deferral recorded in
  [ADR-0074](0074-session-management-and-logout.md), which declined these two mechanisms outright; amended by
  [ADR-0256](0256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md):
  the `check_session_iframe` is the one page relying parties may frame, and the front-channel logout page frames only
  the clients' logout origins)
- **Date:** 2026-10-13
- **Deciders:** NCode Group

## Context

[ADR-0074](0074-session-management-and-logout.md) built the two **cookie-independent** OpenID Connect logout
mechanisms — RP-Initiated Logout (`end_session_endpoint`) and Back-Channel Logout (the signed `logout_token` POST) —
and **deliberately declined** the two **browser-mediated** mechanisms:

- **Front-Channel Logout 1.0** — the OP renders one hidden `<iframe>` per RP so each RP clears its own cookie in the
  user agent.
- **Session Management 1.0** (`check_session_iframe` / `session_state`) — an RP polls an OP-hosted iframe via
  `postMessage` to detect session-state changes.

Both depend on the browser delivering each party's **third-party cookie inside an iframe**, which the browser platform
(Safari ITP, Firefox partitioning, Chrome deprecation) is removing — so they are unreliable by construction going
forward. ADR-0074 recorded the omission as backlog item `D2` behind a clean extensibility seam.

Since then, a concrete need surfaced: some deployments still integrate relying parties that **only** support the
iframe-based mechanisms and cannot (yet) be migrated to Back-Channel Logout. The question is therefore not "should the
OP depend on third-party cookies" (it should not, by default) but "can a deployment that accepts the fragility turn
these on without forking the library."

The repository already has the exact shape for this: a **capability that is available but disabled by default**,
advertised through a discovery setting whose value the operator controls — the same pattern used for, e.g.,
`backchannel_logout_supported` and the PKCE `code_challenge_methods_supported` advertisement.

## Decision

**Implement Front-Channel Logout 1.0 and Session Management 1.0 in full, but make each one available and _disabled by
default_, gated by an opt-in server setting.** The secure default is unchanged — a stock OP advertises neither
mechanism and maintains no browser-state cookie — while a deployment that needs either one enables the corresponding
setting with no code change.

Concretely:

- **Front-Channel Logout rides the existing end-session pipeline.** Two server discovery settings,
  `frontchannel_logout_supported` and `frontchannel_logout_session_supported`, both default **false** and are always
  advertised (so an RP sees the capability as unavailable until enabled). Two per-client settings,
  `frontchannel_logout_uri` and `frontchannel_logout_session_required`, register the RP's iframe URI and whether the OP
  must append `iss`/`sid`. When enabled, the `end_session_endpoint` collects the participating clients' front-channel
  URIs (via `IFrontChannelLogoutService`) into the `EndSessionDisposition` and renders a page with one hidden iframe per
  URI, then performs the validated post-logout redirect (or a confirmation). Back-Channel Logout remains the preferred,
  always-on mechanism; front-channel is an additional best-effort layer.

- **Session Management is gated by one server setting and emits `session_state`.** `session_management_supported`
  defaults **false** and is non-discoverable (the standard discovery signal is the `check_session_iframe` URL itself).
  When enabled: the authorization-ticket handler stamps the `session_state` response parameter, computed by
  `ISessionStateService` as a salted `SHA-256` digest of the client id, the client's redirect-URI origin, and an opaque
  **OP browser state**; that browser state is stored in a non-`HttpOnly` cookie (so the iframe script can read it) that
  is deleted on logout; discovery advertises the OP-hosted `check_session_iframe` endpoint, whose script recomputes
  `session_state` and answers the RP's `postMessage` poll with `changed`/`unchanged`.

- **The seam is the setting, not a fork.** No new persistence, table, or EF migration is introduced; the session model
  and logout pipeline from ADR-0074 are reused unchanged. Enabling either mechanism is a tenant/server setting change.

## Options considered

- **Keep ADR-0074's outright decline (status quo).** Simplest and most secure by default, but a deployment with a
  front-channel-only RP must fork the library or bolt on the mechanism through the mediator seam by hand. Rejected now
  that a concrete need exists: the capability is well-specified and cheap to carry behind an off-by-default flag.

- **Implement and enable by default ("maximum coverage").** Rejected — it would make a stock OP depend on third-party
  cookies the browser platform is removing, advertise a mechanism that degrades to unreliable, and set a non-`HttpOnly`
  browser-state cookie for every deployment. The secure default must remain the cookie-independent pair.

- **Opt-in, available but disabled by default (chosen).** Matches the repository's existing "available-but-off"
  discovery-setting pattern, preserves the secure default, and lets a deployment that accepts the fragility enable it
  per tenant/server without a code change.

## Consequences

- **Easier:** A deployment with a front-channel-only or session-management-dependent RP enables a setting instead of
  forking. The implementation reuses the ADR-0074 session model and end-session pipeline, so there is no new table or
  migration.
- **Harder / caveats:** Both mechanisms remain **best-effort and fragile by construction** — they only work where the
  browser still delivers third-party cookies in an iframe. The `check_session_iframe` is framed cross-site by RPs, so a
  deployment that enables Session Management must ensure its global security headers do **not** forbid framing of that
  endpoint and that the browser-state cookie (`SameSite=None; Secure`, not `HttpOnly`) is acceptable in its threat
  model. The `session_state` origin is derived from the client's `redirect_uri`, so an RP must host its check-session
  RP-iframe on that same origin.
- **You-chose-wrong smells:** enabling these as a substitute for Back-Channel Logout (they are a supplement, not a
  replacement); relying on them in a browser that has already removed third-party cookies.

## References

- Front-Channel Logout 1.0 — <https://openid.net/specs/openid-connect-frontchannel-1_0.html>
- Session Management 1.0 — <https://openid.net/specs/openid-connect-session-1_0.html>
- [ADR-0074](0074-session-management-and-logout.md) — the end-user session model and the two cookie-independent logout
  mechanisms; this ADR reverses its outright decline of the two iframe-based mechanisms.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — greenfield, best-long-term-design posture.
- [ADR-0083](0083-secure-by-default-discovery-metadata.md) — secure-by-default discovery advertisement.
- [ADR-0009](0009-endpoint-families-own-their-route-group.md) — endpoints self-register and self-advertise.
