# 1259. The built-in revocation sources check OCSP and every CRL form RFC 5280 defines

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

The server checks the revocation status of every certificate below the trust anchor of a `tls_client_auth` chain
([ADR-1173](1173-a-mutual-tls-client-is-authenticated-by-its-registered-certificate-chained-to-configured-trust-anchors.md)), and it shares, bounds and
remembers each retrieval
([ADR-1252](1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md)). A
checker that reads only complete, direct CRLs signed by the issuing certificate answers `Unknown` for five common PKI
shapes:

- a PKI that publishes only OCSP;
- a CA that partitions its CRL by reason (`reasons`, `onlySomeReasons`);
- a CA that publishes delta CRLs (`freshestCRL`, `deltaCRLIndicator`);
- an indirect CRL, which a CRL issuer publishes for another CA (`cRLIssuer`, `indirectCRL`, `certificateIssuer`);
- a CRL signed by a key other than the one that issued the certificate, as after a CA key rollover.

Under `online` each shape refuses every client of the PKI, and under `online_soft_fail` each accepts a revoked
certificate. Four further forces apply:

- Where revocation information comes from is an open vocabulary
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)): a deployment can know another source, such as a
  local deny list.
- A signer of revocation information other than the issuer is trusted only through a certification path to the trust
  anchor of the certificate it speaks for (RFC 5280 §6.3.3), and the server downloads no certificate.
- An OCSP response is signed by the CA or by a responder that the CA delegated (RFC 6960 §4.2.2.2), and the
  responder's own revocation is a matter of local policy unless its certificate carries `id-pkix-ocsp-nocheck`.
- A check runs inside client authentication, so every retrieval stays bounded and shared, and every cache stays in the
  replica.

## Decision

**The built-in `ocsp` source queries the certificate's OCSP responders, and the built-in `crl` source processes CRLs
as RFC 5280 §6.3.3 describes. Together they cover OCSP and every CRL form: complete, partitioned, delta, indirect and
signed by another key.** Which sources run and in what order is
[ADR-1400](1400-the-revocation-sources-are-described-and-configuration-selects-and-orders-them.md)'s; every request
goes through the guarded client of
[ADR-1315](1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md).

1. **A check carries its path.** `IClientCertificateRevocationSource` has a `Name` and `CheckAsync`, and
   `ClientCertificateRevocationSourceNames` names the built-in `ocsp` and `crl`. `CheckAsync` receives a
   `ClientCertificateRevocationContext` with the `Certificate`, its `Issuer` (the next element of the validated
   chain), the chain's `TrustAnchor`, the `Intermediates` that can complete a path (the configured ones and the ones
   the client presented, none of them trusted on its own), and the `Depth`: zero for the presented chain, and one more
   for each certificate checked to trust a signer.
2. **OCSP comes first by default.** `OcspClientCertificateRevocationSource` queries a responder, which answers for one
   certificate and is usually fresher than a CRL. A certificate whose authority information access names no `http` or
   `https` responder skips the source without a request. A failed query, or a responder that answers `unknown`, falls
   through to the CRLs, so a PKI that publishes both keeps working while its responder is down.
3. **The OCSP request follows the lightweight profile.** It holds one `CertID` with SHA-1 hashes of the issuer's name
   and key (RFC 5019 §2.1.1), and no requestor name, signature or nonce. It is sent with GET when the location and the
   URL-encoded base64 request together are at most 255 characters (RFC 5019 §5), and otherwise with POST as
   `application/ocsp-request`. A responder that serves pre-produced responses cannot echo a nonce, and a nonce makes
   every request unique to HTTP caches, so freshness rests on `thisUpdate` and `nextUpdate`, as a CRL's does.
4. **An OCSP response is used only when it passes every check.**
   - Its status is `successful`, it is a DER `basic` response with no critical extension, and one single response
     answers for the requested `CertID`.
   - The issuer signed it and its `ResponderID`, by name or by SHA-1 key hash, identifies the issuer; or a delegated
     responder signed it: a certificate in the response that the `ResponderID` identifies, issued and signed by the
     issuer, with the `id-kp-OCSPSigning` extended key usage, and valid at the current time.
   - A delegated responder with `id-pkix-ocsp-nocheck` is not checked. One without it is checked through the CRL
     source at `Depth + 1`, and its response is used only when the responder is `Good`.
   - `thisUpdate` is no later than five minutes after the current time, and `nextUpdate`, when present, is later than
     the current time. A response without `nextUpdate` is used only when its `thisUpdate` is within five minutes of the
     current time.
5. **The CRL source processes the distribution points until every reason is covered.**
   `CrlClientCertificateRevocationSource` reads each distribution point that has an `http` or `https` full name, with
   its `reasons` and its `cRLIssuer` directory name. `nameRelativeToCRLIssuer`, which RFC 5280 tells CAs not to use,
   and lists limited to attribute certificates are not supported.
6. **A list is used only when it is complete, current and in scope.**
   - It is DER, and its issuer matches the point's `cRLIssuer` by `DistinguishedName` when it asserts `indirectCRL`,
     or the certificate's issuer otherwise.
   - It is signed with RSA PKCS#1 v1.5, RSASSA-PSS or ECDSA over SHA-256, SHA-384 or SHA-512.
   - It is current: `thisUpdate` is no later than five minutes after the current time, and `nextUpdate` is present
     and later than the current time.
   - It has no unknown critical extension, its issuing distribution point name, when present, shares a location with
     the point, and `onlyContainsUserCerts` and `onlyContainsCACerts` admit the certificate.
7. **RSASSA-PSS needs explicit parameters.** The algorithm `1.2.840.113549.1.1.10` requires SHA-256, SHA-384 or
   SHA-512 as the hash (absent or NULL hash parameters), MGF1 over the same hash, a salt length equal to the hash
   length, and a trailer field that is absent or 1. Defaulted SHA-1 parameters and SHA-1 signatures are refused.
8. **A point covers the reasons its list covers.**
   - The point covers its `reasons` intersected with the list's `onlySomeReasons`, whichever of the two is present, or
     every reason. A point that adds no reason to those already covered is skipped.
   - An entry matches when its serial number is the certificate's and its issuer is the certificate's issuer; in an
     indirect list, `certificateIssuer` sets the issuer of its entry and of the entries after it. A `removeFromCRL`
     entry does not revoke.
   - The certificate is `Revoked` as soon as a point lists it, `Good` when the covered reasons are every reason, and
     `Unknown` otherwise.
9. **A delta list is required when one is named.** When the certificate or the complete list names a `freshestCRL`
   location, the point uses the first list there that is current, is a delta list (`deltaCRLIndicator`), has the
   issuer, issuing distribution point and authority key identifier of the complete list, has a base CRL number no
   greater than the complete list's `cRLNumber` and a `cRLNumber` greater than it, and is signed by the complete
   list's signer. A delta entry decides before a complete entry. Without a usable delta list, a listed certificate is
   still `Revoked`, and an unlisted one leaves the point uncovered.
10. **A list signed by another key is trusted through a validated path.** The issuing certificate's key is tried
    first. Otherwise a candidate among the trust anchor and the context's intermediates qualifies when its subject
    matches the list's issuer, its key usage includes `cRLSign` when key usage is present, and its key verifies the
    list. The anchor itself is trusted. Any other candidate is trusted when `X509Chain` builds its path to the same
    anchor (custom root trust, no downloads, no platform revocation check, at the current time) and the CRL source
    answers `Good` at `Depth + 1` for every certificate of that path below the anchor. A check deeper than 2 answers
    `Unknown`.
11. **A nested check that is not `Good` makes the data unusable.** The list or response it would trust is unusable,
    never `Revoked`. The server downloads no certificate, so a signer or responder certificate is configured in
    `tls_client_auth_trust_anchors` or presented by the client.
12. **Retrieval is cached per location and verified per check.** The caches live in the replica, behind
    `RevocationDataFetcher` and `RevocationRetrievalCache`.
    - A CRL is cached per location once it is parsed and current, until the earlier of its `nextUpdate` and
      `MaxCacheDuration`, and each check verifies it against that check's signer. The list keeps one verdict for each
      signer.
    - An OCSP response is cached per location, issuer and serial number once it is verified, until the earlier of its
      `nextUpdate` and `MaxCacheDuration`, and is never cached without `nextUpdate`.
    - A responder location that fails at the HTTP level is remembered for `FailureCacheDuration`, so an outage costs
      one timeout per replica and responder.

## Options considered

- **CRLs before OCSP.** Rejected: a CRL lists every revocation of a CA and can weigh megabytes, while a response
  answers for one certificate and is usually fresher; the CRLs remain the fallback.
- **Ask every source and let any `Revoked` win.** Rejected: it doubles the network cost of every check, and a CA's
  sources describe one status, so the first definite answer is the CA's.
- **Send a nonce.** Rejected: a responder that serves pre-produced responses cannot echo one, and a nonce makes every
  request unique to HTTP caches.
- **Verify a list inside the shared retrieval.** Rejected: a signer other than the issuer is trusted through the trust
  anchor of the checking client, so the verdict belongs to the check; a shared verdict would let one tenant's trust
  anchors decide for another tenant.
- **Download a missing signer or responder certificate from its authority information access.** Rejected: the server
  downloads no certificate
  ([ADR-1173](1173-a-mutual-tls-client-is-authenticated-by-its-registered-certificate-chained-to-configured-trust-anchors.md)); the PKI's administrator
  configures the certificate instead.
- **Check a delegated responder or a list signer through OCSP as well.** Rejected: a responder would be asked about
  itself, and checks could loop. The CRL source with a depth bound always ends.
- **Refuse a delegated responder without `id-pkix-ocsp-nocheck`.** Rejected: RFC 6960 leaves its revocation to local
  policy, and checking it through its CRLs keeps such a PKI working.
- **Use a delta list when it can be retrieved, and the complete list alone otherwise.** Rejected: a CA publishes delta
  lists because its complete list lags, so skipping an unreachable delta location would hide every recent revocation.
- **Read complete, direct CRLs signed by the issuer, and ask OCSP nowhere.** Rejected: it answers `Unknown` for the
  five PKI shapes above.

The chosen design, two built-in sources that together process OCSP and every CRL form with a bounded path validation
for foreign signers, wins because each PKI shape authenticates under `online` and no check trusts a signer it did not
validate.

## Consequences

- OCSP-only PKIs, partitioned, delta and indirect CRLs, and CAs whose list is signed by a rolled-over key all
  authenticate under `online`.
- A list signer, or a delegated responder without `id-pkix-ocsp-nocheck`, whose own revocation cannot be determined,
  for example because its certificate names no CRL distribution point, makes its list or response unusable.
- An unreachable responder delays a check by `FetchTimeout` once per `FailureCacheDuration` in each replica before the
  CRLs answer. A deployment that must skip OCSP configures the sources
  ([ADR-1400](1400-the-revocation-sources-are-described-and-configuration-selects-and-orders-them.md)).
- A PKI that publishes delta lists costs a second retrieval, which is cached like the first.
- A list or response that an attacker on the path to an `http` location replaces fails verification and answers
  `Unknown`, which `online_soft_fail` accepts.

## References

- RFC 5280 §5 and §6.3, certificate revocation lists: <https://www.rfc-editor.org/rfc/rfc5280>
- RFC 6960, the Online Certificate Status Protocol: <https://www.rfc-editor.org/rfc/rfc6960>
- RFC 5019, the lightweight OCSP profile: <https://www.rfc-editor.org/rfc/rfc5019>
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md): vocabularies are open unless provably closed.
- [ADR-1173](1173-a-mutual-tls-client-is-authenticated-by-its-registered-certificate-chained-to-configured-trust-anchors.md): `tls_client_auth` certificates
  chain to configured trust anchors.
- [ADR-1252](1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md): a
  revocation list retrieval is shared, a failed one is remembered, and soft-fail is opt-in.
- [ADR-1315](1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md): the guarded client
  every retrieval goes through.
- [ADR-1400](1400-the-revocation-sources-are-described-and-configuration-selects-and-orders-them.md): configuration
  selects and orders the sources.
- Code: `src/NCode.Identity.OpenId.Authentication.Abstractions/ClientCertificates/` for
  `ClientCertificateRevocationContext`, `IClientCertificateRevocationSource` and
  `ClientCertificateRevocationSourceNames`, and `src/NCode.Identity.OpenId.Authentication/ClientCertificates/` for
  `OcspClientCertificateRevocationSource`, `CrlClientCertificateRevocationSource`, `CertificateRevocationList`,
  `OcspResponse`, `RevocationDataFetcher` and `RevocationRetrievalCache`.
