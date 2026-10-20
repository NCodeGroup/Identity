# 107. Mutual-TLS client authentication (`tls_client_auth` and `self_signed_tls_client_auth`)

- **Status:** Accepted (`tls_client_auth` chain validation moved into the server by
  [ADR-0173](0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md))
- **Date:** 2026-10-15
- **Deciders:** NCode Group

## Context

FAPI 2.0 readiness (backlog `F16`) requires **strong client authentication**: a confidential client proves its identity
with asymmetric cryptography rather than a shared secret. `private_key_jwt`
([ADR-0102](0102-jwt-bearer-client-authentication.md)) is one of the two FAPI-permitted mechanisms; **mutual-TLS client
authentication** ([RFC 8705](https://datatracker.ietf.org/doc/html/rfc8705) §2) is the other.

The server already **binds** access tokens to a presented client certificate (`cnf.x5t#S256`, gated by
`tls_client_certificate_bound_access_tokens`, [ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md)) and
already resolves the presented certificate through the deployment-agnostic `IClientCertificateResolver` (direct TLS or a
trusted forwarded header). What was missing is using that certificate to **authenticate** the client at the token
endpoint. RFC 8705 defines two methods:

- **`tls_client_auth` (§2.1)** — a PKI-issued certificate whose subject distinguished name or a subject alternative name
  matches metadata the client registered.
- **`self_signed_tls_client_auth` (§2.2)** — a certificate that matches one the client registered, compared by value.

## Decision

**Add two `IClientAuthenticationHandler` implementations over a shared `MutualTlsClientAuthenticationHandler` base, each
owning its registered method and verifying the presented certificate against the client's registration.** The handlers
reuse the existing `IClientCertificateResolver`; token cert-binding stays the separate concern of the
proof-of-possession service.

- **The client is identified by `client_id` and gated by its registered method.** The handler reads `client_id` from the
  token request body, resolves the client, and **owns** the request only when the client's `token_endpoint_auth_method`
  equals the handler's method — so a certificate presented by a `client_secret`-authenticating client (for token
  binding) is never mistaken for an authentication attempt, and the two mutual-TLS methods never collide. Once owned,
  a missing or non-matching certificate is a conclusive `invalid_client`.

- **`self_signed_tls_client_auth` matches by SHA-256 thumbprint.** The presented certificate's `x5t#S256` is compared
  (constant-time) against each of the client's registered certificates. The registered certificate's DER is read from
  the stored key directly (`AsymmetricSecretKey.CertificateRawData`, newly exposed), so no `X509Certificate2` is
  materialized per candidate, and both thumbprints are hashed into `stackalloc` buffers.

- **`tls_client_auth` matches the registered certificate identity.** Exactly one of five per-client settings is
  configured: `tls_client_auth_subject_dn` (compared by canonical DER via `X500DistinguishedName`, so formatting
  differences do not matter) or one of `tls_client_auth_san_dns` / `_san_uri` / `_san_ip` / `_san_email` (RFC 8705
  §2.1.2). DNS and IP SANs are read through the BCL `X509SubjectAlternativeNameExtension`; the `uniformResourceIdentifier`
  and `rfc822Name` GeneralName choices — the only two the BCL exposes no typed accessor for — are decoded from the
  extension's ASN.1 directly. A client registered for `tls_client_auth` with no configured identity never matches. The
  certificate's chain is validated first, against the client's configured trust anchors
  ([ADR-0173](0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md)).

- **Discovery advertises both methods automatically.** `token_endpoint_auth_methods_supported` is derived from the
  registered handlers ([DefaultClientSettingDescriptorDataSource]), so registering the two handlers advertises
  `tls_client_auth` and `self_signed_tls_client_auth` with no additional wiring.

- **A successful authentication promotes the client to a confidential client** whose authenticating `SecretKey` is the
  matched registered key (self-signed) or a key built from the presented certificate (PKI). The access token is bound to
  the certificate by the existing proof-of-possession path when `tls_client_certificate_bound_access_tokens` is enabled.

## Consequences

- A confidential client can authenticate with mutual TLS using either a self-signed or a PKI certificate, satisfying the
  FAPI strong-client-authentication requirement alongside `private_key_jwt`.
- Exposing `AsymmetricSecretKey.CertificateRawData` lets any certificate-thumbprint consumer avoid materializing a
  certificate; it is new public surface (public certificate data only, strictly less than the existing
  `ExportCertificate()`).
- New public surface: the `tls_client_auth` / `self_signed_tls_client_auth` auth-method constants, the per-client
  `token_endpoint_auth_method` and five `tls_client_auth_*` settings, and `AsymmetricSecretKey.CertificateRawData`.
- Clients using these methods are provisioned through the management API / seeding (which writes the settings directly),
  matching how `private_key_jwt` clients are provisioned today; accepting these methods through Dynamic Client
  Registration (projecting the `tls_client_auth_*` metadata) is a deliberate follow-up (`F16` Phase 4 / conformance).

[DefaultClientSettingDescriptorDataSource]: ../../src/NCode.Identity.OpenId.Authentication/Settings/DefaultClientSettingDescriptorDataSource.cs
