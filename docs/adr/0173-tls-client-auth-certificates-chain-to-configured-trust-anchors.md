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
   - Certificate downloads disabled, so no issuer is fetched from an AIA URL.
   - The `clientAuth` extended key usage required as the application policy (RFC 5280: a certificate without the
     extension is unrestricted).
   - Validity checked at the `TimeProvider`'s current time.
4. **Revocation is checked online by default.** `tls_client_auth_revocation_mode` is `online` (the default),
   `offline`, or `none`; it applies to tenants and clients and excludes the root.
   - A chain whose revocation status cannot be determined fails. A PKI that publishes no CRL or OCSP data must
     therefore set `none` explicitly, which is right for a PKI that issues short-lived certificates instead.
   - An online fetch is bounded to five seconds.
   - The modes mirror the platform chain engine's modes, its only consumer, so the vocabulary is closed
     ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).
5. **Trust is a step of the mutual-TLS handler, ahead of the match.** `MutualTlsClientAuthenticationHandler` asks
   `IsCertificateTrustedAsync` before `MatchCertificateAsync`.
   - A certificate that is not trusted fails with the audit reason `client_certificate_untrusted`, distinct from
     `client_certificate_mismatch`.
   - The self-signed method trusts every certificate at this step, because its thumbprint match is itself the proof.
   - `tls_client_auth` delegates the step to the validator.
6. **The validator is a replaceable service.** `IClientCertificateChainValidator` is registered by
   `AddClientCertificateServices()`, so a deployment with a bespoke trust source (an HSM-held bundle, a remote
   validation service) replaces it without editing a handler.

## Options considered

- **Keep relying on the proxy.** Rejected: it leaves the bypass one proxy setting away, and per-tenant trust and a
  shared host remain impossible.
- **Fall back to the platform trust store when no anchors are configured.** Rejected: it would accept any certificate
  from any public CA that carries the registered subject, and public CAs no longer issue client-authentication
  certificates for this purpose.
- **Default revocation to `none`.** Rejected: secure by default means a revoked certificate fails. An operator whose PKI
  publishes no revocation data opts out with one setting.
- **Merge tenant and client anchors (union).** Rejected: a client that pins its own PKI would still trust every
  tenant CA. The client's administrator already controls the client's credentials, so letting the client replace the
  tenant's anchors grants nothing new.

## Consequences

- The proxy only needs to request the certificate and forward it (`RequireAnyClientCert` or Kestrel's
  `AllowCertificate`). `tls_client_auth` and `self_signed_tls_client_auth` clients share one mutual-TLS alias host, and
  tenants that share a hostname trust different CAs.
- **Breaking:** an existing `tls_client_auth` client fails to authenticate until its tenant or the client configures
  `tls_client_auth_trust_anchors`. A PKI without revocation data must also set `tls_client_auth_revocation_mode` to
  `none`.
- Parsing the PEM anchors costs a little on each `tls_client_auth` request; anchor lists are short.
