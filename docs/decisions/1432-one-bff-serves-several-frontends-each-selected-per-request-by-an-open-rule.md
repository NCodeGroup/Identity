# 1432. One BFF serves several frontends, each selected per request by an open rule

- **Status:** Accepted (amends
  [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)
  decisions 7, 8 and 13,
  [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md) decision 3,
  [ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md) decision 7,
  [ADR-1365](1365-the-bff-and-the-login-redirect-take-no-url-from-the-requests-host.md) decision 2,
  [ADR-1381](1381-the-bff-serves-its-own-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md)
  decisions 7 and 8,
  [ADR-1382](1382-the-bff-sends-browser-security-headers-and-publishes-its-client-keys-only-when-the-host-opts-in.md)
  decisions 1 and 5, and the per-frontend configuration keys that
  [ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md),
  [ADR-1294](1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md),
  [ADR-1299](1299-userinfo-is-read-as-json-under-the-familys-claim-mapping-and-merged-once-at-login.md) and
  [ADR-1301](1301-a-signed-userinfo-response-is-validated-like-an-id-token-under-the-clients-registered-algorithms.md)
  name; decisions 7, 8 and 11 amended by
  [ADR-1442](1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md):
  the frontends follow their configuration and a change takes effect only when the whole set is valid; amended by
  [ADR-1443](1443-a-bff-session-ends-when-its-frontend-is-removed-or-registered-as-another-client.md): a session ends
  when its frontend is removed or registered as another client)
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

A product often has several browser apps, such as a shop and its admin console, on several hosts or on several paths of
one origin. Each is its own OAuth client: it has its own `client_id`, authentication method and key, scopes, callback,
return URLs, security headers and remote APIs. Two of them on one origin must never share a session cookie, the rule
that issue [#89](https://github.com/NCodeGroup/identity/issues/89) sets for the OP's own interactive surfaces
([ADR-1310](1310-the-interactive-surfaces-cookies-are-host-only-and-a-tenants-session-is-named-for-its-path.md)).

Four forces shape the answer:

- A `__Host-` cookie forbids a `Domain` and requires `Path=/`, so a cookie cannot be scoped to a frontend by its
  attributes; its **name** has to carry the frontend.
- The ways to tell frontends apart differ by deployment (a host, a path prefix, and others that a host may add), so the
  rules are an open vocabulary
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
- The BFF's callback, back-channel logout receiver and `/bff/jwks` are reached by the OP or by a browser, not by
  the frontend's own scripts, so a frontend has to be selectable by the URLs the OP is told, not only by routing the
  application writes.
- A request that matches two frontends, or none, must not reach a session or a client registration by accident.

Design [§5.10](../backend-for-frontend.md) and issue [#396](https://github.com/NCodeGroup/identity/issues/396) describe
the need.

## Decision

**A deployment configures one `Bff:Frontends:<name>` entry per frontend. An open, registered selection rule decides per
request which frontend the request belongs to, and every part of the BFF that holds a client registration, a cookie, an
endpoint or a token takes it from that frontend.**

1. **The model.** `BffOptions` holds `Frontends`, a dictionary of `BffFrontendOptions` with ordinal keys, beside the
   deployment-wide `Antiforgery` and `Tokens` settings. Every other setting is per frontend: `Authority`, `PublicOrigin`,
   `ClientId`, `ClientAuthentication`, `Scopes`, `Resources`, `ReturnUrls`, `UserClaims`, `LoginParameters`,
   `TrustedOrigins`, `ErrorPath`, `PathPrefix`, `BackchannelLogoutSessionRequired`, `IdToken`, `UserInfo`,
   `SecurityHeaders`, `Jwks`, `Session`, `DPoP`, `RemoteApis` and `Selection`. A frontend inherits nothing from another,
   so a setting always belongs to the client that its entry names.
2. **The name.** A frontend name is 1 to 63 lowercase ASCII letters, digits and hyphens, starting and ending with a
   letter or digit. It is a valid cookie-name suffix and a stable value in session metadata.
3. **Selection is a strategy collection.** `IBffFrontendSelector` is registered by rule name, with the built-in rules
   `host` and `path` (`BffFrontendSelectorNames`). A frontend's `Selection:Rule` names the rule and `Selection:Values`
   holds its values. Each rule validates its own values (`Validate`), declares whether two frontends that use it overlap
   (`Overlaps`) and matches a request (`IsMatch`). A rule takes no constructor dependencies: options validation reads
   every registered rule, and `IsMatch` reads only the request.
   - **At most one frontend omits the rule.** It serves every request that no other frontend selects. A catalog with a
     single frontend and no rule selects it for every request.
   - **Overlap is refused at startup**: two frontends of one rule whose values can match one request fail the host's
     start. A request that two ruled frontends match anyway fails closed with `InvalidOperationException`, which is a
     `500`.
4. **The host rule.** A value is a DNS name or an IPv4 address with an optional port; without a port it matches any
   port. A value that names the default port of the `PublicOrigin`'s scheme is refused, because a browser's Host
   header never carries it. The host of the frontend's `PublicOrigin` has to be one of its values, so the callback and
   back-channel URIs that the OP is told select the frontend.
5. **The path rule.** The values are local path prefixes, compared at segment boundaries and ignoring case as routing
   does. The frontend's `PathPrefix` and every one of its remote-API paths lie under one of the values, so its
   endpoints select it.
6. **Isolation.** The session cookie is `__Host-Http-bff.<name>` and the login transaction cookie is
   `__Host-Http-bff-login.<name>`, for every frontend including the only one, so adding a frontend never renames
   another's cookie.
   - The session metadata records the frontend; it is authenticated as AEAD associated data
     ([ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)). The
     resolver ignores, and deletes, a cookie whose session belongs to another frontend.
   - The login transaction records its frontend, and the callback refuses a transaction of another frontend as
     `frontend_mismatch` (`BffLoginFailureReasons.FrontendMismatch`).
7. **Endpoints.** An endpoint belongs to frontends through `BffFrontendAttribute` metadata, which allows several and
   takes the union of the names, and a routing `MatcherPolicy` (`IEndpointSelectorPolicy`) keeps only the candidates of
   the frontend that the request selects. Two host-selected frontends can therefore share `/bff/login`.
   - `MapBffEndpoints()` maps each frontend's endpoints under its own `PathPrefix`.
   - Configured remote APIs belong to the frontend that configures them.
   - A local API or page is limited to frontends with `RequireBffFrontend(...)` or `[BffFrontend]`; a YARP route is
     limited with the `BffFrontend` metadata key.
   - An endpoint without a limit serves whichever frontend the request selects. A request that no frontend selects
     reaches no BFF endpoint and no session.
8. **Tokens and registrations.** The request's frontend supplies the client registration (`BffFrontend.Registration`,
   built once per frontend). The BFF's client-credential tokens are cached per frontend, resource and scope. A
   back-channel logout token ends only the sessions of the frontend that received it. A session's trusted origins
   ([ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md)) and `PublicOrigin`
   ([ADR-1365](1365-the-bff-and-the-login-redirect-take-no-url-from-the-requests-host.md)) are the frontend's own.
9. **Distinct callbacks.** Each frontend's callback, `PublicOrigin + PathPrefix + "/callback"`, is unique across the
   deployment, so the OP can tell the clients apart.
10. **Security headers.** `UseBffSecurityHeaders()` writes the selected frontend's headers
    ([ADR-1382](1382-the-bff-sends-browser-security-headers-and-publishes-its-client-keys-only-when-the-host-opts-in.md)),
    and the strict defaults of `BffSecurityHeadersOptions` for a request that selects no frontend. `/bff/jwks` publishes
    the keys of the request's frontend.
11. **Validation and lifetime.** The whole set is validated at startup: each frontend, each rule's values, the overlaps,
    the at-most-one rule-less frontend and the unique callbacks. The frontends are fixed until the host restarts.

## Alternatives considered

- **One BFF deployment per frontend.** Rejected: it multiplies the deployments, their stores and their keys for what is
  one product on one origin, and it cannot share one host's path space between two apps.
- **Root settings that frontends inherit.** Rejected: configuration-list binding merges by index, so inheritance would
  hide which client a setting belongs to. Each frontend states its own.
- **Selection by routing alone (`RequireHost`).** Rejected: the callback, the back-channel receiver and the login
  redirect must also resolve the frontend, and routing says nothing about a request that no endpoint claims or that two
  claim. An open selector decides once, validates its overlaps at startup and fails closed on an ambiguity.
- **First-match ordering.** Rejected: it hides an overlap behind a list order, so a reordered configuration silently
  hands a request to another client. Startup refuses overlaps and a runtime ambiguity fails.
- **A cookie-name suffix only when several frontends exist.** Rejected: adding a second frontend would rename the
  first frontend's cookie and end its users' sessions. The suffix is always present.
- **A second descriptor seam beside the options, which a package registers to add a frontend.** Rejected: a package
  adds a frontend by configuring `BffOptions.Frontends`, so there is one place that lists the frontends and one
  validator for them.

## Consequences

- A deployment configures one `Bff:Frontends:<name>` entry per frontend, and each frontend registers its own client at
  the OP with the metadata of
  [ADR-1294](1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md); its callback is
  `PublicOrigin + PathPrefix + "/callback"`.
- A request that no frontend selects reaches no BFF endpoint and no session.
- Adding or changing a frontend needs a restart.
- A session never answers for another frontend, even when a browser sends both cookies to one origin.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.10
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md),
  [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md),
  [ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md),
  [ADR-1294](1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md),
  [ADR-1310](1310-the-interactive-surfaces-cookies-are-host-only-and-a-tenants-session-is-named-for-its-path.md),
  [ADR-1365](1365-the-bff-and-the-login-redirect-take-no-url-from-the-requests-host.md),
  [ADR-1381](1381-the-bff-serves-its-own-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md),
  [ADR-1382](1382-the-bff-sends-browser-security-headers-and-publishes-its-client-keys-only-when-the-host-opts-in.md)
- `src/NCode.Identity.Bff/Frontends/`, `src/NCode.Identity.Bff/Options/BffFrontendOptions.cs`,
  `src/NCode.Identity.Bff.Abstractions/IBffFrontendSelector.cs`
- Issues [#89](https://github.com/NCodeGroup/identity/issues/89) and
  [#396](https://github.com/NCodeGroup/identity/issues/396)
