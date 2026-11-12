# 1106. A client's jwks_uri key set is fetched when needed, shared between callers, and never served stale

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)

## Context

FAPI 2.0 readiness (issue [#54](https://github.com/NCodeGroup/identity/issues/54)) requires strong client
authentication. `private_key_jwt` ([ADR-1102](1102-a-client-authenticates-with-a-single-use-jwt-assertion-signed-by-its-key-or-its-secret.md))
and signed request objects (JAR, RFC 9101) both verify a client-signed JWT against the client's public keys. A client
onboards those keys as an inline JWK Set at registration, or through a `jwks_uri` that it controls and that the server
resolves at runtime, so the client can rotate keys without re-registering. The `jwks_uri` is the primary onboarding path
for FAPI.

Resolving a `jwks_uri` is a server-side fetch of a client-supplied URL, which is a server-side request forgery sink.
[project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
asks that concurrent callers of one caller-supplied URL share a single fetch (decision 4) and that a failed fetch is
remembered for a short time (decision 5). Until a key set is cached, every token request of a client would start its
own fetch, and a `jwks_uri` that is unreachable, answers with an error or publishes a set the import policy refuses
would be fetched again for every request, so a client that sends many requests would make the server send as many
(issue [#466](https://github.com/NCodeGroup/identity/issues/466)).

## Decision

**A client's key set at its `jwks_uri` is resolved through one replaceable seam, fetched only when a caller needs it,
shared between concurrent callers, remembered when the fetch fails, and never served after a failed refresh.**

1. **`IClientKeyResolver` is the one way to read a client's keys.** `ResolveSecretKeysAsync(OpenIdClient,
   CancellationToken)` is public, registered with `TryAddSingleton` and implemented by `DefaultClientKeyResolver`. The
   `client_assertion` handlers, the request-object and CIBA request loaders, `self_signed_tls_client_auth` and
   `DefaultClientJwtCredentialSelector` all read keys through it.

2. **A client has one source of public keys.** Without a `jwks_uri` the resolver returns the client's stored keys. With
   one it returns the client's shared (non-asymmetric) secrets plus the keys published there, and never the stored
   public keys, as [ADR-1161](1161-every-owners-public-keys-have-one-source-and-are-imported-under-one-key-policy.md) decides.

3. **The URI is validated and the fetch is bounded.** The URI must be absolute `https` and pass `jwks_uri_allowed_hosts`.
   The set is fetched through the guarded client of
   [ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md) under
   `jwks_uri_fetch_timeout` and `jwks_uri_max_size_bytes`, and imported with
   `IClientKeyImporter.TryImportPublishedKeySet`.

4. **An accepted set is cached per URI.** The cache lives for the client's `jwks_uri_cache_duration` (15 minutes by
   default, merged by `Min`; zero disables it). A cached public key owns no native handle, so the cache uses plain
   absolute expiration without eviction cleanup.

5. **Concurrent misses share one fetch.** The in-flight fetches are the shared `SingleFlight<TResult>` of
   [ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md), keyed by the URI and the fetch limits (the
   timeout and the size cap), so a client or tenant with lower limits never makes another client's fetch fail or be
   suppressed. The accepted-set cache stays keyed by the URI alone.

6. **The fetch is independent of its callers.** It runs without any caller's token, so a caller that gives up does not
   break the fetch for the others. It records the accepted set, or the failure, before it leaves the in-flight
   dictionary, and it checks both caches again before it fetches. The log entry of a failed fetch,
   `ClientJwksFetchFailed`, names the client whose request started it.

7. **A fetch that yields no key is remembered.** An unreachable `jwks_uri`, a non-2xx answer, an oversized or malformed
   document and a set the import policy refuses are remembered per replica, in its memory cache, for
   `OutboundHttpOptions.FailureCacheDuration` (`OpenId:OutboundHttp:FailureCacheDuration`, one minute by default,
   validated at start to be above zero and at most one day). While a failure is remembered, the resolver returns no
   published keys and logs `ClientJwksFetchSuppressed` at debug level instead of fetching.

8. **Resolution fails closed.** A failed or suppressed fetch yields no public keys, so signature verification fails. A
   fetch failure never throws and never grants access.

9. **No stale key set is served after a failed refresh.** The template makes the stale value optional; this decision
   declines it. A client that removes a compromised key from its `jwks_uri` must not have the key kept alive by an
   attacker who makes the `jwks_uri` unreachable.

10. **The failure duration is a host option, not a setting.** It protects the server's capacity. A per-scope setting
    merges by `Min`, so a tenant or a client could set it to zero and remove the protection. The fetch limits stay
    settings because lowering them is safe.

## Options considered

- **Key the shared fetch and the failure by the URI alone.** Rejected: a client with a one-second fetch timeout would
  make the fetch of another client's document fail, and the remembered failure would then suppress that client too.
- **A per-scope setting for the failure duration.** Rejected: a tenant or a client could set it to zero and switch the
  protection off, since per-scope settings merge by `Min`.
- **Serve the last good key set while a refresh fails.** Rejected: an attacker who makes the `jwks_uri` unreachable
  would keep a removed, possibly compromised key alive.
- **Reuse `RevocationRetrievalCache`.** Rejected: it carries the revocation feature's options and log entries.
- **Share one fetch per URI, remember failures per fetch limits and resolve through `IClientKeyResolver`.** Chosen.

## Consequences

- A FAPI client publishes its signing keys at a `jwks_uri` and rotates them without re-registering; its
  `private_key_jwt` and request-object signatures validate against the keys resolved at runtime.
- An unreachable `jwks_uri` costs each replica one fetch timeout per `FailureCacheDuration` and set of fetch limits,
  instead of one per request.
- Keys that a client publishes after a failed fetch are seen within `FailureCacheDuration`, or within its
  `jwks_uri_cache_duration` once a fetch succeeds.
- The remembered failure is per replica and is not shared between replicas.
- A fetch failure leaves the client without published public keys, so a client whose `jwks_uri` is down cannot
  authenticate with a key signature until the failure expires and a fetch succeeds.

## References

- [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md),
  decisions 4 and 5
- [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md),
  [ADR-1161](1161-every-owners-public-keys-have-one-source-and-are-imported-under-one-key-policy.md),
  [ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md)
- Issues [#54](https://github.com/NCodeGroup/identity/issues/54) and
  [#466](https://github.com/NCodeGroup/identity/issues/466)
- `DefaultClientKeyResolver.cs` and `OutboundHttpOptions.cs` in `NCode.Identity.OpenId.Authentication`
