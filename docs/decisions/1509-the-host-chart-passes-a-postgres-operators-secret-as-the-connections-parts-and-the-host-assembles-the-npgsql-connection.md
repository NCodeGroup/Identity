# 1509. The host chart passes a Postgres operator's Secret as the connection's parts, and the host assembles the Npgsql connection

- **Status:** Accepted
- **Date:** 2026-11-12
- **Deciders:** NCode Group

## Context

A Postgres operator such as CloudNativePG writes the parts of a connection to a Secret of its own, `<cluster>-app` for
CloudNativePG: `host`, `port`, `dbname`, `user`, `password`, `uri` and `jdbc-uri`. It writes no Npgsql connection
string. Two facts follow for a host that reads one finished connection string (issue
[#780](https://github.com/NCodeGroup/identity/issues/780)):

- **A hand-built copy goes stale.** A deployer who assembles an Npgsql string from the operator's parts and stores it in
  a Secret of their own holds a second copy of the password, and a rotation by the operator leaves the copy behind.
- **A hand-built copy cannot be declared in GitOps.** The string carries the password, so the deployer cannot commit it,
  and the chart cannot read the operator's Secret at render time, because `lookup` returns nothing under
  `helm template` and Argo CD.

The password is also a value that may hold `;`, `'` or `"`, characters that change the meaning of an Npgsql connection
string when they are spliced into one as text.

## Decision

**The chart passes each part of the connection to the pods from the operator's Secret, and the host assembles the
Npgsql connection from the parts with `NpgsqlConnectionStringBuilder`, so a password stays a password whatever
characters it holds.**

1. **Two forms of one connection.** The `OpenIdDatabase` configuration section (`Host`, `Port`, `Name`, `Username`,
   `Password`) and the finished string `ConnectionStrings:openiddb` are two forms of one connection.
   `HostDatabaseConnection`, which the host and the migration worker both use, resolves the connection from whichever
   form is configured. It refuses both forms at once, a parts set that lacks `Host`, `Name`, `Username` or `Password`,
   and a `Port` outside 1-65535, and its messages name configuration paths and never a configured value. `Port` is
   optional and Npgsql defaults it to 5432.
2. **The chart passes the parts as environment variables.** `database.parts` and `migrations.database.parts` each name
   a Secret (`existingSecret`) and the keys that hold the host, port, database, user name and password. The chart
   renders each part as an `OpenIdDatabase__*` environment variable from a `secretKeyRef`. The keys of
   `database.parts` default to CloudNativePG's (`host`, `port`, `dbname`, `user`, `password`), so a CloudNativePG
   cluster needs only the Secret's name. The keys of `migrations.database.parts` fall back to those of
   `database.parts`.
3. **The chart refuses an unusable release.** For `database` and for `migrations.database`, it refuses both
   `existingSecret` and `parts.existingSecret`, and a parts Secret whose host, database, user name or password key is
   empty. It refuses a `database` that names neither form. The migration Job and the server may use different forms.
4. **A rotation reaches the pods at the next pod start.** A `secretKeyRef` environment variable is read when a
   container starts, so a password the operator rotates reaches the pods when they restart.

## Options considered

- **Kubernetes `$(VAR)` expansion into a finished string.** Rejected: the expansion splices the password into the string
  as text, and a password with `;`, `'` or `"` changes the string's meaning.
- **Npgsql's `PG*` environment defaults.** Rejected: the host reads an empty connection string as "no database", so the
  defaults would reach Npgsql only when something else supplied a string, and nothing would refuse a release that mixes
  the two.
- **A chart-made Secret copy, through `lookup` or a hook.** Rejected: `lookup` returns nothing under `helm template`
  and in GitOps, so the chart would render differently from how it installs, and a copy goes stale when the operator
  rotates the password.
- **The host assembles the connection from the parts.** Chosen: the Secret the operator owns stays the only copy, the
  builder quotes every value, and `HostDatabaseConnection` refuses a mixed or partial configuration at host start as
  the chart does at render time.

## Consequences

- No Secret is hand-built for the database: a deployer names the operator's Secret, and a deployer without an operator
  keeps the finished-string form in `database.existingSecret`.
- TLS settings the connection needs, such as `SSL Mode`, arrive through `extraEnv`.
- The `cluster` job of the `Host chart` workflow creates a Secret of the shape an operator writes and installs the kind
  release with the parts form, so the form is proven against a real database and the migration Job.
- The Aspire AppHost keeps supplying `ConnectionStrings:openiddb`, which the host resolves as the finished-string form.

## References

- Issue [#780](https://github.com/NCodeGroup/identity/issues/780)
- [ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md)
- [ADR-1180](1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md)
- [ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md)
- `charts/ncode-identity-host`
