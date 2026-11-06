# 1442. The BFF's frontends follow their configuration, and a change takes effect only when the whole set is valid

- **Status:** Accepted (amends
  [ADR-1432](1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md) decisions 7, 8 and 11
  and [ADR-1433](1433-a-bff-host-refuses-to-start-when-an-endpoints-bff-metadata-is-incomplete.md) decision 4)
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

A deployment that onboards browser apps while it runs, such as a multi-tenant SaaS that adds a tenant's frontend, adds,
changes and removes `Bff:Frontends` entries without restarting its replicas. The options system already says whether a
configuration source changes: `reloadOnChange`, a provider that polls, or options configured in code that never do.

Five forces shape the answer:

- A frontend owns endpoints (its BFF endpoints under its `PathPrefix`, its configured remote APIs, the YARP routes that
  name it), a cache (its client-credential tokens) and derived values (its client registration and security headers),
  so a change has to reach all of them at once.
- A host that is serving cannot refuse to start, so an invalid change must not replace a working set.
- Some checks span the set and what the host maps in code: a remote API mapped with `MapBffRemoteApi` must not overlap
  a frontend's `PathPrefix` or a configured remote API, and a configured remote API must name a registered token
  requirement.
- YARP reloads its routes from the same configuration on its own, so neither side can require the other to have
  applied a change first.
- `IOptionsMonitor<T>.CurrentValue` throws after an invalid change, and its listeners never see one, so it can neither
  keep the last valid value nor say why it was refused.

Issue [#602](https://github.com/NCodeGroup/Identity/issues/602) describes the need.

## Decision

**`BffFrontendCatalog` holds one immutable `BffFrontendSet` (the validated `BffOptions` and the frontends built from
them) and replaces it when the options' change tokens fire and the candidate set passes every check. Everything the BFF
derives from its options reads the current set.**

1. **What follows the configuration.** The whole `Bff` section is one snapshot: `Frontends`, `Antiforgery` and
   `Tokens`. Nothing in the BFF reads `IOptions<BffOptions>`; the catalog's current set is the one live view. A package
   outside the BFF reads the options in effect through `IBffOptionsAccessor.Current`, which is the current set's
   options: the last configuration change that validated as a whole. The `Bff:KeyRings` entries are the exception:
   `AddBffConfiguration` registers a keyed provider for each entry present when it is called, and a provider reads its
   files once when it is first resolved, so a change adds, removes and re-reads no key ring until the process starts
   again ([ADR-1439](1439-a-bff-loads-the-key-rings-its-configuration-names-from-pem-files.md)).
   `AddBffConfiguration` registers the section's `IOptionsChangeTokenSource<BffOptions>`, so a source that reloads
   drives the catalog.
2. **No switch.** The configuration source decides whether the frontends change at run time. A host that wants fixed
   frontends uses a source that does not reload.
3. **The whole set is validated first.** On a change the catalog creates the options through
   `IOptionsFactory<BffOptions>` (strict binding and every `IValidateOptions<BffOptions>`), builds the frontends (each
   selection rule must be registered) and runs every `IBffFrontendSetCheck`. A candidate that fails never takes
   effect: the catalog keeps the last valid set and logs every failure at `Error` as event 19009. A candidate that
   passes replaces the set atomically and is logged as event 19008. The first set is built the same way, and
   `BffStartupCheck` builds it when the host starts, so a failure stops the host.
4. **The checks.** The library registers `BffClientAuthenticationCheck`: each frontend's client-authentication method
   has a registered authenticator. `MapBffRemoteApi(...)` adds a check that no frontend's `PathPrefix` and no
   configured remote API overlaps its path, and `MapBffRemoteApis()` adds a check that every configured remote API
   names a registered token requirement. A check added by a mapping runs against the current set at once, failing
   the mapping, and against every later candidate.
5. **Endpoints follow the set.** The route group that `MapBffEndpoints()` and `MapBffRemoteApis()` return holds an
   `EndpointDataSource` that builds the endpoints of the current set and whose change token is the catalog's.
   Routing rebuilds its matcher when the set changes, and a convention the host adds to the returned group applies to
   every frontend's endpoints, including a frontend that a later change adds.
6. **YARP routes.** YARP reloads its own routes. `AddBffTransforms()` checks that a route's `BffFrontend` is a valid
   frontend name, not that the current set configures it, so a route and its frontend can arrive in one configuration
   change in either order. A route that names a frontend the set lacks serves no request, because the routing policy
   keeps it from every frontend, and the endpoint check reports it.
7. **The endpoint check runs on every change.** `BffEndpointStartupCheck` checks the endpoints again when the endpoints
   or the frontends change, and logs each failure at `Error` as event 19007.
8. **Derived state belongs to the frontend instance.** A `BffFrontend` is immutable and is built for each set; its
   client registration, cookie names and security headers are computed once when it is built. Client-credential
   tokens are cached per frontend instance, resource and scope, so a frontend registered as another client never
   reuses a token of the client before it, and a change drops the cached tokens. Key rings are resolved by name on
   every use. The routing policy, the selection and every handler read the request's frontend from the current set.
9. **A request keeps its frontend.** A request resolves its frontend once and keeps it, so a change during a request
   never mixes two sets. A connection, such as a Blazor circuit or a SignalR hub, looks its frontend up by name each
   time it is used, so a removed frontend ends its connections at their next revalidation
   ([ADR-1443](1443-a-bff-session-ends-when-its-frontend-is-removed-or-registered-as-another-client.md)).

## Alternatives considered

- **An opt-in switch.** Rejected: it duplicates what the configuration source decides, and a switch that disagrees with
  the source helps nobody.
- **`IOptionsMonitor<BffOptions>.OnChange` and `CurrentValue`.** Rejected: an invalid change makes `CurrentValue`
  throw on every read and never reaches the listener, so the BFF could neither keep the last valid set nor log why.
- **Following only `Frontends`.** Rejected: `Antiforgery` and `Tokens` would follow a different snapshot of the same
  section than the frontends.
- **Requiring a YARP route's frontend to be configured when YARP loads the route.** Rejected: YARP and the BFF apply
  one configuration change separately, so a change that adds a frontend and its route would be refused whenever YARP
  applies it first, and stay refused until the next change.
- **Restarting for every change.** Rejected: it drops every request and connection of every frontend for a change to
  one of them, and a deployment that onboards frontends at run time would restart constantly.
- **Keeping an unchanged frontend's instance across a change.** Rejected: it needs a deep comparison of every option,
  while a change is rare and a dropped client-credential token costs one token request.

## Consequences

- Adding, changing or removing a `Bff:Frontends` entry takes effect on each replica when its configuration source
  reloads, without a restart.
- An invalid change is logged and has no effect until a valid one follows.
- Every change rebuilds the BFF's endpoints and drops the cached client-credential tokens.
- Replicas apply a change when their own sources reload, so for a moment two replicas can serve different sets.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.6, §5.10 and §5.12
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1381](1381-the-bff-serves-its-own-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md),
  [ADR-1432](1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md),
  [ADR-1433](1433-a-bff-host-refuses-to-start-when-an-endpoints-bff-metadata-is-incomplete.md),
  [ADR-1443](1443-a-bff-session-ends-when-its-frontend-is-removed-or-registered-as-another-client.md)
- `src/NCode.Identity.Bff/Frontends/BffFrontendCatalog.cs`, `src/NCode.Identity.Bff/Frontends/BffFrontendSet.cs`,
  `src/NCode.Identity.Bff/Endpoints/BffFrontendEndpointDataSource.cs`
- Issues [#602](https://github.com/NCodeGroup/Identity/issues/602) and
  [#123](https://github.com/NCodeGroup/Identity/issues/123)
