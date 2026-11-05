# Plan: feat: keep a tenant's signing keys in a key management service that signs every token

Issue: #165

## Goal

A tenant can opt in, through the open settings vocabulary, to signing keys that never leave a key management service
(AWS KMS, Azure Key Vault / Managed HSM, Google Cloud KMS). The server stores only the key's public half and a
reference to it, signs every token through one asynchronous seam that runs from the signature algorithm to every caller
that signs, verifies each remote signature with the stored public key before a token carries it, publishes the stored
public keys in the JWKS without calling the KMS, provisions and rolls the remote keys over (each successor a new key
version), hands a tenant's keys over to their new custody after the activation delay when the tenant changes custody,
and destroys a remote key in its KMS once its record leaves the store.

## Context for the executor

Work only in `/home/user/Identity/.claude/worktrees/pr-w` (branch `claude/remote-signing-keys`). `cd` there for every
command; edit files only under it.

### Rules that bind every step

- Read first: `AGENTS.md`; `.github/instructions/repository.instructions.md` (especially "Standing direction from the
  project owner" and "The product is described as it is"); `.github/instructions/csharp.instructions.md`,
  `.github/instructions/csharp-production.instructions.md`; `.github/instructions/csharp-tests.instructions.md`,
  `.github/instructions/csharp-testing.instructions.md`; `docs/decisions/README.md`.
- No temporal content anywhere (code comments, XML docs, ADRs, CHANGELOG, docs, test names, commit messages): never
  "now", "no longer", "previously", "was", "new"/"newly" against an earlier state, "replaces", "renamed", "Breaking",
  migration/upgrade/compat/rollback notes. Describe the product as it is.
- Resource rules: `export MSBUILDDISABLENODEREUSE=1` in every shell before `dotnet`; build and test with `-m:2`; run
  only the test projects a step names, filtered as its Done-when says; never run two `dotnet`/`pnpm` commands at once,
  never in the background; never run `./build/dod.ps1`; integration tests only with the filter Step 11 gives; finish
  the last step with `dotnet build-server shutdown`.
- An untracked `/home/user/Identity/Directory.Build.rsp` pins Roslyn 5.6.0 like CI; never stage any `.rsp` file, never
  stage `PLAN.md`.
- `PublicAPI.Unshipped.txt` stays exact: after each change to a public surface, build the project, add exactly the
  lines `RS0016` reports and remove exactly the lines `RS0017` reports (keep the file's sort order).
- No ADR citations inside `///` XML doc comments; put `// See ADR-1413.` (or `ADR-1414`) on its own line above the
  member or type. Check with `pwsh -NoProfile -File build/doc-comment-citations.ps1`.
- No null-forgiving operator (`!`) outside tests. Vocabularies are open (ADR-1150): descriptors, catalogs and
  registered sources, never an `enum` or a `switch` over a vocabulary. The three `switch`-like mappings this plan
  allows are over a provider API's own closed set (Google's three digest fields, the KMS SDK constant classes), and
  each carries a one-line comment saying so.
- Format with `dotnet csharpier format <changed dirs>`; before every commit run `dotnet csharpier check src tests`
  (it must report no unformatted file).
- Commit each step as `Plan step N: <title>` (conventional subject in the final squash comes from the plan title), stage
  only the step's files, never add `Co-authored-by`, `Claude-Session`, "Generated with" or any AI attribution, never
  `!` in a subject, never change `git config user.*`. Before the first commit run `git config core.hooksPath` and set
  it to `.githooks` only when it prints nothing and no hooks of the clone's own exist.
- No EF model change is made by this plan (a remote key is a `PersistedSecret` with existing columns), so no
  migration and no re-scaffold.
- A parallel branch (issue #174, tenant root keys) edits the three `NCode.Identity.Secrets.KeyWrapping.*` packages,
  `docs/capabilities/encryption-at-rest.md` and `CHANGELOG.md`. Keep edits there additive: new files for new types,
  small targeted edits to existing files, no reflowing of paragraphs you do not change.

### The code this plan builds on (read before the step that touches it)

- Secret key model: `src/NCode.Identity.Secrets.Abstractions/Keys/{SecretKey,AsymmetricSecretKey,RsaSecretKey,EccSecretKey,KeyMetadata}.cs`;
  public-only keys `src/NCode.Identity.Secrets/Keys/{PublicRsaSecretKey,PublicEccSecretKey}.cs` (the pattern for the
  remote key classes); factory `src/NCode.Identity.Secrets.Abstractions/KeyMaterial/ISecretKeyFactory.cs` and
  `src/NCode.Identity.Secrets/KeyMaterial/DefaultSecretKeyFactory.cs` (`CreateRsaSpki`, `CreateEccSpki`,
  `CreateEccPublicKey`, `GetCurveName`).
- Key-type seam: `src/NCode.Identity.Secrets.Abstractions/KeyTypes/ISecretKeyTypeDescriptor.cs`,
  `src/NCode.Identity.Secrets/KeyTypes/{SecretKeyTypeDescriptor,AsymmetricSecretKeyTypeDescriptor,RsaSecretKeyTypeDescriptor,EccSecretKeyTypeDescriptor}.cs`.
- Encodings: `src/NCode.Identity.Secrets.Abstractions/SecretEncodings.cs` (`IsPublic` is also used to mean "stored
  unprotected").
- Registration: `src/NCode.Identity.Secrets/DefaultRegistration.cs` (`AddSecretsLibrary`),
  `src/NCode.Identity.Secrets.Persistence/DefaultRegistration.cs`.
- Persistence: `src/NCode.Identity.Secrets.Persistence.Abstractions/DataContracts/PersistedSecret.cs`,
  `ISecretGenerator.cs`, `GenerateSecretRequest.cs`, `SecretOrigins.cs`, `SecretLifecycle.cs`,
  `Protection/SecretProtectorProviderExtensions.cs`; `src/NCode.Identity.Secrets.Persistence/{DefaultSecretSerializer,DefaultSecretGenerator}.cs`,
  `Logging/{Log,EventIds}.cs`.
- JOSE signing: `src/NCode.Identity.Jose.Abstractions/Algorithms/{SignatureAlgorithm,KeyedAlgorithm}.cs`,
  `src/NCode.Identity.Jose.Abstractions/{IJoseEncoder,IJoseSerializer}.cs`, `DPoP/IDPoPProofService.cs`;
  `src/NCode.Identity.Jose/JoseSerializer.Jws.Encode.cs` (compact), `JoseSerializer.Jws.Json.cs` (flattened/general),
  `JoseSerializer.cs` (`SerializeToUtf8`, `EncodeJose`, `WriteCompactSegment`), `Encoders/{CommonJoseEncoder,JoseSigningEncoder,JoseEncryptionEncoder}.cs`,
  `DPoP/DefaultDPoPProofService.cs`, `Algorithms/Signature/{RsaSignatureAlgorithm,EccSignatureAlgorithm}.cs`,
  `Credentials/DefaultCredentialSelector.cs`.
- JWT and SD-JWT: `src/NCode.Identity.Jwt.Abstractions/Encoding/IJwtEncoder.cs`,
  `src/NCode.Identity.Jwt/Encoding/DefaultJwtEncoder.cs`; `src/NCode.Identity.Jose.SdJwt/Issuance/{ISdJwtIssuer,DefaultSdJwtIssuer}.cs`,
  `src/NCode.Identity.Jose.SdJwt/Presentation/{ISdJwtHolder,DefaultSdJwtHolder}.cs`.
- Callers that sign: `src/NCode.Identity.OpenId.Authentication/Tokens/DefaultTokenService.cs`,
  `Sessions/DefaultBackChannelLogoutNotifier.cs`, `Endpoints/Introspection/DefaultIntrospectionEndpointProvider.cs`,
  `Endpoints/UserInfo/DefaultUserInfoEndpointProvider.cs`,
  `Endpoints/Authorization/Results/JwtAuthorizationResponseModeHandler.cs`;
  `src/NCode.Identity.OpenId.Client/Authentication/PrivateKeyJwtClientAuthenticator.cs`,
  `ProofOfPossession/{DPoPProofKey,DefaultDPoPProofFactory}.cs`, `Tokens/ClientRequestSender.cs`,
  `UserInfo/DefaultUserInfoClient.cs`. Find every other caller with
  `grep -rn "CreateEncoder\|\.Encode(\|JwtEncoder\|CreateProof\|\.Issue(\|CreatePresentation(" src tests benchmarks --include=*.cs`.
- JWKS: `src/NCode.Identity.OpenId.Authentication/Endpoints/Jwks/DefaultJwksEndpointHandler.cs` (exports each key
  through its descriptor; a remote key exports its stored public half with no change here).
- Tenant keys (ADR-1169, ADR-1403, ADR-1404): `src/NCode.Identity.OpenId.Core/Tenants/Keys/` —
  `DefaultTenantKeyPolicy.cs`, `ITenantKeyPolicy.cs`, `DefaultTenantKeyRolloverPlanner.cs`, `DefaultTenantKeyRollover.cs`,
  `TenantKeySpecification.cs`, `TenantKeyRetirement.cs`, `TenantKeyRolloverResult.cs`, `TenantKeyChangeReasons.cs`,
  `TenantKeyAuditEvent.cs`, `TenantKeySeedHandler.cs`, `TenantKeyRolloverService.cs`,
  `TenantKeyReconciliationHandler.cs`; `src/NCode.Identity.OpenId.Core/PostCommitWork.cs` (ADR-1351 bounded token).
- Settings vocabulary: `src/NCode.Identity.OpenId.Abstractions/Settings/{OpenIdSettingNames,OpenIdSettingKeys}.cs`;
  the pattern for a data source whose rule is a registered vocabulary:
  `src/NCode.Identity.OpenId.Core/Tenants/TenantEnvironmentSettingDescriptorDataSource.cs`, registered in
  `src/NCode.Identity.OpenId.Core/Settings/DefaultRegistration.cs`.
- KMS packages (ADR-1235, ADR-1260, ADR-1279): `src/NCode.Identity.Secrets.KeyWrapping.AwsKms/`,
  `.AzureKeyVault/`, `.GoogleCloudKms/` — each has `DefaultRegistration.cs`, `Facades/` (client factories, the seam
  the tests fake), `Options/` (+ validator), a key-name rule class (`AwsKmsKeyArn`, `AzureKeyVaultKeyId`,
  `GoogleCloudKmsKeyName`), and `GoogleCloudKmsChecksum`. Tests (Moq, strict) in
  `tests/NCode.Identity.Secrets.KeyWrapping.*.Tests/` show how each client is faked.
- Management deletes: `src/NCode.Identity.OpenId.Management/Endpoints/Tenants/TenantApiEndpointHandler.cs`
  (`DeleteSecretAsync`, `DeleteTenantAsync`).
- Development pruning: `src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperSecretPruningSeedHandler.cs`.
- Host wiring: `src/NCode.Identity.OpenId.Host/Program.cs` (lines registering `Add*KeyWrapper`), host docs
  `src/NCode.Identity.OpenId.Host/README.md` ("### Secret protection").
- Integration patterns: `tests/NCode.Identity.OpenId.Integration.Tests/Endpoints/TenantKeyRolloverTests.cs` (a
  production-keys factory, bootstrap admin, JWKS and secrets helpers), `TenantKeyProvisioningTests.cs` (PATCH
  `{TenantApiPath}/settings`), `Infrastructure/HostApplicationFactory.cs`.
- Docs to keep true: `docs/capabilities/{README,key-rollover,encryption-at-rest,selective-disclosure}.md`,
  `docs/audit-records.md` (tenant.key.* rows), `.github/instructions/csharp-testing.instructions.md` (lines ~377–380
  name `CreateEncoder(...).Encode(payload)`), `AGENTS.md` (Layout, Secrets bullet), `README.md` (packages table row
  "Secrets"), `CHANGELOG.md`.

### Test command shape

`dotnet test tests/<Project>/<Project>.csproj -m:2 --filter "FullyQualifiedName~<A>|FullyQualifiedName~<B>" -- RunConfiguration.TreatNoTestsAsError=true`

Steps 3–5 change public signatures that downstream projects call; a downstream project does not compile between the
step that changes a signature and the step that updates its callers. Step 5 ends with a whole-solution build, after
which every step keeps the solution building.

## Decisions

D1. **A remote key is a family key class, not a separate base.** `RemoteRsaSecretKey : RsaSecretKey, IRemoteSecretKey`
and `RemoteEccSecretKey : EccSecretKey, IRemoteSecretKey` (internal sealed, `src/NCode.Identity.Secrets/Keys/`), each
holding the SubjectPublicKeyInfo (SPKI) exactly as `PublicRsaSecretKey`/`PublicEccSecretKey` do, so `ExportRSA()`,
`ExportECDsa()` and `ExportECDiffieHellman()` return public-only objects. Reason: the signature algorithms key off
`RsaSecretKey`/`EccSecretKey` (`AlgorithmKeyType`), the curve checks read `CurveName`, the descriptors export the JWK
from `ExportRSA`/`ExportECDsa`, and verification uses the public half; deriving from the family classes keeps every one
of them working unchanged. The issue's `RemoteAsymmetricSecretKey : AsymmetricSecretKey` would be refused by every
algorithm's key-type check. `IRemoteSecretKey` (public, `NCode.Identity.Secrets.Keys`, in Secrets.Abstractions) marks
the key:

```csharp
public interface IRemoteSecretKey
{
    RemoteKeyReference Reference { get; }
    RemoteSigningAlgorithm Algorithm { get; }
    ValueTask<int> SignAsync(string algorithm, ReadOnlyMemory<byte> signingInput, Memory<byte> signature,
        CancellationToken cancellationToken);
}
```

D2. **The remote vocabulary lives in `NCode.Identity.Secrets.Abstractions`, namespace `NCode.Identity.Secrets.RemoteKeys`,
folder `RemoteKeys/`** (public, `[PublicAPI]`):

- `RemoteKeyReference` — `readonly record struct` with a constructor `(string provider, string keyId)` that throws
  `ArgumentException` unless `IsValidProviderName(provider)` and `keyId` is non-empty; properties `Provider`, `KeyId`;
  `ToString()` returns `"{Provider}:{KeyId}"`; `static bool TryParse([NotNullWhen(true)] string? value, out RemoteKeyReference reference)`
  splits at the first `':'` (a key id may contain `':'`, as an AWS ARN does); `static bool IsValidProviderName([NotNullWhen(true)] string? name)`
  matches `^[a-z0-9]+(-[a-z0-9]+)*\z` (generated regex).
- `RemoteSigningAlgorithm` — `sealed record`, one row of the open mapping vocabulary: `required string Provider`,
  `required string Algorithm` (JOSE `alg`), `required string SecretType` (`SecretKeyTypes.Rsa`/`Ecc`),
  `required int KeySizeBits`, `string? Curve` (JWK `crv`, `null` for RSA), `required string KeySpec` (the provider's
  key specification), `required string SigningAlgorithm` (the provider's signing algorithm name),
  `HashAlgorithmName? DigestAlgorithm` (the hash the service signs a digest of; `null` means the service receives the
  full signing input), `bool IsDerEncodedSignature` (the service returns an ECDSA signature as a DER `SEQUENCE`).
- `IRemoteSigningAlgorithmSource` — `IEnumerable<RemoteSigningAlgorithm> Algorithms { get; }`; a package registers one
  with `TryAddEnumerable` (distinct implementation types), so a consumer or a sibling package adds rows by registering
  its own source.
- `IRemoteKeyProvider` — `string Name { get; }`;
  `ValueTask<RemoteKeyCreationResult> CreateKeyAsync(RemoteKeyCreationRequest request, CancellationToken cancellationToken)`;
  `ValueTask<byte[]> SignAsync(RemoteSignatureRequest request, CancellationToken cancellationToken)` (returns the
  service's signature bytes as it returns them);
  `ValueTask DestroyKeyAsync(string keyId, CancellationToken cancellationToken)` (idempotent: a key already destroyed,
  scheduled for destruction, disabled or missing completes).
- `RemoteKeyCreationRequest` — `sealed class`: `required string OwnerId`, `required string SecretId`,
  `required RemoteSigningAlgorithm Algorithm`, and `string GroupName` (computed, cached): `"ncid-"` + the first 16 bytes,
  lowercase hex, of SHA-256 over UTF-8 `"{OwnerId}\n{Algorithm.Provider}\n{Algorithm.Algorithm}\n{Algorithm.KeySpec}\n{Algorithm.KeySizeBits}\n{Algorithm.Curve}"`
  (36 characters: valid as a Google crypto key id and an Azure key name). The group is the stable KMS key that each
  generation of one owner's key for one algorithm and spec is a version of.
- `RemoteKeyCreationResult` — `sealed class`: `required string KeyId`, `required ReadOnlyMemory<byte> SubjectPublicKeyInfo`.
- `RemoteSignatureRequest` — `sealed class`: `required string KeyId`, `required RemoteSigningAlgorithm Algorithm`,
  `required ReadOnlyMemory<byte> Data` (the digest when `Algorithm.DigestAlgorithm` is set, the signing input otherwise).
- `IRemoteKeyProviderCatalog` — `IReadOnlyCollection<IRemoteKeyProvider> Providers { get; }`;
  `bool TryGetProvider(string name, [NotNullWhen(true)] out IRemoteKeyProvider? provider)`;
  `bool TryGetAlgorithm(string provider, string algorithm, string secretType, int keySizeBits, string? curve, [NotNullWhen(true)] out RemoteSigningAlgorithm? remoteAlgorithm)`
  (ordinal comparisons).
- `RemoteKeyProviderNames` — constants `AwsKms = "aws-kms"`, `AzureKeyVault = "azure-key-vault"`,
  `GoogleCloudKms = "google-cloud-kms"` (the same names as `KeyWrapperNames`, so one name selects one KMS).

D3. **Storage: the `remote` secret encoding, existing columns.** `SecretEncodings.Remote = "remote"`,
`SecretEncodings.IsRemote(string)`, and `SecretEncodings.IsUnprotected(string) => IsPublic(e) || IsRemote(e)`. A remote
`PersistedSecret` has `SecretType` `rsa`/`ecc`, `SecretEncoding` `remote`, `EncodedValue` the reference's
`ToString()` (plain text, nothing secret), `PublicKey` the base64url SPKI read from the KMS when the key is created,
`KeySizeBits`, `Origin` `generated`, `ManagedBy` as the caller sets. Every caller that uses `IsPublic` to mean "not under
a master key" uses `IsUnprotected` instead (`SecretProtectorProviderExtensions.GetProtectorForSecretsAsync`,
`DeveloperSecretPruningSeedHandler`, `DefaultSecretSerializer`'s private-material branch). `IsPublic` keeps meaning
"public material only" (the tenant key policy's "cannot sign" check stays `IsPublic`). Reason: no schema change; the
stored public key is the JWKS cache, so neither publishing nor verifying ever calls the KMS.

D4. **Deserialization fails closed and binds the key to its mapping row.** `ISecretKeyTypeDescriptor` gains
`bool SupportsRemoteKeys { get; }` and
`SecretKey CreateRemoteKey(KeyMetadata metadata, IRemoteKeyProvider provider, RemoteKeyReference reference, RemoteSigningAlgorithm algorithm, ReadOnlySpan<byte> subjectPublicKeyInfo)`;
`SecretKeyTypeDescriptor` defaults `SupportsRemoteKeys => false` and `CreateRemoteKey` throws
`NotSupportedException($"The '{KeyType}' key type cannot be held by a remote key provider.")`; `RsaSecretKeyTypeDescriptor`
and `EccSecretKeyTypeDescriptor` override both and call `ISecretKeyFactory.CreateRsaRemote` / `CreateEccRemote` (new
factory members with the same parameters, `subjectPublicKeyInfo` first after `metadata`). The factory validates that the
SPKI's size equals `algorithm.KeySizeBits` and, for ECC, that the catalog's curve name equals `algorithm.Curve`
(`ArgumentException` otherwise). `DefaultSecretSerializer` (gains `IRemoteKeyProviderCatalog`), for a remote secret read
with a protector: parses the reference (`FormatException` when malformed), resolves the provider
(`InvalidOperationException` naming the secret and the provider when not registered), requires `PublicKey`
(`InvalidOperationException`), deserializes the stored public key as SPKI through the descriptor to learn its size and
curve, requires a bound `Algorithm` (`InvalidOperationException`), resolves the row with
`catalog.TryGetAlgorithm(provider, persisted.Algorithm, persisted.SecretType, size, curve)` (`InvalidOperationException`
when absent), and returns `descriptor.CreateRemoteKey(...)`. `DeserializePublicSecret` is unchanged (it reads the stored
public key). Reason: a key the host cannot sign with is refused when the tenant loads, never at the first token.

D5. **One signing helper in `NCode.Identity.Secrets`.** `RemoteKeys/RemoteKeySigning` (internal static) implements
`IRemoteSecretKey.SignAsync` for both key classes: it refuses an `algorithm` other than the row's
(`InvalidOperationException`); hashes the signing input with `row.DigestAlgorithm` when set
(`CryptographicOperations.HashData(HashAlgorithmName, ReadOnlySpan<byte>, Span<byte>)` into a stack/rented buffer), else
passes the input; calls `provider.SignAsync(new RemoteSignatureRequest { KeyId = reference.KeyId, Algorithm = row, Data = ... })`;
when `row.IsDerEncodedSignature`, converts the DER `SEQUENCE { r INTEGER, s INTEGER }` to IEEE P1363 `r‖s`, each half
`(KeySizeBits + 7) / 8` bytes, with `RemoteKeys/EcdsaSignatureConverter.ConvertDerToIeeeP1363(ReadOnlySpan<byte> der, int fieldSizeBytes, Span<byte> destination)`
(internal static; `System.Formats.Asn1.AsnReader` with `AsnEncodingRules.DER`; rejects a negative integer, an integer
longer than the field after its sign byte, trailing data and a short destination with `CryptographicException`; left-pads
each half); otherwise copies the bytes. It returns the bytes written, throws `CryptographicException` when the destination
is too small, and logs nothing. Reason: digest and format handling once, so a provider adapter only calls its service.

D6. **The algorithm's signing seam is asynchronous and verifies remote signatures.** `SignatureAlgorithm` gains

```csharp
public virtual ValueTask<int> SignAsync(SecretKey secretKey, ReadOnlyMemory<byte> inputData, Memory<byte> signature,
    CancellationToken cancellationToken)
```

Default: a key that is `IRemoteSecretKey` goes to a private `async` path that throws `JoseInvalidAlgorithmException`
when `!IsKeyCompatible(secretKey)`, awaits
`remoteKey.SignAsync(Code, ...)`, and throws `JoseException("The remote key '{KeyId}' returned a signature that its
public key does not verify.")` unless the length equals `GetSignatureSizeBytes(secretKey.KeySizeBits)` and
`Verify(secretKey, inputData.Span, signature.Span[..written])` is true; any other key calls the synchronous `TrySign`
and returns `ValueTask.FromResult(bytesWritten)` (no state machine), throwing `JoseException` when `TrySign` returns
false. `TrySign` stays the abstract synchronous primitive for local key material. Reason: a wrong key reference, mapping
or salt length can never put an unverifiable signature on a token; RSA-PSS verification with .NET's
`RSASignaturePadding.Pss` checks the hash-length salt (32 bytes for PS256) that AWS, Google and Azure use.

D7. **Encoding is asynchronous only.** `JoseEncoder` (Jose.Abstractions) has exactly these abstract members, mirroring
the existing eight with `ReadOnlyMemory` in place of `ReadOnlySpan` and a trailing `CancellationToken`:

```csharp
public abstract ValueTask EncodeAsync(IBufferWriter<char> tokenWriter, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
public abstract ValueTask<string> EncodeAsync<T>(T payload, CancellationToken cancellationToken);
public abstract ValueTask EncodeAsync<T>(IBufferWriter<char> tokenWriter, T payload, CancellationToken cancellationToken);
public abstract ValueTask<string> EncodeAsync(string payload, CancellationToken cancellationToken);
public abstract ValueTask EncodeAsync(IBufferWriter<char> tokenWriter, string payload, CancellationToken cancellationToken);
public abstract ValueTask<string> EncodeAsync(ReadOnlyMemory<char> payload, CancellationToken cancellationToken);
public abstract ValueTask EncodeAsync(IBufferWriter<char> tokenWriter, ReadOnlyMemory<char> payload, CancellationToken cancellationToken);
public abstract ValueTask<string> EncodeAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
```

No synchronous `Encode` remains. `CommonJoseEncoder` implements the seven in terms of the first; `JoseSigningEncoder`
awaits `JoseSerializer.EncodeAsync(tokenWriter, payload, SigningOptions, cancellationToken)`; `JoseEncryptionEncoder`
calls the synchronous JWE encode and returns a completed `ValueTask`. `JoseSerializer` (internal) builds each JWS
signing input into a pooled byte buffer owned by a lease held across the await (no span crosses an await; C# allows
span locals in async methods only between awaits), awaits `signatureAlgorithm.SignAsync`, then writes the base64url
signature. The JSON serialization computes every signature first, in order, then writes the JSON synchronously.
The signing-input bytes are identical to what the serializer signs today (b64 true:
`ASCII(BASE64URL(header) || '.' || BASE64URL(payload))`; b64 false: `ASCII(BASE64URL(header) || '.') || UTF8(payload)`).
Follow-on: `IJwtEncoder.EncodeAsync(JwtEncodingParameters parameters, CancellationToken cancellationToken)` →
`ValueTask<string>` (no `Encode`); `IDPoPProofService.CreateProofAsync(DPoPProofCreationOptions options, CancellationToken cancellationToken)`
→ `ValueTask<string>` (no `CreateProof`); `ISdJwtIssuer.IssueAsync(SdJwtIssuanceOptions options, CancellationToken cancellationToken)`
→ `ValueTask<SelectiveDisclosureJwt>`; `ISdJwtHolder.CreatePresentationAsync(SdJwtPresentationOptions options, CancellationToken cancellationToken)`
→ `ValueTask<string>`; the client's `DPoPProofKey.CreateProofAsync(HttpMethod method, Uri requestUri, string? accessToken, CancellationToken cancellationToken)`.
Every caller awaits with the token it has. Reason: one path signs every token, so a remote key works everywhere a
local key does; a synchronous twin would sign local keys and fail remote ones, and blocking on a task is banned
(RS0030). This is ADR-1414.

D8. **Catalog.** `src/NCode.Identity.Secrets/RemoteKeys/DefaultRemoteKeyProviderCatalog` (internal sealed;
`IEnumerable<IRemoteKeyProvider>`, `IEnumerable<IRemoteSigningAlgorithmSource>`): throws `InvalidOperationException`
at construction for a provider name that is invalid or registered twice and for two rows with the same
(`Provider`, `Algorithm`, `SecretType`, `KeySizeBits`, `Curve`); keeps rows of providers that are not registered
(harmless data). `AddSecretsLibrary` registers it with `TryAddSingleton<IRemoteKeyProviderCatalog, DefaultRemoteKeyProviderCatalog>()`
and `TryAddSingleton(TimeProvider.System)`.

D9. **Remote generation in the persistence layer.** `ISecretGenerator` gains
`ValueTask<PersistedSecret> GenerateRemoteSecretAsync(GenerateRemoteSecretRequest request, CancellationToken cancellationToken)`;
`GenerateRemoteSecretRequest` (sealed class, `[PublicAPI]`, `[ExcludeFromCodeCoverage]`, beside `GenerateSecretRequest`):
`required string SecretId`, `required string OwnerId`, `required string Provider`, `required string SecretType`,
`required int KeySizeBits`, `string? Curve`, `required string Use`, `required string Algorithm`, `required DateTimeOffset CreatedWhen`,
`DateTimeOffset? ActivatesWhen`, `DateTimeOffset? RetiresWhen`, `required DateTimeOffset ExpiresWhen`, `string? ManagedBy`.
`DefaultSecretGenerator` (gains `IRemoteKeyProviderCatalog`): resolves the lifecycle with `SecretLifecycle.Resolve`
first; requires the descriptor to exist and `SupportsRemoteKeys`, the provider to be registered and the row to exist
(each `InvalidOperationException` before any KMS call); calls `CreateKeyAsync(new RemoteKeyCreationRequest { OwnerId, SecretId, Algorithm = row })`;
deserializes the returned SPKI through the descriptor; when its size or curve differs from the row it calls
`DestroyKeyAsync(result.KeyId)` inside a `try` whose `catch` discards any failure (a comment says the mismatch is the
error reported), then throws `InvalidOperationException` naming the secret, the provider and the mismatch; otherwise it
returns the `PersistedSecret` of D3.

D10. **Destruction after commit, of the KMS keys the server created.** `IRemoteSecretDestroyer`
(Secrets.Persistence.Abstractions, namespace `NCode.Identity.Secrets.Persistence`):
`ValueTask DestroyAsync(IEnumerable<PersistedSecret> removedSecrets)`. `DefaultRemoteSecretDestroyer` (internal sealed,
Secrets.Persistence; `IRemoteKeyProviderCatalog`, `TimeProvider`, `ILogger<DefaultRemoteSecretDestroyer>`) skips every
secret that is not `remote` or whose `Origin` is not `generated`, runs each `DestroyKeyAsync` on its own token bounded
to 30 seconds (`new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider)`), never throws, and logs a
malformed reference, an unregistered provider or a failed destruction with the secret id and provider name only (new
`Log` entries `RemoteKeyDestroyFailed` and `RemoteKeyDestroySkipped`, new `EventIds`). Callers, each after its commit:
the seed (`TenantSeedContext.RunAfterCommit`), the rollover pass (after the announcement in `RunAfterCommitAsync`'s
`finally`), the management tenant-secret delete (after `NotifyTenantChangedAsync`, with `[secret]`) and the management
tenant delete (after `NotifyTenantChangedAsync`, with `tenant.Secrets.Value`). Reason: a destroyed key verifies nothing,
so it must go only after no replica can select it, and a KMS key the operator supplied is the operator's.

D11. **Per-tenant opt-in setting `tenant_signing_key_provider`.** `OpenIdSettingNames.TenantSigningKeyProvider =
"tenant_signing_key_provider"`, `OpenIdSettingKeys.TenantSigningKeyProvider` (`SettingKey<string>`). Declared by
`src/NCode.Identity.OpenId.Core/Tenants/Keys/TenantSigningKeySettingDescriptorDataSource.cs` (pattern:
`TenantEnvironmentSettingDescriptorDataSource`; dependencies `INullChangeToken`, `IRemoteKeyProviderCatalog`):
`AppliesTo = [OpenIdSettingScopes.Tenant]`, `Rule = SettingRules.EmptyOr(SettingRules.OneOf(<registered provider names, ordinal order>))`,
`Default = string.Empty`, `IsDiscoverable = false`, `OnMerge = SettingMerge.Replace` (the server's value is every
tenant's default and a tenant replaces it). Empty: the tenant's signing keys are local. The data source must not read
`OpenIdOptions` (the server-settings validator builds the descriptors while `OpenIdOptions` is being validated).
Encryption keys are always local.

D12. **A provider that is registered can hold every provisioned signature key, or the host does not start.**
`DefaultTenantKeyPolicy` (gains `IRemoteKeyProviderCatalog`) adds to `ResolveSpecifications`'s errors, for each
registered provider and each resolved specification with `Use == SecretKeyUses.Signature` that
`catalog.TryGetAlgorithm(provider.Name, spec.Algorithm, spec.SecretType, spec.KeySizeBits, spec.Curve)` does not find:
`$"The remote key provider '{name}' cannot hold the provisioned '{alg}' key ({secretType}, {size} bits{, curve})."`.
`TenantKeyProvisioningOptionsValidator` and the seed already report these errors. Reason: a tenant can opt in only to a
provider its host configured, and that opt-in must never leave an algorithm without a key or put signing material
outside the KMS.

D13. **Custody is part of a provisioned key.** `TenantKeySpecification` gains `string? RemoteKeyProvider { get; init; }`
(`null` = local). `ITenantKeyPolicy` gains
`IReadOnlyList<TenantKeySpecification> ForTenant(IReadOnlyList<TenantKeySpecification> specifications, IReadOnlySettingCollection settings)`:
when `settings.GetValue(OpenIdSettingKeys.TenantSigningKeyProvider)` is non-empty, every signature specification gets
that provider; others stay as they are. `IsSatisfiedBy` additionally requires the custody to match: a remote spec only
by a `remote` secret whose reference parses and names the spec's provider; a local spec only by a secret that is not
`remote`. `DefaultTenantKeyRolloverPlanner.Plan` applies `ForTenant` to the specifications before planning;
`TenantKeyReconciliationHandler.LacksKeyAsync` applies it to `used` before `HasServingKey`.

D14. **A custody change hands over after the activation delay.** In `PlanUsedSpecification`, when no serving key
satisfies the specification, the *superseded* keys are the held keys that are managed (`ManagedBy` is the rollover),
bound to the specification's algorithm, serving (`IsServing`) and do not satisfy it. When a superseded key is active
now: the hand-over is `Min(now + ActivationDelay, max RetiresWhen of the active superseded keys)`; one key is generated
that activates at the hand-over and retires a lifetime later, reason `TenantKeyChangeReasons.CustodyChanged =
"custody_changed"`; each active superseded key whose `RetiresWhen` is after the hand-over retires at the hand-over
(`ActivatesWhen` kept, `RetiresWhen` = hand-over, `ExpiresWhen` = hand-over + grace, reason `custody_changed`); each
superseded key that is not yet active retires now as the unused-algorithm path does (`ActivatesWhen = Min(ActivatesWhen, now)`,
`RetiresWhen = now`, `ExpiresWhen = now + grace`, reason `custody_changed`); coverage then starts at the generated
key's retirement and the successor chain runs as it does. When no superseded key is active, the existing
`NoActiveKey` path runs (generate active now) and every serving superseded key retires now with reason
`custody_changed`. `TenantKeyRetirement` gains `required string Reason`; the unused path sets `UnusedAlgorithm`;
`DefaultTenantKeyRollover` records `retirement.Reason`. Reason: a verifier that cached the JWKS holds the new key for
the whole delay before the first token it signs, exactly as with a successor; a key the operator supplied keeps its
lifecycle.

D15. **Remote generation and removal in the rollover.** `DefaultTenantKeyRollover` does not destroy anything (its
callers do, after commit, per D10); it generates a specification with `RemoteKeyProvider` through
`SecretGenerator.GenerateRemoteSecretAsync` (owner = tenant id, `ManagedBy = SecretManagers.TenantKeyRollover`), loads
the tenant's protector only when a local key is generated, and returns `TenantKeyRolloverResult.RemovedSecrets`
(`required IReadOnlyList<PersistedSecret>`, the removed records). `TenantKeyAuditEvent` gains
`string? RemoteKeyProvider { get; init; }`, written as detail `remote_key_provider` when present, set from the secret's
reference for every change. A successor of a remote key is a new version of its group: a Google crypto key version, an
Azure key version, or (AWS KMS has no versions for asymmetric keys) a new AWS KMS key tagged with the group.

D16. **Bounded, jittered retry on throttling is the SDK's, pinned explicitly; one layer only.** AWS: the client
factory builds clients from `Facades/AwsKmsClientConfiguration.Create(string region)` returning
`AmazonKeyManagementServiceConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(region), RetryMode = RequestRetryMode.Standard, MaxErrorRetry = 3 }`
(standard mode retries throttling with exponential backoff and full jitter). Azure: the client factory passes
`Facades/AzureKeyVaultClientOptions.Configure<T>(T options) where T : ClientOptions` to every `CryptographyClient` and
`KeyClient`: `Retry.Mode = RetryMode.Exponential`, `MaxRetries = 3`, `Delay = 500 ms`, `MaxDelay = 8 s` (Azure.Core's
exponential backoff is jittered and honors `Retry-After` on 429). Google: `GoogleCloudKmsCallSettings.Retrying`
(`CallSettings.FromRetry(RetrySettings.FromExponentialBackoff(maxAttempts: 4, initialBackoff: 200 ms, maxBackoff: 4 s, backoffMultiplier: 2.0, retryFilter: RetrySettings.FilterForStatusCodes(StatusCode.ResourceExhausted, StatusCode.Unavailable, StatusCode.DeadlineExceeded), backoffJitter: RetrySettings.RandomJitter))`)
merged with the caller's token for `AsymmetricSign`, `GetPublicKey` and `GetCryptoKeyVersion`; create and destroy calls
use the client's defaults with only `ResourceExhausted` retried (`...Creating`, same shape, `maxAttempts: 3`).
Reason: the SDK knows each service's throttling signal; a second retry layer on top would multiply attempts during an
outage.

D17. **Providers live in the existing KMS packages and are opt-in by configuration.** Each package adds a remote key
provider beside its key wrapper, reusing the package's client factory, credentials chain and key-name rules, with
`Add<Vendor>RemoteKeyProvider(IConfiguration configuration)` on `IServiceBuilder<SecretPersistenceLibrary>` (options
bound strictly from `SecretProtection:RemoteKeyProviders:<Vendor>`, validated on start, always: registering the provider
is selecting it). The reference host calls each only when its section exists
(`configuration.GetSection(<Options>.DefaultSectionName).Exists()`), so a host offers exactly the providers it
configured. Renaming the packages to name both roles is a follow-up issue.

- AWS (`aws-kms`): options `AwsKmsRemoteKeyProviderOptions` { `DefaultSectionName = "SecretProtection:RemoteKeyProviders:AwsKms"`,
  `string? Region`, `int PendingWindowInDays = 30` }; validator: region matches `^[a-z0-9]+(-[a-z0-9]+)+\z`, window
  7–30. Create: `CreateKeyAsync(new CreateKeyRequest { KeySpec = KeySpec.FindValue(row.KeySpec), KeyUsage = KeyUsageType.SIGN_VERIFY, Description = $"NCode.Identity signing key {SecretId} of {OwnerId}", Tags = [ncode-identity:owner, ncode-identity:secret, ncode-identity:algorithm, ncode-identity:group] })`,
  then `GetPublicKeyAsync` on the returned `KeyMetadata.Arn`; require `KeyUsage == SIGN_VERIFY`, `KeySpec` equal to
  the row's, the row's signing algorithm in `SigningAlgorithms`, and a valid key ARN (`AwsKmsKeyArn.IsValid`); KeyId =
  the ARN. Sign: `SignAsync(new SignRequest { KeyId, Message = <MemoryStream over Data>, MessageType = row.DigestAlgorithm is null ? MessageType.RAW : MessageType.DIGEST, SigningAlgorithm = SigningAlgorithmSpec.FindValue(row.SigningAlgorithm) })`;
  require `response.KeyId == KeyId` and `response.SigningAlgorithm == row.SigningAlgorithm` (`CryptographicException`
  otherwise). Destroy: `ScheduleKeyDeletionAsync(KeyId, PendingWindowInDays)`; `NotFoundException` completes;
  `KMSInvalidStateException` completes when `DescribeKeyAsync` reports `KeyState.PendingDeletion`, else rethrows.
  The region of every call is the ARN's (`AwsKmsKeyArn.GetRegion`); creation uses the configured region.
- Azure (`azure-key-vault`): options `AzureKeyVaultRemoteKeyProviderOptions` { `DefaultSectionName = "SecretProtection:RemoteKeyProviders:AzureKeyVault"`,
  `string? VaultUri`, `bool HardwareProtected = true` }; validator: an `https` URI matching
  `^https://[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+/?\z`. `IAzureKeyVaultClientFactory` gains `KeyClient GetKeyClient(Uri vaultUri)`
  (one per vault, shared credential). Create: key name `{GroupName}-{h|s}` (hardware or software); RSA rows
  `CreateRsaKeyAsync(new CreateRsaKeyOptions(name, HardwareProtected) { KeySize = row.KeySizeBits, KeyOperations = { KeyOperation.Sign, KeyOperation.Verify }, Tags = {...} })`,
  EC rows `CreateEcKeyAsync(new CreateEcKeyOptions(name, HardwareProtected) { CurveName = new KeyCurveName(row.Curve), KeyOperations = {...}, Tags = {...} })`
  (creating under an existing name adds a version); KeyId = the returned versioned `Id`
  (`AzureKeyVaultKeyId.IsKeyVersionId`); SPKI from `key.Key.ToRSA()` / `key.Key.ToECDsa()`
  `ExportSubjectPublicKeyInfo()`. Sign: `GetClient(KeyId).SignAsync(new SignatureAlgorithm(row.SigningAlgorithm), Data.ToArray(), ct)`;
  require `result.KeyId == KeyId` and `result.Algorithm == row.SigningAlgorithm`. Destroy (Key Vault cannot delete one
  version): `UpdateKeyPropertiesAsync(new KeyProperties(new Uri(KeyId)) { Enabled = false })`; `RequestFailedException`
  with status 404 completes.
- Google (`google-cloud-kms`): options `GoogleCloudKmsRemoteKeyProviderOptions` { `DefaultSectionName = "SecretProtection:RemoteKeyProviders:GoogleCloudKms"`,
  `string? KeyRing`, `bool HardwareProtected = true` }; validator: `GoogleCloudKmsKeyName.IsKeyRingName` (new,
  `^projects/[a-z0-9.:-]+/locations/[a-z0-9-]+/keyRings/[A-Za-z0-9_-]{1,63}\z`). Create: crypto key
  `{KeyRing}/cryptoKeys/{GroupName}-{h|s}`; `CreateCryptoKeyVersionAsync`; on `RpcException` `NotFound` create the crypto
  key (`Purpose = AsymmetricSign`, `VersionTemplate { Algorithm = <row.KeySpec parsed through the protobuf enum descriptor's original name>, ProtectionLevel = Hsm|Software }`,
  `Labels { ncode-identity-owner = OwnerId, ncode-identity-algorithm = lowercase(row.Algorithm) }`,
  `SkipInitialVersionCreation = true`; `AlreadyExists` completes) and create the version again; poll
  `GetCryptoKeyVersionAsync` with `TimeProvider` delays 250 ms doubling to 4 s, at most 60 s in all, until `Enabled`
  (`GenerationFailed` or the bound throws `InvalidOperationException`); `GetPublicKeyAsync`: require `Name` equal to
  the version, `PemCrc32C` equal to `GoogleCloudKmsChecksum.Compute(UTF-8 Pem)` and the algorithm equal to the row's;
  SPKI from the `PUBLIC KEY` PEM. Sign: `AsymmetricSignAsync` with `Digest { Sha256 | Sha384 | Sha512 }` chosen by
  `row.DigestAlgorithm` (Google's three digest fields; `Data` when null) and the matching `DigestCrc32C`/`DataCrc32C`;
  require `VerifiedDigestCrc32C` (or `VerifiedDataCrc32C`), `SignatureCrc32C` equal to the checksum of `Signature`, and
  `Name == KeyId` (`IOException` "can be retried" otherwise, as the wrapper does). Destroy:
  `DestroyCryptoKeyVersionAsync`; `NotFound` and `FailedPrecondition` (already scheduled or destroyed) complete.

D18. **The mapping rows each package registers** (`internal sealed class <Vendor>SigningAlgorithms : IRemoteSigningAlgorithmSource`):

| Provider | JOSE `alg` | Key | `KeySpec` | `SigningAlgorithm` | Digest | DER |
| --- | --- | --- | --- | --- | --- | --- |
| aws-kms | RS256/RS384/RS512 | RSA 2048/3072/4096 | `RSA_{n}` | `RSASSA_PKCS1_V1_5_SHA_{256,384,512}` | SHA-256/384/512 | no |
| aws-kms | PS256/PS384/PS512 | RSA 2048/3072/4096 | `RSA_{n}` | `RSASSA_PSS_SHA_{256,384,512}` | SHA-256/384/512 | no |
| aws-kms | ES256 / ES384 / ES512 | P-256 (256) / P-384 (384) / P-521 (521) | `ECC_NIST_P256` / `ECC_NIST_P384` / `ECC_NIST_P521` | `ECDSA_SHA_256` / `_384` / `_512` | SHA-256/384/512 | yes |
| google-cloud-kms | RS256 | RSA 2048/3072/4096 | `RSA_SIGN_PKCS1_{n}_SHA256` | same as KeySpec | SHA-256 | no |
| google-cloud-kms | RS512 | RSA 4096 | `RSA_SIGN_PKCS1_4096_SHA512` | same | SHA-512 | no |
| google-cloud-kms | PS256 | RSA 2048/3072/4096 | `RSA_SIGN_PSS_{n}_SHA256` | same | SHA-256 | no |
| google-cloud-kms | PS512 | RSA 4096 | `RSA_SIGN_PSS_4096_SHA512` | same | SHA-512 | no |
| google-cloud-kms | ES256 / ES384 | P-256 / P-384 | `EC_SIGN_P256_SHA256` / `EC_SIGN_P384_SHA384` | same | SHA-256/384 | yes |
| azure-key-vault | RS256/384/512, PS256/384/512 | RSA 2048/3072/4096 | `RSA` | the JOSE code | SHA-256/384/512 | no |
| azure-key-vault | ES256 / ES384 / ES512 | P-256 / P-384 / P-521 | `EC` | the JOSE code | SHA-256/384/512 | no (already `r‖s`) |

The defaults every tenant is provisioned with (RS256 and PS256 at 3072 bits, ES256 on P-256) are held by all three.
A row with `DigestAlgorithm = null` (a service such as Infisical whose PSS signs the full message) is supported by the
helper and by the AWS adapter (`MessageType.RAW`); none of the default rows uses it.

D19. **Security posture.** The private key never leaves the KMS; the server logs key identifiers only, never a signing
input, digest or signature; every remote signature is verified locally (D6); a remote key is bound to one algorithm
(`Algorithm` required); KMS keys are created `SIGN_VERIFY`-only (AWS), `Sign`/`Verify` operations only (Azure),
`ASYMMETRIC_SIGN` purpose (Google), hardware-protected by default on Azure and Google; IAM needs are documented on the
capability page (AWS: `kms:CreateKey`, `kms:TagResource`, `kms:GetPublicKey`, `kms:Sign`, `kms:ScheduleKeyDeletion`,
`kms:DescribeKey`; Azure: keys create/sign/update — the `Key Vault Crypto Officer` role on the vault, or create+sign+update
in an access policy; Google: `cloudkms.cryptoKeys.create`, `cloudkms.cryptoKeyVersions.create/get/viewPublicKey/useToSign/destroy`).
A KMS key created by a seed that then loses its commit stays in the KMS unused; its tags/labels name the owner, group
and secret so an operator can find it (documented under Limits).

## ADRs to write

### ADR-1413 — `docs/decisions/1413-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md`

Title: "A tenant's signing keys can be held in a key management service that signs every token". Status: Accepted.
Date: 2026-11-05. Deciders: NCode Group. `**Amends:**` [ADR-1404] (a provisioned key's custody is part of its
specification, a custody change hands over after the activation delay, and the removal of a remote key destroys it
in its KMS after the commit) and [ADR-1104] (the `remote` secret encoding and the descriptor's `CreateRemoteKey`
seam). Context (present tense): a running replica holds every tenant key it loaded (encryption-at-rest limit); KMS
signing narrows that to "can ask the KMS to sign while it runs"; forces: latency and cost per token, throttling,
JWKS must not call the KMS, DER vs P1363, PSS salt, rollover across replicas. Decision: D1–D5, D8–D19 as numbered
rules (keep the mapping table). Options considered (each with why rejected):
`RemoteAsymmetricSecretKey : AsymmetricSecretKey` (every algorithm refuses it); fetching the public key per JWKS
request or per verification (latency, throttling, an outage stops verification); trusting the KMS signature without
local verification (a wrong reference or mapping ships unverifiable tokens); a family retry layer over the SDK's
(multiplies attempts); new provider packages (duplicate clients, credentials and key-name rules); a separate setting
per algorithm or a host-only switch (custody is a tenant's choice; one value covers its signature keys); switching
custody at once (relying parties refuse tokens under an unseen `kid`); remote encryption keys in the same change
(different operation and API; follow-up); deleting Azure keys per rollover (Key Vault deletes whole keys; a version
is disabled); protecting the reference under the master key (it is not secret; integrity comes from the local
verification against the stored public key). Consequences: a KMS round trip and fee per token for an opted-in tenant;
a KMS outage stops that tenant's issuance; Google crypto keys are permanent and only their versions are destroyed;
Azure versions accumulate disabled; AWS creates one key per generation; orphan keys after a lost race; the host
starts only when every configured provider can hold every provisioned signature key. References: code paths,
issue #165, #166, ADR-1235, ADR-1260, ADR-1279, ADR-1403, ADR-1404, ADR-1414; RFC 7518 §3.4 (ECDSA `r‖s`), §3.5 (PSS),
AWS KMS Sign, Azure Key Vault sign, Cloud KMS asymmetric signing docs.

### ADR-1414 — `docs/decisions/1414-signing-a-token-is-asynchronous-from-the-signature-algorithm-to-every-caller.md`

Title: "Signing a token is asynchronous from the signature algorithm to every caller". Status: Accepted. Date
2026-11-05. `**Amends:**` [ADR-1120] (`IJwtEncoder.EncodeAsync`), [ADR-1176] (`IDPoPProofService.CreateProofAsync`),
[ADR-1405] (`ISdJwtIssuer.IssueAsync`, `ISdJwtHolder.CreatePresentationAsync`). Context: a signature can come from a
remote service; the encode path is span-based and synchronous. Decision: D6 and D7 (the member list). Options:
keep sync and add async twins (two paths, the sync one fails for remote keys); sync-over-async (thread-pool
starvation; banned by RS0030); pre-compute signatures before encoding (the signature covers the header and payload the
encoder writes); an async callback on the credentials (spreads the seam outside the algorithm). Consequences: every
caller awaits with its token; a local key completes synchronously through a `ValueTask` without a state machine; the
encoder copies the signing input into a pooled buffer once per signature; tests and the testing instructions use
`EncodeAsync`.

### Index and amended records

- `docs/decisions/README.md`: append rows for 1413 and 1414 after 1408 (number order; 1409–1412 belong to other
  branches), Status `Accepted (amends [1104](...), [1404](...))` and `Accepted (amends [1120](...), [1176](...), [1405](...))`.
- Amended rows: 1104 `...; amended by [1413](...))`; 1404 `Accepted (amends [1169](...), [1221](...); amended by [1413](...))`;
  1120 `Accepted (amended by [1278](...), [1414](...))`; 1176 `Accepted (amended by [1414](...))`; 1405
  `Accepted (amended by [1414](...))`.
- Amended files' `- **Status:**` lines gain `(amended by [ADR-1413](...): <one clause>)` / `[ADR-1414]`, matching how
  1279's line names ADR-1408; their bodies are not edited.
- Checks: `pwsh -NoProfile -File .template/Test-AdrIndex.ps1` and `pwsh -NoProfile -File build/adr-index.ps1`.

## Out of scope

- Remote decryption (tenant encryption keys in a KMS) — follow-up.
- Remote keys for a server, a client, a resource server, the protocol client's or the BFF's own keys, DPoP keys.
- A management API or console surface that shows a key's remote provider or lets an operator generate or import a
  remote key.
- Helm chart values and workload identity for remote key providers that differ from the root key provider.
- Renaming the `NCode.Identity.Secrets.KeyWrapping.*` packages.

## Steps

### [ ] 1. Remote key vocabulary and remote key classes in the Secrets packages

- Files: `src/NCode.Identity.Secrets.Abstractions/RemoteKeys/{RemoteKeyReference,RemoteSigningAlgorithm,IRemoteSigningAlgorithmSource,IRemoteKeyProvider,RemoteKeyCreationRequest,RemoteKeyCreationResult,RemoteSignatureRequest,IRemoteKeyProviderCatalog,RemoteKeyProviderNames}.cs` (new),
  `src/NCode.Identity.Secrets.Abstractions/Keys/IRemoteSecretKey.cs` (new),
  `src/NCode.Identity.Secrets.Abstractions/SecretEncodings.cs`,
  `src/NCode.Identity.Secrets.Abstractions/KeyTypes/ISecretKeyTypeDescriptor.cs`,
  `src/NCode.Identity.Secrets.Abstractions/KeyMaterial/ISecretKeyFactory.cs`,
  `src/NCode.Identity.Secrets.Abstractions/PublicAPI.Unshipped.txt`,
  `src/NCode.Identity.Secrets/RemoteKeys/{DefaultRemoteKeyProviderCatalog,RemoteKeySigning,EcdsaSignatureConverter}.cs` (new),
  `src/NCode.Identity.Secrets/Keys/{RemoteRsaSecretKey,RemoteEccSecretKey}.cs` (new),
  `src/NCode.Identity.Secrets/KeyMaterial/DefaultSecretKeyFactory.cs`,
  `src/NCode.Identity.Secrets/KeyTypes/{SecretKeyTypeDescriptor,RsaSecretKeyTypeDescriptor,EccSecretKeyTypeDescriptor}.cs`,
  `src/NCode.Identity.Secrets/DefaultRegistration.cs`, `src/NCode.Identity.Secrets/PublicAPI.Unshipped.txt`,
  tests `tests/NCode.Identity.Secrets.Abstractions.Tests/RemoteKeys/{RemoteKeyReferenceTests,RemoteKeyCreationRequestTests}.cs` (new),
  `tests/NCode.Identity.Secrets.Abstractions.Tests/SecretEncodingsTests.cs`,
  `tests/NCode.Identity.Secrets.Tests/RemoteKeys/{EcdsaSignatureConverterTests,DefaultRemoteKeyProviderCatalogTests,RemoteSecretKeyTests}.cs` (new),
  `tests/NCode.Identity.Secrets.Tests/KeyMaterial/DefaultSecretKeyFactoryTests.cs`,
  `tests/NCode.Identity.Secrets.Tests/KeyTypes/SecretKeyTypeDescriptorTests.cs`,
  `tests/NCode.Identity.Secrets.Tests/DefaultRegistrationTests.cs`.
- Change: D1, D2, D3 (constants only), D4 (descriptor and factory members), D5, D8. XML docs on every public member;
  `// See ADR-1413.` above each new public type. `RemoteRsaSecretKey`/`RemoteEccSecretKey` copy `PublicRsaSecretKey`/
  `PublicEccSecretKey` and implement `IRemoteSecretKey` through `RemoteKeySigning`; `FormatToString` appends
  `, Provider = '<provider name>'`.
- Tests: reference format/parse round trip (ARN with colons), invalid provider names and empty key id throw;
  `GroupName` is stable, 36 characters, and differs by owner, algorithm, spec, size and curve; `IsRemote`/
  `IsUnprotected`; DER→P1363 against `ECDsa.SignData(..., DSASignatureFormat.Rfc3279DerSequence)` for P-256/P-384/
  P-521 then `VerifyData(..., DSASignatureFormat.IeeeP1363FixedFieldConcatenation)`, plus short `r`, leading-zero `r`,
  negative, oversized, trailing data and short destination failures; catalog duplicate provider, invalid name and
  duplicate row throw, lookups by every field; a remote RSA and ECC key (Moq `IRemoteKeyProvider` that signs with a
  local key) sends the digest for a digest row and the input for a `null`-digest row, converts DER for an ECC row,
  refuses another algorithm, exports only public parameters, and the factory refuses a size or curve that differs from
  the row; the base descriptor reports `SupportsRemoteKeys == false` and throws; registration adds the catalog.
- Pattern: `PublicRsaSecretKey`, `DefaultEccCurveCatalog` (catalog validation), `KeyWrapperNames` (names).
- Done when: `dotnet test tests/NCode.Identity.Secrets.Abstractions.Tests/NCode.Identity.Secrets.Abstractions.Tests.csproj -m:2 --filter "FullyQualifiedName~RemoteKey|FullyQualifiedName~SecretEncodings" -- RunConfiguration.TreatNoTestsAsError=true && dotnet test tests/NCode.Identity.Secrets.Tests/NCode.Identity.Secrets.Tests.csproj -m:2 --filter "FullyQualifiedName~Remote|FullyQualifiedName~EcdsaSignatureConverter|FullyQualifiedName~DefaultSecretKeyFactory|FullyQualifiedName~SecretKeyTypeDescriptor|FullyQualifiedName~DefaultRegistration" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 2. Store, read, generate and destroy remote secrets

- Files: `src/NCode.Identity.Secrets.Persistence.Abstractions/{ISecretGenerator,GenerateRemoteSecretRequest,IRemoteSecretDestroyer}.cs` (two new),
  `src/NCode.Identity.Secrets.Persistence.Abstractions/Protection/SecretProtectorProviderExtensions.cs`,
  `src/NCode.Identity.Secrets.Persistence.Abstractions/PublicAPI.Unshipped.txt`,
  `src/NCode.Identity.Secrets.Persistence/{DefaultSecretSerializer,DefaultSecretGenerator,DefaultRemoteSecretDestroyer,DefaultRegistration}.cs` (destroyer new),
  `src/NCode.Identity.Secrets.Persistence/Logging/{Log,EventIds}.cs`,
  `src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperSecretPruningSeedHandler.cs`,
  tests `tests/NCode.Identity.Secrets.Persistence.Tests/{DefaultSecretSerializerTests,DefaultSecretGeneratorTests}.cs`,
  `tests/NCode.Identity.Secrets.Persistence.Tests/DefaultRemoteSecretDestroyerTests.cs` (new),
  `tests/NCode.Identity.Secrets.Persistence.Tests/Protection/SecretProtectorProviderExtensionsTests.cs` (existing or new;
  find with `grep -rln GetProtectorForSecretsAsync tests`),
  `tests/NCode.Identity.Server.Tests/DevelopmentEnvironment/DeveloperSecretPruningSeedHandlerTests.cs`.
- Change: D3 (callers of `IsUnprotected`), D4 (serializer), D9, D10 (destroyer, logging, registration
  `TryAddSingleton<IRemoteSecretDestroyer, DefaultRemoteSecretDestroyer>()`).
- Tests: a remote secret deserializes to an `IRemoteSecretKey` of the right family, size, curve and row; malformed
  reference, unregistered provider, missing public key, unbound algorithm and missing row each throw; the public
  half deserializes without the catalog; the protector extension never passes a remote value; generation calls the
  provider with the row and group, stores the D3 record (encoding, reference, public key, origin, managed-by, instants),
  refuses an unsupported type, an unregistered provider and a missing row before calling it, and destroys and throws
  when the returned key's size or curve differs; the destroyer destroys only `remote` + `generated` secrets, bounds
  each call, logs and continues on a failure, a malformed reference and an unregistered provider; pruning keeps a remote
  secret when the master key cannot be read.
- Pattern: `DefaultSecretGenerator.GenerateSecret`, `DefaultSecretSerializerTests`, the post-commit token in
  `src/NCode.Identity.OpenId.Core/PostCommitWork.cs`.
- Done when: `dotnet test tests/NCode.Identity.Secrets.Persistence.Tests/NCode.Identity.Secrets.Persistence.Tests.csproj -m:2 --filter "FullyQualifiedName~DefaultSecretSerializer|FullyQualifiedName~DefaultSecretGenerator|FullyQualifiedName~DefaultRemoteSecretDestroyer|FullyQualifiedName~SecretProtectorProviderExtensions" -- RunConfiguration.TreatNoTestsAsError=true && dotnet test tests/NCode.Identity.Server.Tests/NCode.Identity.Server.Tests.csproj -m:2 --filter "FullyQualifiedName~DeveloperSecretPruningSeedHandler" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 3. Asynchronous signing in the JOSE library

- Files: `src/NCode.Identity.Jose.Abstractions/Algorithms/SignatureAlgorithm.cs`,
  `src/NCode.Identity.Jose.Abstractions/IJoseEncoder.cs`, `src/NCode.Identity.Jose.Abstractions/IJoseSerializer.cs` (doc
  comments only), `src/NCode.Identity.Jose.Abstractions/DPoP/IDPoPProofService.cs`,
  `src/NCode.Identity.Jose.Abstractions/PublicAPI.Unshipped.txt`,
  `src/NCode.Identity.Jose/{JoseSerializer.Jws.Encode,JoseSerializer.Jws.Json,JoseSerializer}.cs`,
  `src/NCode.Identity.Jose/Encoders/{CommonJoseEncoder,JoseSigningEncoder,JoseEncryptionEncoder}.cs`,
  `src/NCode.Identity.Jose/DPoP/DefaultDPoPProofService.cs`, `src/NCode.Identity.Jose/PublicAPI.Unshipped.txt` (if it
  changes), every test in `tests/NCode.Identity.Jose.Tests`, `tests/NCode.Identity.Jose.EdDsa.Tests`,
  `tests/NCode.Identity.Jose.Secp256k1.Tests` that encodes or creates a proof, and
  `tests/NCode.Identity.Jose.Tests/Algorithms/Signature/RemoteSignatureTests.cs` (new).
- Change: D6, D7 for the JOSE packages. Async methods take and pass the caller's `CancellationToken`; no
  `ConfigureAwait(false)` (csharp-production.instructions.md). `JoseSerializer.EncodeAsync` replaces
  the internal `Encode` for JWS; the JWE encode stays synchronous. Doc comments describe `EncodeAsync` and
  `SignAsync`; `TrySign`'s doc says it signs with local key material.
- Tests: convert every `Encode(`/`CreateProof(` to the async forms (test methods `async Task`); new
  `RemoteSignatureTests` with a test-only remote RSA key and ECC key (subclasses of `RsaSecretKey`/`EccSecretKey` that
  implement `IRemoteSecretKey` by signing with a held private key and returning P1363) proving: compact, flattened and
  general JWS round-trip through `Decode` with the public key for PS256, RS256 and ES256; a signer that returns another
  key's signature or a short signature fails with `JoseException`; an incompatible remote key fails before the signer is
  called (Moq strict); a local key completes synchronously (`ValueTask.IsCompletedSuccessfully`).
- Pattern: the existing `JoseSerializerTests`, `JoseSerializerJsonTests`, `DefaultDPoPProofServiceTests`.
- Done when: `dotnet test tests/NCode.Identity.Jose.Tests/NCode.Identity.Jose.Tests.csproj -m:2 --filter "FullyQualifiedName~JoseSerializer|FullyQualifiedName~RemoteSignature|FullyQualifiedName~DPoP|FullyQualifiedName~ContributedKeyType" -- RunConfiguration.TreatNoTestsAsError=true && dotnet test tests/NCode.Identity.Jose.EdDsa.Tests/NCode.Identity.Jose.EdDsa.Tests.csproj -m:2 --filter "FullyQualifiedName~DPoP|FullyQualifiedName~OkpEcdhKeyAgreement" -- RunConfiguration.TreatNoTestsAsError=true && dotnet test tests/NCode.Identity.Jose.Secp256k1.Tests/NCode.Identity.Jose.Secp256k1.Tests.csproj -m:2 --filter "FullyQualifiedName~Es256K" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 4. Asynchronous JWT encoding and SD-JWT issuance and presentation

- Files: `src/NCode.Identity.Jwt.Abstractions/Encoding/IJwtEncoder.cs`, `src/NCode.Identity.Jwt.Abstractions/PublicAPI.Unshipped.txt`,
  `src/NCode.Identity.Jwt/Encoding/DefaultJwtEncoder.cs`,
  `src/NCode.Identity.Jose.SdJwt/Issuance/{ISdJwtIssuer,DefaultSdJwtIssuer}.cs`,
  `src/NCode.Identity.Jose.SdJwt/Presentation/{ISdJwtHolder,DefaultSdJwtHolder}.cs`,
  `src/NCode.Identity.Jose.SdJwt/PublicAPI.Unshipped.txt`, tests in `tests/NCode.Identity.Jwt.Tests` and
  `tests/NCode.Identity.Jose.SdJwt.Tests` that encode, issue or present.
- Change: D7 for JWT and SD-JWT. `DefaultJwtEncoder.EncodeAsync` awaits the signing encoder, then the encryption
  encoder for a Nested JWT.
- Pattern: Step 3's conversions.
- Done when: `dotnet test tests/NCode.Identity.Jwt.Tests/NCode.Identity.Jwt.Tests.csproj -m:2 --filter "FullyQualifiedName~DefaultJsonWebTokenService|FullyQualifiedName~DefaultJwtValidator" -- RunConfiguration.TreatNoTestsAsError=true && dotnet test tests/NCode.Identity.Jose.SdJwt.Tests/NCode.Identity.Jose.SdJwt.Tests.csproj -m:2 --filter "FullyQualifiedName~DefaultSdJwt" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 5. Every caller that signs awaits the signing seam

- Files: `src/NCode.Identity.OpenId.Authentication/Tokens/DefaultTokenService.cs`,
  `src/NCode.Identity.OpenId.Authentication/Sessions/DefaultBackChannelLogoutNotifier.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Introspection/DefaultIntrospectionEndpointProvider.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/UserInfo/DefaultUserInfoEndpointProvider.cs` (the private
  encoder becomes `EncodeUserInfoJwtAsync`),
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Results/JwtAuthorizationResponseModeHandler.cs`,
  `src/NCode.Identity.OpenId.Client/Authentication/PrivateKeyJwtClientAuthenticator.cs`,
  `src/NCode.Identity.OpenId.Client/ProofOfPossession/{DPoPProofKey,DefaultDPoPProofFactory}.cs`,
  `src/NCode.Identity.OpenId.Client/Tokens/ClientRequestSender.cs`, `src/NCode.Identity.OpenId.Client/UserInfo/DefaultUserInfoClient.cs`,
  `src/NCode.Identity.OpenId.Client/PublicAPI.Unshipped.txt` (if `DPoPProofKey` is public),
  any other file the Context grep lists, tests in `tests/NCode.Identity.OpenId.Client.Tests`,
  `tests/NCode.Identity.OpenId.Core.Tests` (`Tokens/DefaultTokenServiceTests.cs`,
  `Sessions/DefaultBackChannelLogoutNotifierTests.cs`, `Endpoints/UserInfo/DefaultUserInfoEndpointProviderTests.cs` —
  Moq setups move to `EncodeAsync(It.IsAny<JwtEncodingParameters>(), It.IsAny<CancellationToken>())` returning
  `ValueTask.FromResult(...)`), `tests/NCode.Identity.OpenId.Integration.Tests/Endpoints/AuthorizationRequestObjectTests.cs`
  and any Bff/Integration test that the solution build reports,
  `.github/instructions/csharp-testing.instructions.md` (the `CreateEncoder(new JoseSigningOptions(credentials)).Encode(payload)`
  sentence becomes `...EncodeAsync(payload, cancellationToken)`).
- Change: D7 callers. No behavior change beyond awaiting.
- Done when: `dotnet build NCode.Identity.slnx -m:2 && dotnet test tests/NCode.Identity.OpenId.Client.Tests/NCode.Identity.OpenId.Client.Tests.csproj -m:2 --filter "FullyQualifiedName~PrivateKeyJwtClientAuthenticator|FullyQualifiedName~DPoP|FullyQualifiedName~ClientRequestSender|FullyQualifiedName~DefaultUserInfoClient|FullyQualifiedName~DefaultIdTokenValidator|FullyQualifiedName~DefaultLogoutTokenValidator" -- RunConfiguration.TreatNoTestsAsError=true && dotnet test tests/NCode.Identity.OpenId.Core.Tests/NCode.Identity.OpenId.Core.Tests.csproj -m:2 --filter "FullyQualifiedName~DefaultTokenService|FullyQualifiedName~DefaultBackChannelLogoutNotifier|FullyQualifiedName~DefaultUserInfoEndpointProvider|FullyQualifiedName~DefaultDPoPProofValidator" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 6. A tenant opts in to remote signing keys and the rollover keeps them there

- Files: `src/NCode.Identity.OpenId.Abstractions/Settings/{OpenIdSettingNames,OpenIdSettingKeys}.cs`,
  `src/NCode.Identity.OpenId.Abstractions/PublicAPI.Unshipped.txt`,
  `src/NCode.Identity.OpenId.Core/Tenants/Keys/TenantSigningKeySettingDescriptorDataSource.cs` (new),
  `src/NCode.Identity.OpenId.Core/Settings/DefaultRegistration.cs`,
  `src/NCode.Identity.OpenId.Core/Tenants/Keys/{ITenantKeyPolicy,DefaultTenantKeyPolicy,TenantKeySpecification,DefaultTenantKeyRolloverPlanner,TenantKeyRetirement,TenantKeyChangeReasons,DefaultTenantKeyRollover,TenantKeyRolloverResult,TenantKeyAuditEvent,TenantKeySeedHandler,TenantKeyRolloverService,TenantKeyReconciliationHandler}.cs`,
  `src/NCode.Identity.OpenId.Core/PublicAPI.Unshipped.txt`, tests in
  `tests/NCode.Identity.OpenId.Core.Tests/Tenants/Keys/{DefaultTenantKeyPolicyTests,DefaultTenantKeyRolloverPlannerTests,DefaultTenantKeyRolloverTests,TenantKeyAuditEventTests,TenantKeySeedHandlerTests,TenantKeyRolloverServiceTests,TenantKeyReconciliationHandlerTests,TenantKeyProvisioningOptionsValidatorTests,TenantKeyTestData}.cs`
  and `tests/NCode.Identity.OpenId.Core.Tests/Tenants/Keys/TenantSigningKeySettingDescriptorDataSourceTests.cs` (new).
- Change: D11–D15. The seed handler and the rollover service gain `IRemoteSecretDestroyer` and call it after commit
  (D10); the seed queues it with `context.RunAfterCommit(_ => RemoteSecretDestroyer.DestroyAsync(result.RemovedSecrets))`
  after the publish step. Setting XML doc (OpenIdSettingNames): "the name of the registered remote key provider that
  holds the tenant's signing keys; empty keeps them in the server's store. Not advertised in discovery." The descriptor
  comment in the data source names `(ADR-1413)` like its neighbours.
- Tests: the data source's rule accepts empty and each registered name and refuses others; the policy reports a
  provider that cannot hold a provisioned signature key and ignores encryption keys; `ForTenant` sets the provider on
  signature specs only; `IsSatisfiedBy` custody both ways; planner: opting in with an active local managed key
  generates a remote-custody key activating at now + delay and retires the local key at the hand-over with
  `custody_changed`; an earlier local retirement shortens the hand-over; an upcoming superseded key retires now; no
  active superseded key → generate active now and retire the rest now; an operator key is untouched; opting out mirrors
  opting in; rollover: a remote spec calls `GenerateRemoteSecretAsync` (Moq strict) and never loads the protector, a
  mixed plan loads it once, removals land in `RemovedSecrets`, retirements record their reason, audit events carry
  `remote_key_provider`; the seed and the pass call the destroyer after commit and never before; the pass calls it even
  when publishing throws; reconciliation does not report a lack for a remote-opted tenant holding remote keys.
- Pattern: `TenantEnvironmentSettingDescriptorDataSource`, existing planner and rollover tests and `TenantKeyTestData`.
- Done when: `dotnet test tests/NCode.Identity.OpenId.Core.Tests/NCode.Identity.OpenId.Core.Tests.csproj -m:2 --filter "FullyQualifiedName~NCode.Identity.OpenId.Core.Tenants.Keys|FullyQualifiedName~TenantSigningKeySettingDescriptorDataSource|FullyQualifiedName~DefaultSettingDescriptorDataSource" -- RunConfiguration.TreatNoTestsAsError=true`
  (the folder's tests are in namespace `NCode.Identity.OpenId.Core.Tenants.Keys`).

### [ ] 7. Deleting a tenant or its secret destroys its remote keys

- Files: `src/NCode.Identity.OpenId.Management/Endpoints/Tenants/TenantApiEndpointHandler.cs`,
  `tests/NCode.Identity.OpenId.Management.Tests/Endpoints/Tenants/TenantApiEndpointHandlerTests.cs`.
- Change: D10's management callers (constructor gains `IRemoteSecretDestroyer`).
- Tests: delete secret and delete tenant call the destroyer once with the removed records after save and after the
  tenant-changed notification (Moq `MockSequence` or callback order); a failed save never calls it.
- Done when: `dotnet test tests/NCode.Identity.OpenId.Management.Tests/NCode.Identity.OpenId.Management.Tests.csproj -m:2 --filter "FullyQualifiedName~TenantApiEndpointHandler" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 8. AWS KMS remote key provider

- Files: `src/NCode.Identity.Secrets.KeyWrapping.AwsKms/{AwsKmsRemoteKeyProvider,AwsKmsSigningAlgorithms,DefaultRegistration}.cs`,
  `src/NCode.Identity.Secrets.KeyWrapping.AwsKms/Options/{AwsKmsRemoteKeyProviderOptions,AwsKmsRemoteKeyProviderOptionsValidator}.cs`,
  `src/NCode.Identity.Secrets.KeyWrapping.AwsKms/Facades/{AwsKmsClientConfiguration,DefaultAwsKmsClientFactory}.cs`,
  `src/NCode.Identity.Secrets.KeyWrapping.AwsKms/NCode.Identity.Secrets.KeyWrapping.AwsKms.csproj` (Description: "AWS KMS
  key wrapper and remote key provider for NCode.Identity."), `src/NCode.Identity.Secrets.KeyWrapping.AwsKms/PublicAPI.Unshipped.txt`,
  tests `tests/NCode.Identity.Secrets.KeyWrapping.AwsKms.Tests/{AwsKmsRemoteKeyProviderTests,AwsKmsSigningAlgorithmsTests,AwsKmsClientConfigurationTests,DefaultRegistrationTests}.cs`,
  `tests/.../Options/AwsKmsRemoteKeyProviderOptionsValidatorTests.cs`.
- Change: D16 (AWS), D17 (AWS), D18 rows.
- Tests (Moq strict `IAmazonKeyManagementService` through `IAwsKmsClientFactory`): create sends the spec, usage, tags
  and description and returns the ARN and SPKI; a public key with another spec, usage or missing algorithm is refused;
  sign sends the digest with `DIGEST`, the signing algorithm and the ARN's region client, and refuses a response naming
  another key or algorithm; a `null`-digest row sends `RAW`; destroy schedules with the window, completes on
  `NotFoundException` and on pending deletion, rethrows another invalid state; the rows include every D18 AWS row;
  the client configuration is standard mode with three retries; registration adds the provider, the source, the
  validated options and the factory once when called twice.
- Pattern: `AwsKmsKeyWrapperTests`, `AwsKmsKeyWrapperOptionsValidatorTests`, `DefaultRegistrationTests` in that project.
- Done when: `dotnet test tests/NCode.Identity.Secrets.KeyWrapping.AwsKms.Tests/NCode.Identity.Secrets.KeyWrapping.AwsKms.Tests.csproj -m:2 --filter "FullyQualifiedName~RemoteKeyProvider|FullyQualifiedName~SigningAlgorithms|FullyQualifiedName~ClientConfiguration|FullyQualifiedName~DefaultRegistration" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 9. Azure Key Vault remote key provider

- Files: `src/NCode.Identity.Secrets.KeyWrapping.AzureKeyVault/{AzureKeyVaultRemoteKeyProvider,AzureKeyVaultSigningAlgorithms,DefaultRegistration}.cs`,
  `Options/{AzureKeyVaultRemoteKeyProviderOptions,AzureKeyVaultRemoteKeyProviderOptionsValidator}.cs`,
  `Facades/{IAzureKeyVaultClientFactory,DefaultAzureKeyVaultClientFactory,AzureKeyVaultClientOptions}.cs`, the csproj
  Description, `PublicAPI.Unshipped.txt`; tests `tests/NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests/{AzureKeyVaultRemoteKeyProviderTests,AzureKeyVaultSigningAlgorithmsTests,DefaultRegistrationTests}.cs`,
  `Facades/AzureKeyVaultClientOptionsTests.cs`, `Options/AzureKeyVaultRemoteKeyProviderOptionsValidatorTests.cs`.
- Change: D16 (Azure), D17 (Azure), D18 rows.
- Tests (Moq `KeyClient`, `CryptographyClient`; build results with `KeyModelFactory` and `CryptographyModelFactory`):
  RSA and EC creation options (name with `-h`/`-s`, size or curve, operations, tags), versioned id and SPKI returned;
  a versionless id is refused; sign sends the algorithm and digest to the versioned client and refuses another key id
  or algorithm; destroy disables the version and completes on 404; options set exponential retry, three retries.
- Pattern: `AzureKeyVaultKeyWrapperTests`, `Facades/` tests in that project.
- Done when: `dotnet test tests/NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests/NCode.Identity.Secrets.KeyWrapping.AzureKeyVault.Tests.csproj -m:2 --filter "FullyQualifiedName~RemoteKeyProvider|FullyQualifiedName~SigningAlgorithms|FullyQualifiedName~ClientOptions|FullyQualifiedName~DefaultRegistration" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 10. Google Cloud KMS remote key provider

- Files: `src/NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms/{GoogleCloudKmsRemoteKeyProvider,GoogleCloudKmsSigningAlgorithms,GoogleCloudKmsCallSettings,GoogleCloudKmsKeyName,DefaultRegistration}.cs`,
  `Options/{GoogleCloudKmsRemoteKeyProviderOptions,GoogleCloudKmsRemoteKeyProviderOptionsValidator}.cs`, the csproj
  Description, `PublicAPI.Unshipped.txt`; tests `tests/NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Tests/{GoogleCloudKmsRemoteKeyProviderTests,GoogleCloudKmsSigningAlgorithmsTests,GoogleCloudKmsKeyNameTests,DefaultRegistrationTests}.cs`,
  `Options/GoogleCloudKmsRemoteKeyProviderOptionsValidatorTests.cs`.
- Change: D16 (Google), D17 (Google), D18 rows; the provider takes `TimeProvider` for polling.
- Tests (Moq strict `KeyManagementServiceClient`, `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`
  — add the package reference to the test csproj): version creation on an existing crypto key; crypto key creation on
  `NotFound` with template, protection level, labels and `SkipInitialVersionCreation`; `AlreadyExists` tolerated;
  polling until `Enabled`, failing on `GenerationFailed` and at the bound; public key name, checksum and algorithm
  checks; sign with the digest field for each hash, checksums verified, retrying call settings passed (capture the
  `CallSettings` argument); destroy completes on `NotFound`/`FailedPrecondition`; every D18 Google row parses to a
  `CryptoKeyVersionAlgorithm`; key ring name rule.
- Pattern: `GoogleCloudKmsKeyWrapperTests`, `GoogleCloudKmsKeyNameTests`.
- Done when: `dotnet test tests/NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Tests/NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Tests.csproj -m:2 --filter "FullyQualifiedName~RemoteKeyProvider|FullyQualifiedName~SigningAlgorithms|FullyQualifiedName~GoogleCloudKmsKeyName|FullyQualifiedName~DefaultRegistration" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 11. The host offers its configured providers, and a tenant's tokens are signed remotely end to end

- Files: `src/NCode.Identity.OpenId.Host/Program.cs`, `src/NCode.Identity.OpenId.Host/README.md` (new subsection
  "### Remote signing keys" after "### Secret protection": the three sections and their keys, the tenant setting, the
  permissions, a pointer to the capability page),
  `tests/NCode.Identity.OpenId.Integration.Tests/Infrastructure/TestRemoteKeyProvider.cs` (new: `IRemoteKeyProvider`
  named `test-kms` holding RSA/ECDsa keys in memory, signing digests with `SignHash` — PSS for PS*, PKCS#1 for RS*,
  DER for ECDSA — counting signatures and destructions; plus its `IRemoteSigningAlgorithmSource` rows for RS256/PS256
  3072 and ES256 P-256 with `IsDerEncodedSignature = true` for ES256),
  `tests/NCode.Identity.OpenId.Integration.Tests/Endpoints/RemoteSigningKeyTests.cs` (new).
- Change: Program.cs registers `AddAwsKmsRemoteKeyProvider`, `AddAzureKeyVaultRemoteKeyProvider` and
  `AddGoogleCloudKmsRemoteKeyProvider` each only when its options section exists, each with a one-line comment like
  its wrapper's.
- Tests: (a) a production-keys factory (copy `TenantKeyRolloverTests.ProductionKeysApplicationFactory`) with the test
  provider registered and `OpenId:Server:Settings:tenant_signing_key_provider=test-kms`: the tenant holds three keys,
  each stored with encoding `remote` and no protected material (read through `ITenantStore` from `factory.Services`),
  the JWKS publishes them, a client-credentials token is signed by one of them, validates against the JWKS key, and the
  provider counted a signature; (b) a tenant that starts local and PATCHes `tenant_signing_key_provider=test-kms`
  (pattern: `TenantKeyProvisioningTests` PATCH `{TenantApiPath}/settings`) publishes remote keys that activate after the
  activation delay while the local keys still sign; (c) deleting a remote secret through the management API destroys
  it in the provider.
- Done when: `dotnet test tests/NCode.Identity.OpenId.Integration.Tests/NCode.Identity.OpenId.Integration.Tests.csproj -m:2 --filter "FullyQualifiedName~RemoteSigningKey|FullyQualifiedName~TenantKeyRollover|FullyQualifiedName~TenantKeyProvisioning|FullyQualifiedName~JwksEndpoint|FullyQualifiedName~TokenEndpointClientCredentials|FullyQualifiedName~AuthorizationRequestObject|FullyQualifiedName~JwtSecuredAuthorizationResponse|FullyQualifiedName~UserInfoEndpoint" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 12. ADR-1413 and ADR-1414

- Files: the two new ADR files, `docs/decisions/README.md`, the Status lines of
  `docs/decisions/{1104-import-public-asymmetric-secret-material,1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace,1120-jwt-validation-requirements-and-handlers,1176-dpop-proofs-are-a-jose-building-block,1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator}.md`.
- Change: as "ADRs to write". Copy the shape of `0000-template.md` and the `**Amends:**` line of ADR-1404.
- Done when: `pwsh -NoProfile -File .template/Test-AdrIndex.ps1 && pwsh -NoProfile -File build/adr-index.ps1`

### [ ] 13. Docs and changelog

- Files: `docs/capabilities/remote-signing-keys.md` (new, from `_template.md`), `docs/capabilities/README.md` (row:
  "🗝️ [Remote Signing Keys](./remote-signing-keys.md) | A tenant's signing keys can stay inside AWS KMS, Azure Key Vault
  or Google Cloud KMS: every token is signed there and verified before it leaves, and the private key never reaches the
  server."), `docs/capabilities/key-rollover.md` (a promise and a paragraph on custody: a change of
  `tenant_signing_key_provider` hands over after the delay with `custody_changed`; remote successors are new key
  versions; the setting in the operator guide row), `docs/capabilities/encryption-at-rest.md` (the "Sign and decrypt
  everything inside the KMS" option and the "compromised running replica" limit describe signing as available opt-in,
  linking the new page, and decryption as the remaining gap), `docs/capabilities/selective-disclosure.md`
  (`IssueAsync`, `CreatePresentationAsync`), `docs/audit-records.md` (`custody_changed` for `tenant.key.generated` and
  `tenant.key.retired`; the `remote_key_provider` detail), `AGENTS.md` (Secrets bullet: "opt-in KMS adapters ... that
  wrap the root key and hold remote signing keys", add ADR-1413 link), `README.md` (Secrets row: "KMS adapters (key
  wrappers and remote key providers)"), `CHANGELOG.md`.
- Capability page content: status Available for tenant signing keys; packages; key decisions 1413/1414/1404; promises
  (material never leaves, every signature verified, JWKS never calls the KMS, per-tenant opt-in, hand-over after the
  delay, successors as key versions, destruction after removal, bounded jittered retry); a Mermaid sequence of one token
  signing; the mapping table (D18); the configuration; permissions per provider; Limits and roadmap (latency and fee per
  token, KMS outage stops issuance, orphan key after a lost race, Google crypto keys permanent, Azure versions disabled,
  encryption keys local, management surface and chart follow-ups with their issue numbers once filed — until filed,
  name them without numbers).
- CHANGELOG (`## [Unreleased]`, present tense, catalog style matching neighbours):
  - `### Added` new entry after the key rollover entry: "**A tenant's signing keys can stay inside a key management
    service, which signs every token.**" — setting, providers and their sections, storage (`remote` encoding, stored
    public key), mapping (PS256/ES256 per provider, DER to `r‖s`), hand-over, versions, destruction, retry, the
    `Add<Vendor>RemoteKeyProvider` registrations and the remote vocabulary types; links ADR-1413, ADR-1414, the capability
    page, [#165](https://github.com/NCodeGroup/Identity/issues/165).
  - Edit in place: the JOSE serializer entry (encoders are `JoseEncoder.EncodeAsync` and every signature goes through
    `SignatureAlgorithm.SignAsync`), the DPoP entry (`CreateProofAsync`), the SD-JWT entry (`IssueAsync`,
    `CreatePresentationAsync` where it names the operations), the JWT entry (`IJwtEncoder` (`EncodeAsync`)), each adding
    the ADR-1414 link.
  - `### Security` new entry: "**A remote signing key never leaves its KMS, and every signature it makes is verified
    before a token carries it.**" (local verification, hardware protection by default on Azure and Google, sign-only
    key usage, identifiers-only logging, destruction only of keys the server created).
- Done when: `pwsh -NoProfile -Command "if (-not (Test-Path docs/capabilities/remote-signing-keys.md)) { exit 1 }; if (-not (Select-String -Path CHANGELOG.md -Pattern 'issues/165' -Quiet)) { exit 1 }; if (Select-String -Path docs/capabilities/selective-disclosure.md -Pattern 'ISdJwtIssuer\.Issue\b' -Quiet) { exit 1 }" && pwsh -NoProfile -File build/adr-index.ps1`

### [ ] 14. Final checks

- Files: none beyond fixes the checks require.
- Change: run `dotnet csharpier check src tests`, `pwsh -NoProfile -File build/doc-comment-citations.ps1`,
  `dotnet build NCode.Identity.slnx -m:2`, grep the diff for temporal words
  (`git diff origin/main -- . ':!*.Designer.cs' | grep -nE '^\+.*\b(now|no longer|previously|was|newly|Breaking|migrat|upgrade)\b'`
  and fix every hit that describes a change rather than the product; "now" as an instant in planner code is fine),
  then `dotnet build-server shutdown`.
- Done when: `dotnet csharpier check src tests && pwsh -NoProfile -File build/doc-comment-citations.ps1 && dotnet build NCode.Identity.slnx -m:2`

## Review checklist

- [ ] No synchronous `Encode`, `CreateProof`, `Issue` or `CreatePresentation` remains on a public signing surface; no
      caller blocks on a task; every async method passes its token.
- [ ] `SignatureAlgorithm.SignAsync` verifies every remote signature with the stored public key and its length; a
      local key completes synchronously.
- [ ] The JWS signing input bytes are unchanged (existing round-trip and interop tests pass unmodified apart from
      the async call shape).
- [ ] A remote secret's record holds no private material, and `GetProtectorForSecretsAsync` never passes it.
- [ ] A remote key fails at load, not at first signature, when its provider, row, public key or algorithm is missing.
- [ ] DER→P1363 handles short and leading-zero integers and rejects malformed input; PS256 salt is the hash length.
- [ ] Custody change hands over after the activation delay; operator keys are never retired or destroyed; destruction
      runs only after commit and after the tenant-changed announcement, only for `generated` remote secrets.
- [ ] The setting's rule lists only registered providers and never reads `OpenIdOptions`; the host refuses a provider
      that cannot hold a provisioned signature key.
- [ ] One retry layer per call (the SDK's), configured explicitly; GCP retry filter covers `ResourceExhausted`.
- [ ] Logs carry identifiers only; no digest, input, signature or key material.
- [ ] Vocabularies are open: rows come from registered sources; the only closed mappings are over a provider API's own
      enumerations, each commented.
- [ ] `PublicAPI.Unshipped.txt` exact in every touched project; no ADR citations in `///`; no `!` outside tests;
      csharpier clean.
- [ ] ADR-1413/1414 written, indexed in order, amended records' Status lines and index rows updated, bodies untouched.
- [ ] Docs, capability pages, CHANGELOG and testing instructions describe the product as it is; no temporal phrasing.
- [ ] No EF model change; no `.rsp` or `PLAN.md` staged; commits carry no AI attribution.

## Follow-ups

- Show a key's remote provider in the management API (`SecretResource`) and console, and let an operator generate or
  import a remote key through the management API.
- Helm chart: configure `SecretProtection:RemoteKeyProviders:*` and mount the workload identity of a remote key
  provider that is not the root key provider.
- Remote decryption: tenant encryption keys (RSA-OAEP, ECDH-ES) held in a KMS.
- Remote signing keys for a server, the protocol client's `private_key_jwt` and DPoP keys, and the BFF's client keys.
- Name the `NCode.Identity.Secrets.KeyWrapping.*` packages for both roles they hold (KMS adapters).
- Sweep orphaned KMS keys (tagged with the owner and group) that no stored secret names.
