# 173. `tls_client_auth` certificates chain to configured trust anchors

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

`tls_client_auth` ([RFC 8705 §2.1](https://datatracker.ietf.org/doc/html/rfc8705#section-2.1),
[ADR-0107](0107-mutual-tls-client-authentication.md)) authenticates a client by the subject distinguished name or a
subject alternative name of a certificate issued by a PKI the client trusts. The handler only matched the subject. It
assumed the TLS terminator had already validated the chain against a CA bundle, which caused three problems:

- **Authentication bypass.** A proxy that only requests a certificate (`RequireAnyClientCert`) forwards a self-signed
  certificate unvalidated. A self-signed certificate that carries a client's registered subject DN then authenticated
  as that client.
- **No per-tenant PKI.** The proxy picks its client-CA bundle during the TLS handshake by SNI, before any tenant is
  known, so tenants that share a hostname cannot trust different CAs.
- **No shared host.** A proxy that validates chains rejects the certificates of `self_signed_tls_client_auth` clients,
  so the two RFC 8705 methods cannot share one mutual-TLS alias host.

[ADR-0172](0172-forwarded-client-certificates-are-decoded-by-a-registered-format.md) already surfaces the intermediate
certificates the client presented, so the server has what it needs to build the chain itself.

## Decision

**The server validates a `tls_client_auth` certificate's chain against the client's configured trust anchors before
matching its subject, and trusts no certificate when none are configured. The proxy only requests and forwards the
certificate.**

1. **Trust anchors are a setting.** `tls_client_auth_trust_anchors` is a list of PEM-encoded CA certificates that
   applies to tenants and clients. A client's value replaces the tenant's, so a client can pin its own PKI. Each entry
   must be a single certificate whose basic constraints mark it as a CA.
   - A self-issued entry (subject equals issuer) is a trust anchor.
   - Any other entry is an intermediate that can complete a chain but is never trusted on its own. A bundle can carry
     a root with its issuing CAs, and pinning only an intermediate does not make it an anchor.
2. **Fail closed.** The default is an empty list. A `tls_client_auth` client with no trust anchors authenticates with
   no certificate, and a warning names the client.
3. **The chain is built with the platform engine in custom-root-trust mode.** `IClientCertificateChainValidator`'s
   default uses `X509Chain` as follows:
   - `CustomRootTrust` with the configured anchors, so the machine's certificate store is never trusted.
   - The configured intermediates, plus the intermediates the client presented, as untrusted extra certificates.
   - The platform engine retrieves nothing: certificate downloads are disabled and its own revocation check is off
     (`X509RevocationMode.NoCheck`), so no AIA, CRL or OCSP URL in a certificate is fetched by the platform.
   - The `clientAuth` extended key usage required as the application policy (RFC 5280: a certificate without the
     extension is unrestricted).
   - Validity checked at the `TimeProvider`'s current time.
4. **Revocation is checked by the server, online by default.** `tls_client_auth_revocation_mode` is `online` (the
   default) or `none`; it applies to tenants and clients and excludes the root.
   - Every element of the built chain except the trust anchor is checked against its issuer, the next element, through
     `IClientCertificateRevocationChecker.CheckAsync`. The chain passes only when every result is `Good`.
     `ClientCertificateRevocationStatus` is `Unknown` (the zero value, so an unset result fails closed), `Good` or
     `Revoked`, which is the outcome set of RFC 6960 and is closed
     ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).
   - A chain whose revocation status cannot be determined fails. A PKI that publishes no CRL must therefore set `none`
     explicitly, which is right for a PKI that issues short-lived certificates instead, or replace the checker.
   - `DefaultClientCertificateRevocationChecker` checks certificate revocation lists only. It reads the `http` and
     `https` full-name locations of the certificate's CRL distribution points and ignores a distribution point that
     carries `reasons` or `cRLIssuer`.
   - The list is fetched through the named HTTP client `NCode.Identity.OpenId.ClientCertificateRevocation`, whose
     primary handler is a `SocketsHttpHandler` with the outbound-request guard (`IOutboundHttpForgeryGuard`) as its
     connect callback, so the connection is pinned away from private, loopback and link-local addresses. A fetch
     follows no redirects, takes at most five seconds and reads at most 10 MiB.
   - A list is used only when it passes every check:
     - It is DER, and its issuer matches the issuing certificate's subject by `DistinguishedName` (decision 7).
     - It is signed by the issuing certificate, whose key usage includes `cRLSign` when it carries the extension. The
       supported signatures are RSA PKCS#1 v1.5 and ECDSA, each with SHA-256, SHA-384 or SHA-512.
     - It is current: `thisUpdate` is no later than five minutes from now, and `nextUpdate` is present and later than
       now.
     - It is not a delta list, an indirect list, an attribute-certificate list or a list that carries
       `onlySomeReasons`, and it has no unknown critical CRL extension or CRL entry extension.
     - Its issuing distribution point, when present, names the location it was fetched from, and
       `onlyContainsUserCerts` and `onlyContainsCACerts` are honored.
   - A verified list is cached in `IMemoryCache` per location and issuing certificate (by SHA-256 thumbprint) until the
     earlier of its `nextUpdate` and one hour from now.
   - The vocabulary is `online` and `none`, and it is closed because it is policy: it states whether revocation is
     checked. The mechanism is the replaceable checker, so a deployment that needs another source of revocation data
     replaces the checker and does not extend the vocabulary.
5. **Trust is a step of the mutual-TLS handler, ahead of the match.** `MutualTlsClientAuthenticationHandler` asks
   `IsCertificateTrustedAsync` before `MatchCertificateAsync`.
   - A certificate that is not trusted fails with the audit reason `client_certificate_untrusted`, distinct from
     `client_certificate_mismatch`.
   - The self-signed method trusts every certificate at this step, because its thumbprint match is itself the proof.
   - `tls_client_auth` delegates the step to the validator.
6. **The validator is a replaceable service.** `IClientCertificateChainValidator` is registered by
   `AddClientCertificateServices()`, so a deployment with a bespoke trust source (an HSM-held bundle, a remote
   validation service) replaces it without editing a handler.
7. **The subject distinguished name is matched by RFC 4517 `distinguishedNameMatch`.** The public value type
   `DistinguishedName` is the only parser and matcher of distinguished names. The `tls_client_auth_subject_dn` setting
   rule, the `tls_client_auth` handler and the CRL issuer check all use it.
   - Two names match when they have the same number of relative distinguished names in the same order, the attribute
     types (object identifiers) are equal, and the values are equal. A multi-valued relative distinguished name is a
     set.
   - A character-string value is compared after a string preparation that follows RFC 4518: Unicode NFKC
     normalization, deletion of the characters that are mapped to nothing, each run of whitespace collapsed to one
     space, leading and trailing whitespace trimmed, and upper-casing with the invariant culture. Every string
     attribute is therefore compared with `caseIgnoreMatch`, the equality rule of every attribute that client
     certificates use. A value that is not a character string is compared by its exact encoding.
   - Parsing follows RFC 4514 and accepts what operators paste. Whitespace around `=`, `,` and `+` is ignored, and the
     RFC 2253 quoted values (`CN="Doe, John"`), `\` escapes, `\XX` UTF-8 hexadecimal escapes and `#` hexadecimal BER
     values are accepted. An empty name, `;` separators and the `/C=US/O=Example` form are refused.
   - An attribute type is a keyword from a fixed table, a dotted object identifier, or a dotted object identifier with
     an `OID.` prefix. The keyword table is closed because a dotted object identifier names any other attribute type
     ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).
   - The setting rule accepts a `tls_client_auth_subject_dn` value only when the parser accepts it, so the handler
     matches every value the setting can hold.

## Options considered

- **Keep relying on the proxy.** Rejected: it leaves the bypass one proxy setting away, and per-tenant trust and a
  shared host remain impossible.
- **Fall back to the platform trust store when no anchors are configured.** Rejected: it would accept any certificate
  from any public CA that carries the registered subject, and public CAs no longer issue client-authentication
  certificates for this purpose.
- **Default revocation to `none`.** Rejected: it fails open, and secure by default means a revoked certificate fails.
  An operator whose PKI publishes no revocation data opts out with one setting.
- **Default revocation to `offline`.** Rejected: the mode validates against the revocation data the host holds, so it
  fails every chain in a container, which holds none.
- **Fetch each list and then validate with `X509RevocationMode.Offline`.** Rejected: .NET has no API to give `X509Chain`
  a revocation list, so a custom CRL store cannot feed the platform engine. The server checks the lists itself.
- **Check OCSP in the default checker.** Deferred (backlog `R64`). The CA/Browser Forum made CRLs mandatory and OCSP
  optional, and a deployment whose PKI is OCSP-only replaces the checker.
- **Compare the subject by DER bytes or by `X500DistinguishedName.Name` strings.** Rejected: both fail on the string
  type a CA chose (`PrintableString` or `UTF8String`) and on case, which RFC 4517 `distinguishedNameMatch` treats as
  equal.
- **Merge tenant and client anchors (union).** Rejected: a client that pins its own PKI would still trust every
  tenant CA. The client's administrator already controls the client's credentials, so letting the client replace the
  tenant's anchors grants nothing new.

## Consequences

- The proxy only needs to request the certificate and forward it (`RequireAnyClientCert` or Kestrel's
  `AllowCertificate`). `tls_client_auth` and `self_signed_tls_client_auth` clients share one mutual-TLS alias host, and
  tenants that share a hostname trust different CAs.
- **Breaking:** an existing `tls_client_auth` client fails to authenticate until its tenant or the client configures
  `tls_client_auth_trust_anchors`. A PKI that publishes no CRL must also set `tls_client_auth_revocation_mode` to
  `none`, or replace the checker.
- Revocation data is fetched only through the outbound-request guard, so a certificate cannot direct the server to an
  internal address, and the platform chain engine retrieves nothing.
- **Breaking:** the `offline` revocation mode is not accepted. A stored `tls_client_auth_revocation_mode` of `offline`
  is translated to `online` by a migration.
- **Breaking:** a stored `tls_client_auth_subject_dn` written with `;` separators or in the `/C=US/O=Example` form is
  refused by the parser, so it matches no certificate until it is rewritten as an RFC 4514 string.
- A PKI that publishes OCSP responses and no CRL sets `none` or replaces `IClientCertificateRevocationChecker` with an
  OCSP-capable implementation.
- Certificate-bound access tokens are a client's registered requirement
  (`require_tls_client_certificate_bound_access_tokens`), and the advertised support
  (`tls_client_certificate_bound_access_tokens`) never forces a client
  ([ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md)).
- Parsing the PEM anchors costs a little on each `tls_client_auth` request; anchor lists are short.
