# 1335. A client's `jwks_uri` is fetched single-flight and a failed fetch is remembered

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md) and [ADR-1106](1106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md)

## Context

A client that publishes its public keys at a `jwks_uri` has them fetched while it authenticates, and the server caches
an accepted key set for the client's `jwks_uri_cache_duration`
([ADR-1106](1106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md)). Until a set is cached, every token
request of that client starts its own fetch, and a `jwks_uri` that is unreachable, answers with an error, or publishes a
set the import policy refuses is fetched again for every request, so a client that sends many requests makes the server
send as many.
[project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
asks that concurrent callers of one caller-supplied URL share a single fetch (decision 4) and that a failed fetch is
remembered for a short time (decision 5).
[ADR-1315](1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md) bounded each fetch but left
both open (issue [#466](https://github.com/NCodeGroup/Identity/issues/466)).

## Decision

1. **Concurrent misses share one fetch.** The resolver keeps one in-flight fetch per `jwks_uri` and per set of fetch
   limits, in a dictionary of lazily started tasks, as the revocation retrieval does
   ([ADR-1252](1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md)). The
   fetch runs without any caller's token and is bounded by the client's `jwks_uri_fetch_timeout`; each caller waits with
   its own token, so a caller that gives up does not break the fetch for the others. The fetch writes the accepted set
   to the cache, or the failure entry, before it leaves the in-flight dictionary, so no caller can slip between the two.
   The log of a failed fetch names the client whose request started it.

2. **A fetch that yields no key is remembered.** An unreachable `jwks_uri`, a non-2xx answer, an oversized or malformed
   document, and a set the import policy refuses are remembered in the memory cache of each replica for
   `OutboundHttpOptions.FailureCacheDuration` (`OpenId:OutboundHttp:FailureCacheDuration`, one minute by default,
   validated at start to be greater than zero and at most one day). While it is remembered, the resolver returns no
   published keys and logs `ClientJwksFetchSuppressed` at debug level instead of fetching.

3. **The duration is a host option, not a setting.** It protects the server's capacity. A per-scope setting merges by
   `Min` ([ADR-1315](1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md), decision 4), so
   a tenant or a client could set it to zero and switch the protection off. The fetch limits stay settings only because
   lowering them is safe.

4. **The fetch limits are part of the key.** The key holds the fetch timeout, the size cap and the URI, so a client or a
   tenant that sets lower limits can never make another client's fetch of the same URI fail or be suppressed. The
   accepted-set cache stays keyed by the URI alone.

5. **No stale key set is served after a failed refresh.** The template makes it optional (decision 5). A client that
   removes a compromised key from its `jwks_uri` must not have the key kept alive by an attacker who makes the
   `jwks_uri` unreachable.

## Options considered

- **Key the shared fetch and the failure by the URI alone.** Rejected: a client with a one-second fetch timeout would
  make the fetch of another client's document fail, and the failure would then suppress that client too.
- **A per-scope setting for the failure duration.** Rejected: see decision 3.
- **Serve the last good key set while a refresh fails.** Rejected: see decision 5.
- **Reuse `RevocationRetrievalCache`.** Rejected: it is specific to revocation data and carries that feature's options
  and log entries; extracting a shared helper is a larger change than this decision needs.

## Consequences

- An unreachable `jwks_uri` costs each replica one fetch timeout per `FailureCacheDuration` and set of fetch limits,
  instead of one per request.
- Keys that a client publishes after a failed fetch are seen within `FailureCacheDuration`, or within its
  `jwks_uri_cache_duration` once a fetch succeeds.
- The remembered failure is per replica and is not shared through a distributed cache.

## References

- [ADR-1106](1106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md),
  [ADR-1252](1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md),
  [ADR-1315](1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md)
- Issue [#466](https://github.com/NCodeGroup/Identity/issues/466)
- `DefaultClientKeyResolver.cs` and `OutboundHttpOptions.cs` in `NCode.Identity.OpenId.Authentication`
