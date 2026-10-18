# NCode.Identity

[![CI](https://github.com/NCodeGroup/Identity/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/NCodeGroup/Identity/actions/workflows/ci.yml)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](global.json)

An **OpenID Connect** and **OAuth 2.x** provider for .NET, built as a family of independently-published libraries plus
a runnable reference server. It is not a framework you host as-is: the protocol model, the cryptography, the
persistence, and the composition are separate packages, so a host takes the layers it needs and replaces the rest
through public contracts.

> [!WARNING]
> **Greenfield, pre-release, under active development.** Nothing is shipped — no stable release, and no packages are
> published to NuGet. **Anything can break at any time**: the public API, settings names, database schema, and token
> formats all change without a deprecation cycle or migration path. Breaking changes land as clean cutovers rather
> than compatibility shims ([ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md)). Do not use this
> in production, and do not depend on anything here staying put.

## Status

| Aspect         | Where it stands                                                                                  |
| -------------- | ------------------------------------------------------------------------------------------------ |
| **Stage**      | Pre-1.0, greenfield, actively developed                                                           |
| **Published**  | Nothing — no NuGet packages, no releases, no tags                                                 |
| **Stability**  | None promised; public API, schema, and token formats are all free to change                       |
| **Certified**  | Not yet — OpenID Foundation conformance work is in progress (`T1` in [`BACKLOG.md`](BACKLOG.md))  |
| **Target**     | .NET 10                                                                                           |
| **Repository** | A **monorepo** for now; the packages may split into separate repositories at GA                   |

The whole family is versioned in lockstep from a single
[`version.json`](version.json) ([ADR-0003](docs/adr/0003-central-package-management-and-lockstep-versioning.md)), which
is part of why it currently lives in one repository. That is a convenience of the pre-release phase, not a commitment.

## Why

Most .NET options ask you to accept one opinion about storage, one about the user model, and one about the UI. This
family inverts that: every seam is a public contract with a default implementation you can swap, the security defaults
follow current authoritative guidance rather than historical compatibility, and the data model follows **Auth0's**
familiar shapes (tenants, resource servers, scopes, client grants) while offering finer-grained, data-driven control
where that is cheap to do.

Two consequences worth knowing up front:

- **Secure by default, permissive by configuration.** Authorization code + PKCE is the interactive path: the baseline
  advertises `response_types_supported` as **`code` only**, so the implicit and hybrid combinations that
  [RFC 9700] and OAuth 2.1 remove are off unless an operator deliberately widens the setting. Anonymous dynamic client
  registration is off, and capabilities that weaken posture are opt-in and advertised only when enabled
  ([ADR-0083](docs/adr/0083-secure-by-default-discovery-metadata.md)). The resource-owner password grant exists for
  hosts that genuinely need it, but it is a separate opt-in registration rather than part of the default composition.
- **Settings are a merge lattice, not a flag soup.** Server, tenant, resource-server, and client settings merge through
  declared rules (a floor cannot be raised by a child, a ceiling cannot be exceeded), so a tenant cannot configure its
  way out of a server-level security decision
  ([ADR-0108](docs/adr/0108-fapi-security-profile-merge-lattice-ceiling-injection.md)).

## What works today

**Grants and endpoints** — authorization code with PKCE, refresh token (with rotation and reuse detection), client
credentials, device authorization ([RFC 8628]), CIBA, token exchange ([RFC 8693]), pushed authorization requests
([RFC 9126]), dynamic client registration ([RFC 7591]/[RFC 7592]), token revocation and introspection, UserInfo,
discovery, JWKS, and RP-initiated / front-channel / back-channel logout.

**Client authentication** — `private_key_jwt`, `client_secret_basic` / `_post` / `_jwt`, and mutual TLS
(`tls_client_auth` and `self_signed_tls_client_auth`, [RFC 8705]), with a deployment-agnostic certificate resolver that
reads either the connection certificate or a trusted forwarded header.

**Token security** — sender-constrained access tokens via **DPoP** ([RFC 9449]) and mutual-TLS `cnf.x5t#S256`,
resource indicators ([RFC 8707]), pairwise subject identifiers, ACR and conditional `auth_time`, and a **FAPI 2.0**
security-profile ceiling that tightens the lattice in one move.

**JOSE / JWT** — a from-scratch JWS/JWE/JWT implementation (`NCode.Identity.Jose`) with first-class JWK/JWKS and
thumbprints, requirement-and-handler style validation, strict RFC 7519 `NumericDate` handling, **EdDSA** (Ed25519 /
Ed448) as a clean-room opt-in package, and **ML-DSA** post-quantum signatures through the BCL.

**Operations** — durable auditing with a publish-after-commit outbox, schema-versioned and upcastable at-rest grant
payloads, tenant-scoped data access enforced at the persistence layer, and a management API with resource-based
ownership authorization.

Known gaps, deferred decisions, and follow-ups are tracked in [`BACKLOG.md`](BACKLOG.md) — including the fact that
independent **OpenID Foundation conformance certification** is still in progress, so nothing here claims to be
certified.

## Layout

Production projects are under `src/`, tests under `tests/`.

| Area                 | Packages                                                                                                                           |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| JOSE / JWT           | `NCode.Identity.Jose(.Abstractions)`, `.Jose.EdDsa`, `.Jwt(.Abstractions)`, `.Jwk`, `NCode.Json`                                    |
| Secrets              | `NCode.Identity.Secrets(.Abstractions)`, `.Secrets.Persistence(.Abstractions)`                                                     |
| OpenID protocol      | `NCode.Identity.OpenId.Abstractions`, `.Core`, `.Messages.Abstractions`                                                            |
| Endpoints & grants   | `NCode.Identity.OpenId.Authentication(.Abstractions)`, `.Authentication.Interactive(.Abstractions)`                                |
| Management API       | `NCode.Identity.OpenId.Management(.Abstractions)`                                                                                  |
| Accounts             | `NCode.Identity.OpenId.Accounts*` (local-account store, ASP.NET Core Identity adapter, management)                                 |
| Persistence          | `NCode.Persistence.Abstractions`, `NCode.Identity.Persistence.Abstractions`, `NCode.Identity.OpenId.Persistence*(.EntityFramework)` |
| Composition and host | `NCode.Registration(.AspNetCore)`, `NCode.Identity`, `NCode.Identity.Server`, `NCode.Identity.OpenId.Playground*`                   |

The extensibility model — **service** vs **strategy collection** vs **mediator** — is
[ADR-0001](docs/adr/0001-mediator-vs-dependency-injection-logic-classes.md).

## Running the reference server

The `Playground` is a complete OpenID Provider host used as the reference composition and the local test bed.
[.NET Aspire](docs/adr/0071-aspire-local-dev-orchestrator.md) orchestrates the whole stack — the provider, a
PostgreSQL container, the schema migration worker, and the frontend apps — with deterministic startup ordering:

```pwsh
dotnet tool restore                      # once per clone
./build/frontend-install.ps1             # once per clone (Node 22 via NVS + pnpm workspace)
dotnet run --project src/NCode.Identity.OpenId.Playground.AppHost
```

That brings up the provider at `https://localhost:5001` plus the management console, the flow explorer, and the
conformance posture dashboard. See [`src/frontend/README.md`](src/frontend/README.md) for the frontend toolchain.

## Build and test

[`build/dod.ps1`](build/dod.ps1) is the **Definition of Done** — one entrypoint that CI and developers both run, so a
green run means the same thing in both places. It prints `Definition of Done: PASSED` as its final line.

```pwsh
./build/dod.ps1          # full gate: format check, build + pack, EF model drift, tests + coverage
./build/dod.ps1 -Fast    # local inner loop and pre-push gate: parallel, skips coverage + packing
```

[`CONTRIBUTING.md`](CONTRIBUTING.md) has the full workflow. Work lands on the single `main` trunk
([ADR-0125](docs/adr/0125-trunk-based-development-on-main.md)).

## Documentation

| Document                             | What it is                                                                     |
| ------------------------------------ | ------------------------------------------------------------------------------ |
| [`docs/adr/`](docs/adr/)             | Architecture Decision Records — the reasoning behind every non-obvious decision |
| [`BACKLOG.md`](BACKLOG.md)           | The single source of truth for known pending work                              |
| [`CHANGELOG.md`](CHANGELOG.md)       | Consumer-visible changes                                                       |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Build, conventions, and the change workflow                                    |
| [`GOVERNANCE.md`](GOVERNANCE.md)     | Ownership, versioning, releases, and publishing                                |
| [`SECURITY.md`](SECURITY.md)         | How to report a vulnerability                                                  |

## References

The specifications this family implements, grouped by area.

### OpenID Connect

| Specification                                                                                                     | Purpose                                            |
| ----------------------------------------------------------------------------------------------------------------- | -------------------------------------------------- |
| [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html)                                   | Identity layer on OAuth 2.0: flows, ID tokens, UserInfo |
| [Discovery 1.0](https://openid.net/specs/openid-connect-discovery-1_0.html)                                        | Provider metadata and issuer discovery             |
| [Dynamic Registration 1.0](https://openid.net/specs/openid-connect-registration-1_0.html)                          | Client registration over the protocol              |
| [RP-Initiated Logout 1.0](https://openid.net/specs/openid-connect-rpinitiated-1_0.html)                            | Relying-party initiated end-session                |
| [Session Management 1.0](https://openid.net/specs/openid-connect-session-1_0.html)                                 | Session state and change notification (opt-in)     |
| [Front-Channel Logout 1.0](https://openid.net/specs/openid-connect-frontchannel-1_0.html)                          | Browser-based logout propagation (opt-in)          |
| [Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html)                            | Server-to-server logout propagation                |
| [CIBA Core 1.0](https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html)         | Decoupled authentication without a browser redirect |
| [FAPI 2.0 Security Profile](https://openid.net/specs/fapi-security-profile-2_0-final.html)                         | Financial-grade hardening applied as a settings ceiling |

### OAuth 2.x core

| RFC                                                        | Purpose                           |
| ---------------------------------------------------------- | --------------------------------- |
| [RFC 6749](https://datatracker.ietf.org/doc/html/rfc6749)  | The OAuth 2.0 Authorization Framework |
| [RFC 6750](https://datatracker.ietf.org/doc/html/rfc6750)  | Bearer token usage                |
| [RFC 7009](https://datatracker.ietf.org/doc/html/rfc7009)  | Token revocation                  |
| [RFC 7662](https://datatracker.ietf.org/doc/html/rfc7662)  | Token introspection               |
| [RFC 8414](https://datatracker.ietf.org/doc/html/rfc8414)  | Authorization server metadata     |
| [RFC 9068](https://datatracker.ietf.org/doc/html/rfc9068)  | JWT profile for access tokens     |
| [RFC 9700](https://datatracker.ietf.org/doc/html/rfc9700)  | OAuth 2.0 Security Best Current Practice |

### JOSE — signing, encryption, keys

| RFC                                                        | Purpose                      |
| ---------------------------------------------------------- | ---------------------------- |
| [RFC 7515](https://datatracker.ietf.org/doc/html/rfc7515)  | JSON Web Signature (JWS)     |
| [RFC 7516](https://datatracker.ietf.org/doc/html/rfc7516)  | JSON Web Encryption (JWE)    |
| [RFC 7517](https://datatracker.ietf.org/doc/html/rfc7517)  | JSON Web Key (JWK)           |
| [RFC 7518](https://datatracker.ietf.org/doc/html/rfc7518)  | JSON Web Algorithms (JWA)    |
| [RFC 7519](https://datatracker.ietf.org/doc/html/rfc7519)  | JSON Web Token (JWT)         |
| [RFC 7638](https://datatracker.ietf.org/doc/html/rfc7638)  | JWK thumbprint               |

### Client authentication and registration

| RFC                                                        | Purpose                                        |
| ---------------------------------------------------------- | ---------------------------------------------- |
| [RFC 7521](https://datatracker.ietf.org/doc/html/rfc7521)  | Assertion framework for client authentication  |
| [RFC 7523](https://datatracker.ietf.org/doc/html/rfc7523)  | JWT client authentication (`private_key_jwt`)  |
| [RFC 7591](https://datatracker.ietf.org/doc/html/rfc7591)  | Dynamic client registration                    |
| [RFC 7592](https://datatracker.ietf.org/doc/html/rfc7592)  | Dynamic client registration management         |
| [RFC 8705](https://datatracker.ietf.org/doc/html/rfc8705)  | Mutual-TLS client authentication and certificate-bound tokens |

### Flows and token hardening

| RFC                                                        | Purpose                                   |
| ---------------------------------------------------------- | ----------------------------------------- |
| [RFC 7636](https://datatracker.ietf.org/doc/html/rfc7636)  | PKCE                                      |
| [RFC 8628](https://datatracker.ietf.org/doc/html/rfc8628)  | Device authorization grant                |
| [RFC 8693](https://datatracker.ietf.org/doc/html/rfc8693)  | Token exchange                            |
| [RFC 8707](https://datatracker.ietf.org/doc/html/rfc8707)  | Resource indicators                       |
| [RFC 9126](https://datatracker.ietf.org/doc/html/rfc9126)  | Pushed authorization requests (PAR)       |
| [RFC 9449](https://datatracker.ietf.org/doc/html/rfc9449)  | DPoP — sender-constrained tokens          |

## License

[Apache License 2.0](LICENSE).

[rfc 7591]: https://datatracker.ietf.org/doc/html/rfc7591
[rfc 7592]: https://datatracker.ietf.org/doc/html/rfc7592
[rfc 8628]: https://datatracker.ietf.org/doc/html/rfc8628
[rfc 8693]: https://datatracker.ietf.org/doc/html/rfc8693
[rfc 8705]: https://datatracker.ietf.org/doc/html/rfc8705
[rfc 8707]: https://datatracker.ietf.org/doc/html/rfc8707
[rfc 9126]: https://datatracker.ietf.org/doc/html/rfc9126
[rfc 9449]: https://datatracker.ietf.org/doc/html/rfc9449
[rfc 9700]: https://datatracker.ietf.org/doc/html/rfc9700
