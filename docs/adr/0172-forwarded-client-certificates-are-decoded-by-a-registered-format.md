# 172. Forwarded client certificates are decoded by a registered format

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

Mutual-TLS client authentication ([ADR-0107](0107-mutual-tls-client-authentication.md)) and certificate-bound access
tokens ([ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md)) read the certificate a client presented
through `IClientCertificateResolver`. When TLS terminates at the application, the certificate is on the connection.
Usually it terminates at a reverse proxy, load balancer, or service mesh, which forwards the certificate in a request
header, and every product encodes it its own way:

- ASP.NET Core certificate forwarding and HAProxy send one base64-encoded DER certificate.
- Traefik's `passTLSClientCert` with `pem: true` strips the PEM delimiters and line breaks from each certificate and
  comma-separates the chain the client presented, the client's own certificate first.
- nginx and the AWS Application Load Balancer send URL-encoded PEM; the load balancer sends the whole chain in
  pass-through mode.
- Envoy and Istio send an `X-Forwarded-Client-Cert` list whose elements carry `Cert` and `Chain` pairs.

The formats were a closed `enum` decoded by a `switch`, so supporting another proxy meant editing the library — the
lock-in [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) forbids. Each format decoded a single
certificate, so Traefik's chain failed to parse. A value that failed to parse was read as no certificate, and the
resolver then fell back to the certificate on the connection, which behind a proxy is at most the proxy's own. The
resolver also returned only the client's certificate, while validating a certificate chain needs the intermediate
certificates the client sent with it.

## Decision

**A forwarded certificate is decoded by the decoder registered for the configured format. Behind a proxy, the header is
the only source of the certificate, and a value that cannot be decoded fails the request.**

1. **Formats are an open vocabulary.** An `IClientCertificateHeaderDecoder` is a strategy named by its `Format`.
   `ClientCertificateHeaderFormats` names the known formats — `Base64Der`, `Pem`, `UrlEncodedPem`, and `EnvoyXfcc` — as
   the values a deployment already configured. `MutualTlsForwardingOptions.Format` is a string. The host refuses to
   start when a forwarded header names a format no decoder reads, and the resolver refuses two decoders that claim one
   format. A package supports another proxy by registering a decoder.
2. **Every format decodes a list.** A decoder returns a `PresentedClientCertificate`: the client's own certificate, and
   the other certificates it presented as `Intermediates`, in order. The intermediates are untrusted hints for building
   a chain, never a trust anchor.
   - `Base64Der` is a comma-separated list. A comma never occurs in base64, so one format reads ASP.NET Core's single
     certificate and Traefik's chain without ambiguity.
   - `Pem` and `UrlEncodedPem` read one or more `CERTIFICATE` blocks separated only by whitespace.
   - `EnvoyXfcc` reads the first element, the one added by the proxy closest to the client, honoring quoted values and
     backslash escapes. It prefers `Chain`; when the element also carries `Cert`, `Cert` must be the leaf of `Chain`.
3. **Decoding is all or nothing.** A value that is not entirely certificates in the format — text around a PEM block,
   an empty list entry, a block of another type, a repeated key — is not decoded.
4. **Behind a proxy, the header is the only source.** When a forwarded header is configured, a request without it
   presented no certificate. The certificate on the connection is never read, because it is at most the proxy's own on
   a re-encrypting hop. A deployment that accepts direct TLS does not configure a forwarded header, which a direct
   client could set.
5. **A value that cannot be decoded is `invalid_request`, never "no certificate".** The resolver logs a warning naming
   the header and the format and throws `invalid_request` (`400`). Every consumer today fails closed without a
   certificate, so reading the value as absent would not open those checks. It would, however, hide a proxy that
   encodes differently than configured behind `client_certificate_missing`. A consumer that treats a certificate as
   optional, such as one that binds a token only when a certificate is presented, would also silently issue an unbound
   token to a client that did present one.
6. **The certificate is resolved once per request.** The resolver takes the `OpenIdContext`, keeps its result in the
   request's items so client authentication and token binding see the same certificate, and disposes the certificates
   it decoded with the request. A connection's certificate belongs to the connection.
7. **One purpose namespace.** The resolver, the decoders, the options, and their validator live in
   `NCode.Identity.OpenId.Authentication.ClientCertificates`, registered by `AddClientCertificateServices()`. They serve
   client authentication and token binding alike, so neither the clients nor the tokens namespace owns them.

## Options considered

- **Add a Traefik member to the enum.** Rejected: the next proxy needs another edit, and ADR-0150's test fails — any
  deployment may sit behind a proxy this library has never seen.
- **A dedicated `TraefikBase64Der` format beside a single-certificate `Base64Der`.** Rejected: a list of one is a single
  certificate, so one list-aware format serves both and an operator cannot pick the wrong one.
- **Keep reading an undecodable value as no certificate.** Rejected for the reasons in item 5.
- **Return a status (decoded, absent, malformed) instead of throwing.** Rejected: every consumer would need to handle
  the malformed case, and one that forgot would read it as absent.
- **Fall back to the connection when the header is absent.** Rejected: behind a proxy, the connection's certificate is
  the proxy's.

## Consequences

- Traefik works with the default format: set `OpenId:MutualTls:CertificateHeaderName` to
  `X-Forwarded-Tls-Client-Cert`.
- Chain validation for `tls_client_auth` can use the intermediates a client presents. Kestrel exposes only the client's
  own certificate on a direct-TLS connection, so `Intermediates` is empty there.
- An undecodable forwarded value fails a mutual-TLS request with `invalid_request` and a warning. A client that does
  not use mutual TLS never resolves its certificate and is unaffected.
- **Breaking:** `MutualTlsClientCertificateFormat` is removed and `MutualTlsForwardingOptions.Format` is a string with
  the same names. `IClientCertificateResolver` and `MutualTlsForwardingOptions` move to the `ClientCertificates`
  namespace. `ResolveCertificateAsync` takes an `OpenIdContext` and returns a `PresentedClientCertificate`.

## References

- [ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md) — certificate-bound access tokens and the
  certificate-ingestion seam.
- [ADR-0107](0107-mutual-tls-client-authentication.md) — mutual-TLS client authentication.
- [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md) — configuration is validated when the
  host starts.
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open unless provably closed.
- [Hosting analysis §8](../hosting-analysis.md#8-traefik-mtls-and-tenant-pki) — Traefik's forwarded format.
- [Envoy `x-forwarded-client-cert`](https://www.envoyproxy.io/docs/envoy/latest/configuration/http/http_conn_man/headers#x-forwarded-client-cert)
  and [Traefik `passTLSClientCert`](https://doc.traefik.io/traefik/middlewares/http/passtlsclientcert/).
