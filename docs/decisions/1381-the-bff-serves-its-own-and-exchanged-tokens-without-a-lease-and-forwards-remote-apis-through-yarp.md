# 1381. The BFF serves its own and exchanged tokens without a lease and forwards remote APIs through YARP

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group

## Context

The BFF's first token requirement, `user`, serves the session's own access token and refreshes it single-flight under a
lease on its refresh token
([ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md)). The lease spans the
replicas because the OP rotates refresh tokens: two replicas that present the same refresh token make the OP revoke the
whole family (RFC 9700 §4.14.2).

Phase 3 of the [design](../backend-for-frontend.md) (issue [#395](https://github.com/NCodeGroup/Identity/issues/395))
adds four more requirements and a way to call a remote API with them:

- `client`: the BFF's own token from the `client_credentials` grant (RFC 6749 §4.4).
- `user_or_client`: the user's token when the request has a session, otherwise the BFF's own.
- `exchange`: a token for another resource or scope, from an RFC 8693 token exchange that presents the user's token.
- `none`: no token at all, only a session.
- Remote APIs: a local path prefix that forwards each request to a destination with one of those tokens attached.

Neither new grant has the reuse hazard that the lease guards against. `client_credentials` issues no refresh token
(RFC 6749 §4.4.3), and the BFF keeps no refresh token from an exchange. A second request for either token returns a
second, equally valid token and revokes nothing.

## Decision

1. **`client` is served only to a request that has a session.** The session is what proves the caller is the BFF's
   own frontend. Without one, the requirement answers `401 session_required` and asks the OP for nothing.
2. **The BFF's own tokens are cached in memory on each replica, keyed by resource and scope.**
   - `DefaultBffClientTokenService` is a singleton that holds a `ConcurrentDictionary` of tokens. This is an accepted
     exception to the rule that a singleton is stateless
     ([`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md)). The token
     belongs to the BFF, not to a request or a session, so no request's state leaks into another.
   - The cache is bounded by configuration. The resource and scope come from the endpoint's or remote API's
     registration, never from the request, so it holds at most one entry per configured pair.
   - Correctness does not depend on the cache. A replica that has no entry, or that restarts, requests a new token.
   - Nothing is persisted and no lease spans the replicas. Each replica requests its own token, which costs one grant
     per replica per token lifetime.
3. **`user_or_client` chooses by the session alone and never falls back.** When a session resolves it acts exactly as
   `user`, and a failure of the user's token (such as `401 refresh_failed` or `503 provider_unavailable`) is the answer. Falling back
   to the BFF's own token would call the API with other rights than the user's. Only a request with no session gets
   `client`, without the session check of decision 1. That is the requirement's purpose: an API that serves anonymous
   callers too.
4. **`exchange` presents the session's default user token and caches the result in the session.** The `subject_token`
   is the token that `user` serves for the provider's default resource and scope, with the subject token type
   `urn:ietf:params:oauth:token-type:access_token`. The result is bound to the session's DPoP key and stored in the
   session record under requirement `exchange`, per resource and scope. No lease spans the replicas, because a second
   exchange revokes nothing. A lock inside the replica only keeps one session from exchanging the same resource and
   scope twice at once.
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
   and `Bff:RemoteApis` map a local path prefix to an absolute destination. The destination uses `https` outside
   Development and has no user info, query or fragment.
   - The endpoint is a BFF API (`AsBffApi`), so the session and the anti-forgery header are required as for any other
     BFF API.
   - The forwarder answers `400` for a remaining path with a dot segment, an encoded `/` or `\`, or a backslash, which
     could leave the destination's base path once the upstream server decodes it. It answers `405` for a method
     outside the optional allow-list.
   - It strips the browser's `Cookie`, `Authorization`, `DPoP`, anti-forgery and forwarding headers, and the upstream
     `Set-Cookie`. It never follows a redirect, never uses a proxy, and waits at most the API's timeout for upstream
     activity.
   - It attaches the token through `BffAccessTokenHandler`, which signs a DPoP proof for a DPoP-bound token and
     retries a `use_dpop_nonce` challenge once.

## Alternatives considered

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
- **Forward with `HttpClient` directly, or require the full YARP reverse proxy.** Rejected: `IHttpForwarder` already
  streams the request and response bodies, drops the hop-by-hop headers and maps upstream failures to `502` and `504`. The
  full proxy's route and cluster configuration is more than one mapped prefix needs. The full proxy stays available to
  a host that already uses it.

## Consequences

- An endpoint or remote API names its requirement, resource and scope at registration, and the frontend cannot change
  them.
- A deployment with `n` replicas requests up to `n` client tokens per resource and scope per token lifetime.
- The session record grows by one cached exchanged token per resource and scope that the frontend calls.
- A remote API's upstream sees the BFF, never the browser: no browser credential and no forwarding header reaches it.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.5 and §5.6
- [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)
- [ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md)
- [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md)
- RFC 6749 §4.4, RFC 8693, RFC 9449, RFC 9700
- `src/NCode.Identity.Bff/Tokens/`, `src/NCode.Identity.Bff/RemoteApis/`
- Issue [#395](https://github.com/NCodeGroup/Identity/issues/395)
