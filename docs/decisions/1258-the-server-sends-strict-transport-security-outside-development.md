# 1258. The server sends Strict-Transport-Security outside Development

- **Status:** Accepted (decisions 1 and 6 amended by
  [ADR-1447](1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md):
  the registration and the pipeline step live in `NCode.Identity.AspNetCore`, every host composes them, and a host trusts its
  proxies through the `ForwardedHeaders` section, which `UseTrustedForwardedHeaders()` applies right before
  `UseStrictTransportSecurity()`; and by
  [ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md):
  HTTPS redirection applies outside Production only, because Production serves HTTPS only)
- **Date:** 2026-10-24
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0122](project-template/0122-strict-transport-security-outside-development.md)

## Context

[project-template ADR-0122](project-template/0122-strict-transport-security-outside-development.md) sends
`Strict-Transport-Security` on every response to a request that arrived over HTTPS outside Development, with OWASP's
defaults, from options bound strictly and validated when the host starts. The reference host redirects a plain-HTTP
request to HTTPS (`UseHttpsRedirection`), which protects only the requests that reach it (RFC 6797 §2.3).

The header belongs to the transport, not to a page
([ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)
decision 7). Three facts decide where it is sent:

- The host terminates TLS itself when it authenticates clients by mutual TLS
  ([ADR-1173](1173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md)), so no proxy in front of it
  sends the header.
- Behind a TLS-terminating proxy the host sees plain HTTP. It knows that a request arrived over HTTPS only from the
  `X-Forwarded-Proto` header of a proxy that `ReverseProxy` trusts, which `UseReverseProxy` applies first.

## Decision

**The server's turnkey pipeline sends `Strict-Transport-Security: max-age=31536000; includeSubDomains` on every
response to a request that arrived over HTTPS, outside the Development environment, as the `StrictTransportSecurity`
section configures it.**

1. **A registration and a pipeline step.** `AddStrictTransportSecurity(configuration)` binds
   `StrictTransportSecurityOptions` strictly from the top-level `StrictTransportSecurity` section and validates it
   when the host starts ([ADR-1137](1137-configuration-binds-strictly-and-is-validated-at-startup.md)).
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
7. **ADR-1256 is amended.** Its decision 7 leaves the header to the host's pipeline, and this pipeline step sends it.

## Options considered

- **Call the framework's `AddHsts` and `UseHsts` in the host.** Rejected: the host is a composition root of turnkey
  calls, the framework's default `max-age` is 30 days, and its options are neither bound strictly nor validated.
- **Preload by default.** Rejected (decision 3).
- **Send it in Development.** Rejected (decision 5).
- **Leave it to a proxy.** Rejected: a host that terminates mutual TLS itself has no proxy to send it.

## Consequences

- Behind a proxy that does not set `X-Forwarded-Proto`, or that `ReverseProxy` does not trust, the host sees plain
  HTTP and sends no header; the proxy sends it instead, or the deployment trusts the proxy.
- The Helm chart passes the section through `extraEnv`, and its README describes the ingress case.

## References

- RFC 6797, HTTP Strict Transport Security: <https://www.rfc-editor.org/rfc/rfc6797>
- OWASP HTTP Strict Transport Security Cheat Sheet:
  <https://cheatsheetseries.owasp.org/cheatsheets/HTTP_Strict_Transport_Security_Cheat_Sheet.html>
- OWASP Secure Headers Project: <https://owasp.org/www-project-secure-headers/>
- The HSTS preload list and its requirements: <https://hstspreload.org/>
- [ADR-1137](1137-configuration-binds-strictly-and-is-validated-at-startup.md): configuration binds strictly and is
  validated at startup.
- [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md):
  every page the server writes has a strict Content-Security-Policy and is framed only by design.
- Code: `src/NCode.Identity.Server/StrictTransportSecurityRegistration.cs`,
  `src/NCode.Identity.Server/StrictTransportSecurityApplicationBuilderExtensions.cs`,
  `src/NCode.Identity.Server/StrictTransportSecurity/StrictTransportSecurityOptions.cs`,
  `src/NCode.Identity.Server/StrictTransportSecurity/StrictTransportSecurityOptionsValidator.cs` and
  `hosts/NCode.Identity.OpenId.Host/Program.cs`.
