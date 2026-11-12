# 1381. The BFF serves client and exchanged tokens without a lease and forwards remote APIs through YARP

- **Status:** Accepted
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

The BFF's `user` token requirement serves the session's own access token and refreshes it single-flight under a lease on
its refresh token
([ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md)).
The lease spans the replicas because the OP rotates refresh tokens: two replicas that present the same refresh token
make the OP revoke the whole family (RFC 9700 §4.14.2).

An endpoint or a remote API needs more than that one requirement (issue [#395](https://github.com/NCodeGroup/identity/issues/395)):

- `client`: the BFF's own token from the `client_credentials` grant (RFC 6749 §4.4).
- `user_or_client`: the user's token when the request has a session, otherwise the BFF's own.
- `exchange`: a token for another resource or scope, from an RFC 8693 token exchange that presents the user's token.
- `none`: no token at all, only a session.
- Remote APIs: a local path prefix that forwards each request to a destination with one of those tokens attached.
- YARP routes: a host that already uses the YARP reverse proxy names a requirement on its configured routes.

Neither new grant has the reuse hazard that the lease guards against. `client_credentials` issues no refresh token
(RFC 6749 §4.4.3), and the BFF keeps no refresh token from an exchange. A second request for either token returns a
second, equally valid token and revokes nothing.

A remote API also needs a transport a host can extend. Outbound telemetry, a resilience pipeline or a test server's
handler belong on the requests that leave the BFF, as the protocol client's `IHttpClientFactory` client named
`OpenIdClientDefaults.HttpClientName` allows. A DPoP proof names the URI it is sent to, and the provider compares the
proof's `htu` with the tenant's published origin
([ADR-1365](1365-no-url-the-server-publishes-or-compares-takes-its-origin-from-the-requests-host.md)), so a test cannot
relay the call through a listener of its own: the proof would name the listener.

## Decision

The registered token requirements are `user`, `client`, `user_or_client`, `exchange` and `none`. The BFF's own and
exchanged tokens are served without a lease, and remote APIs are forwarded through YARP's `IHttpForwarder` over a named
HTTP client that a host configures.

1. **`client` is served only to a request that has a session.** The session proves the caller is the BFF's own
   frontend. Without one, the requirement answers `401 session_required` and asks the OP for nothing.
2. **The BFF's own tokens are cached in memory on each replica.**
   - `DefaultBffClientTokenService` is a singleton that holds a `ConcurrentDictionary` of tokens, keyed by frontend
     instance, resource and scope. This is an accepted exception to the rule that a singleton is stateless
     ([`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md)). The token
     belongs to the BFF, not to a request or a session, so no request's state leaks into another.
   - The cache is bounded by configuration, because the resource and scope come from the endpoint's or remote API's
     registration, never from the request. It is dropped when the frontend set changes
     ([ADR-1442](1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md)).
   - Correctness does not depend on the cache: a replica with no entry, or one that restarts, requests a new token.
   - Nothing is persisted and no lease spans the replicas. Each replica requests its own token, which costs up to one
     grant per replica per token lifetime.
3. **`user_or_client` chooses by the session alone and never falls back.** When a session resolves it acts exactly as
   `user`, and a failure of the user's token (such as `401 refresh_failed` or `503 provider_unavailable`) is the answer.
   Only a request with no session gets `client`, without the session check of rule 1. That is the requirement's
   purpose: an API that serves anonymous callers too.
4. **`exchange` presents the session's default user token and caches the result in the session.** The `subject_token`
   is the token that `user` serves for the provider's default resource and scope, with the subject token type
   `urn:ietf:params:oauth:token-type:access_token`. The result is bound to the session's DPoP key and stored in the
   session record under requirement `exchange`, per resource and scope. No lease spans the replicas. A lock inside the
   replica only keeps one session from exchanging the same resource and scope twice at once.
5. **`none` serves no token.** It succeeds with no token when a session resolves and otherwise answers
   `401 session_required`. It marks an endpoint that needs a session but calls no API.
6. **One registered lock table serves every token service, with a key space per purpose.** `BffRefreshLocks` is a
   singleton keyed by `(purpose, key)`, with the purposes `user`, `client` and `exchange`. It serializes token work on
   one key inside one replica.
   - The exchange service holds its lock on a session while it asks the user token service for the subject token. The
     user token service then takes its own lock on the same session to refresh it. If the two shared a key, the
     exchange would wait on itself until the lease timed out.
   - Every wait is bounded by `RefreshLeaseDuration` and answers `503 refresh_wait_timeout` when it runs out. A
     cancelled holder releases its lock, so the next caller requests the token itself.
7. **A remote API is forwarded through YARP's `IHttpForwarder`.** `MapBffRemoteApi(pattern, destination, configure)`
   and the frontend's `RemoteApis` entries map a local path prefix to an absolute destination. An entry has `Path`,
   `Destination`, `TokenRequirement` (default `user`), `Resource`, `Scope`, `AllowedMethods` and `Timeout` (default
   100 seconds). The destination uses `https` outside Development and has no user info, query or fragment.
   - The endpoint is a BFF API, so the session and the anti-forgery header are required as for any other BFF API.
   - The forwarder answers `400` for a remaining path with a dot segment, an encoded `/` or `\`, or a backslash, which
     could leave the destination's base path once the upstream server decodes it. It answers `405` for a method
     outside the optional allow-list.
   - It strips the browser's `Cookie`, `Authorization`, `DPoP`, anti-forgery and forwarding headers, and the upstream
     `Set-Cookie`.
   - It attaches the token through `BffAccessTokenHandler`, which signs a DPoP proof for a DPoP-bound token and retries
     a `use_dpop_nonce` challenge once.
8. **Remote APIs send through the `IHttpClientFactory` client `BffDefaults.RemoteApiHttpClientName`.** The client is
   named `NCode.Identity.Bff.RemoteApis`, and a transport handler asks `IHttpMessageHandlerFactory` for its handler on
   every request, so the factory's handler rotation applies.
   - The BFF registers the client's primary handler: a `SocketsHttpHandler` that follows no redirect, sends or stores
     no cookie, uses no proxy and decompresses no response, with a 15-second connect timeout, multiple HTTP/2
     connections and YARP's trace-header propagator. The API's timeout bounds the wait for upstream activity.
   - The BFF removes the factory's loggers from the client, because the forwarder logs its own requests and the
     factory's loggers would log each forwarded URI with its query.
   - A host adds handlers to the client or sets a primary handler of its own by configuring the client of that name
     after the BFF's registration, and then owns the properties it changes. `BffAccessTokenHandler` stays outside the
     client, so the token and its proof are attached to every request whatever handlers a host adds.
9. **A host that describes its routes in YARP configuration names their token in route metadata.**
   `AddReverseProxy()…AddBffTransforms()` makes every route whose metadata has `BffTokenRequirement` a remote API, with
   the optional `BffResource`, `BffScope` and `BffFrontend`. The keys have no `:`, because YARP's configuration loader
   reads a colon as a section separator. `MapReverseProxy().AsBffRemoteApis()` marks those routes as BFF APIs, so they
   require the session and the anti-forgery header. The support lives in `NCode.Identity.Bff` itself, and YARP routes
   keep YARP's `IForwarderHttpClientFactory`.
   - Every route's metadata is validated when YARP loads it: the requirement must be registered, and `BffResource` or
     `BffScope` without `BffTokenRequirement` is refused, so a typo fails the configuration instead of forwarding
     without a token.
   - A remote API route loses the browser's `Cookie`, `Authorization`, `DPoP` and anti-forgery headers. Its token is
     resolved before anything is forwarded, so a failure answers the same problem as `UseBff()`. The token is attached
     by the same `BffAccessTokenHandler`, and the upstream response loses every `Set-Cookie`.
   - Every other route keeps its cookies but never forwards the BFF's session or login cookie, and an upstream
     `Set-Cookie` for either is dropped.
   - `AddBffTransforms()` decorates the last registered `IForwarderHttpClientFactory`, and the proxy refuses to start
     when another factory is registered after it, because a later factory would forward without the token handler or
     the cookie boundary.
   - A route that names a requirement but is not marked with `AsBffRemoteApis()` fails closed. The token accessor
     serves a token only to an endpoint the BFF guards, so the transform throws, nothing is forwarded, and YARP answers
     `502`. The host's start-time check refuses such a route
     ([ADR-1433](1433-a-bff-host-checks-every-endpoints-bff-metadata-at-start-and-again-whenever-the-endpoints-change.md)).
   - Path and header policy stay with the route: the route's own pattern and YARP transforms decide what is forwarded,
     including the forwarding headers, as for any YARP route.

## Options considered

- **Serve `client` to anonymous callers.** Rejected: any client on the network could then call the API with the BFF's
  rights. `user_or_client` covers the API that serves anonymous callers, and only on the endpoints that name it.
- **Fall back from `user` to `client` in `user_or_client`.** Rejected: an expired session would silently call the API
  with the BFF's rights instead of the user's, and the frontend would never learn the session had ended.
- **Cache the BFF's own tokens in shared state under a lease, as `user` does.** Rejected: nothing is revoked when two
  replicas each request a token, so a lease would only add a shared-state round trip to every request. Storing the
  token would also put a bearer credential in shared state for no gain.
- **Lease the exchange across replicas.** Rejected for the same reason: the exchange presents the user's access token,
  which a second exchange can present again.
- **A private lock table in each service.** Rejected: the table is the one place that bounds every wait and releases a
  cancelled holder's lock, and one registered table with a key space per purpose keeps that logic in one tested type.
- **Forward with a raw `HttpClient`, or require the full YARP reverse proxy.** Rejected: `IHttpForwarder` already
  streams the request and response bodies, drops the hop-by-hop headers and maps upstream failures to `502` and `504`.
  The full proxy's route and cluster configuration is more than one mapped prefix needs. The full proxy stays available
  to a host that already uses it.
- **Put the YARP configuration support in a separate `NCode.Identity.Bff.Yarp` package.** Rejected: the transform needs
  the BFF's internal token resolution, problem writer and cookie names, so a separate package would need new public
  seams for each. `NCode.Identity.Bff` already depends on YARP for the forwarder, so keeping the support in it adds no
  dependency.
- **A remote-API destination resolver service for dynamic routing.** Rejected: YARP's routes already match on host,
  headers and query and pick a destination from a configured cluster, so the allow-list stays configuration. A resolver
  would be one more place where a destination could be derived from the request.
- **A private `SocketsHttpHandler` for the remote-API transport.** Rejected: no host can add a handler, and a test must
  relay the call through a listener that its proof then names.
- **A public handler-source interface of the BFF's own.** Rejected: `IHttpClientFactory` is the framework's seam for the
  handlers of an outbound client, and the protocol client uses it.
- **The named client and the requirements above (chosen).** The session alone chooses the BFF's own token, no lease
  guards a token whose reuse revokes nothing, and a host configures the transport through the framework's seam.

## Consequences

- An endpoint or remote API names its requirement, resource and scope at registration, and the frontend cannot change
  them.
- A deployment with `n` replicas requests up to `n` client tokens per resource and scope per token lifetime.
- The session record grows by one cached exchanged token per resource and scope that the frontend calls.
- A remote API's upstream sees the BFF, never the browser: no browser credential reaches it, and with
  `MapBffRemoteApi` no forwarding header either.
- A YARP route's forwarding headers follow its own transforms, so a host that forwards `X-Forwarded-*` to an upstream
  does so on purpose.
- A host that registers its own `IForwarderHttpClientFactory` must do so before `AddBffTransforms()`.
- A host configures the handlers of every remote API with `AddHttpClient(BffDefaults.RemoteApiHttpClientName)`.
- A test routes the BFF's remote API to a test server with the client's primary handler, and the DPoP proof names the
  provider's published origin.
- `BffDefaults.RemoteApiHttpClientName` is public API of `NCode.Identity.Bff.Protocol.Abstractions`.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.5 and §5.6
- [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)
- [ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md)
- [ADR-1292](1292-the-bffs-cookie-authenticates-only-guarded-endpoints-which-refuse-cross-site-requests-and-reload-cookieless-navigations.md)
- [ADR-1365](1365-no-url-the-server-publishes-or-compares-takes-its-origin-from-the-requests-host.md)
- [ADR-1433](1433-a-bff-host-checks-every-endpoints-bff-metadata-at-start-and-again-whenever-the-endpoints-change.md)
- [ADR-1442](1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md)
- RFC 6749 §4.4, RFC 8693, RFC 9449, RFC 9700
- `src/NCode.Identity.Bff/Tokens/`, `src/NCode.Identity.Bff/RemoteApis/`
- Issues [#395](https://github.com/NCodeGroup/identity/issues/395) and
  [#716](https://github.com/NCodeGroup/identity/issues/716)
