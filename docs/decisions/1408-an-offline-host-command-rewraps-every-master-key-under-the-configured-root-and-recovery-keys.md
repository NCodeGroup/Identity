# 1408. An offline host command rewraps every master key under the configured root and recovery keys

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** NCode Group

## Context

A master key is wrapped under the root key provider configured when it is created, and again under the recovery
public key ([ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).
Moving to another root key provider or root key, or replacing the recovery key pair, leaves every stored master key
wrapped under the old one. The wraps have to be replaced without changing a master key's material, so no stored secret
has to change. When the root key is lost, the keys have to be recovered from their recovery copies with the recovery
private key, which must never reach a process that serves.

## Decision

The host image carries an offline command, `secret-protection rewrap|rewrap-recovery`, run once as a Kubernetes Job. It
rewraps every master key's wrap without changing its material, so no stored secret changes, and it exits without
serving.

1. **The engine is `IMasterKeyRewrapper`.** `RewrapAsync(MasterKeyRewrapRequest)` takes `RecoveryOnly`, `Owner`,
   `DryRun`, `NonconformingOnly` and `RecoveryPrivateKeyPem`, and returns the counts examined, rewrapped, verified and
   failed. Each row is handled in its own unit of work. The row is unwrapped through the provider it names, or from its
   recovery copy with the recovery private key; its key size and key check are verified; it is then wrapped under the
   row's owner root: the configured deployment root, or the owner's root key and access identity
   ([ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)).
2. **The recovery copy is never removed silently.** The command also wraps under the recovery public key when one is
   configured. Without one it leaves an existing recovery copy as it is. A recovery-only request needs a configured
   recovery public key and fails before any row is read. A conflicting write retries, at most three times; a row that
   vanished counts as examined only. A repeated run is equivalent to the first.
3. **Rows under an owner root key stay out of recovery.** Such a row is wrapped only under that key and never gains a
   recovery copy. A recovery-only run removes such a copy, and with a recovery private key the row unwraps through its
   own wrapper. `NonconformingOnly` limits a run to the rows that do not conform to their owner's root; the owner-root-key
   manager uses the same engine online.
4. **The recovery private key lives only in the command.** `RecoveryKeyUnwrapper` imports a `PRIVATE KEY` PEM of an RSA
   key of at least 3072 bits from the file `--recovery-private-key` names, never from configuration. It derives the
   recovery key id from the public half, decrypts with RSA-OAEP and SHA-256 into sensitive buffers that are zeroed, and
   refuses a row whose recovery copy names another key. It is never registered as an `IKeyWrapper`, so a serving
   process cannot reach it.
5. **The verb is `SecretProtectionCommandLine`.** `secret-protection rewrap` and `secret-protection rewrap-recovery`
   accept `--recovery-private-key <file>`, `--owner <type:id>` and `--dry-run`, which unwraps and verifies every key and
   writes nothing. The host parses the arguments with `SecretProtectionCommandLine.Parse` and builds without them
   (`GetHostArguments`), so the command-line configuration provider never reads the verb.
6. **Exit codes.** The command validates the host's options, then exits `0` on success, `1` on a failed key, an explicit
   owner with no master keys or an invalid configuration, and `2` on a usage error, which it logs with the usage.
7. **The chart runs it as a Job.** `secretProtection.rewrapJob` renders a plain Job, not a hook, named for the release
   revision, with the Deployment's service account, image, security contexts, scheduling, environment and mounts. The
   recovery private key Secret is mounted into the Job only.

## Options considered

- **A management endpoint.** A host that lost its root key cannot start to serve it, and the recovery private key must
  never reach a serving process.
- **Configuration as the key source.** Configuration is long-lived and replicated.
- **A separate tool image.** The host image already carries every root key provider and the schema the rows use.
- **Removing the recovery copy when recovery is disabled.** A recovery path is never removed silently.
- **The offline command (chosen).** It runs where the host runs, reads the key from a file the Job alone mounts, and
  leaves the serving process without the key.

## Consequences

- Changing the root key provider or root key, and replacing the recovery key pair, are one Job each.
- The old provider stays configured for the run unless recovery is used.
- `--dry-run` proves recovery without writing.
- The command needs the database, so it runs where the host runs.

## References

- [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-and-it-is-destroyed-once-unnamed.md),
  [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)
- `src/NCode.Identity.Secrets.Persistence/Protection/DefaultMasterKeyRewrapper.cs`,
  `src/NCode.Identity.Server/SecretProtection/SecretProtectionCommandLine.cs`,
  `charts/ncode-identity-host/templates/secret-protection-job.yaml`
