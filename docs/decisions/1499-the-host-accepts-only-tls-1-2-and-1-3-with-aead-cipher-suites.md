# 1499. The host accepts only TLS 1.2 and 1.3 with AEAD cipher suites

- **Status:** Accepted
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

FAPI 1.0 Part 2 §8.5 permits TLS 1.2 or later and a short list of AEAD cipher suites with forward secrecy. RFC 8996
deprecates TLS 1.0 and 1.1. A host that leaves the handshake to the operating system accepts whatever that system
enables, which differs between releases and deployments, and the OpenID Foundation conformance suite checks the
protocol version and the cipher suite of the connection.

- **The handshake precedes tenant resolution.** The host negotiates TLS before it reads the request, so it cannot know
  which tenant, and so which security profile, the connection serves.
- **Platforms differ in what a process may choose.** .NET's `CipherSuitesPolicy` is supported on Linux and macOS and is
  not supported on Windows, where SChannel takes its cipher suites from the system's configuration.

## Decision

**The host accepts only TLS 1.2 and TLS 1.3 everywhere, and restricts the cipher suites where the platform lets a
process choose them.**

1. **Two protocol versions.** `HostHttpsDefaults.Configure` sets `SslProtocols` to TLS 1.2 and TLS 1.3 on every
   platform.
2. **Six cipher suites.** Where the platform supports `CipherSuitesPolicy`, the handshake negotiates only
   `TLS_AES_256_GCM_SHA384`, `TLS_AES_128_GCM_SHA256`, `TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384`,
   `TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256`, `TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384` and
   `TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256`, which are AEAD suites with forward secrecy. On Windows the host does not
   set a policy.
3. **One call holds every HTTPS default.** Kestrel keeps only the last action passed to `ConfigureHttpsDefaults`, so
   `HostHttpsDefaults` also holds the acceptance of any client certificate at the handshake (RFC 8705), and the host
   passes `HostHttpsDefaults.Configure` as that one action.

## Options considered

- **Leave it to the operating system.** Rejected: the accepted protocols and cipher suites then depend on the system
  the host runs on, and a system that still enables TLS 1.0 or 1.1 or a non-AEAD suite fails FAPI 1.0 Part 2 §8.5.
- **A restriction for FAPI tenants only.** Rejected: the handshake precedes tenant resolution, so the host cannot
  apply a tenant's profile to the connection.
- **Windows-specific registry guidance.** Out of scope: the SChannel configuration is a deployment concern, set per
  machine, not in the host's code.

## Consequences

- A client that offers only TLS 1.0, TLS 1.1 or a cipher suite outside the list fails the handshake on Linux and
  macOS.
- Windows hosts cannot choose cipher suites in the process. A deployment on Windows, or behind a TLS-terminating proxy,
  sets the cipher suite policy in its own TLS layer.
- The accepted protocols, the cipher suites and the client certificate acceptance are one type, covered by unit tests.

## References

- Issue [#124](https://github.com/NCodeGroup/Identity/issues/124)
- [FAPI 1.0 Part 2: Advanced](https://openid.net/specs/openid-financial-api-part-2-1_0.html)
- [RFC 8996](https://www.rfc-editor.org/rfc/rfc8996)
- [RFC 8705](https://www.rfc-editor.org/rfc/rfc8705)
