# 1447. Each host composes forwarded-header trust and Strict-Transport-Security from one general package outside Development

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0122](project-template/0122-strict-transport-security-outside-development.md)

## Context

[project-template ADR-0122](project-template/0122-strict-transport-security-outside-development.md) sends
`Strict-Transport-Security` on every response to a request that arrived over HTTPS outside Development, from options
bound strictly and validated when the host starts. The family adds where the helpers live, how a host learns that a
request arrived over HTTPS, and why the host sends the header itself.

A host behind a TLS-terminating ingress outside Production sees plain HTTP to an internal address. Without trusted
forwarded headers it reads the wrong scheme and host, and without the header a browser does not pin HTTPS for the
host's origin. In Production the host serves HTTPS only
([ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md)), so every request arrives over HTTPS. Five facts shape the
decision:

- A host that terminates TLS itself, with no proxy in front of it, has no proxy to send the header. Behind a
  TLS-terminating proxy it sees HTTPS only through `X-Forwarded-Proto` from a proxy that `ForwardedHeaders` trusts.
- The management console host serves behind the same ingress as the OP host and must not reference
  `NCode.Identity.Server`.
- Neither helper names an identity concept, so both are general ASP.NET Core helpers.
- YARP, which a BFF uses for remote APIs, reads its routes from a `ReverseProxy` section by convention and registers
  itself with `AddReverseProxy()`.
- Whether a proxy terminates TLS is the deployment's to say, so a library that adds the helpers for every host would
  decide it for the host.

Issue [#611](https://github.com/NCodeGroup/identity/issues/611) describes the need.

## Decision

**`NCode.Identity.AspNetCore` holds forwarded-header trust and Strict-Transport-Security, each host composes both, and
the header is sent outside Development as `Strict-Transport-Security: max-age=31536000; includeSubDomains`.**

1. **`NCode.Identity.AspNetCore` holds both helpers.** It holds `AddTrustedForwardedHeaders(configuration)` and
   `UseTrustedForwardedHeaders()` with `TrustedForwardedHeadersOptions`, bound from the `ForwardedHeaders` section
   (`KnownProxies`, `KnownNetworks`, `ForwardLimit`) and checked by `TrustedForwardedHeadersOptionsValidator`. It also
   holds `AddStrictTransportSecurity(configuration)` and `UseStrictTransportSecurity()` with
   `StrictTransportSecurityOptions` (section `StrictTransportSecurity`) and
   `StrictTransportSecurityOptionsValidator`. Both options types live in `NCode.Identity.AspNetCore.Options`, bind
   strictly and validate when the host starts
   ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).
   `TrustedForwardedHeadersOptionsValidator` refuses a `KnownProxies` entry that is not an IP address, or an IPv4
   address not written as four decimal octets in canonical form, because the parser also accepts shorthand and octal
   forms that denote another address. It refuses a `KnownNetworks` entry that is not CIDR notation whose address is
   the network's first address, so the network trusted is the one written, and a `ForwardLimit` below 1, which
   consults no forwarded entry.
2. **Each host composes both.** The OP host and the management console host call `UseTrustedForwardedHeaders()` and
   then `UseStrictTransportSecurity()` first in their pipelines, after the proxy-certificate check of
   [ADR-1506](1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md). `AddIdentityServer()` and `AddIdentityBff()` register
   neither, because whether a proxy terminates TLS is the deployment's decision. A BFF host references
   `NCode.Identity.AspNetCore` to do the same.
3. **The header follows the template's defaults.** The header is
   `Strict-Transport-Security: max-age=31536000; includeSubDomains` on every response to an HTTPS request outside
   Development, which is every response in Production. The redirect that pairs with the header is added outside
   Production only. `Enabled` is `true`, `MaxAge` is 365 days, `IncludeSubDomains` is `true` and `Preload` is `false`. The
   issuer's subdomains, such as tenant hosts and the mutual-TLS alias host, are served over HTTPS by the same
   deployment.
4. **The validator guards preload and age.** It refuses `Preload` without `IncludeSubDomains`, `Preload` with a
   `MaxAge` under 365 days, and a negative `MaxAge`. `max-age=0` is allowed: it withdraws the policy.
5. **Development and loopback hosts never receive it.** Development adds nothing. `localhost`, `127.0.0.1` and `[::1]`
   are always excluded. `ExcludedHosts` entries are DNS names, IPv4 addresses or bracketed IPv6 addresses, without a
   scheme or a port.
6. **One component owns the header.** A deployment whose proxy sends `Strict-Transport-Security` sets `Enabled` to
   `false`. Otherwise the host trusts the proxy in `ForwardedHeaders`, so it sees HTTPS and sends the header.
7. **The Helm chart carries the configuration.** It sets `ForwardedHeaders__…` from its `forwardedHeaders` values and
   passes `StrictTransportSecurity__…` through `extraEnv`. Its README describes the ingress case.

## Options considered

- **Keep the helpers in `NCode.Identity.Server`.** Rejected: the management console host must not reference it.
- **A separate hosting package.** Rejected: `NCode.Identity.AspNetCore` is the package that
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)
  names for general ASP.NET Core helpers.
- **The `ReverseProxy` section name.** Rejected: YARP owns that section, and strict binding of trust options from it
  would refuse YARP's keys.
- **Compose them inside `AddManagementConsole`.** Rejected: whether a proxy terminates TLS is the deployment's to say.
- **The framework's `AddHsts` and `UseHsts` defaults.** Rejected: the default `max-age` is 30 days, and its options are
  neither bound strictly nor validated.
- **Leave the header to a proxy.** Rejected: a host that terminates TLS itself has no proxy to send it.
- **Compose both from one general package, per host.** Chosen.

## Consequences

- The console host trusts its ingress through `ForwardedHeaders:KnownNetworks` and sends the header outside
  Development.
- Outside Production, behind a proxy that does not set `X-Forwarded-Proto`, or that `ForwardedHeaders` does not trust,
  the host sees plain HTTP and sends no header; the proxy sends it instead, or the deployment trusts the proxy.
- `NCode.Identity.AspNetCore` references `NCode.Identity.Registration` for strict binding and depends on
  `NCode.Buffers`.
- A host that omits the two calls sends no header and reads the wrong scheme behind an ingress.

## References

- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md),
  [ADR-1173](1173-a-mutual-tls-client-is-authenticated-by-its-registered-certificate-chained-to-configured-trust-anchors.md),
  [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md),
  [ADR-1437](1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md),
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)
- RFC 6797, HTTP Strict Transport Security: <https://www.rfc-editor.org/rfc/rfc6797>
- OWASP HTTP Strict Transport Security Cheat Sheet:
  <https://cheatsheetseries.owasp.org/cheatsheets/HTTP_Strict_Transport_Security_Cheat_Sheet.html>
- `src/NCode.Identity.AspNetCore/`
- Issue [#611](https://github.com/NCodeGroup/identity/issues/611)
