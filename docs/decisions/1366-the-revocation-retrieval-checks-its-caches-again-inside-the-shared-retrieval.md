# 1366. The revocation retrieval checks its caches again inside the shared retrieval

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)

## Context

The revocation retrieval of a client certificate's CRL or OCSP response
([ADR-1252](1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md)) checks its
value cache and its remembered failure, then joins or starts one shared retrieval through `SingleFlight<TResult>`
([ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md)). A caller that checks the caches just before a
previous retrieval records its result, and reaches the helper just after that retrieval leaves the in-flight
dictionary, starts a new retrieval and downloads again what was recorded a moment earlier. The `jwks_uri` resolver
closes the same gap by checking its caches again inside the retrieval
([ADR-1106](1106-a-clients-jwks-uri-key-set-is-fetched-when-needed-shared-between-callers-and-never-served-stale.md) decision 1), and
ADR-1315 lists the revocation retrieval's gap as a consequence to follow up
([#500](https://github.com/NCodeGroup/identity/issues/500)).

## Decision

1. **The revocation retrieval checks its caches again before it downloads.** `RevocationRetrievalCache.RetrieveAsync`,
   the function the shared retrieval runs, first reads the cached value and the remembered failure, as the check before
   the helper does, and returns what it finds without calling the source.
2. **The check before the helper stays.** It answers a cached value or a remembered failure without joining a
   retrieval, and it alone logs that a remembered failure suppressed a retrieval.

## Options considered

- **Check only before the helper.** Rejected: the gap above downloads the same CRL or OCSP response twice.
- **Make the helper check the caches.** Rejected: ADR-1315 decision 3 leaves the value cache with its key and lifetime
  to each owner.

## Consequences

- This amends ADR-1315's consequence that the revocation retrieval does not check its caches again: both owners of
  `SingleFlight<TResult>` check their caches again inside the retrieval.
- A retrieval that finds a cached value or a remembered failure returns it without a download and without the
  suppression log entry.

## References

- [ADR-1252](1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md),
  [ADR-1106](1106-a-clients-jwks-uri-key-set-is-fetched-when-needed-shared-between-callers-and-never-served-stale.md) and
  [ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md)
- Issue [#500](https://github.com/NCodeGroup/identity/issues/500)
- `RevocationRetrievalCache.cs` in `NCode.Identity.OpenId.Authentication`
