# 1172. A forwarded client certificate is decoded by the decoder registered for its format

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

Mutual-TLS client authentication ([ADR-1173](1173-a-mutual-tls-client-is-authenticated-by-its-registered-certificate-chained-to-configured-trust-anchors.md)) and certificate-bound access
tokens ([ADR-1097](1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md)) read the certificate a client presented
through `IClientCertificateResolver`. When TLS terminates at the application, the certificate is on the connection.
Usually it terminates at a reverse proxy, load balancer or service mesh, which forwards the certificate in a request
header, and every product encodes it its own way:

- ASP.NET Core certificate forwarding and HAProxy send one base64-encoded DER certificate.
- Traefik's `passTLSClientCert` with `pem: true` strips the PEM delimiters and line breaks from each certificate and
  comma-separates the chain the client presented, the client's own certificate first.
- nginx and the AWS Application Load Balancer send URL-encoded PEM; the load balancer sends the whole chain in
  pass-through mode.
- Envoy and Istio send an `X-Forwarded-Client-Cert` list whose elements carry `Cert` and `Chain` pairs.

Proxies are an open set: any deployment may sit behind one this library has never seen, so the formats are a vocabulary
a package extends by registering, not a closed list
([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). A client may present a chain, and validating a
certificate chain needs the intermediate certificates the client sent with its own. Behind a proxy the certificate on
the connection is at most the proxy's own, so a value that cannot be decoded must not be read as "no certificate" and
the connection must not stand in for the header.

## Decision

**A forwarded certificate is decoded by the decoder registered for the configured format. Behind a proxy, the header is
the only source of the certificate, and a value that cannot be decoded fails the request.**

1. **The resolver returns what the client presented.** `IClientCertificateResolver.ResolveCertificateAsync(OpenIdContext,
   CancellationToken)` returns a `PresentedClientCertificate`: the client's own `Certificate` and, in order, the other
   certificates it presented as `Intermediates`. The intermediates are untrusted hints for building a chain, never a
   trust anchor. Kestrel exposes only the client's own certificate on a direct-TLS connection, so `Intermediates` is
   empty there.
2. **Formats are an open vocabulary.** An `IClientCertificateHeaderDecoder` is a strategy named by its `Format`.
   `ClientCertificateHeaderFormats` names `Base64Der`, `Pem`, `UrlEncodedPem` and `EnvoyXfcc`.
   `MutualTlsForwardingOptions.Format` (`OpenId:MutualTls`) is a string that defaults to `Base64Der`.
   When a certificate header is configured, the host refuses to start unless a registered decoder reads the configured
   format, and the resolver refuses two decoders of one format. A package
   supports another proxy by registering a decoder.
3. **Every format decodes a list.**
   - `Base64Der` is a comma-separated list. A comma never occurs in base64, so one format reads ASP.NET Core's single
     certificate and Traefik's `passTLSClientCert` chain without ambiguity.
   - `Pem` and `UrlEncodedPem` read one or more `CERTIFICATE` blocks separated only by whitespace.
   - `EnvoyXfcc` reads the first element, honoring quoted values and backslash escapes. It prefers `Chain`; when the
     element also carries `Cert`, `Cert` must be the leaf of `Chain`.
4. **Decoding is all or nothing.** A value that is not entirely certificates in the format — text around a PEM block,
   an empty entry, a block of another type, a repeated key — decodes nothing.
5. **Behind a proxy, the header is the only source.** When `MutualTlsForwardingOptions.CertificateHeaderName` is set,
   a request without that header presented no certificate, and the certificate on the connection is never read.
   Without a forwarded header, a connection whose certificate the host verified as the proxy's
   (`IProxyClientCertificateFeature`, [ADR-1506](1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md)) is never read as an end client's either. A
   deployment that accepts direct TLS sets no header, because a direct client could forge it.
6. **A value that cannot be decoded is `invalid_request`, never "no certificate".** The resolver logs a warning naming
   the header and the format and throws `invalid_request` (`400`). A misconfigured proxy is visible instead of hiding
   behind `client_certificate_missing`, and a token is never issued unbound to a client that did present a certificate.
7. **The certificate is resolved once per request.** The resolver keeps its result in the request's items, so client
   authentication and token binding see the same certificate. It disposes the certificates it decoded with the request;
   a connection's certificate belongs to the connection.
8. **One purpose namespace.** The resolver, the decoders, the options and `MutualTlsForwardingOptionsValidator` live in
   `NCode.Identity.OpenId.Authentication.ClientCertificates`, registered by `AddClientCertificateServices()`. They
   serve client authentication and token binding alike, so neither the clients nor the tokens namespace owns them.

## Options considered

- **An enum member per proxy.** Rejected: the next proxy needs another edit, and the open-vocabulary test of ADR-1150
  fails.
- **A dedicated `TraefikBase64Der` format beside a single-certificate `Base64Der`.** Rejected: a list of one is a single
  certificate, so one list-aware format serves both and an operator cannot pick the wrong one.
- **Read an undecodable value as absent.** Rejected: it hides a proxy that encodes differently than configured, and a
  consumer that binds a token only when a certificate is presented would issue an unbound token to a client that did
  present one.
- **Return a decode status (decoded, absent, malformed).** Rejected: every consumer would need to handle the malformed
  case, and one that forgot would read it as absent.
- **Fall back to the connection when the header is absent.** Rejected: behind a proxy, the connection's certificate is
  the proxy's.
- **A decoder registered per format that fails the request on an undecodable value, with the header as the only source
  behind a proxy.** Chosen.

## Consequences

- Traefik works with the default format: set `OpenId:MutualTls:CertificateHeaderName` to
  `X-Forwarded-Tls-Client-Cert`.
- Chain validation for `tls_client_auth` can use the intermediates a client presents.
- An undecodable forwarded value fails a mutual-TLS request with `invalid_request` and a warning. A client that does
  not use mutual TLS never resolves its certificate and is unaffected.
- A package supports another proxy by registering a decoder, without editing the library.

## References

- [ADR-1097](1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md) — certificate-bound access tokens and the
  certificate-ingestion seam.
- [ADR-1173](1173-a-mutual-tls-client-is-authenticated-by-its-registered-certificate-chained-to-configured-trust-anchors.md) — mutual-TLS client authentication.
- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md) — configuration is validated when the
  host starts.
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open unless provably closed.
- [Hosting analysis §8](../hosting-analysis.md#8-traefik-mtls-and-tenant-pki) — Traefik's forwarded format.
- [Envoy `x-forwarded-client-cert`](https://www.envoyproxy.io/docs/envoy/latest/configuration/http/http_conn_man/headers#x-forwarded-client-cert)
  and [Traefik `passTLSClientCert`](https://doc.traefik.io/traefik/middlewares/http/passtlsclientcert/).
