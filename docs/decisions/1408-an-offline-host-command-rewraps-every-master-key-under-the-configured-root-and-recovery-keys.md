# 1408. An offline host command rewraps every master key under the configured root and recovery keys

- **Status:** Accepted (amended by
  [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md): a row under an owner root key is wrapped only under that key and never gains a recovery copy)
- **Date:** 2026-11-04
- **Deciders:** NCode Group
- **Amends:** [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md), decision 2's
  offline recovery copy (the recovery private key unwraps master keys only inside the offline command);
  [ADR-1279](1279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md), decision 6's
  recovery wrapper (it still only wraps; an unwrapper exists only inside the command, is built from a key file the Job
  mounts, and is never registered as an `IKeyWrapper`)

## Context

A master key is wrapped under the root key provider configured when it is created, and again under the recovery
public key ([ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)). Moving to
another root key provider or root key, or replacing the recovery key pair, leaves every stored master key wrapped under
the old one. The wraps have to be replaced without changing a master key's material, so no stored secret has to change,
and when the root key is lost the keys have to be recovered from their recovery copies with the recovery private key,
which must never reach a process that serves.

## Decision

The host image carries an offline command, `secret-protection rewrap|rewrap-recovery`, run once as a Kubernetes Job. It
rewraps every master key and exits without starting the server.

1. **The engine is `IMasterKeyRewrapper`.** `RewrapAsync(MasterKeyRewrapRequest)` returns the counts examined, rewrapped,
   verified and failed. Each row is handled in its own unit of work: the root key is unwrapped by the provider the row
   names, or from its recovery copy with the recovery private key; the key size and key check are verified; the row is then
   wrapped under the configured root key provider (unless the request is recovery-only) and under the recovery public key
   when one is configured, and updated with its token checked. A conflicting write reads the row again and retries, at most
   three times, and a row that vanished, whether its update conflicts or finds it gone, counts as examined only. When no recovery public key is configured the existing
   recovery copy is left as it is, because a recovery path is never removed silently. A recovery-only request needs a
   configured recovery public key and fails before any row is read. A repeated run rewraps again to an equivalent row.
2. **The recovery private key lives only in the command.** `RecoveryKeyUnwrapper` imports a `PRIVATE KEY` PEM of an RSA
   key of at least 3072 bits, computes the recovery key id from its public half, and decrypts with RSA-OAEP and SHA-256
   into a sensitive buffer that is zeroed. A row whose recovery copy names another key fails. The key is read from the
   file `--recovery-private-key` names, never from configuration, into sensitive buffers that are zeroed when the command
   ends.
3. **The verb is `SecretProtectionCommandLine`.** `secret-protection rewrap` and `secret-protection rewrap-recovery`
   accept `--recovery-private-key <file>`, `--owner <type:id>` and `--dry-run`. The host parses the arguments with
   `SecretProtectionCommandLine.Parse` and builds without them when they select the command (`GetHostArguments`), so the
   command-line configuration provider never reads the verb. The command validates the same options the host validates
   when it starts, returns `0` when no key failed and `1` when one failed, when an explicit `--owner` has no master keys
   or when the configuration is invalid, and logs a usage error with the usage and returns `2`. `--dry-run` unwraps and
   verifies every key and writes nothing.
4. **The chart runs it as a Job.** `secretProtection.rewrapJob` renders a plain Job, not a hook, named for the release
   revision, with the host's service account, image, security contexts, scheduling, environment and mounts. A recovery
   private key Secret is mounted into the Job only. The host Deployment and the Job share the environment, volume mount and
   volume definitions.

## Options considered

- **A management endpoint.** A host that lost its root key cannot start to serve it, and a recovery private key must never
  reach a long-running process.
- **Reading the key from configuration.** Configuration is long-lived and replicated.
- **A separate tool image.** The host image already carries every root key provider and the schema the rows use.
- **Removing the recovery copy when recovery is disabled.** A recovery path is never removed silently.

## Consequences

- Changing the root key provider or root key, and replacing the recovery key pair, are one Job each.
- The old provider stays registered and configured for the run unless the recovery key is used.
- `--dry-run` proves recovery without writing.
- The command needs the database, so it runs where the host runs.

## References

- [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md)
- `src/NCode.Identity.Secrets.Persistence/Protection/DefaultMasterKeyRewrapper.cs`,
  `src/NCode.Identity.Server/SecretProtection/SecretProtectionCommandLine.cs`,
  `charts/ncode-identity-host/templates/secret-protection-job.yaml`
- Issue [#172](https://github.com/NCodeGroup/identity/issues/172)
