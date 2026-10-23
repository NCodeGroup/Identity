# Plan: close the secret protection review's remaining findings

## Goal

Fix the four findings the review of the secret protection key hierarchy (ADR-0228) left open, so no known defect of
that change remains:

1. `DefaultMasterKeyRing` gives a confusing exception for `default(SecretOwner)`.
2. A blank `SecretProtection:RootKeyProvider` crashes at warmup, although production safety reports a blank value as
   "not set, so the ephemeral root key is used".
3. The four management `GetPublicKeyAsync` endpoints load the owner's master keys **before** authorizing the caller,
   which contradicts ADR-0228 decision 7 ("A caller acquires a protector only when it will use one — after the request
   is authorized and validated …").
4. The LocalDev AppHost writes the development root key in place, so a crash mid-write leaves an empty key file that
   every later run reuses.

## Out of scope

- A test project for `NCode.Identity.OpenId.LocalDev` (a development-only Aspire AppHost that is never packed).
- Any change to ADR-0228's decisions; the fixes make the code match them.

## Decisions

- **F1 — A default owner is rejected at the ring's entry points.** `SecretOwner`'s constructor validates both parts,
  but `default(SecretOwner)` bypasses it. `DefaultMasterKeyRing.GetProtectorAsync` and `Evict` throw
  `ArgumentException` (parameter `owner`) for it, with a message that names the factories. The comparer stays as is.
- **F2 — A blank provider name means unset.** `DefaultRootKeyProviderCatalog.GetConfigured` treats a null, empty or
  whitespace `RootKeyProvider` as unset (ephemeral), as production safety already does.
- **F3 — Map after authorization, asynchronously.** `ManagementApiEndpointHandler` gains a `ProcessGetAsync` overload
  whose mapper is `Func<TValue, CancellationToken, ValueTask<TResponse>>` and runs only after authorization succeeds;
  the existing synchronous-mapper overload delegates to it, so there is one implementation. Each `GetPublicKeyAsync`
  acquires its protector inside that mapper. A second, explicit authorization before `ProcessGetAsync` was rejected:
  it authorizes every request twice.
- **F4 — The development root key appears atomically.** LocalDev writes the key to a temporary file in the same
  directory and moves it into place without overwriting; a zero-length `root-key` (left by the old in-place write) is
  deleted and regenerated, which is safe because a key being written is never visible under the final name.

## Context for the executor

- Repository conventions: `.github/instructions/csharp-production.instructions.md` (production) and
  `.github/instructions/csharp-testing.instructions.md` (tests: xUnit, Moq `MockBehavior.Strict` with `.Verifiable()`,
  `Method_State_Expected` names, `#region` per member under test). Format every edited `.cs` file with
  `dotnet csharpier format <file>`.
- Public API changes in a package are listed in that project's `PublicAPI.Unshipped.txt`; the build fails (RS0016/RS0017)
  until the entry matches exactly.
- Builds treat warnings as errors. Never run two `dotnet build`/`dotnet test` at the same time.
- A strict mock with no setup for a member throws when that member is called — that is how a test proves a call does
  **not** happen.
- Do not edit `PLAN.md` beyond marking your own step; do not commit.

## Steps

### [ ] 1. Reject `default(SecretOwner)` in the master key ring
- Files: `src/NCode.Identity.Secrets.Persistence/Protection/DefaultMasterKeyRing.cs`,
  `tests/NCode.Identity.Secrets.Persistence.Tests/Protection/DefaultMasterKeyRingTests.cs`
- Change (F1): add `private static void ThrowIfDefault(SecretOwner owner)` that throws
  `new ArgumentException("The owner is the default value of SecretOwner, which identifies no owner; create it with SecretOwner.ForServer, SecretOwner.ForTenant or its constructor.", nameof(owner))`
  when `owner.OwnerType is null`. Call it first in `GetProtectorAsync` (before `ArgumentNullException.ThrowIfNull`)
  and first in `Evict`. Tests: `GetProtector_DefaultOwner_Throws` (in the `GetProtectorAsync Tests` region; asserts
  `ArgumentException` with `ParamName == "owner"`, and that no store manager is created — queue none) and
  `Evict_DefaultOwner_Throws` (in the `Evict Tests` region).
- Done when: `$t = Get-Content -Raw tests/NCode.Identity.Secrets.Persistence.Tests/Protection/DefaultMasterKeyRingTests.cs; foreach ($n in 'GetProtector_DefaultOwner_Throws','Evict_DefaultOwner_Throws') { if (-not $t.Contains($n)) { exit 1 } }; dotnet test tests/NCode.Identity.Secrets.Persistence.Tests/NCode.Identity.Secrets.Persistence.Tests.csproj --nologo --filter "FullyQualifiedName~DefaultMasterKeyRingTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 2. Treat a blank root key provider name as unset
- Files: `src/NCode.Identity.Secrets.Persistence/Protection/DefaultRootKeyProviderCatalog.cs`,
  `src/NCode.Identity.Secrets.Persistence.Abstractions/Options/SecretProtectionOptions.cs` (doc only),
  `tests/NCode.Identity.Secrets.Persistence.Tests/Protection/DefaultRootKeyProviderCatalogTests.cs`
- Change (F2): `GetConfigured()` reads `ProtectionOptions.Value.RootKeyProvider` into a local and calls
  `Get(string.IsNullOrWhiteSpace(name) ? RootKeyProviderNames.Ephemeral : name)`. In `SecretProtectionOptions`
  `RootKeyProvider`'s summary, change "When unset, the ephemeral provider is used" to "When unset or blank, the
  ephemeral provider is used". Test: `GetConfigured_Blank_ReturnsEphemeral` as a `[Theory]` with `[InlineData("")]`
  and `[InlineData("  ")]` in the `GetConfigured Tests` region, following `GetConfigured_Unset_ReturnsEphemeral`.
- Done when: `$t = Get-Content -Raw tests/NCode.Identity.Secrets.Persistence.Tests/Protection/DefaultRootKeyProviderCatalogTests.cs; if (-not $t.Contains('GetConfigured_Blank_ReturnsEphemeral')) { exit 1 }; dotnet test tests/NCode.Identity.Secrets.Persistence.Tests/NCode.Identity.Secrets.Persistence.Tests.csproj --nologo --filter "FullyQualifiedName~DefaultRootKeyProviderCatalogTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 3. Map a GET after authorization with an asynchronous mapper; use it for client public keys
- Files: `src/NCode.Identity.OpenId.Management.Abstractions/Endpoints/ManagementApiEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Management.Abstractions/PublicAPI.Unshipped.txt`,
  `src/NCode.Identity.OpenId.Management/Endpoints/Clients/ClientApiEndpointHandler.cs` (`GetPublicKeyAsync` only),
  `tests/NCode.Identity.OpenId.Management.Tests/Endpoints/Clients/ClientApiEndpointHandlerTests.cs`
  (`GetPublicKeyAsync Tests` region only)
- Change (F3):
  1. In `ManagementApiEndpointHandler`, add after the `ProcessGetAsync<TValue, TResponse>(HttpContext, TValue?,
     object?, IAuthorizationRequirement, Func<TValue, TResponse>)` overload a new
     `protected internal virtual async ValueTask<IResult> ProcessGetAsync<TValue, TResponse>(HttpContext httpContext,
     TValue? valueOrNull, object? authorizationResource, IAuthorizationRequirement authorizationRequirement,
     Func<TValue, CancellationToken, ValueTask<TResponse>> mapperAsync, CancellationToken cancellationToken)`.
     Move the body of the synchronous overload into it unchanged except `var response = await mapperAsync(valueOrNull,
     cancellationToken);`. Its XML doc is the synchronous overload's, plus: the mapper runs only after authorization
     succeeds, so it may do work an unauthorized caller must not cause (such as loading key material); add the
     `<param>` for `cancellationToken`. Make the synchronous overload an expression body that calls the new one with
     `(value, _) => ValueTask.FromResult(mapper(value))` and `CancellationToken.None`.
  2. Add the new member to `PublicAPI.Unshipped.txt` in the format of the existing `ProcessGetAsync` lines in
     `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` (let the build's RS0016 message give the exact text if unsure).
  3. `ClientApiEndpointHandler.GetPublicKeyAsync`: delete the protector acquisition before `ProcessGetAsync`, and pass
     `cancellationToken` plus an async mapper that acquires it and converts:
     `async (x, ct) => SecretPublicKeyConverter.Convert(await SecretProtectorProvider.GetProtectorForSecretsAsync(SecretOwner.ForTenant(clientSecret.TenantId), [x.Value], ct), x.Value)`
     (a multi-line lambda with a local is fine). Keep the comment that the owner is the secret's tenant, and add one
     line: the protector is acquired only after authorization, so a caller who may not read the secret never loads
     its owner's master keys.
  4. Test: rename `GetPublicKeyAsync_WhenForbidden_ReturnsForbid` to
     `GetPublicKeyAsync_WhenForbidden_ReturnsForbidWithoutProtector` and delete its `SetupSecretProtector();` line, so
     the strict mock fails the test if a protector is acquired. The other `GetPublicKeyAsync` tests stay as they are.
- Done when: `$t = Get-Content -Raw tests/NCode.Identity.OpenId.Management.Tests/Endpoints/Clients/ClientApiEndpointHandlerTests.cs; if (-not $t.Contains('GetPublicKeyAsync_WhenForbidden_ReturnsForbidWithoutProtector')) { exit 1 }; dotnet test tests/NCode.Identity.OpenId.Management.Tests/NCode.Identity.OpenId.Management.Tests.csproj --nologo --filter "FullyQualifiedName~ClientApiEndpointHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 4. Acquire the protector after authorization in the other public-key endpoints
- Files: `GetPublicKeyAsync` in `src/NCode.Identity.OpenId.Management/Endpoints/ResourceServers/ResourceServerApiEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Management/Endpoints/Servers/ServerApiEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Management/Endpoints/Tenants/TenantApiEndpointHandler.cs`; the forbidden test of each in
  `tests/NCode.Identity.OpenId.Management.Tests/Endpoints/{ResourceServers,Servers,Tenants}/*ApiEndpointHandlerTests.cs`
- Change (F3): apply Step 3's items 3 and 4 to each, keeping each handler's own owner expression (a tenant owner for
  resource servers and tenants, the server owner for servers — use exactly what the handler passes today).
- Pattern: Step 3's `ClientApiEndpointHandler.GetPublicKeyAsync` and its forbidden test.
- Done when: `foreach ($f in 'ResourceServers/ResourceServerApiEndpointHandlerTests.cs','Servers/ServerApiEndpointHandlerTests.cs','Tenants/TenantApiEndpointHandlerTests.cs') { $t = Get-Content -Raw "tests/NCode.Identity.OpenId.Management.Tests/Endpoints/$f"; if (-not $t.Contains('GetPublicKeyAsync_WhenForbidden_ReturnsForbidWithoutProtector')) { exit 1 } }; if (git grep -n -E '^        var protector = await SecretProtectorProvider\.GetProtectorForSecretsAsync' -- src/NCode.Identity.OpenId.Management/Endpoints) { exit 1 }; dotnet test tests/NCode.Identity.OpenId.Management.Tests/NCode.Identity.OpenId.Management.Tests.csproj --nologo --filter "FullyQualifiedName~ApiEndpointHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 5. Write the LocalDev development root key atomically
- Files: `src/NCode.Identity.OpenId.LocalDev/Program.cs` (`EnsureDevelopmentRootKey` and the comment above it only)
- Change (F4):
  1. After computing `path`: when `File.Exists(path)` and `new FileInfo(path).Length == 0`, delete it (an empty file
     can only be left by an interrupted write of the old code); otherwise, when it exists, return `path` as today.
  2. Write the encoded key to `Path.Combine(directory, $"root-key.{Guid.NewGuid():N}.tmp")` using the existing
     `FileStreamOptions` (`CreateNew`, `UnixCreateMode` owner read/write), `stream.Write(text[..written])` and
     `stream.Flush(flushToDisk: true)` before the stream is disposed.
  3. Then `File.Move(temporaryPath, path, overwrite: false)`; on `IOException` when `File.Exists(path)` another AppHost
     won the race — use its key. Delete the temporary file in the `finally` when it still exists (wrap in its own
     `try`/`catch (IOException)` so cleanup never hides the real outcome), next to the existing `ZeroMemory` calls.
  4. Remove the now-unneeded `catch (IOException) when (File.Exists(path))` around opening the stream (the temporary
     name is unique). Update the comment above the function: the key is written to a temporary file and moved into
     place, so a crash never leaves a partial key under the final name; an empty file left by an older run is
     replaced.
- Done when: `$t = Get-Content -Raw src/NCode.Identity.OpenId.LocalDev/Program.cs; if (-not ($t.Contains('File.Move(') -and $t.Contains('overwrite: false') -and $t.Contains('flushToDisk: true') -and $t.Contains('.Length == 0'))) { exit 1 }; dotnet csharpier check src/NCode.Identity.OpenId.LocalDev/Program.cs; if ($LASTEXITCODE) { exit 1 }; dotnet build src/NCode.Identity.OpenId.LocalDev/NCode.Identity.OpenId.LocalDev.csproj --nologo`

### [ ] 6. Record the asynchronous-mapper overload in the changelog
- Files: `CHANGELOG.md`
- Change: in `[Unreleased]`, directly after the bullet that starts `- **Protect your own data.**`, add a blank line and
  `- \`ManagementApiEndpointHandler.ProcessGetAsync\` has an overload whose asynchronous mapper runs only after
  authorization succeeds; the secret public-key endpoints use it, so a caller who may not read a secret never loads
  its owner's master keys.` wrapped at 120 columns with a 2-space continuation indent (write the backticks
  literally, without the backslashes shown here).
- Done when: `if ((Get-Content -Raw CHANGELOG.md).Contains('asynchronous mapper runs only after')) { exit 0 } else { exit 1 }`

### [ ] 7. Run the Definition of Done
- Files: none (verification step)
- Change: `pwsh -NoProfile -File ./build/dod.ps1 -Fast`
- Done when: `pwsh -NoProfile -File ./build/dod.ps1 -Fast`

### [ ] 8. Review the work
- Files: none (coordinator)
- Change: placeholder — run the reviewer over `git diff 70f5ec26 HEAD` and queue fix steps.
- Done when: `exit 1`

### [ ] 9. Retire the plan
- Files: `PLAN.md` (delete); history (coordinator)
- Change: placeholder — promote any decision not already carried by the code, the ADR or the changelog; delete
  `PLAN.md`; fold every commit since `70f5ec26` into the single feature commit (`git reset --soft` onto the parent's
  tip and recommit with the feature commit's message); run `./build/dod.ps1`; force-push with a lease.
- Done when: `exit 1`
