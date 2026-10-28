# 258. The server sends Strict-Transport-Security outside Development

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

The reference host redirects a plain-HTTP request to HTTPS (`UseHttpsRedirection`), but a redirect protects only the
requests that reach it. A browser that is given the issuer's host name, or follows an `http://` link, sends its first
request in plain text, and an attacker on the path answers it before the redirect does (RFC 6797 §2.3). HTTP Strict
Transport Security closes that window: a browser that has received `Strict-Transport-Security` over HTTPS sends every
later request for the host, and for its subdomains when the policy includes them, over HTTPS only.

The header belongs to the transport, not to a page
([ADR-0256](0256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)
decision 7). Three facts decide where it is sent:

- The host terminates TLS itself when it authenticates clients by mutual TLS
  ([ADR-0173](0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md)), so no proxy in front of it
  sends the header.
- Behind a TLS-terminating proxy the host sees plain HTTP. It knows that a request arrived over HTTPS only from the
  `X-Forwarded-Proto` header of a proxy that `ReverseProxy` trusts, which `UseReverseProxy` applies first.
- RFC 6797 §7.2 forbids the header on a response sent over plain HTTP, and a browser ignores it there.

## Decision

**The server's turnkey pipeline sends `Strict-Transport-Security: max-age=31536000; includeSubDomains` on every
response to a request that arrived over HTTPS, outside the Development environment, as the `StrictTransportSecurity`
section configures it.**

1. **A registration and a pipeline step.** `AddStrictTransportSecurity(configuration)` binds
   `StrictTransportSecurityOptions` strictly from the top-level `StrictTransportSecurity` section and validates it
   when the host starts ([ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)).
   `UseStrictTransportSecurity()` adds ASP.NET Core's HSTS middleware, which writes the header only when the request
   is HTTPS and its host is not excluded. The reference host calls it right after `UseReverseProxy`, so a request that
   a trusted proxy received over HTTPS counts as HTTPS.
2. **The defaults follow the OWASP recommendation.** `Enabled` is `true`, `MaxAge` is 365 days (31,536,000 seconds),
   `IncludeSubDomains` is `true`, and `Preload` is `false`. The issuer's subdomains, such as tenant hosts and the
   mutual-TLS alias host, are served by the same deployment over HTTPS.
3. **Preload is the domain owner's decision.** It commits every subdomain of the registrable domain to HTTPS in the
   browsers' built-in lists, and removal takes months. The validator refuses `Preload` without `IncludeSubDomains` or
   with a `MaxAge` under 365 days, which the preload list requires.
4. **`MaxAge` may be zero, never negative.** `max-age=0` tells browsers to forget the policy, which is how an
   operator withdraws it.
5. **Development and loopback hosts never receive it.** `UseStrictTransportSecurity()` adds nothing in the
   Development environment, where a browser would remember the policy for host names that a developer also uses for
   plain-HTTP servers. `localhost`, `127.0.0.1` and `[::1]` are always excluded, and `ExcludedHosts` adds host names.
   An excluded host is a DNS name, an IPv4 address or a bracketed IPv6 address, without a scheme or a port, because
   the middleware compares it with the request's host.
6. **One component owns the header.** A deployment whose TLS-terminating proxy sends `Strict-Transport-Security`
   sets `Enabled` to `false`. A proxy that does not send it is trusted in `ReverseProxy`, so the host sees HTTPS and
   sends the header through the proxy.
7. **ADR-0256 is amended.** Its decision 7 leaves the header to the host's pipeline, and this pipeline step sends it.

## Options considered

- **Call the framework's `AddHsts` and `UseHsts` in the host.** Rejected: the host is a composition root of turnkey
  calls, the framework's default `max-age` is 30 days, and its options are neither bound strictly nor validated.
- **Bind `HstsOptions` directly.** Rejected: binding appends configured hosts to its default excluded hosts, and
  nothing would check the preload requirements.
- **Send the header from each page, as the page security headers are sent.** Rejected: the policy protects every
  request to the host, and the JSON endpoints would never send it.
- **Preload by default.** Rejected (decision 3).
- **Send it in Development.** Rejected (decision 5).
- **Leave it to a proxy.** Rejected: a host that terminates mutual TLS itself has no proxy to send it.

## Consequences

- A browser that has reached the issuer over HTTPS sends it no plain-HTTP request for a year, and the policy covers
  its subdomains.
- An operator who serves a subdomain of the issuer's host over plain HTTP sets `IncludeSubDomains` to `false`.
- Behind a proxy that does not set `X-Forwarded-Proto`, or that `ReverseProxy` does not trust, the host sees plain
  HTTP and sends no header; the proxy sends it instead, or the deployment trusts the proxy.
- The Helm chart passes the section through `extraEnv`, and its README describes the ingress case.

## References

- RFC 6797, HTTP Strict Transport Security: <https://www.rfc-editor.org/rfc/rfc6797>
- OWASP HTTP Strict Transport Security Cheat Sheet:
  <https://cheatsheetseries.owasp.org/cheatsheets/HTTP_Strict_Transport_Security_Cheat_Sheet.html>
- OWASP Secure Headers Project: <https://owasp.org/www-project-secure-headers/>
- The HSTS preload list and its requirements: <https://hstspreload.org/>
- [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md): configuration binds strictly and is
  validated at startup.
- [ADR-0256](0256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md):
  every page the server writes has a strict Content-Security-Policy and is framed only by design.
- Code: `src/NCode.Identity.Server/StrictTransportSecurityRegistration.cs`,
  `src/NCode.Identity.Server/StrictTransportSecurityApplicationBuilderExtensions.cs`,
  `src/NCode.Identity.Server/StrictTransportSecurity/StrictTransportSecurityOptions.cs`,
  `src/NCode.Identity.Server/StrictTransportSecurity/StrictTransportSecurityOptionsValidator.cs` and
  `src/NCode.Identity.OpenId.Host/Program.cs`.
