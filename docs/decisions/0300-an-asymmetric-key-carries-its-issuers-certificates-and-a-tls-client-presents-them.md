# 300. An asymmetric key carries its issuers' certificates, and a TLS client presents them

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

The protocol client authenticates with `tls_client_auth` by presenting a certificate from the registration's key ring
([ADR-0288](0288-the-protocol-client-authenticates-with-a-tls-client-certificate-from-its-key-ring.md)). A PKI usually
issues such a certificate from an intermediate CA, while the OP trusts only the root: it validates the presented chain
to its `tls_client_auth_trust_anchors`
([ADR-0107](0107-mutual-tls-client-authentication.md)) and builds that chain from the certificates the client presents
([ADR-0172](0172-forwarded-client-certificates-are-decoded-by-a-registered-format.md)). TLS 1.3 sends the client's
certificate followed by the certificates that certify it (RFC 8446 §4.4.2), and TLS 1.2 does the same (RFC 5246
§7.4.6).

`AsymmetricSecretKey` carried one certificate, so `ClientCertificateProvider` built its
`SslStreamCertificateContext` with no additional certificates. The handshake then carried the client's certificate
alone, unless the intermediate happened to be in the machine's certificate store, and an OP that trusts only the root
refused it. The key model had nowhere to keep the chain.

## Decision

**An asymmetric secret key carries the certificates of the authorities that issued its certificate, and the protocol
client presents them with the client certificate.**

1. **The issuers live on the key.** `AsymmetricSecretKey.IssuerCertificatesRawData` is a list of DER-encoded
   certificates, empty by default, next to `CertificateRawData`. They are public, so they are not protected the way the
   private key is. `ExportIssuerCertificates()` materializes them as an `X509Certificate2Collection` that the caller owns.
   The RSA and ECC key constructors take the list as an optional last parameter.
2. **One factory method takes a whole chain.** `ISecretKeyFactory.Create(KeyMetadata, X509Certificate2Collection)`
   accepts what loading a PKCS#12 file returns. Exactly one certificate must hold a private key: it is the key's own
   certificate. Every other certificate, except a copy of that one, becomes an issuer certificate, in the order given.
   No private key, or more than one, is an `ArgumentException`, because the key's certificate would be ambiguous. The
   single-certificate `Create(KeyMetadata, X509Certificate2)` keeps its behavior, including accepting a public-only
   certificate ([ADR-0103](0103-public-only-asymmetric-secret-keys.md)), and gives the key no issuers.
3. **The protocol client presents them.** When `ClientCertificateProvider` selects a certificate, it also caches the
   key's issuer certificates under the same thumbprint, and the handler's `ClientCertificateContext` is
   `SslStreamCertificateContext.Create(certificate, issuers, offline: true)`. Offline, the context builds the chain from
   the given certificates and the local stores and downloads nothing. A key without issuers presents its certificate
   alone, as before. The cached issuers are disposed with the provider, like the certificates.
4. **The key's lifetime keeps them; persistence does not.** Only a key built in memory from certificates carries a
   certificate with a private key: a stored private key is PKCS#8 without a certificate, and a stored certificate is a
   public key a peer registered, which presents nothing. The secrets store therefore keeps no issuers, and its format
   does not change.
5. **A published JWK does not carry the chain.** The family's JWK sets publish public keys without `x5c`, so the
   issuers do not reach them.

## Options considered

- **A separate chain store keyed by certificate thumbprint.** Rejected: the chain belongs to the certificate, and a
  second store would have to be kept consistent with every key ring that rotates.
- **Building the chain from the machine's certificate stores.** Rejected: it works only where an operator has installed
  the intermediate, which a container image or a key-management service does not do, and it fails silently otherwise.
- **Downloading intermediates through the certificate's Authority Information Access (`offline: false`).** Rejected: it
  makes an outbound request during a handshake, to a URL from the certificate, and fails where egress is restricted.
- **Treating the first certificate of the collection as the key's.** Rejected: PKCS#12 does not order its bags, and the
  certificate with the private key is the one the key signs with.

## Consequences

- A host that loads a client certificate from a PKCS#12 file passes the loaded collection to the factory, and an OP that
  trusts only the root accepts the handshake.
- `AsymmetricSecretKey`, `RsaSecretKey`, `EccSecretKey` and `ISecretKeyFactory` gain public members; a custom
  `ISecretKeyFactory` must implement the new method, a breaking addition accepted under the pre-release posture
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- A host that loads a PEM certificate and key builds the leaf with `X509Certificate2.CreateFromPem` and adds the issuer
  certificates it loads separately to the collection.

## References

- RFC 8446 §4.4.2, Certificate: <https://www.rfc-editor.org/rfc/rfc8446#section-4.4.2>
- RFC 5246 §7.4.6, Client Certificate: <https://www.rfc-editor.org/rfc/rfc5246#section-7.4.6>
- RFC 8705 §2.1, PKI Mutual-TLS Method: <https://www.rfc-editor.org/rfc/rfc8705#section-2.1>
- [ADR-0288](0288-the-protocol-client-authenticates-with-a-tls-client-certificate-from-its-key-ring.md) — the protocol
  client's TLS client certificate
- Issue [#407](https://github.com/NCodeGroup/Identity/issues/407)
