# 1350. A shared fetch is single-flight through one helper

- **Status:** Accepted (amended by [ADR-1366](1366-the-revocation-retrieval-checks-its-caches-again-inside-the-shared-retrieval.md):
  the revocation retrieval checks its caches again inside the shared retrieval)
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)

## Context

[project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
asks that concurrent callers of one caller-supplied URL share a single fetch (decision 4) and that a failed fetch is
remembered for a short time (decision 5). The revocation retrieval
([ADR-1252](1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md)) and the
`jwks_uri` resolver
([ADR-1106](1106-a-clients-jwks-uri-key-set-is-fetched-when-needed-shared-between-callers-and-never-served-stale.md)) each kept their own
`ConcurrentDictionary` of lazily started tasks and their own `:unavailable` memory-cache entry, so a fix to one copy
missed the other ([#482](https://github.com/NCodeGroup/identity/issues/482)). ADR-1106 rejected reusing
`RevocationRetrievalCache` because that type carries the revocation feature's options and logs.

## Decision

1. **One helper keeps one retrieval in flight per key.** `SingleFlight<TResult>` keeps the in-flight retrievals in a
   `ConcurrentDictionary<string, Lazy<Task<TResult>>>`. A retrieval runs without any caller's token, each caller stops
   waiting with its own token (`WaitAsync`), and the retrieval leaves the dictionary when it ends, after it recorded its
   result.

2. **One helper remembers a failure.** `RememberFailure(key)` stores `{key}:unavailable` in `IMemoryCache` for the
   duration the owner gives, `IsFailureRemembered(key)` reads it, and `FailureCacheDuration` returns that duration, so
   the owner's log reports the duration the helper remembers.

3. **Each owner keeps what is its own.** The owner decides what counts as a failure, keeps the value cache with its key
   and lifetime, makes the check before it calls the helper, and keeps its log entries and options. The `jwks_uri`
   resolver still checks its caches again inside the retrieval (ADR-1106 decision 1).

4. **Each owner creates its own instance.** The helper is not a dependency-injection service, so the keys of two
   features never share a dictionary.

5. **Any other shared remote fetch uses the helper.** A new fetch that several callers share does not add a third copy.

## Options considered

- **Keep two copies.** Rejected: the copies drift, as issue #482 shows.
- **Reuse the revocation cache.** Rejected: it carries the revocation options and logs (ADR-1106).
- **A helper that also caches the value.** Rejected: the owners cache under different keys and lifetimes.
- **A singleton service.** Rejected: the keys of different features would mix in one dictionary.

## Consequences

- There is no behavior change: the cache keys, the logs and the durations stay as they were.
- The revocation retrieval does not check its caches again inside the retrieval, unlike the resolver; making it do so
  is a follow-up.
- A fix to the single-flight mechanism reaches every owner.

## References

- [ADR-1252](1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md) and
  [ADR-1106](1106-a-clients-jwks-uri-key-set-is-fetched-when-needed-shared-between-callers-and-never-served-stale.md)
- Issue [#482](https://github.com/NCodeGroup/identity/issues/482)
- `SingleFlight.cs`, `RevocationRetrievalCache.cs` and `DefaultClientKeyResolver.cs` in
  `NCode.Identity.OpenId.Authentication`
