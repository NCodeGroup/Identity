# Work in progress: #174 and #165

This folder records unfinished work so that it can be picked up again. It is not product documentation. Remove the
folder before this branch is proposed for merge.

## #174, per-tenant root keys (bring your own key)

The plan is [`174-tenant-root-keys-plan.md`](./174-tenant-root-keys-plan.md): decisions D1–D14, ADR-1411 and ADR-1412,
and steps 1–19.

**Done:** steps 1–13, committed on this branch:

- **Steps 1–5:** ADR-1411 and ADR-1412 are written and the records they amend are updated. `IReferencedKeyWrapper` is
  added. AWS KMS, Google Cloud KMS and Azure Key Vault can each wrap under a named key.
- **Steps 6–8:** the owner root key contracts, options and audit events are added. The secret format, options validator
  and factory follow the owner's wrap rule. The ring re-validates keys wrapped under an owner root key and fails closed.
- **Steps 9–10:** rotation, status and the rewrap engine use the owner's root key. `DefaultOwnerRootKeyManager` is added.
- **Steps 11–13:** EF Core gets the owner root key store, the master key column and the re-scaffolded migration.
  OpenId.Core announces changes and re-validates in the background. The server binds `SecretProtection:OwnerRootKeys`.

**Partly done:** step 14, the management API. The commit `wip: management API for owner root keys` holds this step
unfinished. It has not been built or tested.

**Remaining:**

- Finish step 14, the management API.
- Step 15: integration test that brings, revokes, restores and removes a tenant root key.
- Step 16: console.
- Step 17: chart.
- Step 18: docs and changelog.
- Step 19: whole-change check. Build the solution with `-m:2`, which must give 0 warnings. Run the affected test
  projects, `dotnet csharpier check src tests`, the ADR index checks and `./build/dod.ps1 -Fast -Affected`.
- Review the change, fix what the review finds, then open the pull request with `Closes #174`.

The work so far was planned on Opus and executed on Sonnet, one step at a time, with each step's test command from the
plan. Steps 1–13 were committed by the executor, but only after their own step-level tests; the whole-change build has
not been run yet.

## #165, remote (KMS-resident) signing keys

The plan is [`165-remote-signing-keys-plan.md`](./165-remote-signing-keys-plan.md). It reserves ADR-1413 and ADR-1414.
No code is written yet, and the work belongs on its own branch from `main`.
