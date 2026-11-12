# 1442. A BFF serves the frontends its configuration lists, each selected per request by an open rule

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

A product often has several browser apps, such as a shop and its admin console, on several hosts or on several paths of
one origin. Each is its own OAuth client: it has its own `client_id`, authentication method and key, scopes, callback,
return URLs, security headers and remote APIs. Two of them on one origin must never share a session cookie, the rule
that issue [#89](https://github.com/NCodeGroup/identity/issues/89) sets for the OP's own interactive surfaces
([ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)).

A deployment that onboards browser apps while it runs, such as a multi-tenant SaaS that adds a tenant's frontend, adds,
changes and removes `Bff:Frontends` entries without restarting its replicas. The options system already says whether a
configuration source changes.

Forces:

- A `__Host-` cookie forbids a `Domain` and requires `Path=/`, so a cookie cannot be scoped to a frontend by its
  attributes; its **name** has to carry the frontend.
- The ways to tell frontends apart differ by deployment (a host, a path prefix, and others that a host may add), so the
  rules are an open vocabulary ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
- The callback, back-channel logout receiver and `/bff/jwks` are reached by the OP or a browser, so a frontend has to
  be selectable by the URLs the OP is told, not only by routing.
- A request that matches two frontends, or none, must not reach a session or registration.
- A frontend owns endpoints, a cache of client-credential tokens and derived values, so a change has to reach all of
  them at once, and a serving host cannot refuse to start, so an invalid change must not replace a working set.
- Some checks span the set and what the host maps in code, and YARP reloads its routes from the same configuration on
  its own, so neither side can require the other to have applied a change first.
- `IOptionsMonitor<T>.CurrentValue` throws after an invalid change, and its listeners never see one, so it can neither
  keep the last valid value nor say why it was refused.

Design [§5.10](../backend-for-frontend.md) describes the need.

## Decision

**A deployment configures one `Bff:Frontends:<name>` entry per frontend. An open, registered selection rule decides per
request which frontend the request belongs to, and every part of the BFF that holds a client registration, a cookie, an
endpoint or a token takes it from that frontend. The set of frontends follows the configuration, and a change takes
effect only when the whole set is valid.**

1. **The model.** `BffOptions.Frontends` is a dictionary of `BffFrontendOptions` with ordinal keys, one
   `Bff:Frontends:<name>` entry per OAuth client, beside the deployment-wide `Antiforgery`, `Tokens`,
   `SessionRetirement` and `KeyRings` settings. Every other setting is per frontend: `Authority`, `PublicOrigin`,
   `ClientId`, `ClientAuthentication`, `Scopes`, `Resources`, `ReturnUrls`, `UserClaims`, `LoginParameters`,
   `TrustedOrigins`, `ErrorPath`, `PathPrefix`, `BackchannelLogoutSessionRequired`, `IdToken`, `UserInfo`,
   `SecurityHeaders`, `Jwks`, `Session`, `DPoP`, `RemoteApis` and `Selection`. A frontend inherits nothing from another,
   so a setting always belongs to the client that its entry names.
2. **The name.** A frontend name is 1 to 63 lowercase ASCII letters, digits and hyphens, starting and ending with a
   letter or digit. It suffixes every cookie name, for the only frontend too, and is recorded in session metadata and in
   the login transaction.
3. **Selection is a strategy collection.** `IBffFrontendSelector` is registered by rule name, with the built-in rules
   `host` and `path` (`BffFrontendSelectorNames`). A frontend's `Selection:Rule` names the rule and `Selection:Values`
   holds its values. Each rule validates its own values (`Validate`), declares whether two frontends that use it overlap
   (`Overlaps`) and matches a request (`IsMatch`). A rule takes no constructor dependencies: options validation reads
   every registered rule, and `IsMatch` reads only the request.
   - **At most one frontend omits the rule.** It serves every request that no other frontend selects. A catalog with a
     single frontend and no rule selects it for every request.
   - **Overlap refuses the set**: two frontends of one rule whose values can match one request fail validation. A
     runtime ambiguity throws `InvalidOperationException` (`500`).
4. **The host rule.** A value is a DNS name or an IPv4 address with an optional port; without a port it matches any
   port. A value that names the default port of the `PublicOrigin`'s scheme is refused, because a browser's Host
   header never carries it. The host of the frontend's `PublicOrigin` has to be one of its values, so the callback and
   back-channel URIs that the OP is told select the frontend.
5. **The path rule.** The values are local path prefixes, compared at segment boundaries and ignoring case as routing
   does. They have to cover the frontend's `PathPrefix` and every one of its remote-API paths, so its endpoints select
   it.
6. **Isolation.** The session cookie is `__Host-Http-bff.<name>` and the login transaction cookie is
   `__Host-Http-bff-login.<name>`, for every frontend including the only one, so adding a frontend never renames
   another's cookie.
   - The session metadata records the frontend; it is authenticated as AEAD associated data
     ([ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)). The
     resolver ignores, and deletes, a cookie whose session belongs to another frontend.
   - The login transaction records its frontend, and the callback refuses a transaction of another frontend as
     `frontend_mismatch` (`BffLoginFailureReasons.FrontendMismatch`).
   - A back-channel logout token ends only the sessions of the frontend that received it. Trusted origins
     ([ADR-1292](1292-the-bffs-cookie-authenticates-only-guarded-endpoints-which-refuse-cross-site-requests-and-reload-cookieless-navigations.md))
     and `PublicOrigin`
     ([ADR-1365](1365-no-url-the-server-publishes-or-compares-takes-its-origin-from-the-requests-host.md)) are the
     frontend's own.
7. **Endpoints.** An endpoint belongs to frontends through `BffFrontendAttribute` metadata, which allows several and
   takes the union of the names, and a routing `MatcherPolicy` (`IEndpointSelectorPolicy`) keeps only the candidates of
   the frontend that the request selects. Two host-selected frontends can therefore share `/bff/login`.
   - `MapBffEndpoints()` maps each frontend's endpoints under its own `PathPrefix`.
   - Configured remote APIs belong to the frontend that configures them.
   - A local API or page is limited to frontends with `RequireBffFrontend(...)` or `[BffFrontend]`; a YARP route is
     limited with the `BffFrontend` metadata key.
   - An endpoint without a limit serves the frontend the request selects. A request that no frontend selects reaches no
     BFF endpoint and no session.
8. **Distinct callbacks.** Each frontend's callback, `PublicOrigin + PathPrefix + "/callback"`, is unique across the
   deployment, so the OP can tell the clients apart.
9. **Security headers and keys.** `UseBffSecurityHeaders()` writes the selected frontend's headers
   ([ADR-1382](1382-the-bffs-browser-security-headers-and-its-client-key-endpoint-are-each-opt-in-for-the-host.md)),
   and the strict defaults of `BffSecurityHeadersOptions` for a request that selects no frontend. `/bff/jwks` publishes
   the keys of the request's frontend.
10. **The set follows the configuration.** `BffFrontendCatalog` holds one immutable `BffFrontendSet` (the validated
    `BffOptions` and the frontends built from them). `AddBffConfiguration` registers the section's
    `IOptionsChangeTokenSource<BffOptions>`; when its change tokens fire, the catalog creates a candidate through
    `IOptionsFactory<BffOptions>` (strict binding and every `IValidateOptions<BffOptions>`), builds the frontends (each
    selection rule must be registered), runs every `IBffFrontendSetCheck` and replaces the set atomically, logging
    event 19011. A candidate that fails never takes effect: the catalog keeps the last valid set and logs every failure
    at `Error` as event 19012. `BffStartupCheck` builds the first set the same way when the host starts, and a failure
    stops the host.
    - **No switch.** The configuration source decides whether the frontends change at run time.
    - **One live view.** Nothing reads `IOptions<BffOptions>`; other packages read `IBffOptionsAccessor.Current`. The
      `Bff:KeyRings` entries load once, when the host starts
      ([ADR-1439](1439-a-bff-loads-its-configured-key-rings-at-start-and-refuses-frontends-naming-an-unknown-ring.md),
      [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md)).
11. **The set checks.** The checks are `BffClientAuthenticationCheck` (each frontend's client-authentication method has a
    registered authenticator), `BffKeyRingCheck`, the `MapBffRemoteApi` overlap check (no frontend's `PathPrefix` and no
    configured remote API overlaps its path) and the `MapBffRemoteApis()` requirement check (every configured remote API
    names a registered token requirement). A check that a mapping adds runs at once against the current set, failing the
    mapping, and against every later candidate. The endpoint check of
    [ADR-1433](1433-a-bff-host-checks-every-endpoints-bff-metadata-at-start-and-again-whenever-the-endpoints-change.md)
    runs again when the frontends change.
12. **Endpoints follow the set.** The group from `MapBffEndpoints()` and `MapBffRemoteApis()` is an
    `EndpointDataSource` over the current set, so a convention the host adds to it applies to frontends added later.
13. **YARP routes.** YARP reloads its own routes. `AddBffTransforms()` checks that a route's `BffFrontend` is a valid
    frontend name, so a route and its frontend can arrive in either order. A route that names a missing frontend serves
    nothing and the endpoint check reports it.
14. **Derived state belongs to the frontend instance.** A `BffFrontend` is immutable and is built for each set; its
    client registration, cookie names and security headers are computed when it is built. Client-credential tokens are
    cached per frontend instance, resource and scope
    ([ADR-1381](1381-the-bff-serves-client-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md)),
    so a change drops them.
    Key rings resolve by name on every use. The routing policy, the selection and every handler read the request's
    frontend from the current set.
15. **A request keeps its frontend.** A request resolves its frontend once, so a change during a request never mixes two
    sets. A connection, such as a Blazor circuit or a SignalR hub, looks its frontend up by name each time it is used, so
    a removed frontend ends its connections at their next revalidation
    ([ADR-1443](1443-a-bff-session-ends-when-its-frontend-is-removed-or-registered-as-another-client.md)).

## Options considered

- **One BFF deployment per frontend.** Rejected: it multiplies deployments, stores and keys, and cannot share one
  host's path space between two apps.
- **Root settings that frontends inherit.** Rejected: inheritance hides which client a setting belongs to.
- **Selection by routing alone (`RequireHost`).** Rejected: the callback and the back-channel receiver must also
  resolve the frontend, and routing says nothing about a request that no endpoint or two endpoints claim.
- **First-match ordering.** Rejected: a reordered configuration would silently hand a request to another client.
- **A cookie-name suffix only when several frontends exist.** Rejected: adding a second frontend would rename the first
  frontend's cookie and end its users' sessions. The suffix is always present.
- **A second descriptor seam beside the options.** Rejected: a package adds a frontend by configuring
  `BffOptions.Frontends`, so one place lists the frontends.
- **An opt-in reload switch.** Rejected: it duplicates what the configuration source decides.
- **`IOptionsMonitor<BffOptions>.OnChange` and `CurrentValue`.** Rejected: an invalid change throws on every read and
  never reaches the listener.
- **Following only `Frontends`.** Rejected: `Antiforgery` and `Tokens` would follow a different snapshot.
- **Requiring a YARP route's frontend to be configured when YARP loads the route.** Rejected: YARP and the BFF apply one
  change separately, so a route arriving first would be refused until the next change.
- **Restarting for every change.** Rejected: it drops every request and connection of every frontend.
- **Keeping an unchanged frontend's instance across a change.** Rejected: it needs a deep comparison of every option,
  while a dropped client-credential token costs one token request.

## Consequences

- A deployment configures one `Bff:Frontends:<name>` entry per frontend, and each frontend registers its own client at
  the OP with the metadata of
  [ADR-1294](1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md); its callback is
  `PublicOrigin + PathPrefix + "/callback"`.
- A request that no frontend selects reaches no BFF endpoint and no session. A session never answers for another
  frontend, even when a browser sends both cookies to one origin.
- A `Bff:Frontends` entry added, changed or removed takes effect on each replica when its source reloads, without a
  restart; an invalid change is logged and has no effect until a valid one follows.
- Every change rebuilds the endpoints and drops the cached client-credential tokens; replicas apply it as their own
  sources reload, so two can briefly differ.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.6, §5.10 and §5.12
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md),
  [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md),
  [ADR-1292](1292-the-bffs-cookie-authenticates-only-guarded-endpoints-which-refuse-cross-site-requests-and-reload-cookieless-navigations.md),
  [ADR-1294](1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md),
  [ADR-1365](1365-no-url-the-server-publishes-or-compares-takes-its-origin-from-the-requests-host.md),
  [ADR-1381](1381-the-bff-serves-client-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md),
  [ADR-1382](1382-the-bffs-browser-security-headers-and-its-client-key-endpoint-are-each-opt-in-for-the-host.md),
  [ADR-1433](1433-a-bff-host-checks-every-endpoints-bff-metadata-at-start-and-again-whenever-the-endpoints-change.md),
  [ADR-1439](1439-a-bff-loads-its-configured-key-rings-at-start-and-refuses-frontends-naming-an-unknown-ring.md),
  [ADR-1443](1443-a-bff-session-ends-when-its-frontend-is-removed-or-registered-as-another-client.md),
  [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md)
- `src/NCode.Identity.Bff/Frontends/`, `src/NCode.Identity.Bff/Options/BffFrontendOptions.cs`,
  `src/NCode.Identity.Bff.Abstractions/IBffFrontendSelector.cs`
- Issues [#89](https://github.com/NCodeGroup/identity/issues/89), [#123](https://github.com/NCodeGroup/identity/issues/123),
  [#396](https://github.com/NCodeGroup/identity/issues/396) and [#602](https://github.com/NCodeGroup/identity/issues/602)
