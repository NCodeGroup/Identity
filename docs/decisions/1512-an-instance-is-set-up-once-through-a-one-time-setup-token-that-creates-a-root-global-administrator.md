# 1512. An instance is set up once through a one-time setup token that creates a root global administrator

- **Status:** Accepted
- **Date:** 2026-11-12
- **Deciders:** NCode Group

## Context

A Production instance starts with an empty database: it has no administrator, and no credential is safe to ship as a
default. Every management authority is a role assignment
([ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md)),
so the first administrator has to come from somewhere an operator controls (issues
[#777](https://github.com/NCodeGroup/identity/issues/777) and
[#776](https://github.com/NCodeGroup/identity/issues/776)).

Three facts shape how that administrator is created:

- **The first request is anonymous.** Until an administrator exists, nobody can authenticate, so whatever creates the
  administrator must be guarded by something other than a login, and it must stop working once it has been used.
- **Logs are private to the operator.** Whoever can read a replica's log already has the same authority over the
  deployment as the operator, so a secret written there once reaches the operator and nobody else.
- **Replicas share only the database.** There is no shared memory or file system between replicas, so any state that
  decides whether setup is allowed lives in the database.

## Decision

**An instance is set up once, through a setup wizard guarded by a one-time setup token, and setup creates a root global
administrator.**

1. **The OP serves a setup wizard at `/setup`.** The page is the `@ncode/app-setup` bundle. It reads the instance state
   from `GET /setup/api/status` and creates the root administrator with `POST /setup/api/root`. After it creates the
   root, it lists the remaining configuration and links to the first-use document.
2. **A setup token guards the wizard.** A configured `Setup:Token` of at least 32 characters is the token: the host holds
   only its SHA-256 hash, compares it with `CryptographicOperations.FixedTimeEquals`, never logs it, and generates no
   token. Without a configured token, an uninitialized instance generates a 256-bit base64url token when it starts. It
   stores only the SHA-256 hash of the token, with its issue and expiry instants, in the `InstanceSetup` row that every
   replica reads, and logs the plaintext once at Warning on the replica that stored it. The generated token lives for
   `Setup:GeneratedTokenLifetime`, which defaults to 1 hour. A starting replica issues a token in place of a stored one that was issued
   before its process started, so a restart regenerates the token. Replicas that start together in one rollout converge
   through the row's concurrency token: one stores its token, and the others log at Information that another replica
   issued it.
3. **Setup is single use.** Creating the root administrator marks the `InstanceSetup` row initialized in the same save.
   Once the instance is initialized, every `/setup` route answers 404 for good, and a concurrent second attempt loses on
   the concurrency token and answers 404.
4. **The setup surface is hardened.** `/setup` answers 404 to a request that is not HTTPS in Production. It rate-limits
   `POST /setup/api/root` to 10 requests per minute per client address and answers 429 beyond that. It serves its page
   with a nonce Content-Security-Policy (`script-src 'nonce-…' 'strict-dynamic'`, `style-src 'nonce-…'`,
   `img-src 'self'`, `connect-src 'self'`, `form-action 'none'`)
   ([ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md))
   and `Cache-Control: no-store`. It never writes request or response bodies to HTTP logging, because its endpoints carry
   `WithConfidentialBodies()`
   ([ADR-1503](1503-http-logging-writes-a-body-only-for-an-endpoint-that-opts-in-and-never-for-one-that-carries-credentials.md)).
   The root password must satisfy the control-plane tenant's password policy and be at least 15 characters. The wizard
   reads the token from the URL fragment (`#token=`), which reaches neither the server nor a referrer, and strips it
   with `history.replaceState`.
5. **The root administrator is a control-plane local account holding `GlobalAdmin` at the platform node.** One role
   assignment (`GlobalAdmin` on the platform resource) in the control-plane tenant confers every management authority, so
   setup writes no other assignment. The account, its identity, the assignment and the initialized mark are staged on one
   store manager and saved once.
6. **Setup also runs unattended.** The host's `setup` command reads `Setup:Root:UserName`, `Setup:Root:Email` and
   `Setup:Root:Password`, needs no token, creates the root through the same service, and exits 0 when the instance is
   already initialized, so a Job can run it on every release. The chart references an operator Secret for the token
   (`setup.token.existingSecret` and `setup.token.key`, passed as `Setup__Token`) and offers an opt-in `setup.job` that
   runs the `setup` command with `Setup__Root__*` from an operator Secret. The chart generates no token Secret.
7. **`BootstrapAdmin` is a development convenience.** The Production safety check refuses the `BootstrapAdmin` section
   in Production like the other development seeders, so the setup token is the Production path
   ([ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md)).
8. **Setup can register the management console's client.** The wizard's console step, or `Setup:ManagementConsole`
   (`ClientId`, which defaults to `mgmt-console`, `BaseUrl` and `Jwks`) for the headless command, registers the console
   as a confidential client of the control-plane tenant in the same save as the root administrator
   ([ADR-1437](1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md)).
   The client has a requested, stable `client_id`, and its public JWK Set is imported by value, never through a
   `jwks_uri` fetch, because a console on a private network cannot be fetched from. Setup stages the client through
   `IClientCreator`, the seam the management API's client creation uses, so it passes the same `IClientValidator`,
   settings and coherence validation, and a refused registration saves nothing, not even the root administrator.

The setup packages follow the package shape of
[ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md):
`NCode.Identity.OpenId.Setup.Abstractions` holds the options, the wire contracts and the service contracts, and
`NCode.Identity.OpenId.Setup` implements them over the abstractions.

## Options considered

- **A static administrator password in configuration.** Rejected: a password that sits in configuration, in a values
  file or in a repository stays valid after setup, and a default one is a credential every deployment shares.
- **A token that is only logged.** Rejected: a GitOps install, which has no browser and no one reading a log during the
  rollout, cannot use it, and an operator cannot choose the token ahead of time.
- **A shared secret that is only configured.** Rejected: an operator who has not prepared a Secret cannot reach the
  wizard, and a chart that generates the Secret needs `lookup`, which GitOps renderers do not run.
- **A token that is configured or, failing that, generated (chosen).** An operator who prepared a Secret supplies it, so
  a GitOps install needs no browser. An operator who did not prepare one reads the generated token from the log. Both
  paths end the same way: creating the root administrator.

## Consequences

- An operator reads the generated token from the log of the one replica that stored it, and the token expires after
  `Setup:GeneratedTokenLifetime`. A restart before setup issues a fresh token.
- A configured token has no lifetime, and it is useless once the instance is initialized. The operator removes it from
  the deployment after setup.
- `/setup` is a claimed path segment like `/api`, so no tenant or client path uses it.
- No chart value holds an administrator password for the interactive path, and the chart needs no bootstrap
  administrator values.
- The `setup` command trusts whoever can run the image with the database connection, which is already full authority
  over the instance.

## References

- Issues [#777](https://github.com/NCodeGroup/identity/issues/777) and
  [#776](https://github.com/NCodeGroup/identity/issues/776)
- [ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md),
  [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md),
  [ADR-1437](1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md),
  [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md),
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md),
  [ADR-1503](1503-http-logging-writes-a-body-only-for-an-endpoint-that-opts-in-and-never-for-one-that-carries-credentials.md)
