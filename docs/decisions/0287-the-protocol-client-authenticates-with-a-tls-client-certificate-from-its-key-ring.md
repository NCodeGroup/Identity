# 287. The protocol client authenticates with a TLS client certificate from its key ring

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

The backend-for-frontend ([design](../backend-for-frontend.md) §5.1, issue #123) authenticates to the OP with the
methods the registration names. [ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md)
shipped `client_secret_basic`, `client_secret_post` and `private_key_jwt`, and
[ADR-0286](0286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md) left mutual TLS as the
last phase-1b item. RFC 8705 §2 defines two methods: `tls_client_auth` (the OP validates the certificate's PKI chain
and subject) and `self_signed_tls_client_auth` (the OP matches the certificate against the client's registered keys).
Both need the client to present a TLS client certificate on the connection, and RFC 8705 §5 lets the provider publish
`mtls_endpoint_aliases` that a client using mutual TLS sends requests to. This ADR records where the certificate comes
from, how it reaches the wire and which endpoint the request targets, for
`NCode.Identity.OpenId.Client` ([ADR-0285](0285-the-openid-protocol-client-needs-no-aspnet-core-runtime.md) keeps it
free of ASP.NET Core).

## Decision

**The protocol client authenticates with `tls_client_auth` and `self_signed_tls_client_auth` by presenting a
certificate from the registration's key ring on a dedicated HTTP client, and sends token, revocation and PAR requests
to the provider's `mtls_endpoint_aliases` when it has them.**

1. **The authenticator declares the transport.** `IClientAuthenticator` gains `bool UsesMutualTls { get; }`: a method
   that authenticates with the TLS client certificate returns `true`, and the request sender then picks the certificate
   and the mTLS endpoint alias. The strategy declares the trait and the sender never infers it from the method name
   ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)), so a third-party mTLS method gets the same
   transport by returning `true`. The three existing methods return `false`.
2. **The certificate comes from `ClientRegistration.KeyRing`**, the ring that already holds the client's credentials;
   there is no new registration property. A key qualifies when it is an `RsaSecretKey` or `EccSecretKey` with a
   certificate, its `Metadata.Use` is not `enc` (so an ID-token decryption key in the same ring is never presented), it
   holds a private key, its certificate allows client authentication (no EKU extension, or one listing
   `1.3.6.1.5.5.7.3.2`; no KeyUsage extension, or one with `DigitalSignature`), and the time is within the certificate's
   `NotBefore`..`NotAfter`. The first qualifying key in ring order wins, so a staged certificate that is not yet valid
   is skipped until it is. No qualifying key throws `InvalidOperationException`, a configuration error.
3. **One `IHttpClientFactory` client per certificate.** A TLS client certificate belongs to the connection, and
   `SocketsHttpHandler` pools connections per host, not per certificate, so one handler can never serve two
   certificates. The client name is `OpenIdClientDefaults.MutualTlsHttpClientNamePrefix`
   (`"NCode.Identity.OpenId.Client.MutualTls:"`, public so a host can configure those clients) followed by the uppercase
   hex SHA-256 of the certificate's DER. An internal `IConfigureNamedOptions<HttpClientFactoryOptions>` gives every name
   with that prefix a `SocketsHttpHandler` with redirects and cookies off, whose
   `SslOptions.ClientCertificateContext` is `SslStreamCertificateContext.Create(certificate, null, offline: true)`, so
   the certificate is always presented, whatever CA list the server sends. Rotation needs no code: a new certificate
   gets a new name, and the factory's two-minute handler lifetime retires idle clients.
4. **The certificate is selected before the factory is asked for a client.** `IHttpClientFactory` caches an exception
   thrown while it builds a handler for a name, so selection, the private-key check and materialization run in
   `ClientCertificateProvider.SelectHttpClientName`, which caches each materialized certificate by thumbprint. Building
   the handler afterwards only looks the certificate up. The provider is a DI singleton and disposes the cached
   certificates with the container. Cached certificates are not evicted: there is one per distinct certificate seen, and
   disposing one under a live handler would break its handshakes. On Windows the certificate is re-imported through
   PKCS#12, because SChannel cannot use an ephemeral private key for TLS.
5. **The private key is probed explicitly.** The client package sees only `Secrets.Abstractions`, which has no "has
   private key" member for RSA or EC keys, so the provider calls `ExportParameters(includePrivateParameters: true)` on
   the exported `RSA` or `ECDsa`, treats a `CryptographicException` as "public only", and zeroes the private parameter
   arrays. A key whose certificate does not match its private key is a configuration error, also an
   `InvalidOperationException`.
6. **Endpoint aliases (RFC 8705 §5).** `OpenIdProviderMetadata.MtlsEndpointAliases` (`mtls_endpoint_aliases`) is parsed
   at discovery, and every alias must pass the endpoint rule of ADR-0270 D5 (https, or http on loopback) or discovery
   fails. When the authenticator uses mTLS and the alias map has the endpoint's metadata name, the request goes to the
   alias. The DPoP proof's `htu`, the DPoP nonce cache and `ClientAuthenticationContext.Endpoint` all use the alias, the
   URL actually called. The sender learns the metadata name from an internal `ProviderEndpoint(MetadataName, Url)`
   record struct that replaces its `string endpoint` parameter.
7. **`client_id` is always sent** by both methods (RFC 8705 §2 requires it). They share an internal abstract base,
   `MutualTlsClientAuthenticator`, and differ only in `MethodName`: the OP, not the client, tells PKI validation from
   thumbprint matching.
8. **Proof that the certificate reaches the wire.** A unit test runs a real TLS handshake against a `TcpListener` and
   `SslStream` server bound to the loopback address on an ephemeral port, so it is hermetic. The integration test proves
   interoperability with the NCode OP through the test host's forwarded-certificate header, because `TestServer`
   terminates no TLS.

## Options considered

- **One handler choosing the certificate per request.** Rejected: connections pool per host, so a connection opened
  with one certificate would serve a request that needs another.
- **A dedicated `ClientRegistration` key-ring property for the certificate.** Rejected: the certificate is the
  client's credential, and `KeyRing` already holds those.
- **Inferring the transport from the method name.** Rejected: a closed list of names would stop a third-party method
  from using mutual TLS ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).
- **Building the handler without first selecting the certificate.** Rejected: the factory caches the exception it
  throws, so a configuration error would outlive its fix.

## Consequences

- A custom `IClientAuthenticator` must implement `UsesMutualTls`, a breaking addition accepted under the pre-release
  posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- A host can configure the per-certificate clients by name prefix, for example to set a proxy.
- A client certificate issued by an intermediate CA reaches the OP without its chain, because `AsymmetricSecretKey`
  carries one certificate. Presenting intermediates needs chain storage in the secrets key model and is a follow-up.
- Certificate-bound access tokens (RFC 8705 §3) and mutual TLS to resource servers are not covered.

## References

- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — pre-release posture
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- [ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) — the protocol client
- [ADR-0285](0285-the-openid-protocol-client-needs-no-aspnet-core-runtime.md) — no ASP.NET Core runtime
- [ADR-0286](0286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md) — DPoP
- RFC 8705 §2 and §5 (mutual-TLS client authentication, endpoint aliases)
- [Backend-for-frontend design](../backend-for-frontend.md) §5.1
- Issue #392
