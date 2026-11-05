# Plan: feat: wrap a tenant's master keys under a root key the tenant brings

Issue: #174

## Goal

A tenant names a root key it controls (a key wrapper name such as `aws-kms`, `google-cloud-kms` or `azure-key-vault`,
plus a key reference such as a KMS key ARN). The server validates it by wrapping and unwrapping a probe through that
key, wraps every master key of the tenant under it with no operator recovery copy, rotates the tenant onto fresh master
key material created under it, and re-validates it every hour by default. A revoked, disabled or destroyed tenant key
makes the tenant's data unreadable everywhere (backups included, for every value written or rewrapped under it), and the
server fails closed with an operator-visible state (`unavailable`), an audit event, a log, a `503` on the management
API and a console banner. The management API and console report the reference and its last validation, never material.

## Context for the executor

Work only in `/home/user/Identity/.claude/worktrees/pr-v` (branch `claude/tenant-root-keys`). Every command runs from
that folder. Resource rules: `export MSBUILDDISABLENODEREUSE=1` in every shell before `dotnet`; build with `-m:2`; run
only the test project a step names (filtered); never `./build/dod.ps1`; never two dotnet/pnpm commands at once; no
background jobs; run `dotnet build-server shutdown` when finished. An untracked `Directory.Build.rsp` at
`/home/user/Identity` pins Roslyn; never stage `.rsp` files or `PLAN.md`.

Read first: `AGENTS.md`, `.github/instructions/repository.instructions.md` (standing direction: describe the product as
it is, no temporal words such as "now", "no longer", "previously", "was", no migration/compat/rollback notes anywhere,
including commit messages), `.github/instructions/csharp.instructions.md`, `csharp-production.instructions.md`,
`csharp-tests.instructions.md`, `csharp-testing.instructions.md`, `docs/decisions/README.md`.

Mechanics that every step follows:

- One type per file, license header `// Copyright © NCode Group` + `// SPDX-License-Identifier: Apache-2.0`.
- No ADR citation inside `///` doc comments; put `// See ADR-1411.` (or 1412) on the line under the doc comment, as
  `DefaultMasterKeyManager.cs` does. Check with `pwsh -NoProfile -File build/doc-comment-citations.ps1`.
- No null-forgiving operator outside tests. `[PublicAPI]` on public types. Keep every touched project's
  `PublicAPI.Unshipped.txt` exact (add every new public member line; change lines whose signature changes; the build
  fails on drift with RS0016/RS0017).
- Logging only through `[LoggerMessage]` methods in the package's `Logging/Log.cs` with a named constant in
  `Logging/EventIds.cs` (`Base + n`, next free number; `NCode.Identity.Secrets.Persistence` is band 7000, its last is
  `Base + 15`; `NCode.Identity.OpenId.Core` band 1000, last `Base + 32`; `NCode.Identity.OpenId.Management` band 3000,
  read its file for the last number).
- Options: strongly typed, bound strictly, validated at start (`IValidateOptions<SecretProtectionOptions>`), messages
  name the full configuration key (copy `src/NCode.Identity.Secrets.Persistence/Options/MasterKeyOptionsValidator.cs`).
  A period handed to a timer is checked with `NCode.Time.TimerPeriod.IsValid`.
- Effects after a commit (observers, audit) run on their own bounded token (`PostCommitWork.RunAsync` in OpenId.Core,
  `NotifyAsync` with `ObserverTimeout` in `DefaultMasterKeyManager`), never the caller's.
- Format: `dotnet csharpier format <changed dirs>`; before every commit `dotnet csharpier check src tests` must report no
  unformatted file.
- Tests: xUnit v2, Moq strict (`MockRepository(MockBehavior.Strict)`, `.Verifiable()`), names
  `Method_Condition_Result`, `FakeTimeProvider`. Existing tests whose strict mocks meet a new store call
  (`GetStore<IOwnerRootKeyStore>()`, `FindAsync`) are updated in the same step.
- Done-when commands: `dotnet test tests/<Project>/<Project>.csproj -m:2 --filter "FullyQualifiedName~<Class>" -- RunConfiguration.TreatNoTestsAsError=true`.
- Commits: conventional subjects without `!`, no `Co-authored-by`, `Claude-Session` or "Generated with" lines. Before
  the first commit run `git config core.hooksPath`; when it prints nothing and no own hooks exist, run
  `git config core.hooksPath .githooks`. Stage only the files of the step.

The seams this work builds on (read each before changing it):

- Key wrapping contract: `src/NCode.Identity.Secrets.Abstractions/KeyWrapping/IKeyWrapper.cs`, `WrappedKey.cs`,
  `KeyWrapperNames.cs`, `WrappedKeyCiphertext.cs` (ADR-1279).
- KMS adapters: `src/NCode.Identity.Secrets.KeyWrapping.AwsKms/AwsKmsKeyWrapper.cs` (+ `AwsKmsKeyArn.cs`),
  `src/NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms/GoogleCloudKmsKeyWrapper.cs` (+ `GoogleCloudKmsKeyName.cs`),
  `src/NCode.Identity.Secrets.KeyWrapping.AzureKeyVault/AzureKeyVaultKeyWrapper.cs` (+ `AzureKeyVaultKeyId.cs`). Each
  wraps under the key in its options and unwraps under `WrappedKey.KeyId`, which already names the exact key (ARN,
  crypto key version, versioned key id) independent of configuration (ADR-1260).
- Key hierarchy: `src/NCode.Identity.Secrets.Persistence/Protection/MasterKeyFactory.cs`, `DefaultKeyWrapperCatalog.cs`,
  `IKeyWrapperCatalog.cs`, `DefaultMasterKeyRing.cs`, `MasterKeyRingEntry.cs`, `DefaultMasterKeyManager.cs`,
  `DefaultMasterKeyRewrapper.cs`, `SecretProtectionFormat.cs`, `RecoveryKeyWrapper.cs`, `DefaultRegistration.cs`
  (ADR-1235, ADR-1407, ADR-1408).
- Contracts: `src/NCode.Identity.Secrets.Persistence.Abstractions/` (`DataContracts/PersistedMasterKey.cs`,
  `Protection/IMasterKeyStore.cs`, `IMasterKeyManager.cs`, `MasterKeyInfo.cs`, `MasterKeyStatus.cs`,
  `MasterKeyRewrapRequest.cs`, `IMasterKeyRewrapper.cs`, `IMasterKeyChangeObserver.cs`, `Options/*.cs`,
  `Auditing/*.cs`).
- OpenId.Core background work: `src/NCode.Identity.OpenId.Core/SecretProtection/MasterKeyMaintenanceService.cs`
  (lease, paging, audit after commit), `MasterKeyChangeAnnouncer.cs`, `DefaultRegistration.cs`;
  `src/NCode.Identity.OpenId.Core/Tenants/Keys/TenantKeyRolloverService.cs` (sends `TenantChangedCommand` from a
  scoped mediator after a commit).
- EF Core: `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Entities/MasterKeyEntity.cs`,
  `Stores/MasterKeyStore.cs`, `Stores/CoreDbContextExtensions.cs`, `CoreModelContributor.cs`, `DefaultRegistration.cs`,
  `Stores/TenantStore.cs` (tenant delete removes its master keys around line 637); host migrations in
  `src/NCode.Identity.OpenId.Host.Database/Migrations/OpenId/`.
- Server: `src/NCode.Identity.Server/SecretProtection/SecretProtectionRegistration.cs` (strict binding and the set of
  allowed children), `DevelopmentEnvironment/DeveloperSecretPruningSeedHandler.cs` (prunes on `CryptographicException`).
- Management: `src/NCode.Identity.OpenId.Management/Endpoints/EncryptionKeys/EncryptionKeyEndpoints.cs`,
  `Endpoints/Tenants/TenantApiEndpointHandler.cs` (encryption key handlers near line 1760),
  `Endpoints/Servers/ServerApiEndpointHandler.cs`, `Endpoints/ManagementRootGroup.cs`,
  `Endpoints/UnreadableSettingsEndpointFilter.cs` (filter pattern), `Auditing/DefaultManagementAuditRecorder.cs`,
  `src/NCode.Identity.OpenId.Management.Abstractions/Contracts/EncryptionKeys/*.cs`,
  `src/NCode.Identity.OpenId.Management.Abstractions/Auditing/IManagementAuditRecorder.cs`.
- Frontend: `src/frontend/packages/api/src/contracts.ts`, `src/frontend/packages/store/src/managementApi.ts` and
  `index.ts`, `src/frontend/packages/ui/src/components/EncryptionKeyTable.tsx` (+ test, stories) and `index.ts`,
  `src/frontend/apps/mgmt/src/containers/OwnerEncryptionKeysTab.tsx` (+ test).
- Chart: `charts/ncode-identity-host/values.yaml`, `values.schema.json`, `templates/_helpers.tpl` (credential
  conditions in `googleCloudCredentialConfiguration`, `hostEnv`, `hostVolumeMounts`, `hostVolumes`), `README.md`,
  `ci/*.yaml`.
- Docs: `docs/capabilities/encryption-at-rest.md`, `docs/audit-records.md`, `CHANGELOG.md` (`## [Unreleased]`, groups
  `### Added` and `### Security` only), `docs/decisions/README.md`.

ADR numbers reserved for this work: 1411 and 1412 (1409 and 1410 belong to an open pull request and are not in this
branch's index; add 1411 and 1412 after 1408 in number order).

## Decisions

### D1. Where the tenant's root key lives: a per-owner binding record, not a settings-cascade setting

The tenant's root key is an **owner root key**: one `PersistedOwnerRootKey` row per secret owner, stored beside the
master keys in the secrets persistence layer (table `OwnerRootKeys`), set and removed through `IOwnerRootKeyManager`
and the management endpoints `PUT`/`DELETE /api/tenants/{tenantId}/encryption-keys/root-key`. It is not a setting of the
settings cascade. Reasons: the settings cascade validates values synchronously and value by value, while a root key is
accepted only after an asynchronous round trip through its provider; changing it is an operation (rotate the master key,
rewrap every master key row) and not a value merge; a cascade or a security profile must never move a tenant onto a key
(a server-scope value would put every tenant under one customer's key); the record carries operational state (last
validation, failure, `unavailable`) that does not belong in a settings document; and the secrets layer that wraps master
keys must not depend on the OpenID settings model. The vocabulary is open (ADR-1150) in two places: the provider is any
registered `IKeyWrapper` that also implements `IReferencedKeyWrapper` (D2), and the owner is any `SecretOwner` except
the `server` owner type, whose master keys protect the deployment's own Data Protection ring and are always wrapped by
the deployment root. The code says "owner root key"; the management API, console and docs say "tenant root key".

### D2. A key wrapper that wraps under a key the caller names (ADR-1412)

New public interface in `NCode.Identity.Secrets.Abstractions`, folder `KeyWrapping/`, namespace
`NCode.Identity.Secrets.KeyWrapping`:

```csharp
public interface IReferencedKeyWrapper : IKeyWrapper
{
    bool TryValidateKeyReference(string keyReference, [NotNullWhen(false)] out string? failure);

    ValueTask<WrappedKey> WrapAsync(
        string keyReference,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    );
}
```

- `TryValidateKeyReference` checks only the syntax; `failure` is a phrase that follows the field name ("must be the ARN
  of a KMS key or alias, such as arn:aws:kms:us-west-2:111122223333:alias/tenant-root").
- `WrapAsync(keyReference, …)` throws `ArgumentException` (param `keyReference`) for a reference that fails
  `TryValidateKeyReference`, wraps under the named key with the same binding, checks and error mapping as the configured
  wrap, and reports in `WrappedKey.KeyId` the identifier that `IKeyWrapper.UnwrapAsync` needs to address the key
  without configuration. Unwrapping goes through the existing `UnwrapAsync`; no new unwrap member.
- Implemented by `AwsKmsKeyWrapper` (reference: key or alias ARN, `AwsKmsKeyArn.IsValid`; region from the ARN;
  `KeyId` is the reported key ARN), `GoogleCloudKmsKeyWrapper` (reference: crypto key name without version,
  `GoogleCloudKmsKeyName.IsCryptoKeyName`; the reported version must be a version of the referenced crypto key, else
  `IOException`), `AzureKeyVaultKeyWrapper` (reference: key id with or without version, `AzureKeyVaultKeyId.IsKeyId`;
  the reported key version must be a version of the referenced key: equal to the reference when it carries a version,
  else its versionless prefix equals the reference compared `OrdinalIgnoreCase`; else `InvalidOperationException`; the
  3072-bit floor applies). Each refactors its configured `WrapAsync(key, context, ct)` into a private
  `WrapUnderAsync(string keyReference, …)` that both public members call, so the configured and the referenced wrap
  share one code path. Error messages that say "configured crypto key" say "named crypto key".
- `EphemeralKeyWrapper`, `LocalKeyWrapper` and `RecoveryKeyWrapper` do not implement it: their key is a process secret or
  a deployment secret, never a key a third party controls.
- `IKeyWrapperCatalog` gains `bool TryGetReferenced(string name, [NotNullWhen(true)] out IReferencedKeyWrapper? wrapper)`
  and `IReferencedKeyWrapper GetReferenced(string name)` (throws `InvalidOperationException` naming the wrapper and
  listing the registered referenced wrappers when it is not registered or does not implement the interface).
- No new registration: the adapters stay registered as `IKeyWrapper`, and the catalog tests `is IReferencedKeyWrapper`.

### D3. Configuration: `SecretProtection:OwnerRootKeys`

New public options class `NCode.Identity.Secrets.Persistence.Options.OwnerRootKeyOptions`, property
`SecretProtectionOptions.OwnerRootKeys { get; set; } = new();`:

- `string[] Providers { get; set; } = [];` — the key wrapper names a tenant may choose. Empty (the default) means no
  tenant can bring a key; `PUT` answers `400`. The operator opts in, because the server calls the tenant's KMS with the
  deployment's own workload identity.
- `TimeSpan ValidationInterval { get; set; } = TimeSpan.FromHours(1);` — how often an `active` owner root key is probed,
  and how long a replica reuses master key material unwrapped under an owner root key before it unwraps it again.
  Between one minute and one day, and a period a timer accepts.
- `TimeSpan RetryInterval { get; set; } = TimeSpan.FromMinutes(1);` — how often an `unavailable` or `removing` owner root
  key is retried, how long a replica refuses an owner whose root key failed before it tries again, and the tick of the
  validation service. Between one second and `ValidationInterval`, and a period a timer accepts.

Validator `OwnerRootKeyOptionsValidator` (internal sealed, `IValidateOptions<SecretProtectionOptions>`, in
`src/NCode.Identity.Secrets.Persistence/Options/`, constructor takes `IEnumerable<IKeyWrapper>`): every provider is
non-blank, appears once (ordinal), is registered and implements `IReferencedKeyWrapper` (message names
`SecretProtection:OwnerRootKeys:Providers`, the offending name and the registered referenced wrapper names in ordinal
order); intervals as above, each message naming the full key. Registered with `TryAddEnumerable` beside
`MasterKeyOptionsValidator` in `AddPersistenceLibrary`. `SecretProtectionRegistration` (Server) binds the
`OwnerRootKeys` child strictly and adds it to `SecretProtectionChildren`. The allowed list governs `PUT` only: an owner
already bound to a provider that leaves the list keeps working, because its data must stay readable.

### D4. Data model

`PersistedOwnerRootKey` (public sealed, `ISupportConcurrencyToken`, `[ExcludeFromCodeCoverage]`, in
`Secrets.Persistence.Abstractions/DataContracts/`):

| Member | Type | Meaning |
| --- | --- | --- |
| `OwnerType` | `required string { get; init; }` | owner kind |
| `OwnerId` | `required string { get; init; }`, `[MaxLength(MaxLengths.ResourceId)]` | owner id |
| `KeyWrapperName` | `required string { get; set; }` | the referenced key wrapper, such as `aws-kms` |
| `KeyReference` | `required string { get; set; }` | the reference the tenant gave |
| `State` | `required string { get; set; }` | one of `OwnerRootKeyStates` |
| `CreatedWhen` | `required DateTimeOffset { get; set; }` | when this key reference was set (reset when the reference changes) |
| `StateChangedWhen` | `required DateTimeOffset { get; set; }` | when `State` last changed |
| `ValidatedWhen` | `DateTimeOffset? { get; set; }` | last successful probe |
| `FailedWhen` | `DateTimeOffset? { get; set; }` | last failed probe or failed completion |
| `Failure` | `string? { get; set; }` | description of the last failure, at most 1024 characters, never material; cleared on success |
| `ConcurrencyToken` | `string { get; set; } = string.Empty;` | |
| `Owner` | `SecretOwner` (computed) | |

`OwnerRootKeyStates` (public static, `Protection/`): `Active = "active"` (the key wraps every master key of the owner and
new ones), `Unavailable = "unavailable"` (the last probe failed: the owner fails closed), `Removing = "removing"` (new
master keys are wrapped under the deployment root while the rows move back under it). A state the code does not know is
treated as `unavailable` (fail closed). `OwnerRootKeyInfo` (public sealed record, no material) mirrors the row:
`(SecretOwner Owner, string KeyWrapperName, string KeyReference, string State, DateTimeOffset CreatedWhen,
DateTimeOffset StateChangedWhen, DateTimeOffset? ValidatedWhen, DateTimeOffset? FailedWhen, string? Failure)` with
`static OwnerRootKeyInfo From(PersistedOwnerRootKey)`.

`PersistedMasterKey` gains `string? RootKeyReference { get; set; }`: the owner root key reference that `RootWrappedKey` is
wrapped under, `null` when the deployment root wraps it. `MasterKeyInfo` gains a last positional parameter
`string? RootKeyReference` (mapped in `From`). `MasterKeyStatus` gains a last positional parameter
`OwnerRootKeyInfo? RootKey`.

`IOwnerRootKeyStore : IStore<PersistedOwnerRootKey>` (public, `Protection/`), owner matched case-insensitively:
`ValueTask<PersistedOwnerRootKey?> FindAsync(SecretOwner owner, CancellationToken)`;
`ValueTask<PagedResult<PersistedOwnerRootKey>> GetDuePageAsync(DateTimeOffset validatedBefore, DateTimeOffset retryBefore, PageRequest page, CancellationToken)`
(rows where `State == active` and `ValidatedWhen` is null or before `validatedBefore`, or `State` is `unavailable` or
`removing` and `FailedWhen` is null or before `retryBefore`; keyset order by row id);
`ValueTask<bool> RemoveAsync(PersistedOwnerRootKey item, CancellationToken)` (concurrency checked, `false` when gone).
`UpdateAsync` throws `StoreConcurrencyException` on a token mismatch and `InvalidOperationException` when the row is gone,
as `MasterKeyStore.UpdateAsync` does.

EF: `OwnerRootKeyEntity` (public sealed, `Entities/`, table `OwnerRootKeys`): `Id` (`long`, `[Key]`, `[UseIdGenerator]`),
`OwnerType` (`[Unicode(false)]`, max 50), `OwnerId`, `NormalizedOwnerId` (max `MaxLengths.ResourceId`),
`KeyWrapperName` (max 100), `KeyReference` (max 2048), `State` (max 50), `CreatedWhen`, `StateChangedWhen`,
`ValidatedWhen?`, `FailedWhen?`, `Failure` (max 1024, nullable), `ConcurrencyToken` (`[ConcurrencyCheck]`); unique index
on (`OwnerType`, `NormalizedOwnerId`). `MasterKeyEntity` gains `RootKeyReference` (`required string?`, `[Unicode(false)]`,
max 2048). `TenantStore` removes the tenant's `OwnerRootKeys` row with its master keys. The OpenId host context's single
`InitialCreate` is re-scaffolded (ADR-1394).

### D5. How a master key is wrapped under an owner root key

- `MasterKeyFactory.CreateAsync(SecretOwner owner, int version, PersistedOwnerRootKey? rootKey, CancellationToken)`:
  when `rootKey.State` is `active`, the material is wrapped by `Catalog.GetReferenced(rootKey.KeyWrapperName)
  .WrapAsync(rootKey.KeyReference, material, rootContext, ct)`, the row gets `RootKeyReference = rootKey.KeyReference`
  and **no recovery copy** (`RecoveryKeyId`/`RecoveryWrappedKey` null) whatever the recovery options say. When it is
  `unavailable` or an unknown state, `OwnerRootKeyUnavailableException` is thrown before any wrap. When it is `removing`
  or null, the deployment root (`Catalog.GetConfigured()`) wraps it and the recovery copy follows the recovery options.
- `MasterKeyFactory.WrapAsync(MasterKey masterKey, PersistedOwnerRootKey? rootKey, CancellationToken)` returns an
  internal `readonly record struct MasterKeyWraps(WrappedKey Root, string? RootKeyReference, WrappedKey? Recovery, bool ClearsRecovery)`
  with the same rule; it replaces `WrapUnderRootAsync` and `WrapUnderRecoveryAsync`. `ClearsRecovery` is true for an
  owner root key.
- `UnwrapAsync(row)` is unchanged: `Catalog.Get(row.RootKeyProvider).UnwrapAsync` addresses the key by the stored
  `RootKeyId`, so a row under a tenant key unwraps through the same path.
- The root context of an owner-root-keyed row is the same `SecretProtectionFormat.WriteRootContext` binding (key id +
  owner), so a tenant key never unwraps another owner's rows.
- Invariant, enforced by every writer: **every master key row of an owner whose root key is `active` or `unavailable`
  has `RootKeyProvider == KeyWrapperName`, `RootKeyReference == KeyReference` and no recovery copy**. A row "conforms"
  to its owner's root when this holds, or, for an owner with no owner root key or one that is `removing`, when its
  `RootKeyReference` is null.

### D6. Setting, changing and removing an owner root key (through the ADR-1407/1408 machinery)

`IOwnerRootKeyManager` (public, `Secrets.Persistence.Abstractions/Protection/`), implemented by internal sealed
`DefaultOwnerRootKeyManager` in `Secrets.Persistence/Protection/`, registered `TryAddSingleton`:

- `IReadOnlyList<string> AllowedKeyWrapperNames { get; }` — `OwnerRootKeys.Providers` in ordinal order.
- `bool TryValidateKeyReference(string keyWrapperName, string keyReference, [NotNullWhen(false)] out string? failure)` —
  allowed, registered and referenced-capable, then the wrapper's syntax check.
- `ValueTask<OwnerRootKeyInfo?> GetAsync(SecretOwner owner, CancellationToken)`.
- `ValueTask<OwnerRootKeyChange> SetAsync(SecretOwner owner, string keyWrapperName, string keyReference, CancellationToken)`.
- `ValueTask<OwnerRootKeyRemoval> RemoveAsync(SecretOwner owner, CancellationToken)`.
- `ValueTask<OwnerRootKeyValidation> ValidateAsync(SecretOwner owner, CancellationToken)`.

Result records (public sealed, `Protection/`): `OwnerRootKeyChange(OwnerRootKeyInfo RootKey, string? Change,
MasterKeyRotation? Rotation, MasterKeyRewrapResult Rewrap)`; `OwnerRootKeyRemoval(OwnerRootKeyInfo? Remaining, bool
Removed, MasterKeyRewrapResult Rewrap)` (`Remaining` null once the record is deleted; `Removed` true when this call moved
the key to `removing`); `OwnerRootKeyValidation(OwnerRootKeyInfo? RootKey, string? Change, MasterKeyRotation? Rotation,
MasterKeyRewrapResult Rewrap)`. `Change` is one of `RootKeyChanges` (D9) or null.

Every method refuses an owner whose `OwnerType` is `server` (ordinal ignore case) with `ArgumentException`
("the server's master keys are wrapped by the deployment root key"). Owners are normalized.

**SetAsync(owner, name, reference):**

1. `TryValidateKeyReference` fails → `ArgumentException(failure, nameof(keyReference))` (management pre-validates and
   answers `400`).
2. Read the owner's binding and master key rows in one store manager (`IOwnerRootKeyStore.FindAsync`,
   `IMasterKeyStore.ListAsync`). `sameKey` = binding exists, its state is not `removing`, and name and reference are equal
   (ordinal).
3. When not `sameKey` and the owner has an active row, check rotation spacing first with the internal static helper
   `MasterKeyRotationSpacing.GetRemaining(rows, refreshInterval, now)` (extracted from `DefaultMasterKeyManager.RotateAsync`,
   which uses it too): a remaining delay throws `MasterKeyRotationTooSoonException` before anything is written.
4. Probe (D7) the named key. A failure: when `sameKey`, record it as `ValidateAsync` would (state `unavailable`,
   `FailedWhen`, `Failure`, observers) and throw `OwnerRootKeyProbeException`; otherwise throw
   `OwnerRootKeyProbeException` and write nothing.
5. Write the binding (add when absent; update with the token when present): `State = active`, name, reference,
   `CreatedWhen` = now unless `sameKey`, `StateChangedWhen` = now when the state changes, `ValidatedWhen` = now,
   `Failure = null`. A failed add re-reads in a new store manager: a binding that another writer stored first →
   `StoreConcurrencyException` (412). `Change` = `RootKeyChanges.Set` when not `sameKey`, `RootKeyChanges.Restored`
   when `sameKey` and the state went from `unavailable` to `active`, else null.
6. Evict the owner from `ISecretProtectorProvider` and call every `IOwnerRootKeyChangeObserver.ChangedAsync(owner)` on
   a bounded token (30 s, failures logged and swallowed).
7. Complete (shared with `ValidateAsync`): when the owner has no active row, or its active row's `CreatedWhen` is before
   the binding's `CreatedWhen`, call `IMasterKeyManager.RotateAsync(owner)` (D5 makes the successor wrap under the
   tenant key with no recovery copy). `MasterKeyRotationTooSoonException` and `StoreConcurrencyException` from this
   rotation are logged (`OwnerRootKeyRotationDeferred`) and leave `Rotation` null: the validation service completes it.
   Then conform every row: `IMasterKeyRewrapper.RewrapAsync(new MasterKeyRewrapRequest { Owner = owner,
   NonconformingOnly = true })` (D8), which rewraps every retired row under the tenant key and drops its recovery copy.
   The data keys under retired master keys move to the new active key through the ADR-1407 rewrap job the rotation's
   observer dispatches and the daily pass; the retired master keys are destroyed after `RetirementGrace` once nothing
   names them.

Why rotate: the earlier master key material exists in backups wrapped by the deployment root and its recovery copy, so
only material created under the tenant key gives the tenant sole control of every value written or rewrapped afterwards.

**RemoveAsync(owner):** no binding → `OwnerRootKeyRemoval(null, false, empty)`. Otherwise set `State = removing`
(`StateChangedWhen` = now, `Removed = true` when it was not `removing`), save, evict and notify; conform
(`NonconformingOnly`, which moves every row under the deployment root, with a recovery copy when one is configured,
unwrapping each through the tenant key); when `Rewrap.Failed == 0`, delete the binding (`RemoveAsync`, token checked),
evict and notify, and return `Remaining = null`; otherwise record `FailedWhen`/`Failure` ("n master keys could not be
unwrapped by the owner root key") and return the `removing` info. Removal does not rotate: the material was only ever
under the stronger root.

**ValidateAsync(owner):** no binding → all-null result. `removing` → conform and delete as `RemoveAsync` does (no
probe). Otherwise probe: success sets `ValidatedWhen`, clears `Failure`, and moves `unavailable` → `active`
(`Change = Restored`); failure sets `FailedWhen` and `Failure` and moves `active` → `unavailable`
(`Change = Unavailable`). A token conflict on save re-reads the binding and returns it with no change. A state change
evicts and notifies. When the state is `active` afterwards, complete (step 7 of `SetAsync`).

### D7. Validation by the provider: a wrap-and-unwrap probe

Internal sealed `OwnerRootKeyProbe` (`Secrets.Persistence/Protection/`), method
`ValueTask<string?> ProbeAsync(IReferencedKeyWrapper wrapper, string keyReference, SecretOwner owner, CancellationToken)`
returns null on success or the failure description. It draws 32 random bytes into a sensitive rental, writes a probe
context with new `SecretProtectionFormat.GetProbeContextSize(SecretOwner)` / `WriteProbeContext(Guid probeId,
SecretOwner owner, Span<byte>)` under the new label `"ncode-identity/secret-protection/v1/root-probe"u8` (same layout as
the root context, so a probe ciphertext can never be presented as a master key), wraps under the reference, unwraps the
result through `UnwrapAsync` into a sensitive pooled writer, compares with `CryptographicOperations.FixedTimeEquals`, and
zeroes both. It proves the key exists, is enabled, and that the deployment's identity holds both the encrypt and the
decrypt permission, through any provider, without a provider-specific "describe key" call. Every exception except an
`OperationCanceledException` for a cancelled token is a failure: `$"{exception.GetType().Name}: {exception.Message}"`,
truncated to 1024 characters; a mismatch is "the key unwrapped a value other than the one it wrapped". Any failure,
transient or definitive, fails closed: an outage of the tenant's KMS already stops a replica that has to unwrap, and an
`unavailable` key is retried every `RetryInterval` and restored at its first success.

### D8. The rewrap engine wraps each row under its owner's root (ADR-1408 amended)

`MasterKeyRewrapRequest` gains `bool NonconformingOnly { get; init; }`. `DefaultMasterKeyRewrapper` reads each owner's
binding once per run (dictionary keyed by the normalized owner, through `IOwnerRootKeyStore.FindAsync`); the target is
the binding when its state is `active`, `unavailable` or unknown, and the deployment root otherwise (none or
`removing`). Per row:

- A row that conforms to an owner root key target is never rewritten: neither the deployment root nor the recovery key
  wraps it (examined only; a dry run still unwraps and verifies it through the tenant key).
- `NonconformingOnly` and the row conforms → examined only, nothing unwrapped.
- Unwrap: with a recovery private key, a row whose `RootKeyReference` is null unwraps from its recovery copy; a row whose
  `RootKeyReference` is set has no recovery copy and unwraps through its own wrapper (the tenant key).
- `RecoveryOnly` with an owner root key target: a row that carries a recovery copy has it removed (rewrapped), otherwise
  examined only; root wraps are not touched.
- Wraps come from `MasterKeyFactory.WrapAsync(masterKey, target)`: root provider, root key id, root wrapped key and
  `RootKeyReference` replaced; recovery copy replaced when the deployment root is the target and recovery is configured,
  cleared when an owner root key is the target, left as it is otherwise (ADR-1408's "never removed silently" holds for
  deployment-rooted rows).
- The `IMasterKeyRewrapper` doc comments describe both callers: the offline `secret-protection` command and the online
  conformance of an owner root key change.

### D9. Audit

New public types in `src/NCode.Identity.Secrets.Persistence.Abstractions/Auditing/` (copy `MasterKeyAuditEvent.cs` and
siblings): `RootKeyAuditEvent : AuditEvent, ISupportAuditDetails` with `Change`, `Reason`, `OwnerType`, `OwnerId`,
`KeyWrapperName`, `KeyReference`, `State`, `Failure?`; `EventName => RootKeyAuditEventNames.For(Change)`;
`EventCategory => AuditEventCategories.Key`; `Resources => [new(AuditResourceTypes.RootKey, KeyReference), new(OwnerType, OwnerId)]`;
details `reason`, `provider`, `key_reference`, `state`, and `failure` when present. `RootKeyAuditEventNames`
(`Prefix = "root_key"`, `Set`, `Removed`, `Unavailable`, `Restored`, `For(change)`); `RootKeyChanges` (`Set = "set"`,
`Removed = "removed"`, `Unavailable = "unavailable"`, `Restored = "restored"`, open set); `RootKeyChangeReasons`
(`Requested = "requested"`, `ValidationFailed = "validation_failed"`, `ValidationSucceeded = "validation_succeeded"`).
Outcome: `failure` for `unavailable` (severity warning by the base class), `success` otherwise. New
`AuditResourceTypes.RootKey = "root_key"` in `src/NCode.Identity.Events.Abstractions/Audit/AuditResourceTypes.cs`.
`MasterKeyChangeReasons.RootKeyChanged = "root_key_changed"` (a rotation that moves an owner onto its root key);
`MasterKeyAuditEvent` gains `string? RootKeyReference { get; init; }` written as `root_key_reference` when present.

Who records what: the management API (source `management`, administrator actor, request) records `root_key.set` and
`root_key.removed` (reason `requested`) and `root_key.unavailable`/`root_key.restored` from a validate call (reasons
`validation_failed`/`validation_succeeded`), plus `master_key.rotated` with reason `root_key_changed` for the rotation a
set or validate performed. The validation service (source `openid`, system actor) records `root_key.unavailable`,
`root_key.restored` and `master_key.rotated`/`root_key_changed`. Each is published after its commit; none carries
material. `root_key.removed` is recorded when the key moves to `removing`, since new master keys are wrapped under the
deployment root from that point; the completion of a removal is logged, not audited.

### D10. Periodic re-validation, caching and multiple replicas

- **Shared state.** Internal sealed `OwnerRootKeyValidationService : BackgroundService` in
  `src/NCode.Identity.OpenId.Core/SecretProtection/` (copy `MasterKeyMaintenanceService`): waits for
  `IMaterializedServerAccessor`, then every `RetryInterval` takes the lease
  `owner-root-key-validation:{serverId}` (`IExpiringKeySet.TryAddAsync`, expiring after half of `RetryInterval`), pages
  `IOwnerRootKeyStore.GetDuePageAsync(now - ValidationInterval, now - RetryInterval, page of 100)`, and calls
  `IOwnerRootKeyManager.ValidateAsync(owner)` for each due owner in its own try block; publishes the audit events of D9
  through `PostCommitWork`. So an `active` key is probed once per `ValidationInterval` by one replica, and an
  `unavailable` or `removing` key every `RetryInterval`. Registered in `AddSecretProtectionServices`.
- **Propagation.** Every state change calls `IOwnerRootKeyChangeObserver.ChangedAsync(owner)`. New public interface
  `IOwnerRootKeyChangeObserver` (`Protection/`, one member `ValueTask ChangedAsync(SecretOwner owner, CancellationToken)`).
  OpenId.Core registers internal sealed `OwnerRootKeyChangeAnnouncer`: for a tenant owner it sends
  `TenantChangedCommand(tenantId, SettingsChanged: false)` through a mediator from a new scope (evicts the tenant's
  materialized keys and master keys here and broadcasts `tenant-changed`, which evicts both on every replica); for any
  other owner it publishes the owner on `MasterKeyBroadcast.ChannelName`.
- **Ring (`DefaultMasterKeyRing`).**
  - `MasterKeyRingEntry` gains `bool IsOwnerRooted` and `long UnwrappedTimestamp` (when its material last came from the
    root key provider) and `bool NeedsValidation(TimeProvider, TimeSpan validationInterval)` (`IsOwnerRooted` and the
    elapsed time since `UnwrappedTimestamp` is at least the interval).
  - The fast paths also require `!cached.NeedsValidation(…)`.
  - `ReadRowsAsync` reads the owner's binding (`IOwnerRootKeyStore.FindAsync`, same store manager) when it must create the
    first key, or when the active row or any row read has a `RootKeyReference`; it passes the binding to
    `Factory.CreateAsync` when creating.
  - An owner-rooted load whose binding state is `unavailable` or unknown throws `OwnerRootKeyUnavailableException`.
  - An entry that needs validation is reloaded with no cached material reused: every row it needs is unwrapped again
    through the tenant key, so a revoked or destroyed key stops this replica within `ValidationInterval` even when the
    shared state is never updated. A refresh that reuses material carries `UnwrappedTimestamp` over (the minimum of the
    reused entry's and now when rows are added).
  - Any exception of an owner-rooted load except a caller cancellation removes the owner's entry, records the failure in
    a `ConcurrentDictionary<SecretOwner, long>` of failure timestamps, logs `OwnerRootKeyLoadFailed`, and throws
    `OwnerRootKeyUnavailableException` wrapping it. Within `RetryInterval` of a recorded failure `GetProtectorAsync`
    throws `OwnerRootKeyUnavailableException` at once, with no store or KMS call. `Evict`, `EvictAll` and a successful
    load clear the record.
  - Evicted keys are not zeroed (a protector handed out earlier may still use them), as the ring already does.
- **Bound.** A revocation takes effect within `ValidationInterval`: on every replica at once through the broadcast, within
  one `RefreshInterval` on a replica that missed it (its refresh reads the `unavailable` state), and within
  `ValidationInterval` on its own unwrap even if no probe runs. A tenant's materialized signing keys leave each replica
  with the tenant-changed broadcast, and at the latest after `TenantCacheLifetime`.
- **Cost.** One probe (two KMS calls) per owner root key per `ValidationInterval` by one replica, and one unwrap per
  cached master key per replica per `ValidationInterval`.

### D11. Failing closed

New public `OwnerRootKeyUnavailableException : Exception` (`Protection/`, property `SecretOwner Owner`, constructors
`(SecretOwner owner)`, `(SecretOwner owner, Exception? innerException)`, message "The root key of owner '{owner}' is
unavailable: its master keys cannot be unwrapped until the key is restored and validated again."). It does **not**
derive from `CryptographicException`, so development pruning never deletes a tenant-keyed owner's master keys. New
public `OwnerRootKeyProbeException : Exception` (properties `Owner`, `KeyWrapperName`, `KeyReference`, `Failure`).
Management installs `OwnerRootKeyUnavailableEndpointFilter` on the management root group (beside
`UnreadableSettingsEndpointFilter`): `503 Service Unavailable`, `Retry-After` = `RetryInterval` in whole seconds (at
least 1), problem detail "The tenant's root key is unavailable, so its encryption keys cannot be used until it is
restored.", extensions `ownerType`, `ownerId`; logs `OwnerRootKeyUnavailable`. Protocol endpoints answer as for any
unhandled failure (`server_error`).

### D12. Management API and console

Tenant only (the server handler is unchanged apart from passing nothing for the root key):

- `GET /api/tenants/{tenantId}/encryption-keys` → `EncryptionKeysResource` gains `EncryptionRootKeyResource? RootKey`
  and `required List<string> RootKeyProviders` (allowed providers; empty on a server); `EncryptionKeyResource` gains
  `string? RootKeyReference`.
- `PUT /api/tenants/{tenantId}/encryption-keys/root-key` (`Operations.Update`), body `EncryptionRootKeyRequest
  { required string Provider; required string KeyReference }` → `200` `EncryptionRootKeyChangeResource { required
  EncryptionRootKeyResource RootKey; EncryptionKeyResource? ActiveKey; EncryptionKeyResource? RetiredKey; required int
  RewrappedKeys; required int FailedKeys }`. `400` validation problem (keys `provider`, `keyReference`) when blank, not
  allowed, not registered or malformed; `422` problem when the probe fails (detail "The key could not wrap and unwrap a
  probe through '{provider}': {failure}"); `409` with `Retry-After` when the owner rotated within twice
  `RefreshInterval` (reuse `EncryptionKeyEndpoints.RekeyTooSoonDetail` handling); `412` on a lost race; `404` for an
  unknown tenant.
- `DELETE /api/tenants/{tenantId}/encryption-keys/root-key` (`Operations.Update`) → `204` once every master key is under
  the deployment root (also when none was set); `202` with `EncryptionRootKeyResource` (state `removing`) when master
  keys remain under the tenant key (the validation service retries every `RetryInterval`).
- `POST /api/tenants/{tenantId}/encryption-keys/root-key/validate` (`Operations.Update`) → `200`
  `EncryptionRootKeyResource`; `404` when no root key is set or the tenant is unknown.
- `EncryptionRootKeyResource { required string Provider; required string KeyReference; required string State; required
  DateTimeOffset CreatedWhen; required DateTimeOffset StateChangedWhen; DateTimeOffset? ValidatedWhen; DateTimeOffset?
  FailedWhen; string? Failure }`; `EncryptionRootKeyStates` (`Active`, `Unavailable`, `Removing`; open, unknown reads as
  unavailable). All in `src/NCode.Identity.OpenId.Management.Abstractions/Contracts/EncryptionKeys/`.
- `IManagementAuditRecorder`: `RecordMasterKeyRotatedAsync(HttpContext, MasterKeyRotation, string reason, CancellationToken)`
  (the rekey passes `MasterKeyChangeReasons.Requested`) and new `RecordRootKeyChangedAsync(HttpContext, OwnerRootKeyInfo
  rootKey, string change, string reason, CancellationToken)`.
- Endpoint names `api/tenants/encryption-keys/root-key/set`, `/remove`, `/validate`; the route comment block at the top
  of `TenantApiEndpointHandler.cs` lists the three routes. The scope family is `encryption_keys`, as for rekey.
- Console (`OwnerEncryptionKeysTab`, tenant only): a "Root key" section that shows provider, reference, state chip,
  created, last validated, last failure; "Set root key" (dialog: provider select from `rootKeyProviders`, key reference
  field, server errors shown in the dialog), "Validate" and confirmed "Remove". With no allowed provider and no root key,
  the section says the deployment offers no tenant root keys and shows no set action. With a root key, the recovery
  warning is replaced by an info alert: "This tenant's keys are wrapped only by its own root key. Revoking or destroying
  that key makes its data unreadable, and no operator can recover it." An `unavailable` state shows an error alert with
  the failure. `EncryptionKeyTable` gains a "Root key" column: the reference, or "Deployment" when null.

### D13. Chart

`secretProtection.ownerRootKeys.providers: []` (values, schema: array of `aws-kms`, `azure-key-vault`,
`google-cloud-kms`, unique) renders `SecretProtection__OwnerRootKeys__Providers__<i>`. A provider's workload credentials
(AWS web identity, Azure workload identity, Google external account) are mounted when it is the root key provider **or**
listed in `ownerRootKeys.providers`: a new helper `identity-host.usesKeyWrapper` (dict with root and name) returns
`"true"` for either, and the conditions in `googleCloudCredentialConfiguration`, `hostEnv`, `hostVolumeMounts` and
`hostVolumes` use it. The provider's own key values (`awsKms.keyArn`, `AWS_REGION`, `azureKeyVault.keyId`,
`googleCloudKms.keyName`) stay rendered only for the root provider. New CI values file
`charts/ncode-identity-host/ci/tenant-root-keys-values.yaml` (root `local` with a key Secret, `ownerRootKeys.providers:
[aws-kms, google-cloud-kms]` with web identity and federation values). The intervals are tuned through `extraEnv`
(`SecretProtection__OwnerRootKeys__ValidationInterval`, `SecretProtection__OwnerRootKeys__RetryInterval`).

### D14. Out of scope

Owner root keys for the server owner; a per-tenant KMS credential (the deployment's workload identity calls every
tenant key); protocol-level `temporarily_unavailable` mapping; OpenBao/PKCS#11 providers (#173).

## ADRs to write

### ADR-1412 — "A key wrapper can wrap under a key the caller names"

File `docs/decisions/1412-a-key-wrapper-can-wrap-under-a-key-the-caller-names.md`. Status `Accepted`, Date
`2026-11-05`, Deciders `NCode Group`, `**Amends:**` ADR-1279 (decision 1's contract gains `IReferencedKeyWrapper`;
decision 5's root role stays selection by name) and ADR-1260 (the Azure and Google adapters wrap under a named key as
well as the configured one).

- Context: a wrapper is bound to the one key its options name; a tenant root key needs the same provider to wrap under
  a key chosen at run time, and unwrap already addresses a key through `WrappedKey.KeyId` alone.
- Decision: D2 in full (interface, members, syntax check, exceptions, the three adapters, the version-belongs-to check
  for Google and Azure, one shared private wrap path, which wrappers do not implement it and why, catalog members, no
  new registration).
- Options considered: a reference parameter on `IKeyWrapper.WrapAsync` (rejected: ephemeral, local and recovery have no
  meaning for it and would have to throw); a provider-specific wrapper instance per reference created by a factory
  (rejected: duplicates the client caches and validators the adapters already have); a marker interface without members
  (rejected, as ADR-1279 rejects one: the capability is the members).
- Consequences: a third-party wrapper becomes a candidate for tenant root keys by implementing two members; the
  configured and the referenced wrap cannot drift; unwrap needs no change.
- References: ADR-1279, ADR-1260, ADR-1411, the three adapter files, issue #174.

### ADR-1411 — "A tenant can wrap its master keys under a root key it controls"

File `docs/decisions/1411-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md`. Status `Accepted`, Date
`2026-11-05`, Deciders `NCode Group`, `**Amends:**` ADR-1235 (decision 1: an owner other than the server may name its own
root key, which wraps its master keys in place of the deployment root; decision 2: such a master key has no recovery
copy; decision 9: revoking or destroying the owner's key shreds its data wherever a copy is; decision 13: an
owner-rooted cache entry is unwrapped again every validation interval and refused while its key is unavailable);
ADR-1407 (rotation creates the successor under the owner's root key; the status reports the root key; reason
`root_key_changed`; the ring re-validates); ADR-1408 (the rewrap engine wraps each row under its owner's root, skips a
row under an owner root key, never adds a recovery copy to it, unwraps it through its own key with a recovery private
key, and also serves the online conformance); ADR-1260 decision 8 (the chart mounts a provider's credentials when it is
the root or an owner root key provider).

- Context (present tense): a tenant that must hold the key to its own data (regulated customers, Auth0's
  bring-your-own-key) needs its master keys wrapped by a key it can revoke, with no operator recovery path; the server
  reaches that key with its own workload identity; a revocation must take effect within a bounded period across
  replicas without a KMS call per request.
- Decision: D1, D3–D11 and D12–D13 in summary, numbered, each with its rule stated plainly (binding record and why not a
  setting; options; data model and invariant; wrap rule; set/change/remove sequences including rotation and the reason
  for it; probe; rewrap engine; audit; re-validation, propagation and the bound; failing closed; API and console; chart).
- Options considered:
  - A setting in the settings cascade (rejected: synchronous value validation; a change is an operation; cascade and
    profiles must never choose a tenant's key; operational state; layering).
  - Rewrapping the existing master keys without rotating (rejected: their material is in backups under the deployment
    root and recovery key, so values written later would stay readable to whoever holds such a backup).
  - Keeping an operator recovery copy (rejected: the tenant's control is the point; the issue requires none).
  - A provider-specific "describe key" check (rejected: provider-specific, proves neither permission; the probe proves
    both through any provider).
  - Treating a transient failure as available (rejected: fail closed; an unavailable key is retried every
    `RetryInterval`).
  - Checking the tenant's key on every request (rejected: latency, cost and the tenant's KMS quotas).
  - Every registered wrapper as a choice (rejected: `local` and `ephemeral` are deployment secrets; the operator opts in
    per provider because the deployment's identity makes the calls).
  - Destroying the master keys when the key becomes unavailable (rejected: the tenant may restore its key; the KMS
    already shreds).
  - A bring-your-own key for the server owner (rejected: its master keys protect the deployment's Data Protection ring).
- Consequences: data under a tenant-keyed master key is unreadable once the tenant revokes or destroys its key, in the
  live database and in every backup; a backup taken before the key was set keeps the earlier master keys under the
  deployment root and their recovery copies, so the values in it stay readable to whoever holds that backup and the
  deployment root until it expires; values written or rewrapped after the set never exist under a key the operator
  controls; the tenant's availability depends on its KMS and on the grants it gives the deployment's identity; no
  operator can recover a tenant-keyed owner; cost per D10; a tenant admin may name any key the deployment's identity can
  use, which the owner-bound context keeps from unwrapping another owner's rows, so an operator grants its identity a
  tenant's key only through that tenant's own key policy.
- References: ADR-1235, ADR-1260, ADR-1279, ADR-1407, ADR-1408, ADR-1412, ADR-1150, ADR-1351, the main code files,
  issue #174, Auth0 "Bring Your Own Key".

Status lines and index rows to update (append, keep the existing text): ADR-1235 Status gains "; amended by
[ADR-1411](1411-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md): an owner may wrap its master keys
under a root key it controls, with no recovery copy, re-validated periodically"; ADR-1260 gains "; amended by
[ADR-1411](…): the chart mounts a provider's credentials when tenants may choose it; amended by [ADR-1412](…): the Azure
and Google adapters wrap under a key the caller names"; ADR-1279 gains "; amended by [ADR-1412](…): a key wrapper can
wrap under a key the caller names"; ADR-1407 `Status: Accepted` becomes "Accepted (amended by [ADR-1411](…): rotation
wraps the successor under the owner's root key, the status reports it, and an owner-rooted cache entry is re-validated)";
ADR-1408 likewise "Accepted (amended by [ADR-1411](…): a row under an owner root key is wrapped only under that key and
never gains a recovery copy)". Index rows in `docs/decisions/README.md` for 1235, 1260, 1279, 1407 and 1408 get the same
"amended by [1411](./…)"/"[1412](./…)" additions, and two new rows for 1411 and 1412 after 1408 with
"Accepted (amends [1235](./…), [1260](./…), [1407](./…), [1408](./…))" and "Accepted (amends [1260](./…), [1279](./…))".
Do not edit the bodies of accepted records.

## Steps

### [ ] 1. Write ADR-1411 and ADR-1412 and update the amended records

- Files: `docs/decisions/1411-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md` (new),
  `docs/decisions/1412-a-key-wrapper-can-wrap-under-a-key-the-caller-names.md` (new), `docs/decisions/README.md`,
  `docs/decisions/1235-*.md`, `1260-*.md`, `1279-*.md`, `1407-*.md`, `1408-*.md` (Status lines only).
- Change: as "ADRs to write". Present tense, no temporal words; link code paths that exist after this plan.
- Pattern: `docs/decisions/1407-*.md` and `1408-*.md` (metadata, Amends line, sections).
- Done when: `pwsh -NoProfile -Command "./.template/Test-AdrIndex.ps1; if ($LASTEXITCODE) { exit $LASTEXITCODE }; ./build/adr-index.ps1"` exits 0.

### [ ] 2. Add `IReferencedKeyWrapper` and the catalog members

- Files: `src/NCode.Identity.Secrets.Abstractions/KeyWrapping/IReferencedKeyWrapper.cs` (new),
  `src/NCode.Identity.Secrets.Abstractions/PublicAPI.Unshipped.txt`,
  `src/NCode.Identity.Secrets.Persistence/Protection/IKeyWrapperCatalog.cs`, `DefaultKeyWrapperCatalog.cs`,
  `tests/NCode.Identity.Secrets.Persistence.Tests/Protection/DefaultKeyWrapperCatalogTests.cs`.
- Change: D2 interface (XML docs state the syntax-only check, the `ArgumentException`, and that `KeyId` addresses the key
  for `UnwrapAsync` without configuration; `// See ADR-1412.`). Catalog: index referenced wrappers in a second frozen
  dictionary at construction; `TryGetReferenced`, `GetReferenced` (message: "No key wrapper that wraps under a key the
  caller names is registered with the name '{name}'. The registered ones are: …."). Tests: referenced wrapper found;
  plain wrapper refused by `GetReferenced` and `TryGetReferenced`; unknown name refused; message lists names in ordinal
  order.
- Pattern: `DefaultKeyWrapperCatalog.Get` and its tests.
- Done when: `dotnet test tests/NCode.Identity.Secrets.Persistence.Tests/NCode.Identity.Secrets.Persistence.Tests.csproj -m:2 --filter "FullyQualifiedName~DefaultKeyWrapperCatalogTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 3. AWS KMS wraps under a named key

- Files: `src/NCode.Identity.Secrets.KeyWrapping.AwsKms/AwsKmsKeyWrapper.cs`,
  `tests/NCode.Identity.Secrets.KeyWrapping.AwsKms.Tests/AwsKmsKeyWrapperTests.cs`.
- Change: implement `IReferencedKeyWrapper` (D2); private `WrapUnderAsync(string keyArn, …)` shared with the configured
  wrap; failure phrase "must be the ARN of a KMS key or alias, such as arn:aws:kms:us-west-2:111122223333:alias/tenant-root".
  Tests: referenced wrap sends `KeyId = reference` to the client of the reference's region with the encryption context,
  returns the reported ARN; invalid reference throws `ArgumentException` without a client call;
  `TryValidateKeyReference` true/false cases; a wrapped key from a referenced wrap unwraps through `UnwrapAsync`.
- Pattern: the existing `WrapAsync` tests in the same file.
- Done when: `dotnet test tests/NCode.Identity.Secrets.KeyWrapping.AwsKms.Tests/NCode.Identity.Secrets.KeyWrapping.AwsKms.Tests.csproj -m:2 --filter "FullyQualifiedName~AwsKmsKeyWrapperTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 4. Google Cloud KMS wraps under a named key

- Files: `src/NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms/GoogleCloudKmsKeyWrapper.cs`,
  `tests/NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Tests/GoogleCloudKmsKeyWrapperTests.cs`.
- Change: as step 3 with the crypto key name rule, the CRC32C checks unchanged, and the reported version checked against
  the referenced crypto key (`IOException` otherwise). Tests mirror step 3 plus "a version of another crypto key throws
  `IOException`".
- Pattern: the existing wrap tests.
- Done when: `dotnet test tests/NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Tests/NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Tests.csproj -m:2 --filter "FullyQualifiedName~GoogleCloudKmsKeyWrapperTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 5. Azure Key Vault wraps under a named key

- Files: `src/NCode.Identity.Secrets.KeyWrapping.AzureKeyVault/AzureKeyVaultKeyWrapper.cs`,
  `src/NCode.Identity.Secrets.KeyWrapping.AzureKeyVault/AzureKeyVaultKeyId.cs` (add an internal helper that returns the
  versionless part of a key version id), `tests/NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests/AzureKeyVaultKeyWrapperTests.cs`,
  `tests/NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests/AzureKeyVaultKeyIdTests.cs`.
- Change: as step 3 with the key id rule; the reported version must belong to the referenced key (D2); the 3072-bit floor
  applies. Tests mirror step 3 plus "a version of another key throws `InvalidOperationException`" and the helper's cases.
- Pattern: the existing wrap tests.
- Done when: `dotnet test tests/NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests/NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests.csproj -m:2 --filter "FullyQualifiedName~AzureKeyVaultKeyWrapperTests|FullyQualifiedName~AzureKeyVaultKeyIdTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 6. Contracts: owner root key data, store, manager, exceptions, options, audit

- Files (new, `src/NCode.Identity.Secrets.Persistence.Abstractions/`): `DataContracts/PersistedOwnerRootKey.cs`,
  `Protection/OwnerRootKeyStates.cs`, `Protection/OwnerRootKeyInfo.cs`, `Protection/IOwnerRootKeyStore.cs`,
  `Protection/IOwnerRootKeyManager.cs`, `Protection/OwnerRootKeyChange.cs`, `Protection/OwnerRootKeyRemoval.cs`,
  `Protection/OwnerRootKeyValidation.cs`, `Protection/IOwnerRootKeyChangeObserver.cs`,
  `Protection/OwnerRootKeyUnavailableException.cs`, `Protection/OwnerRootKeyProbeException.cs`,
  `Options/OwnerRootKeyOptions.cs`, `Auditing/RootKeyAuditEvent.cs`, `Auditing/RootKeyAuditEventNames.cs`,
  `Auditing/RootKeyChanges.cs`, `Auditing/RootKeyChangeReasons.cs`. Changed: `DataContracts/PersistedMasterKey.cs`
  (`RootKeyReference`), `Protection/MasterKeyInfo.cs` (last positional `RootKeyReference`), `Protection/MasterKeyStatus.cs`
  (last positional `RootKey`), `Protection/MasterKeyRewrapRequest.cs` (`NonconformingOnly`),
  `Protection/IMasterKeyRewrapper.cs` (docs), `Options/SecretProtectionOptions.cs` (`OwnerRootKeys`),
  `Auditing/MasterKeyChangeReasons.cs` (`RootKeyChanged`), `Auditing/MasterKeyAuditEvent.cs` (`RootKeyReference` +
  detail), `PublicAPI.Unshipped.txt`; `src/NCode.Identity.Events.Abstractions/Audit/AuditResourceTypes.cs` (`RootKey`)
  and its `PublicAPI.Unshipped.txt`. Every call site the new positional parameters break, found with
  `grep -rn "new MasterKeyInfo(\|new MasterKeyStatus(" src tests` (pass the mapped value in `MasterKeyInfo.From`,
  `null` for `RootKey` in `DefaultMasterKeyManager.GetStatusAsync` until step 9 fills it, and the right value in tests).
  Tests: `tests/NCode.Identity.Secrets.Persistence.Tests/Auditing/RootKeyAuditEventTests.cs` (new; name, category,
  resources, details with and without failure, outcome/severity) and `MasterKeyAuditEventTests.cs`
  (`root_key_reference`).
- Change: D4, D6 (signatures and XML docs only), D9, D11 (exception types), D3 (options class only).
- Pattern: `PersistedMasterKey.cs`, `IMasterKeyStore.cs`, `IMasterKeyManager.cs`, `MasterKeyRotationTooSoonException.cs`,
  `MasterKeyOptions.cs`, `MasterKeyAuditEvent.cs` and its test.
- Done when: `dotnet test tests/NCode.Identity.Secrets.Persistence.Tests/NCode.Identity.Secrets.Persistence.Tests.csproj -m:2 --filter "FullyQualifiedName~RootKeyAuditEventTests|FullyQualifiedName~MasterKeyAuditEventTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 7. Format, options validator and the factory's wrap rule

- Files: `src/NCode.Identity.Secrets.Persistence/Protection/SecretProtectionFormat.cs` (probe context),
  `Protection/MasterKeyFactory.cs` (D5: `CreateAsync` with `rootKey`, `WrapAsync`, `MasterKeyWraps`; remove
  `WrapUnderRootAsync`/`WrapUnderRecoveryAsync`), `Protection/MasterKeyWraps.cs` (new),
  `Protection/OwnerRootKeyProbe.cs` (new, D7), `Options/OwnerRootKeyOptionsValidator.cs` (new, D3),
  `DefaultRegistration.cs` (validator, probe singleton; doc list), every caller of the changed factory members
  (`DefaultMasterKeyRing.cs`, `DefaultMasterKeyManager.cs`, `DefaultMasterKeyRewrapper.cs`: pass `rootKey: null` for now),
  tests `SecretProtectionFormatTests.cs`, `MasterKeyFactoryTests.cs`, new `OwnerRootKeyProbeTests.cs`, new
  `tests/NCode.Identity.Secrets.Persistence.Tests/Options/OwnerRootKeyOptionsValidatorTests.cs`, and a test double
  `tests/NCode.Identity.Secrets.Persistence.Tests/Protection/FakeReferencedKeyWrapper.cs` (in-memory AES-GCM per reference
  through `AesGcmKeyWrap`, `KeyId = reference + "/v1"`, `Revoke(reference)` makes wrap and unwrap throw
  `InvalidOperationException("revoked")`, counts calls).
- Change: factory creates under an active owner root key with `RootKeyReference` and no recovery copy even when recovery
  is configured; throws `OwnerRootKeyUnavailableException` for `unavailable`/unknown; deployment root for null/`removing`.
  Probe returns null on success, the failure text otherwise (revoked wrapper, mismatching wrapper, cancellation
  propagates). Validator per D3.
- Pattern: `MasterKeyFactory.CreateAsync`, `MasterKeyOptionsValidator` and their tests.
- Done when: `dotnet test tests/NCode.Identity.Secrets.Persistence.Tests/NCode.Identity.Secrets.Persistence.Tests.csproj -m:2 --filter "FullyQualifiedName~MasterKeyFactoryTests|FullyQualifiedName~OwnerRootKeyProbeTests|FullyQualifiedName~OwnerRootKeyOptionsValidatorTests|FullyQualifiedName~SecretProtectionFormatTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 8. The ring re-validates owner-rooted keys and fails closed

- Files: `src/NCode.Identity.Secrets.Persistence/Protection/DefaultMasterKeyRing.cs`, `Protection/MasterKeyRingEntry.cs`,
  `Logging/EventIds.cs`, `Logging/Log.cs` (`OwnerRootKeyLoadFailed`, `OwnerRootKeyRefused`),
  `tests/NCode.Identity.Secrets.Persistence.Tests/Protection/DefaultMasterKeyRingTests.cs`, `MasterKeyRingEntryTests.cs`.
- Change: D10 ring bullets. Tests: first key of a bound owner is created under the tenant key with no recovery copy;
  owner-rooted entry reused within `ValidationInterval` without an unwrap and unwrapped again after it; a deployment-rooted
  entry never needs validation; binding `unavailable` → `OwnerRootKeyUnavailableException`, entry removed; unwrap failure
  of an owner-rooted row → `OwnerRootKeyUnavailableException` wrapping it, and a second call within `RetryInterval`
  throws without a store call; after `RetryInterval` it reloads; `Evict` clears the failure; a deployment-rooted unwrap
  failure still propagates unchanged (`CryptographicException`); existing tests updated for the binding read on first-key
  creation.
- Pattern: the existing staleness and eviction tests in `DefaultMasterKeyRingTests`.
- Done when: `dotnet test tests/NCode.Identity.Secrets.Persistence.Tests/NCode.Identity.Secrets.Persistence.Tests.csproj -m:2 --filter "FullyQualifiedName~DefaultMasterKeyRingTests|FullyQualifiedName~MasterKeyRingEntryTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 9. Rotation, status and the rewrap engine follow the owner's root

- Files: `src/NCode.Identity.Secrets.Persistence/Protection/DefaultMasterKeyManager.cs` (status reads the binding and
  fills `RootKey`; `RotateAsync` reads the binding in its `ListAsync` store manager and passes it to `CreateAsync`; spacing
  through the new `MasterKeyRotationSpacing`), `Protection/MasterKeyRotationSpacing.cs` (new internal static),
  `Protection/DefaultMasterKeyRewrapper.cs` (D8), tests `DefaultMasterKeyManagerTests.cs`, `DefaultMasterKeyRewrapperTests.cs`.
- Change: D5, D8. Tests: rotation of a bound owner wraps the successor under the tenant key with no recovery copy;
  rotation with an `unavailable` binding throws `OwnerRootKeyUnavailableException` and writes nothing; status carries the
  root key; rewrapper: a conforming tenant-keyed row is examined only (dry run verifies it through the tenant key); a
  nonconforming deployment-rooted row of a bound owner is wrapped under the tenant key and loses its recovery copy; a
  tenant-keyed row of an unbound or `removing` owner moves under the deployment root with a recovery copy; with a
  recovery private key a tenant-keyed row unwraps through its own wrapper; recovery-only clears a recovery copy on a
  tenant-keyed row; `NonconformingOnly` skips conforming rows without an unwrap.
- Pattern: existing tests in both files.
- Done when: `dotnet test tests/NCode.Identity.Secrets.Persistence.Tests/NCode.Identity.Secrets.Persistence.Tests.csproj -m:2 --filter "FullyQualifiedName~DefaultMasterKeyManagerTests|FullyQualifiedName~DefaultMasterKeyRewrapperTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 10. `DefaultOwnerRootKeyManager`

- Files: `src/NCode.Identity.Secrets.Persistence/Protection/DefaultOwnerRootKeyManager.cs` (new),
  `DefaultRegistration.cs` (`TryAddSingleton<IOwnerRootKeyManager, DefaultOwnerRootKeyManager>`, doc list),
  `Logging/EventIds.cs`, `Logging/Log.cs` (`OwnerRootKeySet`, `OwnerRootKeyProbeFailed`, `OwnerRootKeyStateChanged`,
  `OwnerRootKeyRemovalIncomplete`, `OwnerRootKeyRemoved`, `OwnerRootKeyRotationDeferred`, `OwnerRootKeyObserverFailed`),
  `tests/NCode.Identity.Secrets.Persistence.Tests/Protection/DefaultOwnerRootKeyManagerTests.cs` (new).
- Change: D6 exactly (sequence, results, `Change` values, server refused, observers on a bounded token, completion with
  deferred rotation, removal without rotation). Tests cover: set of a new key (probe, binding, rotation, conformance,
  observers, `Change = set`); set with an invalid or not-allowed provider (`ArgumentException`, nothing written); probe
  failure (`OwnerRootKeyProbeException`, nothing written); too soon (`MasterKeyRotationTooSoonException` before any
  write); same key while unavailable restores (`Change = restored`, no rotation when the active key was created under it);
  change to another key rotates; rotation lost to a race is deferred; removal completes (binding deleted, rows moved,
  `Removed = true`) and removal with a failing row stays `removing` with `FailedWhen`; validate success/failure state
  transitions and their changes; validate of `removing` completes removal; server owner refused.
- Pattern: `DefaultMasterKeyManager` and its tests (strict mocks, queued store managers).
- Done when: `dotnet test tests/NCode.Identity.Secrets.Persistence.Tests/NCode.Identity.Secrets.Persistence.Tests.csproj -m:2 --filter "FullyQualifiedName~DefaultOwnerRootKeyManagerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 11. EF Core: the owner root key store, the master key column and the re-scaffolded migration

- Files: `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Entities/OwnerRootKeyEntity.cs` (new),
  `Entities/MasterKeyEntity.cs` (`RootKeyReference`), `Stores/OwnerRootKeyStore.cs` (new), `Stores/MasterKeyStore.cs`
  (map `RootKeyReference` in add, update and read), `Stores/CoreDbContextExtensions.cs` (`OwnerRootKeys`),
  `CoreModelContributor.cs` (table `OwnerRootKeys`), `DefaultRegistration.cs` (`AddStore<TDbContext, IOwnerRootKeyStore,
  OwnerRootKeyStore>()`), `Stores/TenantStore.cs` (remove the tenant's `OwnerRootKeys` row with its master keys),
  `PublicAPI.Unshipped.txt`; `src/NCode.Identity.OpenId.Host.Database/Migrations/OpenId/*` (delete the three files and
  re-scaffold with `dotnet ef migrations add InitialCreate --project src/NCode.Identity.OpenId.Host.Database --startup-project src/NCode.Identity.OpenId.Host.Database --context OpenIdDbContext --output-dir Migrations/OpenId`
  after `dotnet tool restore`; strip the UTF-8 BOM from each generated file); tests
  `tests/NCode.Identity.OpenId.Persistence.EntityFramework.Tests/Stores/OwnerRootKeyStoreTests.cs` (new: add/find
  case-insensitively, update with token, conflicting token throws, missing row throws, remove true/false, due page
  filtering and paging), `MasterKeyStoreTests.cs` (`RootKeyReference` round trip), `TenantStoreLifecycleTests.cs`
  (tenant delete removes its owner root key).
- Change: D4 EF part.
- Pattern: `MasterKeyEntity`, `MasterKeyStore` (`GetPageAsync` keyset with `KeysetCursor`), `MasterKeyStoreTests`.
- Done when: `dotnet test tests/NCode.Identity.OpenId.Persistence.EntityFramework.Tests/NCode.Identity.OpenId.Persistence.EntityFramework.Tests.csproj -m:2 --filter "FullyQualifiedName~OwnerRootKeyStoreTests|FullyQualifiedName~MasterKeyStoreTests|FullyQualifiedName~TenantStoreLifecycleTests" -- RunConfiguration.TreatNoTestsAsError=true`
  and `dotnet test tests/NCode.Identity.OpenId.Host.Database.Tests/NCode.Identity.OpenId.Host.Database.Tests.csproj -m:2 -- RunConfiguration.TreatNoTestsAsError=true`
  and `dotnet ef migrations has-pending-model-changes --project src/NCode.Identity.OpenId.Host.Database --startup-project src/NCode.Identity.OpenId.Host.Database --context OpenIdDbContext` all exit 0.

### [ ] 12. OpenId.Core: announce changes and re-validate in the background

- Files: `src/NCode.Identity.OpenId.Core/SecretProtection/OwnerRootKeyChangeAnnouncer.cs` (new),
  `SecretProtection/OwnerRootKeyValidationService.cs` (new), `SecretProtection/DefaultRegistration.cs`,
  `Logging/EventIds.cs`, `Logging/Log.cs` (`OwnerRootKeyValidationPassFailed`, `OwnerRootKeyValidationOwnerFailed`,
  `OwnerRootKeyAuditPublishFailed`, `OwnerRootKeyAnnouncementFailed`), tests
  `tests/NCode.Identity.OpenId.Core.Tests/SecretProtection/OwnerRootKeyChangeAnnouncerTests.cs` and
  `OwnerRootKeyValidationServiceTests.cs` (new).
- Change: D10 service and announcer, D9 audit from the service (`RootKeyAuditEvent` and, for a completion rotation,
  `MasterKeyAuditEvent` with `RootKeyChanged`; tenant id set for tenant owners; system actor; source `openid`). Tests:
  lease lost → nothing runs; due owners validated each in its own try block; an `unavailable` change publishes
  `root_key.unavailable` with outcome failure; a restored change publishes `root_key.restored`; a rotation publishes
  `master_key.rotated`/`root_key_changed`; a failing owner does not stop the pass; announcer sends
  `TenantChangedCommand(tenant, false)` for a tenant and broadcasts `master-key-changed` for another owner type.
- Pattern: `MasterKeyMaintenanceService` + tests, `MasterKeyChangeAnnouncer` + tests, `TenantKeyRolloverService`
  (scoped mediator after commit).
- Done when: `dotnet test tests/NCode.Identity.OpenId.Core.Tests/NCode.Identity.OpenId.Core.Tests.csproj -m:2 --filter "FullyQualifiedName~OwnerRootKey|FullyQualifiedName~MasterKeyMaintenanceServiceTests|FullyQualifiedName~MasterKeyChangeAnnouncerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 13. Server: bind `SecretProtection:OwnerRootKeys`

- Files: `src/NCode.Identity.Server/SecretProtection/SecretProtectionRegistration.cs`,
  `tests/NCode.Identity.Server.Tests/SecretProtection/SecretProtectionRegistrationTests.cs`.
- Change: bind `OwnerRootKeys` strictly in the `Configure` callback and add it to `SecretProtectionChildren`. Tests: binds
  providers and both intervals; an unknown child of `OwnerRootKeys` fails strict binding; a provider that is not a
  referenced wrapper (`local`) fails validation at start with the D3 message.
- Pattern: `AddSecretProtection_BindsMasterKeys`, `AddSecretProtection_UnknownMasterKeysChild_FailsStrictBinding`.
- Done when: `dotnet test tests/NCode.Identity.Server.Tests/NCode.Identity.Server.Tests.csproj -m:2 --filter "FullyQualifiedName~SecretProtectionRegistrationTests|FullyQualifiedName~DeveloperSecretPruningSeedHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 14. Management API

- Files: `src/NCode.Identity.OpenId.Management.Abstractions/Contracts/EncryptionKeys/EncryptionRootKeyResource.cs`,
  `EncryptionRootKeyRequest.cs`, `EncryptionRootKeyChangeResource.cs`, `EncryptionRootKeyStates.cs` (new),
  `EncryptionKeysResource.cs`, `EncryptionKeyResource.cs`, `Auditing/IManagementAuditRecorder.cs`,
  `PublicAPI.Unshipped.txt`; `src/NCode.Identity.OpenId.Management/Endpoints/EncryptionKeys/EncryptionKeyEndpoints.cs`
  (shared mapping; `GetAsync` takes `IOwnerRootKeyManager?`; new `SetRootKeyAsync`, `RemoveRootKeyAsync`,
  `ValidateRootKeyAsync`), `Endpoints/Tenants/TenantApiEndpointHandler.cs` (constructor takes `IOwnerRootKeyManager`;
  three routes and handlers; route comment), `Endpoints/Servers/ServerApiEndpointHandler.cs` (passes no manager),
  `Endpoints/OwnerRootKeyUnavailableEndpointFilter.cs` (new), `Endpoints/ManagementRootGroup.cs`,
  `Auditing/DefaultManagementAuditRecorder.cs`, `Logging/EventIds.cs`, `Logging/Log.cs`; tests
  `tests/NCode.Identity.OpenId.Management.Tests/Endpoints/Tenants/TenantApiEndpointHandlerTests.cs`,
  `Endpoints/Servers/*` where the handler's construction or encryption key responses are asserted,
  `Endpoints/OwnerRootKeyUnavailableEndpointFilterTests.cs` (new), `Auditing/DefaultManagementAuditRecorderTests.cs`.
- Change: D12, D11 filter, D9 management audit. Tests: GET includes root key and providers; PUT 200 with rotation and
  audit (`root_key.set`, `master_key.rotated`/`root_key_changed`), 400 per invalid field, 422 on probe failure, 409 with
  Retry-After, 404 unknown tenant, 403 without `Update`; DELETE 204, 202 with `removing`; validate 200 with audit on a
  change and 404 without a root key; filter answers 503 with Retry-After and extensions.
- Pattern: `RekeyEncryptionKeysAsync`, `EncryptionKeyEndpoints.RekeyAsync`, `UnreadableSettingsEndpointFilter` and their
  tests.
- Done when: `dotnet test tests/NCode.Identity.OpenId.Management.Tests/NCode.Identity.OpenId.Management.Tests.csproj -m:2 --filter "FullyQualifiedName~TenantApiEndpointHandlerTests|FullyQualifiedName~ServerApiEndpointHandlerTests|FullyQualifiedName~OwnerRootKeyUnavailableEndpointFilterTests|FullyQualifiedName~DefaultManagementAuditRecorderTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 15. Integration test: bring, revoke, restore and remove a tenant root key

- Files: `tests/NCode.Identity.OpenId.Integration.Tests/Infrastructure/TestReferencedKeyWrapper.cs` (new; name
  `test-tenant-kms`, AES-GCM with `System.Security.Cryptography.AesGcm` per reference, `Revoke`/`Restore`),
  `tests/NCode.Identity.OpenId.Integration.Tests/Endpoints/TenantRootKeyTests.cs` (new, with its own
  `HostApplicationFactory` subclass that registers the wrapper as `IKeyWrapper` and sets
  `SecretProtection:OwnerRootKeys:Providers:0 = test-tenant-kms`).
- Change: one test sets the key (200; every key row names the reference and has no recovery copy; a client created
  before still authenticates); revokes it and validates (state `unavailable`, `root_key.unavailable` in the audit store);
  a management call that needs the tenant's protector answers 503; restores and validates (`active`, client
  authenticates); removes (204; rows back under the deployment root). A second test: PUT with a provider not in the list
  answers 400, and PUT within twice the refresh interval of a rekey answers 409.
- Pattern: `MasterKeyRotationTests.cs` (administrator client, factory subclass, `ClientAuthenticationProbe`).
- Done when: `dotnet test tests/NCode.Identity.OpenId.Integration.Tests/NCode.Identity.OpenId.Integration.Tests.csproj -m:2 --filter "FullyQualifiedName~TenantRootKeyTests|FullyQualifiedName~MasterKeyRotationTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 16. Console

- Files: `src/frontend/packages/api/src/contracts.ts`, `src/frontend/packages/store/src/managementApi.ts`,
  `src/frontend/packages/store/src/index.ts`, `src/frontend/packages/store/src/managementApi.test.ts`,
  `src/frontend/packages/ui/src/components/EncryptionKeyTable.tsx`, `.test.tsx`, `.stories.tsx`,
  `src/frontend/packages/ui/src/components/EncryptionRootKeyCard.tsx`, `.test.tsx`, `.stories.tsx` (new, presentational),
  `src/frontend/packages/ui/src/components/EncryptionRootKeyFormDialog.tsx`, `.test.tsx` (new),
  `src/frontend/packages/ui/src/index.ts`, `src/frontend/apps/mgmt/src/containers/OwnerEncryptionKeysTab.tsx`, `.test.tsx`.
- Change: D12 console; mutations `setTenantRootKey` (PUT), `removeTenantRootKey` (DELETE), `validateTenantRootKey` (POST)
  taking `tenantId`, each invalidating the tenant's `EncryptionKey` tag.
- Pattern: `rekeyEncryptionKeys`, `SecretFormDialog`, `ConfirmDialog`, `OwnerEncryptionKeysTab` tests.
- Done when: `pnpm --dir src/frontend exec turbo run typecheck lint test --filter=@ncode/api --filter=@ncode/store --filter=@ncode/ui --filter=@ncode/app-mgmt`
  and `pnpm --dir src/frontend run format:check` exit 0 (install first with
  `pwsh -NoProfile -File build/frontend-install.ps1` when `node_modules` is missing).

### [ ] 17. Chart

- Files: `charts/ncode-identity-host/values.yaml`, `values.schema.json`, `templates/_helpers.tpl`,
  `ci/tenant-root-keys-values.yaml` (new), `README.md` (new subsection "Tenant root keys" under secret protection:
  enabling providers, credentials, the grants a tenant gives the deployment's identity on its key for each cloud, the
  intervals, what revocation does, that no recovery copy exists).
- Change: D13.
- Pattern: the existing `awsWebIdentity`/`azureWorkloadIdentity`/`googleCloudCredentials` conditions.
- Done when: `pwsh -NoProfile -Command "if (Get-Command helm -ErrorAction SilentlyContinue) { helm lint charts/ncode-identity-host -f charts/ncode-identity-host/ci/tenant-root-keys-values.yaml; exit $LASTEXITCODE } else { exit 0 }"` exits 0.

### [ ] 18. Docs and changelog

- Files: `docs/capabilities/encryption-at-rest.md`, `docs/audit-records.md`, `CHANGELOG.md`.
- Change:
  - Encryption at rest: Status row names tenant root keys; Key decisions row adds ADR-1411 and ADR-1412; promise 9
    ("**A tenant can hold the key to its own data.** Its master keys are wrapped only by a key in its own KMS, with no
    operator recovery copy, and revoking or destroying that key makes its data unreadable everywhere within an hour.");
    the picture shows a tenant KMS wrapping one tenant's master key; new section "Tenant root keys (bring your own key)"
    after "The root of trust" (providers, set/change/remove, rotation and why, probe, states, intervals table
    `SecretProtection:OwnerRootKeys:` `Providers`/`ValidationInterval`/`RetryInterval`, failing closed with 503 and audit,
    backups, the management routes, console); Recovery section: a tenant-keyed master key has no recovery copy and the
    `secret-protection` command leaves it under the tenant key; "Why this design": the server-key bullet says it would
    stop a tenant from bringing its own root key; remove the #174 roadmap row; Further reading lists ADR-1411 and 1412.
  - Audit records: resource type `root_key`; the `key` category covers a tenant's root key; actor table names
    `root_key.*`; new "#### Root keys" section with the four events, resources, reasons, details and outcomes; the master
    key table adds reason `root_key_changed` and the `root_key_reference` detail.
  - CHANGELOG `## [Unreleased]`: under `### Added` a new entry "**A tenant brings its own root key.**" (providers,
    routes, scopes, probe, rotation, conformance, states, intervals, console, audit events, `IReferencedKeyWrapper`,
    `IOwnerRootKeyManager`, links `[#174](https://github.com/NCodeGroup/Identity/issues/174)`, ADR-1411, ADR-1412); edit
    the master key entry to name `root_key_changed` only if it lists reasons (it does not; leave it) and the offline
    rewrap entry to say a master key under a tenant's root key is left under it and never gains a recovery copy; under
    `### Security` a new entry "**A revoked tenant root key shreds the tenant's data and the server fails closed.**"
    (no operator recovery copy, re-validated hourly by default, fails closed within that period, `503`,
    `root_key.unavailable`). Present tense, catalog wording, no change-log phrasing.
- Pattern: the ADR-1407/1408 entries and sections.
- Done when: `pwsh -NoProfile -Command "./build/doc-comment-citations.ps1; if ($LASTEXITCODE) { exit $LASTEXITCODE }; ./build/adr-index.ps1; if ($LASTEXITCODE) { exit $LASTEXITCODE }; ./.template/Test-AdrIndex.ps1"` exits 0.

### [ ] 19. Whole-change check

- Files: none beyond fixes the checks require.
- Change: `dotnet build NCode.Identity.slnx -m:2` (warnings are errors), `dotnet csharpier check src tests`, then run
  once more the tests of steps 2–15 by project (one at a time, filtered as in each step), and
  `dotnet test tests/NCode.Identity.Secrets.Abstractions.Tests/NCode.Identity.Secrets.Abstractions.Tests.csproj -m:2 -- RunConfiguration.TreatNoTestsAsError=true`.
  Finish with `dotnet build-server shutdown`.
- Done when: `dotnet build NCode.Identity.slnx -m:2` and `dotnet csharpier check src tests` exit 0.

## Review checklist

- No text says "now", "no longer", "previously", "was", "Breaking", or carries a migration, upgrade, compatibility or
  rollback note (code comments, XML docs, ADRs, docs, CHANGELOG, test names, commit messages).
- `IReferencedKeyWrapper` is implemented by the three KMS adapters only; local, ephemeral and recovery do not.
- Every master key row of an `active`/`unavailable` owner root key names the key's wrapper and reference and has no
  recovery copy; no code path writes a recovery copy for such a row (factory, rewrapper, recovery-only mode).
- Setting or changing a key rotates the master key (unless the active key was already created under it); removal does
  not rotate; spacing is checked before any write.
- The probe uses its own context label and zeroes both buffers.
- `OwnerRootKeyUnavailableException` does not derive from `CryptographicException`; development pruning never deletes a
  tenant-keyed owner's keys.
- The ring never reuses owner-rooted material past `ValidationInterval`, refuses an `unavailable` binding, negative-caches
  for `RetryInterval`, and leaves deployment-rooted behavior unchanged.
- The validation service runs under its lease, handles each owner in its own try block, publishes audit after commit on
  a bounded token.
- Management responses carry no wrapped key, key check or material; 400/409/412/422/503 as decided; the server routes
  have no root key endpoints.
- `PublicAPI.Unshipped.txt` files are exact; no ADR citations in `///`; CSharpier clean; no `!` outside tests.
- The OpenId `InitialCreate` is the only migration of its context, BOM-less, and has no pending model changes.
- Chart mounts a provider's credentials for the root provider or a listed owner root key provider only.
- ADR-1411/1412 exist, are indexed, and the amended records' Status lines and index rows name them; bodies unchanged.
- CHANGELOG entries sit under `### Added` and `### Security` and link #174.

## Follow-ups

- Map `OwnerRootKeyUnavailableException` on the protocol endpoints to `temporarily_unavailable` where the specification
  defines it (authorization endpoint) instead of `server_error`.
- A per-tenant KMS credential (for a tenant whose key cannot grant the deployment's own identity), if a customer needs it.
