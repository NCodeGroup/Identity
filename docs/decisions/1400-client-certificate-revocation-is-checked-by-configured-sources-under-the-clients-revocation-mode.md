# 1400. Client certificate revocation is checked by configured sources under the client's revocation mode

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0144](project-template/0144-correctness-guards-fail-closed-capacity-guards-fail-open.md), [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)

## Context

The `tls_client_auth` checker of [ADR-1173](1173-a-mutual-tls-client-is-authenticated-by-its-registered-certificate-chained-to-configured-trust-anchors.md)
must decide whether a client's certificate chain holds a revoked certificate. Several forces shape that check:

- [RFC 8705 §2.1](https://www.rfc-editor.org/rfc/rfc8705#section-2.1) leaves revocation to the PKI that issued the
  client's certificate, and FAPI 2.0 adds no rule for it. [project-template ADR-0144](project-template/0144-correctness-guards-fail-closed-capacity-guards-fail-open.md)
  makes a revocation check a correctness guard, so it fails closed and a relaxation is one explicit opt-in.
- Revocation data comes from more than one kind of source (OCSP responders and certificate revocation lists), and a
  consumer or sibling package can add another, so the set of sources is an open vocabulary
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
- One retrieval of a list or response serves every tenant whose certificates name the same location, so its limits and
  the choice of sources belong to the deployment, not to a tenant.
- Without care, a burst of checks repeats one download, an unreachable distribution point costs the full timeout on
  every request, and a retrieval that one caller has just recorded is downloaded again by the next.
- [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
  makes a fetch that many requests share single-flight, remembers a failed fetch for a short, configured time and makes
  its limits options validated when the host starts.

## Decision

**A certificate chain is checked by the configured revocation sources, in their configured order, under the client's
`tls_client_auth_revocation_mode`; concurrent checks share one retrieval, a retrieval that yields nothing is remembered
for a configured time, and the soft-fail mode is the one opt-in that accepts an unknown status.**

1. **The revocation mode is a policy setting.** `tls_client_auth_revocation_mode` (a tenant and client setting, merged
   with `Replace`) is `online` (the default), `online_soft_fail` or `none`. It is a closed policy vocabulary: three
   named behaviors, not an extension point. A PKI that publishes no revocation data, or issues short-lived
   certificates, sets `none`.
2. **The chain validator asks a checker per element.** It checks each element below the trust anchor against its issuer
   through `IClientCertificateRevocationChecker.CheckAsync(ClientCertificateRevocationContext, …)`.
   `ClientCertificateRevocationStatus` is `Unknown` (the zero value), `Good` or `Revoked`. Under `online`, `Unknown` is
   refused. Under `online_soft_fail`, `Unknown` is accepted with the warning
   `ClientCertificateRevocationUnknownAccepted`. Both modes refuse `Revoked`.
3. **The default checker asks its sources in turn.** `DefaultClientCertificateRevocationChecker` asks the selected
   `IClientCertificateRevocationSource`s one after another. The first `Good` or `Revoked` decides, and when every source
   answers `Unknown` the check is `Unknown`. A host replaces the checker only to combine sources in another way.
4. **A source is described by data.** A `ClientCertificateRevocationSourceDescriptor`
   (`NCode.Identity.OpenId.Authentication.Abstractions`; a required `Name` and an `Order`) is contributed through
   `ICollectionDataSource<ClientCertificateRevocationSourceDescriptor>`. `AddClientCertificateServices()` contributes
   `ocsp` (order 100) and `crl` (order 200). What each built-in source checks is
   [ADR-1259](1259-the-built-in-revocation-sources-check-ocsp-and-every-crl-form-rfc-5280-defines.md)'s.
5. **The catalog is built from descriptors alone.** `IClientCertificateRevocationSourceCatalog` exposes `SourceNames`,
   ordered by `Order` and then by ordinal name, and `TryGet`. It refuses an empty, whitespace or duplicate name and
   constructs no source.
6. **Options carry the deployment's choices.** `ClientCertificateRevocationOptions` binds strictly from
   `OpenId:ClientCertificateRevocation` and is validated when the host starts
   ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)):
   - `FetchTimeout`, the time one retrieval may take, 5 seconds by default;
   - `MaxResponseSizeBytes`, the largest response read, 10 MiB by default;
   - `MaxCacheDuration`, the longest a verified response is reused, 1 hour by default;
   - `FailureCacheDuration`, how long a retrieval that yielded nothing is remembered, 1 minute by default;
   - `Sources`, the names of the sources to use, in order.

   They are options, not settings, because one retrieval serves every tenant that names a location and reachability is
   the deployment's.
7. **The options are validated against the described sources.** `ClientCertificateRevocationOptionsValidator` fails
   the host when `Sources` names a source no descriptor describes (the message lists the registered ones) or repeats a
   name. An empty `Sources` means every described source in default order, never "check nothing". The timeouts and
   durations must be periods a timer accepts and `MaxResponseSizeBytes` at least one.
8. **A mismatch stops the host at start.** The checker's constructor refuses a source without a descriptor, two sources
   of one name and a descriptor without a source. The hosted `ClientCertificateRevocationCheckerWarmup` resolves the
   checker when the host starts, so such a mismatch is found then and not on the first request.
9. **Concurrent checks share one retrieval.** `RevocationRetrievalCache` shares retrievals through
   `SingleFlight<TResult>` ([ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md)).
   The retrieval runs without the caller's cancellation token, on its own copy of the issuing certificate and bounded by
   `FetchTimeout`, so a caller that cancels or disposes its chain does not break it for the others. A retrieval that
   yields nothing is remembered for `FailureCacheDuration`; while it is remembered the source is not asked, the
   suppression is logged as `RevocationRetrievalSuppressed` at the debug level, and the status is `Unknown`.
10. **The shared retrieval checks the caches again.** Inside the shared retrieval, the value cache and the remembered
    failure are read again, and what they hold is returned without a download. The check before the helper stays: it
    answers a cached value or a remembered failure without joining a retrieval, and it alone logs the suppression.
    The `jwks_uri` resolver checks its caches the same way
    ([ADR-1106](1106-a-clients-jwks-uri-key-set-is-fetched-when-needed-shared-between-callers-and-never-served-stale.md)).
11. **Every revocation cache is process-local.** The caches are `IMemoryCache`, never the shared-state port
    ([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)):
    lists are large and rebuildable, and one replica's network failure must not stop the others. The remembered failure
    is the back-off between retrievals, and the guarded client's GET retry runs within `FetchTimeout`.

## Options considered

- **A shared-state cache.** Rejected: lists are large and rebuildable, and one replica's network failure must not stop
  the others.
- **A soft-fail boolean setting.** Rejected: one policy setting states one policy, and a boolean beside `none` has a
  meaningless combination.
- **Per-tenant timeouts or per-tenant source selection.** Rejected: one retrieval serves every tenant that names the
  location, and reachability is the deployment's.
- **Registration order or an `enum` of sources.** Rejected: an `enum` is closed and registration order is implicit;
  descriptors with an explicit `Order` keep the vocabulary open and the order visible
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
- **An empty `Sources` meaning no check.** Rejected: a revocation check that silently checks nothing fails open.
- **Ignoring an unknown source name.** Rejected: a misspelled name would drop a check without notice, so the host
  refuses to start instead.
- **Any `Revoked` answer winning over an earlier `Good`.** Rejected: the first definite answer decides, so the order
  of sources is the order of authority.
- **Checking the caches only before the helper.** Rejected: a caller that passes that check just before another
  retrieval records its result downloads the same response twice.

The chosen design is the configured, described and ordered sources under a policy mode, with a shared retrieval that
checks its caches again.

## Consequences

- An outage of a revocation source costs each replica one timeout per `FailureCacheDuration`.
- Under `online`, a certificate that a failed retrieval covers is refused for up to `FailureCacheDuration` after the
  source recovers.
- `online_soft_fail` admits a revoked certificate whenever its status is unknown, not only while a source is
  unreachable: a list that fails verification, which an attacker on the path to an `http` distribution point can serve,
  one that is too large, not yet valid or past its next update, and the forms a source does not support all let a
  revoked certificate through. An operator opts into it only for a PKI whose outage must not stop its clients.
- A deployment whose responders are unreachable sets `Sources` to `crl` to avoid them.
- A new source is one class and one descriptor; a misconfigured name, duplicate or missing pairing stops the host when
  it starts.
- A retrieval that finds a cached value or a remembered failure returns it without a download and without the
  suppression log entry.

## References

- [ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md):
  client-authentication methods are named by registered descriptors.
- [ADR-1173](1173-a-mutual-tls-client-is-authenticated-by-its-registered-certificate-chained-to-configured-trust-anchors.md):
  `tls_client_auth` certificates chain to configured trust anchors.
- [ADR-1259](1259-the-built-in-revocation-sources-check-ocsp-and-every-crl-form-rfc-5280-defines.md): what the built-in
  sources check.
- [ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md): the
  guarded client and `SingleFlight<TResult>`.
- [RFC 5280](https://www.rfc-editor.org/rfc/rfc5280) and [RFC 8705](https://www.rfc-editor.org/rfc/rfc8705).
- Code: `src/NCode.Identity.OpenId.Authentication.Abstractions/ClientCertificates/` for
  `ClientCertificateRevocationSourceDescriptor` and `IClientCertificateRevocationSourceCatalog`, and
  `src/NCode.Identity.OpenId.Authentication/ClientCertificates/` for `ClientCertificateRevocationOptions`,
  `ClientCertificateRevocationOptionsValidator`, `DefaultClientCertificateRevocationSourceCatalog`,
  `DefaultClientCertificateRevocationSourceDataSource`, `DefaultClientCertificateRevocationChecker`,
  `RevocationRetrievalCache` and `DefaultClientCertificateChainValidator`.
