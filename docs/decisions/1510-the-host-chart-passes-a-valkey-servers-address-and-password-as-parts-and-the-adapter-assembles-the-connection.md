# 1510. The host chart passes a Valkey server's address and password as parts, and the adapter assembles the connection

- **Status:** Accepted
- **Date:** 2026-11-12
- **Deciders:** NCode Group

## Context

A Valkey operator writes the server's address and password to a Secret of its own, and it writes no StackExchange.Redis
connection string. A host that reads one finished connection string leaves two facts for the deployer (issue
[#780](https://github.com/NCodeGroup/identity/issues/780)):

- **A hand-built copy goes stale.** The deployer assembles a connection string from the operator's parts and stores it
  in the Secret that `valkey.existingSecret` and `managementConsole.valkey.existingSecret` name. That Secret holds a
  second copy of the password, and a rotation by the operator leaves the copy behind.
- **A hand-built copy cannot be declared in GitOps.** The string carries the password, so the deployer cannot commit it,
  and the chart cannot read the operator's Secret at render time, because `lookup` returns nothing under
  `helm template` and Argo CD.

The Postgres connection already takes its parts from the operator's Secret
([ADR-1509](1509-the-host-chart-passes-a-postgres-operators-secret-as-the-connections-parts-and-the-host-assembles-the-npgsql-connection.md)).
The Valkey connection takes the same form.

## Decision

**The chart passes each part of the Valkey connection to the pods from the operator's Secret, and the adapter builds
the `ConfigurationOptions` from the parts itself, so a password stays a password whatever characters it holds.**

1. **Two forms of one connection.** The `ValkeyConnection` configuration section (`Host`, `Port`, `Username`,
   `Password`, `Ssl`) and the finished string `ConnectionStrings:valkey` are two forms of one connection. `AddValkey`
   resolves the connection from whichever form is configured and builds the `ConfigurationOptions` directly from the
   parts, so a password holding `,` or `=` stays a password. `Port` defaults to 6379, and `Username`, `Password` and
   `Ssl` are optional. The section binds strictly, so a misspelled part stops the host
   ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).
   `AddValkey` refuses both forms at once, a section without `Host`, a `Port` outside 1-65535 and an
   `Ssl` that is not a boolean, and its messages name configuration paths and never a configured value.
2. **The chart passes the parts as environment variables.** `valkey.parts` and `managementConsole.valkey.parts` each
   name a Secret (`existingSecret`), the keys that hold the host, port, user name and password (`hostKey`, `portKey`,
   `usernameKey`, `passwordKey`), and the boolean `ssl`. The chart renders each part as a `ValkeyConnection__*`
   environment variable from a `secretKeyRef`. The keys of `valkey.parts` default to `host`, `port`, none for the user
   name and `password`. The keys of `managementConsole.valkey.parts` fall back to those of `valkey.parts`.
3. **The chart refuses an unusable release.** It refuses a connection that names both its connection-string Secret and
   its parts Secret, and a parts Secret without a host key.
4. **A rotation reaches the pods at the next pod start.** A `secretKeyRef` environment variable is read when a
   container starts, so a password the operator rotates reaches the pods when they restart.

## Options considered

- **The `Valkey` options section.** Rejected: that section binds strictly and holds the tuning options of the adapter,
  not the server it connects to.
- **Kubernetes `$(VAR)` expansion into a connection string.** Rejected: the expansion splices the password into the
  string as text, and a password with `,` changes the string's meaning.
- **A chart-made Secret copy, through `lookup` or a hook.** Rejected: a copy goes stale when the operator rotates the
  password, and `lookup` returns nothing under `helm template`, so the chart would render differently from how it
  installs.
- **The adapter builds the connection from the parts.** Chosen: the Secret the operator owns stays the only copy, the
  `ConfigurationOptions` carry each value as a value, and `AddValkey` refuses a mixed or incomplete configuration at
  host start as the chart does at render time.

## Consequences

- No Secret is hand-built for Valkey: a deployer names the operator's Secret, and a deployer without an operator keeps
  the finished-string form in `valkey.existingSecret`.
- TLS to the server is the boolean `ssl`, and anything further arrives through `extraEnv`.
- The same resolver serves the host and the management console host.

## References

- Issue [#780](https://github.com/NCodeGroup/identity/issues/780)
- [ADR-1509](1509-the-host-chart-passes-a-postgres-operators-secret-as-the-connections-parts-and-the-host-assembles-the-npgsql-connection.md)
- [ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md)
- [ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)
- `charts/ncode-identity-host`
