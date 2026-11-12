# 1088. Front-channel logout and session management are available but disabled by default

- **Status:** Accepted
- **Date:** 2026-10-13
- **Deciders:** NCode Group

## Context

The OP's cookie-independent logout mechanisms are RP-Initiated Logout (`end_session_endpoint`) and Back-Channel Logout
(the signed `logout_token` POST), built on the end-user session model of
[ADR-1074](1074-session-management-and-logout.md). Two browser-mediated mechanisms remain:

- **Front-Channel Logout 1.0**: the OP renders one hidden `<iframe>` per relying party so each one clears its own
  cookie in the user agent.
- **Session Management 1.0** (`check_session_iframe` and `session_state`): a relying party polls an OP-hosted iframe
  through `postMessage` to detect a change of session state.

Both depend on the browser delivering each party's third-party cookie inside an iframe, which the browser platform
(Safari ITP, Firefox partitioning, Chrome's deprecation) is removing, so they are fragile by construction. Some
deployments still integrate relying parties that support only the iframe-based mechanisms and cannot adopt Back-Channel
Logout. The question is not whether the OP should depend on third-party cookies by default (it should not) but whether a
deployment that accepts the fragility can turn these mechanisms on without forking the library.

The repository already has the shape for this: a capability that is available but disabled by default, advertised
through a discovery setting whose value the operator controls, as with `backchannel_logout_supported` and the PKCE
`code_challenge_methods_supported` advertisement.

## Decision

**Front-Channel Logout 1.0 and Session Management 1.0 are implemented in full, and each is off until an operator
enables it.** A stock OP advertises neither mechanism and sets no browser-state cookie. A deployment that needs either
one enables the corresponding setting with no code change.

**Front-channel logout rides the end-session pipeline.**

- Two discovery settings, `frontchannel_logout_supported` and `frontchannel_logout_session_supported`, default to
  `false` and are always advertised, so a relying party sees the capability as unavailable until it is enabled.
- Two per-client settings, `frontchannel_logout_uri` and `frontchannel_logout_session_required`, register the relying
  party's logout page and whether the OP must append `iss` and `sid`.
- `LogoutChannelCoherenceRule` refuses a client that registers a `frontchannel_logout_uri` while
  `frontchannel_logout_supported` is false, or that requires the session while `frontchannel_logout_session_supported`
  is false, so what the OP does always matches what it advertises.
- When front-channel logout is enabled, the end-session endpoint collects the participating clients' URIs through
  `IFrontChannelLogoutService` into the `EndSessionDisposition` and renders one hidden iframe per URI. It appends
  `iss` and `sid` only while `frontchannel_logout_session_supported` is true. It then performs the validated
  post-logout redirect or shows a confirmation.
- Back-Channel Logout stays the preferred mechanism. Front-channel logout is an additional best-effort layer.

**Session management is gated by one setting and emits `session_state`.**

- `session_management_supported` defaults to `false` and is not discoverable. The `check_session_iframe` URL is the
  discovery signal.
- When it is enabled, the authorization ticket handler stamps the `session_state` response parameter.
  `ISessionStateService` computes it as a salted SHA-256 digest over the client id, the origin of the client's
  redirect URI and an opaque OP browser state.
- The browser state lives in a non-`HttpOnly` cookie with `SameSite=None; Secure`, so the iframe script can read it.
  Logout deletes the cookie.
- Discovery advertises the OP-hosted `check_session_iframe` endpoint, whose script recomputes `session_state` and
  answers the relying party's `postMessage` poll with `changed` or `unchanged`.

**Framing.** The `check_session_iframe` is the one page relying parties may frame, and the front-channel logout page
frames only the clients' logout origins
([ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)).

**The seam is the setting, not a fork.** Both mechanisms reuse the session model and the end-session pipeline, with no
persistence, table or migration of their own. Enabling either one is a setting change; the platform's settings are the
cascade root
([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)).

## Options considered

- **Decline both mechanisms.** The OP stays cookie-independent, but a deployment with a front-channel-only relying
  party must fork the library or bolt the mechanism on through the mediator seam by hand. Rejected: both are
  well-specified and cheap to carry behind an off-by-default setting.
- **Implement both and enable them by default.** Rejected: a stock OP would depend on third-party cookies the browser
  platform is removing, advertise mechanisms that degrade to unreliable, and set a non-`HttpOnly` browser-state cookie
  for every deployment. The secure default must remain the cookie-independent pair.
- **Implement both, available but disabled by default (chosen).** It matches the repository's available-but-off
  discovery-setting pattern, keeps the secure default and lets a deployment that accepts the fragility enable either
  mechanism without a code change.

## Consequences

- A deployment with a front-channel-only or session-management-dependent relying party enables a setting instead of
  forking. The implementation reuses the session model and end-session pipeline, so there is no new table or
  migration.
- Both mechanisms are best-effort and fragile by construction: they work only where the browser still delivers
  third-party cookies in an iframe.
- A deployment that enables session management must ensure its security headers do not forbid framing of the
  `check_session_iframe`, and that a `SameSite=None; Secure` cookie that is not `HttpOnly` is acceptable in its threat
  model.
- The `session_state` origin comes from the client's redirect URI, so a relying party hosts its check-session iframe on
  that origin.
- A background session termination
  ([ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)) is invisible to
  `check_session_iframe`.
- Smells that the choice was wrong: enabling these as a substitute for Back-Channel Logout, which they supplement and
  do not replace, and relying on them in a browser that has already removed third-party cookies.

## References

- Front-Channel Logout 1.0: <https://openid.net/specs/openid-connect-frontchannel-1_0.html>
- Session Management 1.0: <https://openid.net/specs/openid-connect-session-1_0.html>
- [ADR-1074](1074-session-management-and-logout.md): the end-user session model and the two cookie-independent logout
  mechanisms.
- [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md):
  greenfield, best-long-term-design posture.
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md):
  secure-by-default discovery advertisement.
- [ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md):
  endpoints self-register and self-advertise.
- [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md),
  [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md) and
  [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md): the
  framing, background termination and settings-root records this one relies on.
