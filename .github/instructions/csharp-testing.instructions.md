---
description: "NCode.Identity test-project conventions — true unit tests, xUnit, Moq .Verifiable() with MockBehavior.Strict, Method_State_Expected naming, #region organization, hermetic."
applyTo: "tests/**/*.cs"
---

# NCode.Identity test conventions

NCode.Identity's own rules for test projects. The project template's
[`csharp-tests.instructions.md`](csharp-tests.instructions.md) holds the general test conventions and applies to every
`*.Tests` project; this file keeps what the family adds to them or decides differently, and a bullet that names a
template rule links its section there. Where the two differ, this file wins. Production code's testability rules — the
seams, facades, and `InternalsVisibleTo` wiring that make code mockable — live in
[`csharp-production.instructions.md`](csharp-production.instructions.md) §7.

## Structure & naming

- 👁 **Test files mirror the folder structure and namespace of the code under test** — template
  [File Location and Namespace](csharp-tests.instructions.md#file-location-and-namespace)
  (`NCode.Identity.Jose/JoseSerializer.cs` → `NCode.Identity.Jose.Tests/JoseSerializerTests.cs`).
- 👁 **Test method names use the `Method_State_Expected` idiom** — template
  [Test Style](csharp-tests.instructions.md#test-style).
- ⏳ **Warnings are errors in test projects too** — template
  [Warnings and Analyzers](csharp-tests.instructions.md#warnings-and-analyzers) and
  [ADR-1127](../../docs/decisions/1127-warnings-are-errors-in-every-project.md); relax a rule that is noise in tests in
  the `[tests/**/*.cs]` block of [`.editorconfig`](../../.editorconfig) with a one-line justification, never with a
  project-level `NoWarn`.
- ✅ **Prefer a real fix over a suppression for nullable warnings** — template
  [Warnings and Analyzers](csharp-tests.instructions.md#warnings-and-analyzers); `build/ProjectTemplate.props`
  references `Nullable.Extended.Analyzer` only in projects whose name does not end in `.Tests`.
- 👁 **Organize tests with `#region` directives** by the member under test — template
  [Regions](csharp-tests.instructions.md#regions).
- 👁 **Build a persisted fixture with its builder.** `PersistedClientBuilder`, `PersistedTenantBuilder`,
  `PersistedTeamBuilder` and `PersistedResourceServerBuilder` live in `NCode.Identity.OpenId.Persistence.TestSupport`
  and `PersistedSecretBuilder` in `NCode.Identity.Secrets.Persistence.TestSupport`, each in the namespace of the type
  it builds. `Build()` copies the identifiers into the settings and secrets documents; their concurrency tokens come
  from `SettingsConcurrencyToken` and `SecretsConcurrencyToken`. `BuildSettings()` and `BuildSecrets()` build one
  document for a store that returns it alone, and a test that needs the document a built fixture carries reads it from
  the fixture (`team.Settings` in `TeamApiEndpointHandlerTests`). Set only the members that differ from the builder's
  defaults, and keep a private factory in a test file only for the values its tests vary
  (`CreateClient(bool isDisabled)` in `DefaultPrincipalResolverTests`). A concurrency token or identifier a test reads
  stays.
- 👁 **A fixture that more than one test project builds lives in a shared support project**, such as
  `NCode.Identity.OpenId.Persistence.TestSupport` or `NCode.Identity.Secrets.Persistence.TestSupport`, never as a copy
  per test file.

## Style

- 👁 **No `// Arrange`, `// Act`, `// Assert` comments; one behavior per test** — template
  [Test Style](csharp-tests.instructions.md#test-style).
- 👁 **The production code-style rules apply in tests** — template
  [Test Style](csharp-tests.instructions.md#test-style).
- 👁 **Name an `IServiceCollection` `serviceCollection`** —
  [`csharp-production.instructions.md`](csharp-production.instructions.md) §4: every local, parameter and lambda
  parameter, as in `var serviceCollection = new ServiceCollection();` and
  `builder.ConfigureTestServices(serviceCollection => …)`; an `IServiceProvider` keeps its own name.
- 👁 **Prefer the size-specific xUnit assertions** — template [Assertions](csharp-tests.instructions.md#assertions).
- 👁 **Compare an `ImmutableArray<T>` by its elements** — template
  [Assertions](csharp-tests.instructions.md#assertions).
- 👁 **Assert on the exception type the contract guarantees** — template
  [Assertions](csharp-tests.instructions.md#assertions). Signing with a public-only RSA key raises
  `CryptographicException` on Windows and a derived `Interop+Crypto+OpenSslCryptographicException` on OpenSSL-backed
  platforms, so only `Assert.ThrowsAny<CryptographicException>` holds on both; keep `Assert.Throws<T>` for the family's
  own `JoseException` and `OpenIdException`.
- 👁 **A test that a rule refuses an alternative makes the alternative viable** — template
  [What a Test Proves](csharp-tests.instructions.md#what-a-test-proves): to prove a token is encrypted to a resource
  server's key or not at all, give the tenant a key the policy permits, and pair the test with a control.
- 👁 **Test a curve binding with a key whose size the algorithm accepts.** An algorithm checks a key's size before its
  curve, so a key on a curve of another size is refused by the size check, and the test passes with the curve rule
  removed. Use a same-size named curve where one exists (`brainpoolP256r1` for `ES256`, `brainpoolP384r1` for
  `ES384`, P-256 for `ES256K` and secp256k1 for `ES256`); otherwise use a stub key that declares the algorithm's size on another curve, because no other named
  curve has 521 bits and Ed25519 and Ed448 differ in size (`StubEccSecretKey` in `EccSignatureAlgorithmTests`,
  `StubOkpSecretKey` in `EdDsaSignatureAlgorithmTests`). An `OKP` key's use is tested the same way: an X25519 key
  has an Ed25519 key's size, so it proves that the curve's use, not the size check, refuses it for `EdDSA` and an
  Ed25519 key for `ECDH-ES`
  ([ADR-1390](../../docs/decisions/1390-x25519-and-x448-agree-keys-beside-the-okp-signing-keys-and-a-curves-use-separates-them.md)).
- 👁 **A test of a curve the platform may lack skips through a fact attribute, and Linux proves the gate.** .NET serves
  secp256k1 only where the platform's cryptography does (not on macOS), so a test that needs the curve is a
  `[Secp256k1Fact]`, which sets `Skip` from the package's own probe, rather than one that returns early and reports a
  pass. One unconditional test asserts that the probe answers `true` on Linux, where CI runs, so a gate that wrongly
  answers `false` cannot skip every test silently (`Secp256k1CurveTests`)
  ([ADR-1392](../../docs/decisions/1392-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md)).
- 👁 **Test a contributed key type's private members with a member no standard key type uses.** A JWK counts a
  member as private when it is a standard private name (`d`, `k`, `priv` and the RSA factors) or one of its key
  type's `JsonWebKeyTypeDescriptor.PrivateMembers`, so a contributed type that keeps its private half in `d` is
  refused by the standard names, and the test passes with the type's own rule removed. Give the type a private
  member of its own (`sk` on a made-up `XKP` type), assert that the key read without its descriptor
  (`new JsonWebKey(element)`) reports no private material, and assert the refusal's error text, because a later
  check, the descriptor's JWK import, refuses the same key with another message (`ContributedKeyTypeDPoPProofTests`).
- 👁 **A fix that moves a refusal earlier is proven by a request the old code deferred** — template
  [What a Test Proves](csharp-tests.instructions.md#what-a-test-proves), such as an authorization request without
  `prompt=none`, which the old code answers with the login challenge (`302`).
- ✅ **Tests encode text through a strict encoding too.** Use `SecureEncoding.UTF8` / `SecureEncoding.ASCII`; the build
  enforces it with `RS0030`. A test that needs UTF-16 or a byte-order mark builds a strict encoding, such as
  `new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true)`. See
  [ADR-1266](../../docs/decisions/1266-text-is-encoded-and-decoded-only-through-strict-encodings.md).
- ✅ **No decision-record citations in `///` doc comments in tests either.** Put the citation on a `//` line after the
  doc comment (`// See ADR-1201.`), as production code does
  ([`csharp-production.instructions.md`](csharp-production.instructions.md) §10); the `doc comment citations` stage of
  [`build/dod.ps1`](../../build/dod.ps1) scans `tests/` as well as `src/`.

## Isolation & mocking

- 👁 Tests are **true unit tests** that isolate the code under test with Moq, and use the real engine where the engine
  is the unit — template [Real Collaborators](csharp-tests.instructions.md#real-collaborators).
- 👁 **Every Moq setup ends with `.Verifiable()`, names its lambda parameter `x`, and is verified by a `UnitTestBase`
  that owns a `MockRepository(MockBehavior.Strict)`** — template
  [Strict Mocks, Verified Automatically](csharp-tests.instructions.md#strict-mocks-verified-automatically).
- 👁 **`MockBehavior.Loose` only in the template's two cases** — template
  [Strict Mocks, Verified Automatically](csharp-tests.instructions.md#strict-mocks-verified-automatically): a partial
  mock of an abstract base (`CreatePartialMock<KeyManagementAlgorithm>()`) and a value-provider stub (an
  `IReadOnlySettingCollection` or `IOpenIdError` shim).
- 👁 **Hermetic: no network, no ambient host** — template [Isolation](csharp-tests.instructions.md#isolation); a stub a
  single test needs is a small local `private sealed` type in the test file.
- 👁 **A test that listens to a framework meter sees the whole process** — template
  [Isolation](csharp-tests.instructions.md#isolation).
- 👁 **Dispose a test's real service provider asynchronously** — template
  [Real Collaborators](csharp-tests.instructions.md#real-collaborators): a test that resolves the real algorithms or
  key types (`AddSecretsLibrary().AddJoseLibrary()`) implements `IAsyncLifetime` and awaits its `DisposeAsync()`.
- 👁 **Keep test-only reach through `InternalsVisibleTo`** — template
  [InternalsVisibleTo](csharp-tests.instructions.md#internalsvisibleto).
- 👁 **A strict `.Verifiable()` setup is consumed on every path that uses the shared scaffold** — template
  [Strict Mocks, Verified Automatically](csharp-tests.instructions.md#strict-mocks-verified-automatically).
- 👁 **A source-generated `ILogger<T>` needs only a loose mock.** `[LoggerMessage]` methods check `IsEnabled` first, and
  a loose mock returns `false`, so `Log` is never called and no setup is needed — a sanctioned value-provider loose mock
  (`CreateLooseMock<ILogger<T>>()`). When the log entry **is** the behavior under test (an error logged instead of
  thrown), use a strict mock with `IsEnabled(level)` returning `true` and a `.Verifiable()`
  `Log(level, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), …, It.IsAny<Func<It.IsAnyType, Exception?, string>>())`
  setup; `It.IsAnyType` matches the generated state struct.
- 👁 **Mock the enumerator the code actually calls** — template
  [Setup Techniques](csharp-tests.instructions.md#setup-techniques).
- 👁 **Moq cannot match a `ReadOnlySpan<T>` / `Span<T>` argument** — template
  [Setup Techniques](csharp-tests.instructions.md#setup-techniques) (`ISecretGenerator.ImportSharedSecretVerifier`).
- 👁 **Compute a setup's expected value outside the Moq expression** — template
  [Setup Techniques](csharp-tests.instructions.md#setup-techniques) (`CS8640`, `CS9226`):
  `var expectedKey = ExpiringKeys.Create(…);`.
- 👁 **Stop the unit at the mediator to inspect a command it builds midway** — template
  [Setup Techniques](csharp-tests.instructions.md#setup-techniques) (`DefaultTokenExchangeGrantHandlerTests`).
- 👁 **Run a function argument through a strict setup.** When the unit passes a function that the collaborator calls
  midway (the `isRefusedAsync` of `ILocalAccountStore.VerifyCredentialAsync`), set up
  `.Returns((T1 _, T2 _, Func<…> callback, CancellationToken cancellationToken) => HelperAsync(callback, …,
  cancellationToken))` with a static `async` helper rather than an `async` lambda, so the delegate returns the member's
  own `ValueTask<T>` (`LocalAccountStoreSetup.SetupVerification`).
- 👁 **Match a `JsonElement` argument by content** — template
  [Setup Techniques](csharp-tests.instructions.md#setup-techniques).
- 👁 **Make one call of a strict setup fail by adding a narrower setup after the general one** — template
  [Setup Techniques](csharp-tests.instructions.md#setup-techniques) (`PlatformApiEndpointHandlerTests`,
  `DefaultTenantClientCoherenceCheckerTests`).
- 👁 **Answer a call differently each time from one counted setup, not `SetupSequence`.** Moq 4.21 has no
  `.Verifiable()` on `SetupSequence`, so a sequence escapes the repository's verification and an answer it lists but
  never gives passes unnoticed. Count the calls in a local, answer from the count, and end the setup with
  `.Verifiable(Times.Exactly(n))`:
  `.Returns(() => ++saves == 1 ? ValueTask.FromException(exception) : ValueTask.CompletedTask)`
  (`StoreManagerMockExtensions.SetupSaveChangesFailingOnce`), or
  `.ReturnsAsync(() => ++batches == 1 ? BatchSize : 5)` for a value. When the call that answers differently has
  arguments of its own, give it its own setup, or the narrower setup above. `SetupSequence` stays only in the
  read-and-retry case under [Persistence & integration tests](#persistence--integration-tests).
- ⏳ **Read time from a `FakeTimeProvider`** — template
  [Time and Concurrency](csharp-tests.instructions.md#time-and-concurrency). A test project that reads time
  references `Microsoft.Extensions.TimeProvider.Testing` in its own `.csproj`, as it references `Moq`. Start the clock
  at a fixed instant and move it with `Advance`, or with `SetUtcNow` to an exact later instant; it never moves back, so
  a test that needs an earlier time starts there; `DefaultOpenIdClientResolverTests` and
  `DefaultInProcessExpiringKeySetTests` show it. Never write a `TimeProvider` subclass or mock `GetUtcNow`. A strict
  `TimeProvider` mock stays only where a test drives a timer loop through the captured `CreateTimer` callback, below,
  whose setup pins the period. Rolling out: the strict `GetUtcNow` mocks still in other test classes move to it
  ([#509](https://github.com/NCodeGroup/Identity/issues/509)).
- 👁 **Drive a `BackgroundService`'s timer loop through the captured timer callback.** A `PeriodicTimer` on a
  `TimeProvider` asks it for `CreateTimer(callback, state, period, period)`: set that up on the strict `TimeProvider`
  mock, capture the callback and state in a `TaskCompletionSource`, return a strict `ITimer` whose `Dispose()` is set
  up, and start the service. Invoke the callback once per tick and await a `TaskCompletionSource` that the tick's work
  completes (`WaitAsync` with a timeout) before the next tick. Tick twice with the first tick's work failing, then
  `StopAsync` and assert `ExecuteTask.IsCompletedSuccessfully`, so a loop that ends after one tick or on a failed tick
  fails the test (`ExecuteAsync_WhenAPruneFails_PrunesAgainAtTheNextTick` in `AuditRecordPruneServiceTests`,
  `GrantPruneServiceTests` and `TenantUsagePruneServiceTests`).
- 👁 **Capture an event handler a strict mock receives and invoke it directly** — template
  [Setup Techniques](csharp-tests.instructions.md#setup-techniques) (`ValkeyReplicaBroadcastSubscriberTests`).
- 👁 **A change-token producer in a test returns a fresh token after each change** — template
  [Setup Techniques](csharp-tests.instructions.md#setup-techniques): swap in a new `CancellationTokenSource` before
  cancelling the old one (`TeamSettingsReconciliationServiceTests`).
- 👁 **Test the rate limiter with descriptors of the test's own.** `DefaultRequestRateLimiterTests` mocks
  `IRateLimitDescriptorCatalog` and declares sliding and fixed descriptors, so the built-in table is tested only in
  `DefaultRateLimitDescriptorDataSourceTests`; match the batch with
  `It.Is<IReadOnlyList<ExpiringCounterIncrement>>(x => x.SequenceEqual(expected))`, and build the `RedisValue` arrays a
  Valkey test expects from the same CLR types the adapter passes (`long`, `double`).
- 👁 **Prove a shared retrieval with a gated stub handler.** The handler awaits a `TaskCompletionSource`: start two
  calls, release the gate, and assert one request; cancel the first caller and dispose the input it passed to prove that
  the retrieval runs on its own token and its own copy (`DefaultClientCertificateRevocationCheckerTests`).
- 👁 **Test a migration rule with synthetic migrations.** Private sealed `Migration` subclasses in the test file,
  carrying the markers under test, exercise `ExpandOnlyRule.Check` without a database; a theory over the host's real
  migrations proves that each one is classified (`ExpandOnlyRuleTests`), and `InitialCreateTests` reads the same
  migrations through `HostMigrationCatalog`.

## Persistence & integration tests

- 👁 **Store and EF behavior is tested against a real provider, not a mocked store** — template
  [Entity Framework Core](csharp-tests.instructions.md#entity-framework-core); a `SavingChanges`-only interceptor never
  runs, because the codebase saves through `SaveChangesAsync`
  ([ADR-1012](../../docs/decisions/1012-interceptor-managed-concurrency-tokens.md)).
- 👁 **A handler that writes through more than one store needs an end-to-end test** — template
  [Entity Framework Core](csharp-tests.instructions.md#entity-framework-core): a store resolves the tenant that owns a
  row with a database query, so a row a tenant owns can be added only after the tenant row commits
  (`TenantProvisioningTests`).
- 👁 **EF InMemory enforces no concurrency token, unique index (filtered or not) or foreign key** — template
  [Entity Framework Core](csharp-tests.instructions.md#entity-framework-core); test them on SQLite in-memory
  (`ClientStoreRelationalRemovalTests`, `TenantStoreRelationalRemovalTests`).
- 👁 **EF Core's SQLite provider cannot compare or order a `DateTimeOffset`** — template
  [Entity Framework Core](csharp-tests.instructions.md#entity-framework-core); test a grant's `ExpiresWhen` or
  `RevokedWhen` query on EF InMemory.
- 👁 **Name an EF InMemory database once, outside the options lambda** — template
  [Entity Framework Core](csharp-tests.instructions.md#entity-framework-core)
  (`AuditRecordStoreTests.CreateInMemoryProvider`).
- 👁 **A test that adds an entity to the `OpenIdDbContext` model calls `EnableServiceProviderCaching(false)`** —
  template [Entity Framework Core](csharp-tests.instructions.md#entity-framework-core)
  (`TenantStoreContributedEntityRemovalTests`).
- 👁 **A lost race through the store manager is `StoreConcurrencyException`;** only a test that calls
  `DbContext.SaveChangesAsync` directly sees EF's `DbUpdateConcurrencyException`. See
  [ADR-1208](../../docs/decisions/1208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md).
- 👁 **Build a stored grant's payload with `TestGrantPayloadProtector.Protect(...)`**
  (which wraps the real `DefaultPersistedPayloadSerializer`), never a bare `JsonSerializer` call. The server stores a
  protected, versioned envelope, so a test that hands the code the bare payload passes while the code fails on every
  stored row (`DefaultSessionServiceTests`).
- 👁 **Seed a settings document the vocabulary cannot honor through the store, and prove it unreadable first.** The
  management API refuses such a document, so `SeedPublicClientWithSettingsAsync` writes it with an unregistered
  setting name; assert that the incoherent-client report lists it with a `documentError` before the behavior under
  test, so the test cannot pass on a document the server reads (`UnreadableSettingsDocumentTests`).
- 👁 **Make a served tenant's own settings document unreadable after the management client authenticates.**
  `HostApplicationFactory.ReplaceTenantSettingsAsync` writes the document through the store and leaves the materialized
  tenant cached, so the token still validates and the management request reads the document
  (`UnreadableSettingsDocumentTests`). A handler test that a read authorizes before it resolves gives the strict
  resolver no setup (`GetEffectiveSettingsAsync_WhenForbidden_ReturnsForbidWithoutResolvingTheSettings`).
- 👁 **A strict-mock test of a read-and-retry loop returns a new object for each read** — template
  [Setup Techniques](csharp-tests.instructions.md#setup-techniques).
- 👁 **Test a read-modify-write through the update function it passes** — template
  [Setup Techniques](csharp-tests.instructions.md#setup-techniques) (`DefaultSessionServiceTests`,
  [ADR-1222](../../docs/decisions/1222-a-grants-payload-is-changed-by-a-read-modify-write.md)).
- 👁 **A mocked `OpenIdContext` or `OpenIdTenant` that the code under test disposes sets up its protected
  `DisposeAsyncCore`** — template [Setup Techniques](csharp-tests.instructions.md#setup-techniques)
  (`EndedSessionTerminationServiceTests`).
- 👁 **A global query filter is bypassed for change-tracked entities** — template
  [Entity Framework Core](csharp-tests.instructions.md#entity-framework-core); the tenant-scoping filter
  ([ADR-1018](../../docs/decisions/1018-tenant-scoped-data-access-at-the-persistence-layer.md)) behaves this way, and EF
  InMemory honors query filters, so it covers the case.
- 👁 **Data-protection integration tests are not hermetic under a shared host** — template
  [Integration Tests](csharp-tests.instructions.md#integration-tests): give each such test its own
  `using var factory = new HostApplicationFactory();`.
- 👁 **Each `HostApplicationFactory` instance owns one in-memory database and one root key, which every host it builds
  shares.** The factory keeps its `InMemoryDatabaseRoot` and root key on the instance. `factory.WithWebHostBuilder(…)`
  builds a separate host (its own service provider, caches, and hosted services) over that database and root key,
  because hosts that share a database must unwrap the same master keys. So data one host commits is visible to the
  others, but a tenant one host materialized, or usage it recorded and has not flushed, is not. Seed before the host
  that must see the data serves its first request. A test that needs isolation creates its own
  `HostApplicationFactory`. The factory also owns one Data Protection key ring, in its own in-memory
  `DeveloperDataProtectionDbContext` database, so no test reads or writes the machine's key directory; a directly
  built host that starts with `AddConfiguration` sets `KeyManagementOptions.XmlRepository` to an in-memory repository
  (`IdentityServerBuilderExtensionsTests`).
- 👁 **Never assert a startup failure through `WebApplicationFactory`** — template
  [Integration Tests](csharp-tests.instructions.md#integration-tests).
- 👁 **The integration host is stopped by its own run loop, once** — template
  [Integration Tests](csharp-tests.instructions.md#integration-tests): `HostApplicationFactory.CreateHost` returns an
  `ApplicationRunLoopHost`, whose `StopAsync` calls `StopApplication` and waits for `ApplicationStopped`, and whose
  `Dispose` leaves disposal to the run loop. Never call `StopAsync` or `Dispose` on a factory's host; dispose the
  factory.
- 👁 **The integration host logs only through the console and EventSource providers.** It inherits them from the host's
  composition root, so a test writes nothing to the machine's Windows Event Log; `ProgramTests` pins the set.
- 👁 **A directly built host that composes the whole server needs a store-factory stub.** `AddIdentityServer()` leaves
  the persistence provider to the host, yet a client-authentication handler, the effective-settings resolver, and
  services the host starts depend on `IStoreManagerFactory`. A configuration-only test registers a never-called strict
  `new Mock<IStoreManagerFactory>(MockBehavior.Strict).Object` rather than disabling DI validation or composing a
  database.
- 👁 **Prove that composition constructs no strategy by registering one that throws** — template
  [Real Collaborators](csharp-tests.instructions.md#real-collaborators)
  (`AddIdentityServer_ReadingTheOpenIdOptions_ConstructsNoClientAuthenticationHandler`).
- 👁 **A test host pins an issuer for every tenant it serves.** A tenant is served only beneath its stored issuer
  ([ADR-1165](../../docs/decisions/1165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)), so
  `HostApplicationFactory` pins the static tenant to `HostApplicationFactory.TestServerIssuer` (`http://localhost`). A
  test that changes the tenant path pins a matching `StaticSingle:Issuer`; a test that selects a dynamic strategy
  configures its issuer template (or `Root:Issuer`, which the dynamic strategies require) and seeds tenants with an
  issuer; and a directly built host that starts configures `StaticSingle:Issuer`. A tenant seeded without an issuer
  fails closed when it is served.
- 👁 **The integration host runs in the `Development` environment, without its launch profile** — template
  [Integration Tests](csharp-tests.instructions.md#integration-tests): `HostApplicationFactory` loads
  `appsettings.Development.json`, never `launchSettings.json`, whose environment variables pin each launch profile's
  issuer and bootstrap administrator.
- 👁 **The integration host seeds no developer clients unless a test opts in.** The developer clients are public and use
  `http` redirect URIs, and the seeder checks them for coherence, so with them seeded a test that narrows the platform's, the team's
  or the tenant's ceilings (a FAPI profile, `token_endpoint_auth_methods_supported`, `redirect_uri_kinds_supported`)
  would stop the static tenant from being served. `HostApplicationFactory.SeedsDeveloperClients` is `false`, and a test
  that exercises the developer clients uses `DeveloperSeedingHostApplicationFactory`, which returns `true`
  (`DeveloperSpaClientSeedingTests`).
- 👁 **Configuration the composition root reads while it registers services is a host setting in a test factory** —
  template [Integration Tests](csharp-tests.instructions.md#integration-tests): supply `DeveloperKeys:PruneUnreadableKeys`,
  which `AddDevelopmentEnvironment` reads, with `builder.UseSetting(key, value)`, and assert an effect only that
  composition has, so a value that did not arrive fails the test.
- 👁 **A test that needs a tenant encryption key has the host provision it.** The host provisions only the default
  signing keys, and a tenant reads its settings and keys once, when it is first served, so neither can be added after a
  request reaches it. List a key-management
  algorithm such as `RSA-OAEP-256` beside a signing algorithm in `OpenId:Tenant:KeyProvisioning:Keys`
  ([ADR-1169](../../docs/decisions/1169-every-tenant-is-provisioned-with-persisted-keys.md)), and persist the settings the
  tenant needs with `HostApplicationFactory.SeedTenantSettingsAsync` before the first request. Assert that the token is
  a JWE (five segments), so a key that was never provisioned fails the test (`TokenIntrospectionEndpointTests`).
- 👁 **Drive the authorization endpoint the way a conforming client does.** Every authorization code is bound to an
  `S256` PKCE challenge by default ([ADR-1144](../../docs/decisions/1144-authorization-codes-are-bound-to-pkce-by-default.md)),
  so an integration test that requests a code sends `TestPkce.QueryParameters` (or `TestPkce.FormFields` at the pushed
  authorization request endpoint) and redeems it with `TestPkce.CodeVerifier`; a test without them exercises the
  refusal, not the flow. Sign a user in with `InteractiveLogin.SignInAsync` rather than another copy of the antiforgery
  dance.
- 👁 **Call the management API through the one management token request.** `ManagementTokenRequest` is the
  `client_credentials` request (`client_secret_basic`, scope `read:clients`) a test sends for a management token.
  `BootstrapAdminApplicationFactory.CreateAdministratorClientAsync` returns a client acting as the bootstrap
  administrator, with the token path when the control plane's tenant is served under its own path;
  `HostApplicationFactory.CreateManagementClientAsync` one acting as another shared-secret client; and
  `BootstrapAdminApplicationFactory.TokenRequest` builds the message, reads the access token, authorizes an existing
  client or creates one from a `WithWebHostBuilder` host, so no test writes another copy of the request.
- 👁 **Drive the device flow through its endpoints.** Start it at `/oauth2/device_authorization`, sign the user in with
  `InteractiveLogin.SignInAsync`, approve at `/oauth2/device` with the session cookie and the form fields `user_code`
  and `action=approve`, and redeem the `device_code` at `/oauth2/token` (`LocalAccountLifecycleTests`).
- 👁 **Post an interactive form through `InteractiveLogin.PostFormAsync`.** It reads the page's antiforgery tokens and
  posts the fields with them and the session cookie; a bundle page answers `503` in the integration host but still
  issues the tokens. Assert a refused sign-in with `SubmitLoginAsync`, which returns the response, and
  `ReadSessionCookie`.
- 👁 **Prove a page's security headers over HTTP, for a returning browser too** — template
  [Integration Tests](csharp-tests.instructions.md#integration-tests): find the policy's nonce in the page and prove a
  `'sha256-…'` source by hashing the page's own `<script>` text (`HtmlPageSecurityTests`).
- 👁 **Prove a header that depends on the forwarded scheme with a forwarded request** — template
  [Integration Tests](csharp-tests.instructions.md#integration-tests): send `X-Forwarded-Proto: https` over `http`
  through a host that trusts `127.0.0.1` in `ForwardedHeaders:KnownProxies`, with a control whose host trusts no proxy
  (`StrictTransportSecurityTests`).
- 👁 **A password a test sets through an entry point meets the password policy.** The management API, the
  change-password page and the seeders refuse a password shorter than 15 characters or on the common-password list
  (`passwordpassword` is), so such a test uses a long, unlisted passphrase; `SeedLocalAccountAsync` writes to the store
  and skips the policy. A test of the failed-attempt limit lowers `password_failed_attempts_limit` with
  `SeedTenantSettingsAsync` before the first request (`PasswordAttemptLimitTests`).
- 👁 **Prove an equal-time verification by recording that the hasher ran, never by measuring time.** Elapsed time is
  flaky in the parallel gate. Assert that the path without a stored hash verifies the dummy hash: a strict
  `IPasswordHasher<TUser>` setup for the ASP.NET Core Identity adapter, a recording `IPasswordHasher` fake for the
  Entity Framework store (Moq cannot match its span arguments), and a recording decorator that a derived
  `HostApplicationFactory` registers end to end (`PasswordVerificationTimingTests`). A refusal at the attempt limit is
  proven the same way (`SignIn_WhenTheLimitIsReached_VerifiesAgainstTheDummyHash`).
- 👁 **Test the ASP.NET Core Identity adapter against a real `UserManager<TUser>` as well as a mocked one.** A mocked
  `UserManager` hides what its own methods do: `CreateAsync(user, password)` runs the password validators, and
  `CheckPasswordAsync` rehashes and rotates the security stamp.
  `AspNetIdentityLocalAccountStoreEntityFrameworkTests` composes
  `AddIdentityCore<IdentityUser>().AddEntityFrameworkStores<IdentityDbContext>()` over EF InMemory with the adapter's
  own registration. Write a password that is not in NFC with escapes (`"cafe\u0301"`), never as a pasted character an
  editor may normalize. The user manager's hasher is the adapter's `NormalizingPasswordHasher<TUser>`, so a hash of a
  password as typed comes from a raw `PasswordHasher<TUser>` (`SetStoredPasswordHashAsync`), and a mocked
  `UserManager<TUser>` is built with a hasher wrapped in it.
- 👁 **Assert a URI the server must deliver as written on `Headers.Location.OriginalString`** — template
  [Assertions](csharp-tests.instructions.md#assertions).
- 👁 **A private-use redirect URI in a test is a reverse domain name** (`com.example.app:/callback`);
  `myapp://callback` is of no registered kind and is refused
  ([ADR-1203](../../docs/decisions/1203-redirect-uris-are-matched-exactly.md)).
- 👁 **Seed a client registered for the method the test authenticates with.** A client authenticates only with its
  registered `token_endpoint_auth_method`
  ([ADR-1145](../../docs/decisions/1145-a-client-authenticates-only-with-its-registered-method.md)), so each integration-test
  seeder registers one: `SeedConfidentialClientAsync` (`client_secret_post`), `SeedClientSecretBasicClientAsync`,
  `SeedClientSecretJwtClientAsync`, `SeedSharedSecretClientWithSettingsAsync` (either shared-secret method, with
  redirect URIs and settings), the certificate and `jwks_uri` seeders (`private_key_jwt`), the mutual-TLS seeders, and
  `SeedPublicClientAsync` (`none`). A negative test whose client is registered for another method is refused for the
  wrong reason — `invalid_client` from the method mismatch — and passes without exercising the rule it names.
- 👁 **Drive the protocol client against the host, and record what it sends.** Route
  `OpenIdClientDefaults.HttpClientName` to `factory.Server.CreateHandler()` with `ConfigurePrimaryHttpMessageHandler`,
  and add `RecordingHttpMessageHandler` with `AddHttpMessageHandler` over one `ConcurrentQueue<RecordedHttpExchange>`.
  Each exchange keeps the request's path, `Authorization` scheme, `DPoP` header and body with the response's status
  code, `DPoP-Nonce` header and body, so a test proves the wire — a `201` push that carries `client_id` once and no
  `request_uri` — as well as the client's result (`AuthorizationRequestClientEndToEndTests`).
- 👁 **Run DPoP end to end with a nonce, and model a key rotation with a ring per state.** Set `dpop_nonce_required`
  in the seeded client's settings so the first token request gets the `use_dpop_nonce` challenge; register static
  `DPoPKeyRing`s — the old key, a stranger key, and the rotated ring with both — and switch registrations of the same
  client between them, because the client resolves the ring on every call; compare `cnf.jkt` with an RFC 7638
  thumbprint computed in the test; and authorize again for each code a test expects to be refused, because the OP
  consumes a code before it checks `dpop_jkt` (`TokenClientDPoPEndToEndTests`).
- 👁 **Read the logout token the server delivers from the back-channel logout client.** Set
  `HostApplicationFactory.BackChannelLogoutExchanges` to a `ConcurrentQueue<RecordedHttpExchange>` before the first
  request, which replaces the primary handler of the one SSRF-guarded client (`GuardedHttpClient.Name`,
  [ADR-1315](../../docs/decisions/1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md)) with
  a stub whose `POST` handler is
  `StubRecordingHttpMessageHandler` (answers `200`, records each `POST`); the server notifies a client only after it
  issued that client an ID token in the session, so run the code flow first, end the session with
  `GET /oauth2/end_session` and the session cookie, and poll the queue with a deadline because a background job
  delivers the token; the protocol client's container adds `AddSharedStateServices().AddInProcessExpiringKeySet()`,
  because `AddOpenIdClientLibrary()` registers no `IExpiringKeySet` and the logout-token validator looks each `jti` up
  in one and `TryRecordHandledAsync` records it (`LogoutTokenValidatorEndToEndTests`).
- 👁 **Build a certificate subject with `X500DistinguishedNameBuilder` to choose each value's string type.** A subject
  DN is matched by RFC 4517 rules, not by bytes.
- 👁 **Serve revocation data through a stub handler, never the network.** Build a CRL with
  `CertificateRevocationListBuilder` (`TestCrls` for the extensions and entries the builder cannot write), signed by a
  CA whose key usage includes `CrlSign`, and an OCSP response with `TestOcspResponses`, signed by the issuer or by a
  responder from `TestCertificates.CreateOcspResponder`. Unit tests stub `IHttpClientFactory` and answer by
  `RequestUri`: an OCSP GET appends the URL-encoded base64 request to the responder's location. Integration tests set
  `HostApplicationFactory.CertificateRevocationListResponder` before the first request.
- 👁 **Pin a protocol encoder or parser to bytes another implementation wrote** — template
  [What a Test Proves](csharp-tests.instructions.md#what-a-test-proves) (`OcspOpenSslFixtures`, written by OpenSSL with
  RSA keys where the builder signs with ECDSA).
- 👁 **Test a contract with an artifact the .NET gate does not build through a fact the workflow that builds it runs** —
  template [Integration Tests](csharp-tests.instructions.md#integration-tests): `InteractiveBundleShellTests` reads the
  built `apps/auth` `index.html` under `[BuiltAuthBundleFact]`, which skips unless `NCODE_AUTH_BUNDLE_INDEX` names it.
  Frontend CI sets the variable after `pnpm run build`; locally, run `pnpm exec vite build` in
  `src/frontend/apps/auth` and set the variable to the absolute path of `dist/index.html`.
- 👁 **Validate new tests in Release before running `dod.ps1`** — template
  [Integration Tests](csharp-tests.instructions.md#integration-tests).
- 👁 **Build a JWT with exact time claims through `IJoseSerializer`, not `IJwtEncoder`.** The JWT encoder
  (`JwtEncoderOptions.EnsureTokenLifetime`, on by default) injects a default `iat` / `nbf` / `exp` and overwrites a
  non-integer `iat`, so a test of lifetime validation (an absent, malformed, or far-off time claim) must encode its
  payload with `IJoseSerializer.CreateEncoder(new JoseSigningOptions(credentials)).EncodeAsync(payload, cancellationToken)`.
- 👁 **Assert a background job's effect by polling with a deadline** — template
  [Time and Concurrency](csharp-tests.instructions.md#time-and-concurrency)
  (`TenantKeyProvisioningTests.WaitForSecretIdsAsync`).
- 👁 **Prove a worker's concurrency with a job that waits for a release** — template
  [Time and Concurrency](csharp-tests.instructions.md#time-and-concurrency) (`InProcessJobDispatchTests`); the worker
  invokes handlers through reflection, so a dispatched test job fails with `ValueTask.FromException`.
- 👁 **Change configuration at runtime through the host's `IConfigurationRoot`** — template
  [Integration Tests](csharp-tests.instructions.md#integration-tests) (`TenantKeyProvisioningTests`).

## End-to-end tests

- 👁 **A suite is a `*.EndToEnd.Tests` project, which only `./build/e2e.ps1` and the `End-to-end` check run.**
  `./build/dod.ps1` never runs it
  ([ADR-1430](../../docs/decisions/1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md)).
- 👁 **A project under `tests/` whose name does not end in `.Tests` is a support project.** The solution compiles it, no
  tier of `build/test-projects.ps1` lists it, and it is no test project and has no JUnit logger, so a suite references
  it and no gate tests it
  ([ADR-1441](../../docs/decisions/1441-a-bff-connection-ends-with-its-session-even-when-idle-and-a-blazor-web-app-proves-it.md)).
- 👁 **Host a Blazor Web App through `WebApplicationFactory<TEntryPoint>.UseKestrel()` over the suite's certificate,
  then call `StartServer()`.** The factory locates the app's static web assets, which a `WebApplication.CreateBuilder`
  in the test process does not
  ([ADR-1441](../../docs/decisions/1441-a-bff-connection-ends-with-its-session-even-when-idle-and-a-blazor-web-app-proves-it.md)).
- 👁 **An end-to-end suite is not hermetic by design.** Every host stays on loopback over the run's throwaway
  certificate, the container reaches the host only through `TestcontainersSettings.ExposeHostPortsAsync`, and
  credentials are generated per run
  ([ADR-1430](../../docs/decisions/1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md)).
- 👁 **One collection fixture serves all tests, and each test gets a fresh browser context**
  ([ADR-1430](../../docs/decisions/1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md)).
- 👁 **Browser theories run over `BrowserNames.All`:** Chromium and Firefox on every OS, and WebKit except on Windows,
  because Playwright's Windows WebKit port does not use Safari's cookie stack and mishandles `SameSite` and `Secure`
  ([ADR-1430](../../docs/decisions/1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md)).
- 👁 **Assert server-side counters as well as browser outcomes, and read an API response through `fetch` in the
  page.** Firefox shows a JSON response in a viewer, so a navigation to it reads nothing
  ([ADR-1430](../../docs/decisions/1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md)).
- 👁 **`localhost` and `127.0.0.1` are different sites;** a port never changes a site
  ([ADR-1430](../../docs/decisions/1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md)).
- 👁 **A test reads its files, such as an image's build context, from the output directory.** Copy them there with
  `CopyToOutputDirectory` and resolve them from `AppContext.BaseDirectory`. A CI build (`CI=true`) is a
  `ContinuousIntegrationBuild` that maps source paths to `/_/`, so a path found from the source, such as
  `[CallerFilePath]` or Testcontainers' `CommonDirectoryPath.GetProjectDirectory()`, exists only on a developer's
  machine.
- 👁 **Reproduce a pull request's `End-to-end` failure on its merge with `main`.** The check runs on that merge, and
  Dependabot moves the provider image there, so a branch can pass locally against an older provider than the one CI
  runs.
- 👁 **A client that calls Keycloak's Admin API requests the `kc-admin-api` scope.** Keycloak accepts only a token whose
  audience is the realm's Admin API, which that client scope adds, so the realm lists it among the client's
  `optionalClientScopes` and the token request asks for it.
