# 1173. A mutual-TLS client is authenticated by its registered certificate, chained to configured trust anchors

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

FAPI 2.0 readiness (issue [#54](https://github.com/NCodeGroup/identity/issues/54)) requires strong client
authentication: a confidential client proves its identity with asymmetric cryptography rather than a shared secret.
`private_key_jwt` ([ADR-1102](1102-a-client-authenticates-with-a-single-use-jwt-assertion-signed-by-its-key-or-its-secret.md))
is one of the two permitted mechanisms; mutual-TLS client authentication
([RFC 8705 §2](https://datatracker.ietf.org/doc/html/rfc8705#section-2)) is the other. RFC 8705 defines two methods:

- **`tls_client_auth` (§2.1).** A PKI-issued certificate whose subject distinguished name or a subject alternative name
  matches metadata the client registered.
- **`self_signed_tls_client_auth` (§2.2).** A certificate that matches one the client registered, compared by value.

The server already resolves the presented certificate through the deployment-agnostic `IClientCertificateResolver`
([ADR-1172](1172-a-forwarded-client-certificate-is-decoded-by-the-decoder-registered-for-its-format.md)), which also
surfaces the intermediate certificates the client presented, and it binds access tokens to that certificate as a
separate concern ([ADR-1097](1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md)).

Matching only the subject of the certificate, and leaving chain validation to the TLS terminator, has three problems:

- **Authentication bypass.** A proxy that only requests a certificate (`RequireAnyClientCert`) forwards a self-signed
  certificate unvalidated. A self-signed certificate that carries a client's registered subject DN would authenticate
  as that client.
- **No per-tenant PKI.** The proxy picks its client-CA bundle during the TLS handshake by SNI, before any tenant is
  known, so tenants that share a hostname cannot trust different CAs.
- **No shared host.** A proxy that validates chains rejects the certificates of `self_signed_tls_client_auth` clients,
  so the two methods cannot share one mutual-TLS alias host.

## Decision

**A mutual-TLS client is authenticated by the certificate it presents matching its registration. For `tls_client_auth`
the server validates the certificate's chain against the client's configured trust anchors before it matches the
subject, and trusts no certificate when none are configured. The proxy only requests and forwards the certificate.**

1. **Two handlers over one base.** `tls_client_auth` and `self_signed_tls_client_auth` are two
   `IClientAuthenticationHandler` implementations over `MutualTlsClientAuthenticationHandler`. The handler reads
   `client_id` from the token request body, resolves the client, and owns the request only when the client's
   registered `token_endpoint_auth_method` equals the handler's method
   ([ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md)). A
   certificate presented only for token binding is therefore no authentication attempt, and the two methods never
   collide. Once the handler owns the request, a missing or non-matching certificate is a conclusive `invalid_client`.
   The certificate comes from `IClientCertificateResolver`.
2. **Trust is a step ahead of the match.** The handler asks `IsCertificateTrustedAsync` (the base trusts every
   certificate) before `MatchCertificateAsync`.
   - A certificate that is not trusted fails with the audit reason `client_certificate_untrusted`, distinct from
     `client_certificate_mismatch`.
   - `self_signed_tls_client_auth` trusts every certificate at this step, because its thumbprint match is itself the
     proof.
   - `tls_client_auth` delegates the step to `IClientCertificateChainValidator.ValidateAsync`.
3. **`self_signed_tls_client_auth` matches by SHA-256 thumbprint.** The presented certificate's thumbprint is compared
   with `CryptographicOperations.FixedTimeEquals` against each certificate of the keys `IClientKeyResolver` returns,
   with both thumbprints hashed into `stackalloc` buffers. The registered certificate's DER is read from the stored key
   through `AsymmetricSecretKey.CertificateRawData`, so no `X509Certificate2` is created per candidate.
4. **`tls_client_auth` matches exactly one registered identity.** The identity is `tls_client_auth_subject_dn`, or one
   of `tls_client_auth_san_dns`, `tls_client_auth_san_uri`, `tls_client_auth_san_ip` and `tls_client_auth_san_email`
   (RFC 8705 §2.1.2). DNS and IP subject alternative names are read through `X509SubjectAlternativeNameExtension`; the
   URI and rfc822Name names are decoded from the extension's ASN.1. A client with no configured identity never matches.
5. **Trust anchors are a setting.** `tls_client_auth_trust_anchors` is a list of PEM-encoded CA certificates that
   applies to tenants and clients with merge `Replace`, so a client's value replaces the tenant's and a client can pin
   its own PKI. Each entry must be a single certificate whose basic constraints mark it as a CA.
   - A self-issued entry (subject equals issuer) is a trust anchor.
   - Any other entry is an intermediate that can complete a chain but is never trusted on its own. A bundle can carry a
     root with its issuing CAs, and pinning only an intermediate does not make it an anchor.
   - The default is an empty list, so the validator fails closed: a `tls_client_auth` client with no trust anchors
     authenticates with no certificate, and a warning names the client.
6. **The chain is built with the platform engine in custom-root-trust mode.** The default
   `IClientCertificateChainValidator`, registered by `AddClientCertificateServices()` and replaceable, uses `X509Chain`
   as follows:
   - `CustomRootTrust` with the configured anchors, so the machine's certificate store is never trusted.
   - The configured intermediates, plus the intermediates the client presented, as untrusted extra certificates.
   - Certificate downloads disabled and the platform's own revocation check off (`X509RevocationMode.NoCheck`), so the
     platform fetches no AIA, CRL or OCSP location.
   - The `clientAuth` extended key usage required as the application policy (RFC 5280: a certificate without the
     extension is unrestricted).
   - Validity checked at the `TimeProvider`'s current time.

   The server then checks revocation under `tls_client_auth_revocation_mode`, through the revocation sources of
   [ADR-1400](1400-the-revocation-sources-are-described-and-configuration-selects-and-orders-them.md) and
   [ADR-1259](1259-the-built-in-revocation-sources-check-ocsp-and-every-crl-form-rfc-5280-defines.md).
7. **The subject distinguished name is matched by RFC 4517 `distinguishedNameMatch`.** The public class
   `DistinguishedName`, in the namespace `NCode.Identity.OpenId.ClientCertificates`, is the only parser and matcher of
   distinguished names. The `tls_client_auth_subject_dn` setting rule, the handler and the revocation issuer checks all
   use it.
   - Two names match when they have the same number of relative distinguished names in the same order, the attribute
     types (object identifiers) are equal, and the values are equal. A multi-valued relative distinguished name is a
     set.
   - A character-string value is compared after the string preparation of RFC 4518: Unicode NFKC normalization,
     deletion of the characters that are mapped to nothing, each run of whitespace collapsed to one space, leading and
     trailing whitespace trimmed, and upper-casing with the invariant culture. A value that is not a character string
     is compared by its exact encoding.
   - Parsing follows RFC 4514 and accepts what operators paste. Whitespace around `=`, `,` and `+` is ignored, and
     quoted values (`CN="Doe, John"`), `\` escapes, `\XX` hexadecimal escapes and `#` hexadecimal BER values are
     accepted. An empty name, `;` separators and the `/C=US/O=Example` form are refused.
   - An attribute type is a keyword from a fixed table, a dotted object identifier, or a dotted object identifier with
     an `OID.` prefix. The keyword table is closed because a dotted object identifier names any other attribute type
     ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
   - The setting rule accepts a `tls_client_auth_subject_dn` value only when the parser accepts it, so the handler
     matches every value the setting can hold.
8. **Success makes the client confidential.** The authenticating key is the matched registered key
   (`self_signed_tls_client_auth`) or a key built from the presented certificate (`tls_client_auth`). Access-token
   binding follows only the client's `require_tls_client_certificate_bound_access_tokens`
   ([ADR-1097](1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md)).
9. **The proxy only requests and forwards the certificate.** It uses `RequireAnyClientCert`, or Kestrel's
   `AllowCertificate`. Both methods therefore share one mutual-TLS alias host, and tenants on one hostname trust
   different CAs.

## Options considered

- **Rely on the proxy's chain validation.** Rejected: a self-signed certificate with a registered DN would
  authenticate, and per-tenant trust and a shared host stay impossible.
- **Fall back to the platform trust store when no anchors are configured.** Rejected: it would accept any certificate
  from any public CA that carries the registered subject.
- **Validate with the platform's revocation engine, or with `offline` revocation.** Rejected: .NET has no API to give
  `X509Chain` a revocation list, and the `offline` mode validates against revocation data the host holds, so it fails
  every chain in a container, which holds none. The server checks revocation itself.
- **Compare distinguished names by DER bytes or by `X500DistinguishedName.Name` strings.** Rejected: both fail on the
  string type a CA chose (`PrintableString` or `UTF8String`) and on letter case, which RFC 4517
  `distinguishedNameMatch` treats as equal.
- **Merge tenant and client anchors (union).** Rejected: a client that pins its own PKI would still trust every tenant
  CA. The client's administrator already controls the client's credentials, so letting the client replace the tenant's
  anchors grants nothing new.
- **Validate the chain at the server against configured anchors, then match the registered identity.** Chosen: the
  server holds the tenant and the client, so it is the only place that can pick the right anchors.

## Consequences

- A confidential client can authenticate with mutual TLS using either a self-signed or a PKI certificate, which
  satisfies the strong-client-authentication requirement alongside `private_key_jwt`.
- A `tls_client_auth` client fails to authenticate until its tenant or the client configures
  `tls_client_auth_trust_anchors`. A PKI that publishes no revocation data sets `tls_client_auth_revocation_mode`
  explicitly or replaces the revocation sources.
- A self-signed certificate with a registered subject DN does not authenticate a `tls_client_auth` client, because it
  chains to no configured anchor.
- A stored `tls_client_auth_subject_dn` written with `;` separators or in the `/C=US/O=Example` form is refused by the
  parser, so it is written as an RFC 4514 string.
- Revocation data is fetched only through the guarded outbound client
  ([ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md)), so a certificate
  cannot direct the server to an internal address, and the platform chain engine retrieves nothing.
- A deployment with a bespoke trust source (an HSM-held bundle, a remote validation service) replaces
  `IClientCertificateChainValidator` without editing a handler.
- `AsymmetricSecretKey.CertificateRawData` lets any thumbprint consumer avoid creating a certificate; it exposes public
  certificate data only.
- Parsing the PEM anchors costs a little on each `tls_client_auth` request; anchor lists are short.
- Clients using these methods are provisioned through the management API or seeding, which writes the settings
  directly, as `private_key_jwt` clients are.

## References

- [RFC 8705](https://datatracker.ietf.org/doc/html/rfc8705) §2, §2.1, §2.1.2, §2.2: mutual-TLS client authentication.
- RFC 4514, RFC 4517 and RFC 4518: distinguished-name syntax, matching and string preparation. RFC 5280: the
  `clientAuth` extended key usage.
- Issue [#54](https://github.com/NCodeGroup/identity/issues/54): FAPI 2.0 readiness.
- Code: `MutualTlsClientAuthenticationHandler`, `IClientCertificateChainValidator`, `IClientCertificateResolver`,
  `DistinguishedName` and `AsymmetricSecretKey.CertificateRawData`.
- [ADR-1097](1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md),
  [ADR-1102](1102-a-client-authenticates-with-a-single-use-jwt-assertion-signed-by-its-key-or-its-secret.md),
  [ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1172](1172-a-forwarded-client-certificate-is-decoded-by-the-decoder-registered-for-its-format.md),
  [ADR-1259](1259-the-built-in-revocation-sources-check-ocsp-and-every-crl-form-rfc-5280-defines.md),
  [ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md),
  [ADR-1400](1400-the-revocation-sources-are-described-and-configuration-selects-and-orders-them.md).
