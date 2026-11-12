# 1300. The protocol client authenticates with a key ring's TLS client certificate and its issuers' certificates

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

The backend-for-frontend authenticates to the OP with the methods its registration names. RFC 8705 §2 defines two
mutual-TLS methods: `tls_client_auth`, where the OP validates the certificate's PKI chain and subject, and
`self_signed_tls_client_auth`, where the OP matches the certificate against the client's registered keys. Both need the
client to present a TLS client certificate on the connection, and RFC 8705 §5 lets the provider publish
`mtls_endpoint_aliases` that a client using mutual TLS sends its requests to. `NCode.Identity.OpenId.Client` must do
this while staying free of ASP.NET Core
([ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md)).

A PKI usually issues a client certificate from an intermediate CA, while the OP trusts only the root: it validates the
presented chain to its `tls_client_auth_trust_anchors`
([ADR-1173](1173-a-mutual-tls-client-is-authenticated-by-its-registered-certificate-chained-to-configured-trust-anchors.md))
and builds that chain from the certificates the client presents
([ADR-1172](1172-a-forwarded-client-certificate-is-decoded-by-the-decoder-registered-for-its-format.md)). TLS 1.3 sends
the client's certificate followed by the certificates that certify it (RFC 8446 §4.4.2), and TLS 1.2 does the same
(RFC 5246 §7.4.6). A handshake that carries the client's certificate alone is refused by an OP that trusts only the
root, unless the intermediate happens to be in the machine's certificate store. The key model therefore needs a place
for the chain.

Three constraints shape the transport. A TLS client certificate belongs to the connection, and `SocketsHttpHandler`
pools connections per host, not per certificate, so one handler can never serve two certificates.
`IHttpClientFactory` caches an exception thrown while it builds a handler for a name, so a configuration error would
outlive its fix if the handler were built first. And the client package sees only `Secrets.Abstractions`, which has no
"has private key" member for RSA or EC keys.

## Decision

**The protocol client authenticates with `tls_client_auth` and `self_signed_tls_client_auth` by presenting a
certificate from the registration's key ring, together with the certificates of the authorities that issued it, on a
dedicated HTTP client, and sends its requests to the provider's `mtls_endpoint_aliases` when it has them.**

1. **The authenticators.** Both methods share the internal base `MutualTlsClientAuthenticator` and differ only in
   `MethodName`: the OP, not the client, tells PKI validation from thumbprint matching. Both always send `client_id`,
   which RFC 8705 §2 requires.
2. **The authenticator declares the transport.** `IClientAuthenticator.UsesMutualTls` returns `true` for a method that
   authenticates with the TLS client certificate, and the request sender then picks the certificate and the mTLS
   endpoint alias. The strategy declares the trait and the sender never infers it from the method name
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)), so a third-party mTLS method gets the same
   transport by returning `true`. The other built-in authenticators return `false`.
3. **The certificate comes from `ClientRegistration.KeyRing`**, the ring that holds the client's credentials. A key
   qualifies when it is an `RsaSecretKey` or `EccSecretKey` with a certificate, its `Metadata.Use` is not `enc` (so an
   ID-token decryption key in the same ring is never presented), it holds a private key, its certificate allows client
   authentication (no EKU extension, or one listing `1.3.6.1.5.5.7.3.2`; no KeyUsage extension, or one with
   `DigitalSignature`), and the time is within the certificate's `NotBefore`..`NotAfter`. The first qualifying key in
   ring order wins, so a staged certificate that is not yet valid is skipped until it is. No qualifying key throws
   `InvalidOperationException`, a configuration error.
4. **The issuers live on the key.** `AsymmetricSecretKey.IssuerCertificatesRawData` is a list of DER-encoded
   certificates of the authorities that issued the key's certificate, next to `CertificateRawData`. It is empty by
   default. The certificates are public, so they are not protected the way the private key is.
   `ExportIssuerCertificates()` returns them as an `X509Certificate2Collection` that the caller owns. The RSA and ECC
   key constructors take the list as an optional last parameter.
5. **One factory method takes a whole chain.** `ISecretKeyFactory.Create(KeyMetadata, X509Certificate2Collection)`
   accepts what loading a PKCS#12 file returns. Exactly one certificate must hold a private key: it is the key's own
   certificate. Every other certificate, except a copy of that one, becomes an issuer certificate, in the order given.
   No private key, or more than one, is an `ArgumentException`, because the key's certificate would be ambiguous. The
   single-certificate `Create(KeyMetadata, X509Certificate2)` accepts a public-only certificate
   ([ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md))
   and gives the key no issuers.
6. **One `IHttpClientFactory` client per certificate.** The client name is
   `OpenIdClientDefaults.MutualTlsHttpClientNamePrefix` (`"NCode.Identity.OpenId.Client.MutualTls:"`, public so a host
   can configure those clients) followed by the uppercase hex SHA-256 of the certificate's DER. The internal
   `MutualTlsHttpClientOptionsSetup`, an `IConfigureNamedOptions<HttpClientFactoryOptions>`, gives every name with that
   prefix a `SocketsHttpHandler` with redirects and cookies off, whose `SslOptions.ClientCertificateContext` is
   `SslStreamCertificateContext.Create(certificate, issuers, offline: true)`. The certificate is therefore always
   presented, whatever CA list the server sends, with its issuers' certificates. Offline, the context builds the chain
   from the given certificates and the local stores and downloads nothing, so an OP that trusts only the root accepts a
   client certificate issued by an intermediate. A key without issuers presents its certificate alone. A new
   certificate gets a new name, and the factory's two-minute handler lifetime retires idle clients.
7. **The certificate is selected before the factory is asked for a client.** Selection, the private-key check and
   materialization run in `ClientCertificateProvider.SelectHttpClientName`, which caches each materialized certificate,
   and its issuers, by thumbprint. Building the handler afterwards only looks them up. The provider is a DI singleton
   and disposes the cached certificates and issuers with the container. Cached entries are never evicted: there is one
   per distinct certificate seen, and disposing one under a live handler would break its handshakes. On Windows the
   certificate is re-imported through PKCS#12, because SChannel cannot use an ephemeral private key for TLS.
8. **The private key is probed explicitly.** The provider calls `ExportParameters(includePrivateParameters: true)` on
   the exported `RSA` or `ECDsa`, treats a `CryptographicException` as "public only", and zeroes the private parameter
   arrays. A key whose certificate does not match its private key throws `InvalidOperationException`.
9. **Endpoint aliases (RFC 8705 §5).** `OpenIdProviderMetadata.MtlsEndpointAliases` (`mtls_endpoint_aliases`) is parsed
   at discovery, and every alias must pass the endpoint rule of
   [ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md)
   (https, or http on loopback) or discovery fails. When the authenticator uses mTLS and the alias map has the
   endpoint's metadata name, the request goes to the alias. The DPoP proof's `htu`, the DPoP nonce cache and
   `ClientAuthenticationContext.Endpoint` all use the alias, the URL actually called. The sender learns the metadata
   name from the internal `ProviderEndpoint(MetadataName, Url)` record struct.
10. **Persistence keeps no issuers.** Only a key built in memory from certificates carries a certificate with a private
    key: a stored private key is PKCS#8 without a certificate, and a stored certificate is a public key a peer
    registered, which presents nothing. The secrets store therefore keeps no issuers. The family's JWK sets publish
    public keys without `x5c`, so the issuers do not reach them either.
11. **Proof that the certificate reaches the wire.** A unit test runs a real TLS handshake against a `TcpListener` and
    `SslStream` server bound to the loopback address on an ephemeral port, so it is hermetic. The integration test
    proves interoperability with the NCode OP through the test host's forwarded-certificate header, because
    `TestServer` terminates no TLS.

## Options considered

- **One handler choosing the certificate per request.** Rejected: connections pool per host, so a connection opened
  with one certificate would serve a request that needs another.
- **A dedicated `ClientRegistration` key-ring property for the certificate.** Rejected: the certificate is the
  client's credential, and `KeyRing` already holds those.
- **Inferring the transport from the method name.** Rejected: a closed list of names would stop a third-party method
  from using mutual TLS ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
- **Building the handler before selecting the certificate.** Rejected: the factory caches the exception it throws, so
  a configuration error would outlive its fix.
- **A separate chain store keyed by certificate thumbprint.** Rejected: the chain belongs to the certificate, and a
  second store would have to be kept consistent with every key ring that rotates.
- **Building the chain from the machine's certificate stores.** Rejected: it works only where an operator has
  installed the intermediate, which a container image or a key-management service does not do, and it fails silently
  otherwise.
- **Downloading intermediates through the certificate's Authority Information Access (`offline: false`).** Rejected:
  it makes an outbound request during a handshake, to a URL from the certificate, and fails where egress is
  restricted.
- **Taking the collection's first certificate as the key's.** Rejected: PKCS#12 does not order its bags, and the
  certificate with the private key is the one the key signs with.
- **Chosen:** a per-certificate named client built after selection, with the issuers carried on the key and presented
  offline.

## Consequences

- A custom `IClientAuthenticator` implements `UsesMutualTls`, and a custom `ISecretKeyFactory` implements the
  collection overload of `Create`.
- A host configures the per-certificate clients by name prefix, for example to set a proxy.
- A host that loads a client certificate from a PKCS#12 file passes the loaded collection to the factory, and an OP
  that trusts only the root accepts the handshake. A host that loads a PEM certificate and key builds the leaf with
  `X509Certificate2.CreateFromPem` and adds the issuer certificates it loads separately to the collection.
- Certificate-bound access tokens (RFC 8705 §3) and mutual TLS to resource servers are not covered.

## References

- RFC 8705 §2, §2.1 and §5: <https://www.rfc-editor.org/rfc/rfc8705>
- RFC 8446 §4.4.2, Certificate: <https://www.rfc-editor.org/rfc/rfc8446#section-4.4.2>
- RFC 5246 §7.4.6, Client Certificate: <https://www.rfc-editor.org/rfc/rfc5246#section-7.4.6>
- [ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md)
  — the protocol client and its endpoint rule
- [ADR-1286](1286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md) — DPoP
- [ADR-1173](1173-a-mutual-tls-client-is-authenticated-by-its-registered-certificate-chained-to-configured-trust-anchors.md)
  and
  [ADR-1172](1172-a-forwarded-client-certificate-is-decoded-by-the-decoder-registered-for-its-format.md) — the OP side
- [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md) —
  public-only certificates
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- Issues #392 and [#407](https://github.com/NCodeGroup/identity/issues/407)
