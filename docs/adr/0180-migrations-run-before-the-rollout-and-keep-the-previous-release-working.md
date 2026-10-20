# 180. Migrations run before the rollout and keep the previous release working

- **Status:** Accepted (expand-and-contract checked by
  [ADR-0184](0184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md))
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

The host never migrates on boot outside Development
([ADR-0041](0041-host-owned-ef-migrations-deliberate-outside-development.md)). Schema changes are a deliberate step,
and replicas starting at once must not race to apply them. Locally, the Aspire AppHost runs the migration worker
`NCode.Identity.OpenId.Host.MigrationService` before the host
([ADR-0073](0073-postgres-dev-database-and-aspire-migration-worker.md)). A Kubernetes deployment through the host's
chart ([ADR-0179](0179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)) needs the
same step. During a rolling update, the old and new versions run at the same time.

## Decision

1. **The host image carries the migration worker.** The image publishes the worker to `/app/migrations` next to the
   host ([ADR-0178](0178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md)). The schema a
   release migrates to is always the schema that release's host reads. One image is built, signed, and published, and
   a separate migration image could never fall out of step with the host.
2. **The chart runs the worker as a `pre-install` and `pre-upgrade` Job.** Helm waits for the Job before it changes
   anything else. A failed migration fails the release while the previous version still serves, and no pod is
   replaced. The Job runs once per release, with `backoffLimit` 1 and a 10-minute deadline by default. A failed Job
   stays for its logs until the next release replaces it.
3. **The Job runs as restricted as the server.** It runs as the image's non-root user with a read-only root
   filesystem, no capabilities, and no service-account token. Its pod is labeled `app.kubernetes.io/component:
   migrations` and the server's pods `server`, so neither the Service nor the PodDisruptionBudget selects the Job.
4. **The migration connection can differ from the runtime connection.** `migrations.database.existingSecret` names a
   connection with schema rights and defaults to the host's database Secret. A deployment that separates them runs the
   host without the right to change its own schema.
5. **Every migration keeps the previous release working: expand, then contract.** The previous release serves while
   the Job runs and until the rollout replaces its last pod. A Helm rollback returns to it without running a
   migration, because no hook runs on rollback and EF Core cannot safely reverse every migration. So:
   - a release only expands the schema: it adds tables, nullable or defaulted columns, and indexes;
   - a later release contracts it, once no running version reads the old shape;
   - a rename is an add, a copy, and a later drop, across at least two releases.

   The host's README states the rule for every migration author.
6. **The chart can leave migrations to someone else.** `migrations.enabled: false` removes the Job for a deployment
   whose DBA or pipeline applies a generated idempotent script.

## Consequences

- An install or upgrade takes as long as its migrations, and Helm's `--timeout` must cover them. A long data migration
  belongs in code that runs after the rollout, not in the Job.
- The image grows by the worker's few assemblies that the host does not already ship.
- A unit test fails every migration that drops, renames, or tightens schema, unless the migration is marked a
  deliberate contract step ([ADR-0184](0184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md)).
  Raw SQL in a migration stays a review item.
- On a first install, the Job uses the namespace's `default` service account, because the chart's own account does not
  exist before the hook runs. It never mounts a token.
