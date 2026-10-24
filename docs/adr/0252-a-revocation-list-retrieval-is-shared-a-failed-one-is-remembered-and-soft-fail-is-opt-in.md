# 252. A revocation list retrieval is shared, a failed one is remembered, and soft-fail is opt-in

- **Status:** Accepted (amended by
  [ADR-0259](0259-revocation-is-checked-by-registered-sources-ocsp-before-every-crl-form.md): OCSP and every CRL form)
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

The checker of [ADR-0173](0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md) (backlog `R64`) has
four weaknesses:

- It retrieves a list once per concurrent check, so a burst of requests for one list repeats one download.
- While a distribution point is unreachable or serves an unusable list, it pays the full fetch and its timeout on every
  request.
- Its limits, a five-second timeout, a 10 MiB size and a one-hour cache, are fixed in code.
- It refuses a list signed with RSASSA-PSS, and it refuses every unknown status, so an outage of a revocation source
  stops every client of that PKI.

## Decision

**Concurrent checks of a list share one retrieval, a retrieval that yields no usable list is remembered for a
configured time, and `online_soft_fail` is a revocation mode that a tenant or client opts into.**

1. **The limits are options.** `ClientCertificateRevocationOptions` binds strictly from
   `OpenId:ClientCertificateRevocation` and is validated when the host starts
   ([ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)):
   - `FetchTimeout`, the time one retrieval may take, five seconds by default;
   - `MaxResponseSizeBytes`, the largest list read, 10 MiB by default;
   - `MaxCacheDuration`, the longest a verified list is reused, one hour by default;
   - `FailureCacheDuration`, how long a failed retrieval is remembered, one minute by default.

   The validator requires `FetchTimeout` to be a period that a timer accepts, `MaxResponseSizeBytes` to be at least one,
   and both durations to be above zero. They are options, not settings, because one retrieval serves every tenant that
   names the same list.
2. **Concurrent checks share one retrieval.** The checker keeps a `ConcurrentDictionary` of
   `Lazy<Task<CertificateRevocationList?>>` keyed by the cache key, which holds the location and the SHA-256 thumbprint
   of the issuing certificate. The retrieval runs with `CancellationToken.None`, bounded by `FetchTimeout`, on its own
   certificate that it loads from the raw data of the issuing certificate. A caller that cancels, or disposes the
   certificates of its chain, does not break the retrieval for the others. The retrieval stores its result in the cache
   and removes its own entry when it ends. Each caller waits with `WaitAsync(cancellationToken)`.
3. **A failed retrieval is remembered.** A retrieval that yields no usable list stores `{cacheKey}:unavailable` in
   `IMemoryCache` for `FailureCacheDuration`. While the entry exists, the checker does not retrieve the list, logs
   `CertificateRevocationListRetrievalSuppressed` at the debug level, and treats the list as unavailable, so a
   certificate that only that list covers has an unknown status. Every cache is process-local and never uses the
   shared-state port ([ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)): a list
   can be rebuilt from its source and can weigh 10 MiB, and one replica's network failure must not stop the others.
4. **RSASSA-PSS lists verify.** The signature algorithm `1.2.840.113549.1.1.10` is accepted only with explicit
   parameters ([RFC 4055](https://www.rfc-editor.org/rfc/rfc4055)): a hash of SHA-256, SHA-384 or SHA-512 whose
   parameters are absent or NULL, MGF1 over the same hash, a salt length equal to the hash length (32, 48 or 64 bytes),
   and a trailer field that is absent or 1. Parameters that leave a field to its default, which is SHA-1, and SHA-1
   signatures are refused.
5. **`online_soft_fail` accepts an unknown status.** `tls_client_auth_revocation_mode` accepts `online`,
   `online_soft_fail` and `none`. Under `online_soft_fail`, a revoked certificate is refused, and a certificate whose
   status is unknown is accepted with the warning `ClientCertificateRevocationUnknownAccepted`. The default is
   `online`, which fails closed:
   [RFC 8705 §2.1](https://www.rfc-editor.org/rfc/rfc8705#section-2.1) leaves revocation to the PKI that issued the
   client's certificate, FAPI 2.0 adds no rule for it, and a secure default fails closed.
6. **The rest of `R64` stays in the backlog.** OCSP with a response cache, delta CRLs, partitioned CRLs (`reasons` and
   `onlySomeReasons`), indirect CRLs and a CRL signed by another key of the CA stay unsupported, and the checker answers
   unknown for them. A deployment whose PKI needs them replaces `IClientCertificateRevocationChecker`.

## Options considered

- **A shared-state cache.** Rejected: lists are large and rebuildable, and one replica's network failure must not stop
  the others.
- **A separate soft-fail boolean setting.** Rejected: one policy setting states one policy, and a boolean beside `none`
  has a meaningless combination.
- **Per-tenant timeouts as settings.** Rejected: one retrieval serves every tenant that names the list.
- **Retrying with backoff.** Rejected: the remembered failure is the backoff.

## Consequences

- An outage costs each replica one timeout per `FailureCacheDuration`.
- Under `online`, a certificate that the failed list covers is refused for up to `FailureCacheDuration` after the
  source recovers.
- `online_soft_fail` accepts every certificate whose status is unknown, not only while its source is unreachable: a
  list that fails verification, which an attacker on the path to an `http` distribution point can serve, a list that is
  too large, not yet valid or past its next update, and the list forms that the checker does not support all let a
  revoked certificate through. An operator opts into it only for a PKI whose outage must not stop its clients.
- An option that a timer or a size refuses stops the host when it starts, not the first time a list is retrieved.
- This implements the resilience and policy parts of backlog `R64`; OCSP and the remaining list forms stay there.

## References

- [ADR-0173](0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md): `tls_client_auth` certificates
  chain to configured trust anchors.
- [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md): replicas share single-use
  state through an atomic expiring key set.
- [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md): configuration binds strictly and is
  validated at startup.
- [RFC 5280](https://www.rfc-editor.org/rfc/rfc5280): the certificate revocation list profile.
- [RFC 4055](https://www.rfc-editor.org/rfc/rfc4055): RSASSA-PSS in certificates and revocation lists.
- [RFC 8705](https://www.rfc-editor.org/rfc/rfc8705): mutual-TLS client authentication.
- Code: `src/NCode.Identity.OpenId.Authentication/ClientCertificates/DefaultClientCertificateRevocationChecker.cs`,
  `src/NCode.Identity.OpenId.Authentication/ClientCertificates/ClientCertificateRevocationOptions.cs`,
  `src/NCode.Identity.OpenId.Authentication/ClientCertificates/CertificateRevocationList.cs`, and
  `src/NCode.Identity.OpenId.Authentication/ClientCertificates/DefaultClientCertificateChainValidator.cs`.
