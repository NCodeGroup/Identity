---
description: "NCode.Identity test-project conventions — true unit tests, xUnit, Moq .Verifiable() with MockBehavior.Strict, Method_State_Expected naming, #region organization, hermetic."
applyTo: "tests/**/*.cs"
---

# NCode.Identity test conventions

Rules for test projects. Production code's testability rules — the seams, facades, and `InternalsVisibleTo` wiring that
make code mockable — live in [`csharp-production.instructions.md`](csharp-production.instructions.md) §7.

## Structure & naming

- 👁 **Test files mirror the folder structure and namespace of the code under test**, with a `.Tests` suffix on the
  namespace:

    ```
    // Production                              // Test
    NCode.Identity.Jose/JoseSerializer.cs      NCode.Identity.Jose.Tests/JoseSerializerTests.cs
    namespace NCode.Identity.Jose;             namespace NCode.Identity.Jose.Tests;
    ```

- 👁 **Test method names use the `Method_State_Expected` underscore idiom**
  (`Encode_WhenGivenValidPayload_ReturnsCompactJws`). The analyzer rules that are legitimate on shipping code but noise
  in tests (`CA1707`, `CA1816`, `CA1859`, `CA1861`, `CA1869`, `CA2012`, `CA2215`) are disabled for test projects in
  [`.editorconfig`](../../.editorconfig); they stay active on production code.
- ⏳ **Warnings are errors in test projects too**, inherited from the root `TreatWarningsAsErrors`
  ([ADR-0127](../../docs/adr/0127-warnings-are-errors-in-every-project.md)). The criterion is "code we maintain", not
  "code we ship" — and the `xunit.analyzers` rules in particular report real defects *in the tests themselves*, which
  is the worst class of warning to ignore. To relax a rule that is genuine noise in tests, add it to the
  `[tests/**/*.cs]` block in [`.editorconfig`](../../.editorconfig) **with a one-line justification**; for a single
  isolated site, use a tight `#pragma warning disable/restore` with a reason instead. 🚫 Never reintroduce
  `TreatWarningsAsErrors=false` or reach for a project-level `NoWarn`.
- ✅ **Prefer a real fix over a suppression for nullable warnings.** Test projects are exempt from the production ban on
  the null-forgiving operator (`Nullable.Extended.Analyzer` is gated on `IsPackable != false`), so `null!` is available
  — but a `[MaybeNullWhen(false)]` `out` argument should be given a **nullable local** (`IReadOnlyCollection<string>?
  supported = …`) and a maybe-null result should be narrowed with `Assert.NotNull(x)` before dereferencing.
- 👁 **Organize tests with `#region` directives** grouping tests by the member under test:
    - `Constructor Tests` for constructors
    - `{MethodName} Tests` for methods (e.g. `Dispose Tests`)
    - `{PropertyName} Property Tests` for properties (e.g. `Span Property Tests`)
    - `Generic Type Tests` for generic-type-parameter behavior

## Style

- 👁 **Do NOT use `// Arrange`, `// Act`, `// Assert` comments.** Separate the logical sections of a test with blank
  lines instead. One behavior per test.

    ```csharp
    [Fact]
    public void MyMethod_WhenCondition_ExpectedBehavior()
    {
        const string input = "test";

        var result = MyMethod(input);

        Assert.Equal("expected", result);
    }
    ```

- 👁 The production code-style rules also apply in tests: **collection expressions** (`byte[] data = [1, 2, 3];`),
  **`const` for literals**, and **UTF-8 string literals** (`"Hello"u8.ToArray()`) — except inline literal arrays passed
  straight to assertion helpers, which stay inline (`CA1861` is off for tests).
- 👁 **Prefer the size-specific xUnit assertions**: `Assert.Empty(x)` for count 0 and `Assert.Single(x)` for count 1,
  not `Assert.Equal(0/1, x.Count)`. `Assert.Equal(n, x.Count)` is fine for n > 1.
- 👁 **Compare an `ImmutableArray<T>` by its elements: `Assert.Equal<T>(expected, actual)`.** `ImmutableArray<T>` is
  `IEquatable<ImmutableArray<T>>` by **reference** to its backing array, so `Assert.Equal(["a"], immutableArray)` binds
  the collection expression to a new `ImmutableArray<T>` and fails on equal elements; the explicit element type selects
  the sequence overload.
- 👁 **Assert on the exception type the contract guarantees, not the one a platform happens to throw.**
  `Assert.Throws<T>` demands an **exact** type match, so it fails on a derived exception. When the concrete type is an
  implementation detail that varies by platform or provider, assert the documented base type with
  `Assert.ThrowsAny<T>`. Signing with a public-only RSA key is the canonical case: Windows raises
  `CryptographicException` itself, while OpenSSL-backed platforms raise a derived
  `Interop+Crypto+OpenSslCryptographicException`, so only `Assert.ThrowsAny<CryptographicException>` holds on both.
  Keep `Assert.Throws<T>` where the exact type **is** the contract (a library's own
  `JoseException`/`OpenIdException`, `ArgumentNullException`, and similar).
- 👁 **A test that a rule refuses an alternative must make the alternative viable.** When the behavior under test is
  "refuse rather than fall back" (encrypt to a resource server's key or fail, never to the tenant's), arrange the
  fallback so it **would succeed** — give the tenant a key the policy permits — and pair the test with a control that
  shows the same fallback succeeding when the rule does not apply. Otherwise the refusal also happens because the
  fallback itself fails, the same error is asserted, and the test passes with the rule removed.
- 👁 **A fix that moves a refusal earlier is proven by a request the old code deferred.** When the change is *when* a
  request is refused (credentials checked before sign-in rather than when the response is written), send a request the
  old code accepts and parks, for example an authorization request without `prompt=none`, which the old code answers
  with the login challenge (`302`). A request the old code also refuses at the end passes with the fix removed.

## Isolation & mocking

- 👁 Tests are **true unit tests**: isolate the code under test by mocking its collaborators (Moq).
- 👁 **Isolate the unit, but use the real engine where the engine _is_ the unit.** Mock the **external** collaborators a
  type depends on; drive the composition itself (registration, dispatch, serialization end-to-end) through the real DI
  container (`new ServiceCollection().Add…().BuildServiceProvider()`) when that wiring is what you are testing. Share
  small builders rather than re-standing-up the container in every test.
- 👁 **Every Moq setup MUST end with `.Verifiable()`**, use `x` as the lambda parameter, and be **auto-verified** — do
  not sprinkle manual `mock.Verify(...)` calls. Use a `UnitTestBase` that owns a `MockRepository(MockBehavior.Strict)`
  and verifies it on dispose, so an unmet or unexpected call fails the test:

    ```csharp
    public abstract class UnitTestBase : IDisposable
    {
        private readonly MockRepository _mocks = new(MockBehavior.Strict);

        protected Mock<T> CreateMock<T>() where T : class => _mocks.Create<T>();

        public void Dispose() => _mocks.Verify();
    }

    mockService
        .Setup(x => x.DoSomething(It.IsAny<string>()))
        .Returns("result")
        .Verifiable();
    ```

- 👁 **`MockBehavior.Strict`** so an unexpected call fails loudly (a real isolation guarantee).
- 👁 **`MockBehavior.Loose` is the sanctioned exception for two narrow cases**, via a shared `CreateLooseMock<T>` /
  `CreatePartialMock<T>` base helper: (a) a **partial mock** of an abstract base class where unset members must fall
  through to the real base (`CreatePartialMock<KeyManagementAlgorithm>()`), which Moq only supports under Loose; and
  (b) a pure **value-provider stub** whose members return data the test never asserts on (an `IReadOnlySettingCollection`
  or `IOpenIdError` shim), where strict setup adds ceremony but no isolation value. Default to `Strict`; reach for Loose
  only in these two cases.
- 👁 **Hermetic — no network, no ambient host.** Use `DefaultHttpContext` with a test `RequestServices`, in-memory
  fakes, and constructed `ClaimsPrincipal`s. A resource/principal/stub a single test needs is a small local
  `private sealed` type in the test file.
- 👁 **Dispose a test's real service provider asynchronously.** A test that resolves the real algorithms or key types
  (`AddSecretsLibrary().AddJoseLibrary()`) owns a container holding a service that implements only
  `IAsyncDisposable` (the algorithm-collection provider), so a synchronous `ServiceProvider.Dispose()` throws
  `InvalidOperationException` from every test in the class. Implement `IAsyncLifetime` and `await` its
  `DisposeAsync()` instead of implementing `IDisposable`.
- 👁 **Keep test-only reach through `InternalsVisibleTo`** (`internal`), never by widening the production surface. The
  project under test exposes internals with:

    ```csharp
    [assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]  // lets Moq proxy internal types/members
    [assembly: InternalsVisibleTo("NCode.Identity.Jose.Tests")] // the matching test assembly
    ```

- 👁 **A strict `.Verifiable()` setup must be consumed on every path that uses the shared scaffold.** If a collaborator
  member is read only on _some_ code paths, move its setup **out** of the shared fixture and into just the tests that
  exercise that path (have the fixture hand back the mock). Do **not** drop `.Verifiable()` to dodge an "unmet setup"
  failure — that discards the isolation guarantee.
- 👁 **A source-generated `ILogger<T>` needs only a loose mock.** `[LoggerMessage]` methods check `IsEnabled` first, and
  a loose mock returns `false`, so `Log` is never called and no setup is needed — a sanctioned value-provider loose mock
  (`CreateLooseMock<ILogger<T>>()`). When the log entry **is** the behavior under test (an error logged instead of
  thrown), use a strict mock with `IsEnabled(level)` returning `true` and a `.Verifiable()`
  `Log(level, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), …, It.IsAny<Func<It.IsAnyType, Exception?, string>>())`
  setup; `It.IsAnyType` matches the generated state struct.
- 👁 **Mock the enumerator the code actually calls.** `OfType<T>()` and `Cast<T>()` enumerate through the
  **non-generic** `IEnumerable.GetEnumerator()`, so a strict mock of a collection interface consumed that way needs
  `mock.As<IEnumerable>().Setup(x => x.GetEnumerator())`; a setup of the generic `GetEnumerator()` is never hit and the
  strict mock throws.
- 👁 **Moq cannot match a `ReadOnlySpan<T>` / `Span<T>` argument.** A ref struct cannot be an `It.IsAny<T>()` type
  argument, so a collaborator member that takes a span (for example `ISecretGenerator.ImportSharedSecret`) cannot be
  set up. Exercise the code that calls it through an integration test against the real collaborator instead of
  widening the member's signature for the mock.
- 👁 **Compute a setup's expected value outside the Moq expression.** A setup lambda is an expression tree, which cannot
  hold a ref struct or the expanded form of a `params ReadOnlySpan<T>` call (`CS8640`, `CS9226`) — so
  `Setup(x => x.ContainsAsync(ExpiringKeys.Create("p", tenant, nonce), ct))` does not compile. Build the value into a
  local first (`var expectedKey = ExpiringKeys.Create(…);`) and match it in the setup; this also keeps the matched value
  visible in the test.
- 👁 **Stop the unit at the mediator to inspect a command it builds midway.** When the behavior under test is the
  content of a command the unit sends before more work (the subject a token exchange validates), capture the command in
  the setup's callback and return `ValueTask.FromException(...)`, so the unit stops there and the test mocks nothing
  after it (`DefaultTokenExchangeGrantHandlerTests`).
- 👁 **Match a `JsonElement` argument by content.** `JsonElement` compares document references, and a Moq matcher is an
  expression tree that cannot contain `out _`, so call a static helper
  (`It.Is<JsonElement>(e => HasProperty(e, "foo"))`).
- 👁 **Capture an event handler a strict mock receives and invoke it directly.**
  `SetupAdd(x => x.Event += It.IsAny<EventHandler<TArgs>>()).Callback<EventHandler<TArgs>>(h => captured = h)`, plus a
  `SetupRemove` when the unit detaches when it stops (`ValkeyReplicaBroadcastSubscriberTests`).
- 👁 **A change-token producer in a test returns a fresh token after each change.** `ChangeToken.OnChange` asks the
  producer for a new token after its callback; a producer that returns the cancelled token again fires forever. Swap in
  a new `CancellationTokenSource` before cancelling the old one (`ServerSettingsReconciliationServiceTests`).

## Persistence & integration tests

- 👁 **Store / EF behavior is tested against a real provider, not a mocked store.** Anything that depends on
  `SaveChanges` side effects — concurrency-token generation, cascade deletes, `SaveChangesInterceptor`s, value
  converters — must run against an actual `DbContext` (EF **InMemory** covers most cases). Endpoint tests that mock the
  store cannot observe these effects: a `SavingChanges`-only interceptor sat dead for exactly this reason (the codebase
  saves via `SaveChangesAsync`; see
  [ADR-0012](../../docs/adr/0012-interceptor-managed-concurrency-tokens.md)). Keep both layers — mock the store for
  endpoint logic, and exercise the store itself against a real provider.
- 👁 **A handler that writes through more than one store needs an end-to-end test.** A store resolves the tenant that
  owns a row with a database query, not from the change tracker, so a row a tenant owns — its creator's ownership
  assignment, a seeded resource — can be added only after the tenant row is committed. A store-mocked endpoint test
  cannot see that order: the management API's tenant create added the ownership row in the same unit of work as the
  tenant, failed on every call, and its unit tests passed. Cover such a handler through the host
  (`TenantProvisioningTests` creates a tenant through the management API, then serves it).
- 👁 **EF InMemory does not enforce `[ConcurrencyCheck]`.** It honors a change-tracker interceptor but does **not** put
  the token in the `UPDATE … WHERE` clause, so it cannot detect a concurrent-write conflict. A true
  optimistic-concurrency test uses a **relational** provider — SQLite in-memory (a shared, open `SqliteConnection` +
  `EnsureCreated`) with two `DbContext`s to stand in for the racing writers.
- 👁 **EF InMemory does not enforce unique indexes** either — including a **filtered** unique index
  (`.HasIndex(…).IsUnique().HasFilter("… IS NOT NULL")`). A test that must observe the constraint (a duplicate
  rejected, or multiple rows allowed past a filtered `NULL`) runs against **SQLite in-memory** (`EnsureCreated` builds
  and enforces the index); the collision surfaces as a `DbUpdateException` on the offending `SaveChanges`.
- 👁 **EF InMemory enforces no foreign key.** A deletion whose dependents reference the deleted row is tested on SQLite
  in-memory, where a missed dependent fails `SaveChanges` (`ClientStoreRelationalRemovalTests`,
  `TenantStoreRelationalRemovalTests`).
- 👁 **EF Core's SQLite provider cannot compare or order a `DateTimeOffset`.** Test a store query that filters on one,
  such as a grant's `ExpiresWhen` or `RevokedWhen`, on EF InMemory.
- 👁 **A test that adds an entity to the `OpenIdDbContext` model calls `EnableServiceProviderCaching(false)`.** Entity
  Framework caches the model per context type across service providers, so without it the test may receive a model
  built without its entity (`TenantStoreContributedEntityRemovalTests`).
- 👁 **A lost race through the store manager is `StoreConcurrencyException`;** only a test that calls
  `DbContext.SaveChangesAsync` directly sees EF's `DbUpdateConcurrencyException`. See
  [ADR-0208](../../docs/adr/0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md).
- 👁 **Build a stored grant's `PayloadJson` with the real `DefaultPersistedPayloadSerializer`**
  (`new DefaultPersistedPayloadSerializer([])`), never `JsonSerializer.SerializeToElement(payload)`. The server stores a
  versioned envelope, so a test that hands the code the bare payload passes while the code fails on every stored row
  (`DefaultSessionServiceTests`).
- 👁 **A strict-mock test of a read-and-retry loop returns a new object for each read**
  (`SetupSequence(...).ReturnsAsync(first).ReturnsAsync(second)`): the code under test changes the object it read, so
  one shared instance makes the second read see the first attempt's own write.
- 👁 **Test a read-modify-write through the update function it passes.** The strict setup of `UpdatePayloadAsync`
  captures the `update` argument in its callback; the test invokes it with the stored grant the scenario means, such as
  one a concurrent request changed, and asserts the update it returns (`DefaultSessionServiceTests`,
  [ADR-0222](../../docs/adr/0222-a-grants-payload-is-changed-by-a-read-modify-write.md)).
- 👁 **A mocked `OpenIdContext` or `OpenIdTenant` that the code under test disposes sets up its protected
  `DisposeAsyncCore`.** `mock.Protected().Setup<ValueTask>("DisposeAsyncCore")…Verifiable()`; `DisposeAsync` is not
  virtual and calls it (`EndedSessionTerminationServiceTests`).
- 👁 **A global query filter is bypassed for change-tracked entities.** A `HasQueryFilter` predicate (e.g. the
  tenant-scoping filter, [ADR-0018](../../docs/adr/0018-tenant-scoped-data-access-at-the-persistence-layer.md)) is
  applied to a **database** query, but a first-level-cache hit — an entity already tracked from a prior `Add`/read,
  including a store's local-first lookup — returns the row **without** re-applying the filter. A test that exercises a
  query filter must `ChangeTracker.Clear()` (or use a fresh `DbContext`) after seeding so the assertion hits the
  database query where the filter actually runs. EF InMemory **does** honor query filters, so it covers this case.
- 👁 **Data-protection integration tests are not hermetic under a shared host.** A `WebApplicationFactory`
  `IClassFixture` that protects/unprotects secrets (confidential clients, encrypted key material) leaks
  keyring/serializer state between tests, and the second run fails (`invalid_client`). Give each such test its own
  `using var factory = new HostApplicationFactory();`.
- 👁 **Each factory instance owns an isolated in-memory database, which every host it builds shares.**
  `HostApplicationFactory` holds one `InMemoryDatabaseRoot`. `factory.WithWebHostBuilder(…)` builds a separate host
  (its own service provider, caches, and hosted services) over that database. So data one host commits is visible to
  the others, but a tenant one host materialized, or usage it recorded and has not flushed, is not. Seed before the
  host that must see the data serves its first request.
- 👁 **Never assert a startup failure through `WebApplicationFactory`.** When the host's entry point throws after
  `Build()` — mapping endpoints resolves options, so a configuration error usually lands there — the host's
  `await using var app` disposes the service provider, and under load the factory's deferred `StartAsync` reaches that
  disposed provider first and throws an `ObjectDisposedException` that masks the real error. The test passes alone and
  fails in the parallel gate. Assert startup failures against a host built directly
  (`Host.CreateEmptyApplicationBuilder`, the registration under test, then `StartAsync`), and keep factory-based tests
  for hosts that start.
- 👁 **The integration host is stopped by its own run loop, once.**
  - The host's entry point awaits `RunAsync`, which stops the host when the application stops and then disposes it.
  - `WebApplicationFactory` would stop the same host again, concurrently. The run loop then disposed the service
    provider under the other stop's work: a usage flush failed, and its warning hit the disposed Windows Event Log
    provider and threw from a random test's disposal.
  - `HostApplicationFactory.CreateHost` returns an `ApplicationRunLoopHost`. Its `StopAsync` calls `StopApplication`
    and waits for `ApplicationStopped`, and its `Dispose` leaves disposal to the run loop.
  - Never call `StopAsync` or `Dispose` on a factory's host; dispose the factory.
- 👁 **The integration host logs only through the console and EventSource providers.** It inherits them from the host's
  composition root, so a test writes nothing to the machine's Windows Event Log; `ProgramTests` pins the set.
- 👁 **A directly built host that composes the whole server needs a store-factory stub.** `AddIdentityServer()` leaves
  the persistence provider to the host, yet a client-authentication handler, the effective-settings resolver, and
  services the host starts depend on `IStoreManagerFactory`. A configuration-only test registers a never-called strict
  `new Mock<IStoreManagerFactory>(MockBehavior.Strict).Object` rather than disabling DI validation or composing a
  database.
- 👁 **Prove that composition constructs no strategy by registering one that throws.** Register
  `services.AddSingleton<IClientAuthenticationHandler>(_ => throw new InvalidOperationException(…))`, configure a
  server setting so options validation reads the setting-descriptor catalog, and read `IOptions<OpenIdOptions>.Value`
  (`AddIdentityServer_ReadingTheOpenIdOptions_ConstructsNoClientAuthenticationHandler`).
- 👁 **A test host pins an issuer for every tenant it serves.** A tenant is served only beneath its stored issuer
  ([ADR-0165](../../docs/adr/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)), so
  `HostApplicationFactory` pins the static tenant to `HostApplicationFactory.TestServerIssuer` (`http://localhost`). A
  test that changes the tenant path pins a matching `StaticSingle:Issuer`; a test that selects a dynamic strategy
  configures its issuer template (or `Root:Issuer`, which the dynamic strategies require) and seeds tenants with an
  issuer; and a directly built host that starts configures `StaticSingle:Issuer`. A tenant seeded without an issuer
  fails closed when it is served.
- 👁 **The integration host runs in the `Development` environment, without its launch profile.**
  `HostApplicationFactory` sets `Development`, so the host loads `appsettings.Development.json` — the developer seed's
  `DeveloperInteractiveSeed:TenantId`, though the factory seeds none of the developer clients — but never
  `launchSettings.json`, whose environment variables pin each launch profile's issuer and bootstrap administrator.
  Configure what a test depends on in the factory (`ConfigureAppConfiguration`) rather than assuming a launch profile's
  value, and expect an unhandled exception to reach the client as the developer exception page's `500`.
- 👁 **The integration host seeds no developer clients unless a test opts in.** The developer clients are public and use
  `http` redirect URIs, and the seeder checks them for coherence, so with them seeded a test that narrows the server's
  or the tenant's ceilings (a FAPI profile, `token_endpoint_auth_methods_supported`, `redirect_uri_kinds_supported`)
  would stop the static tenant from being served. `HostApplicationFactory.SeedsDeveloperClients` is `false`, and a test
  that exercises the developer clients uses `DeveloperSeedingHostApplicationFactory`, which returns `true`
  (`DeveloperSpaClientSeedingTests`).
- 👁 **Configuration the composition root reads while it registers services is a host setting in a test factory.** A
  `ConfigureAppConfiguration` overlay reaches options bound later, but not a value `Program.cs` reads to decide what to
  register — `DeveloperKeys:Mode`, read by `AddDevelopmentEnvironment`, keeps its default and the test silently runs
  the wrong composition. Supply such a value with `builder.UseSetting(key, value)`, which reaches the host's entry point
  the way `UseEnvironment` does, and assert on an effect only that composition has (the provisioned keys of
  `TenantKeyProvisioningTests`), so a value that did not arrive fails the test.
- 👁 **A test that needs a tenant encryption key has the host provision it.** The default `Ephemeral` developer keys are
  one in-memory RSA signing key, and a tenant reads its settings and keys once, when it is first served, so neither can
  be added after a request reaches it. Select `DeveloperKeys:Mode` `None` (a host setting, above), list a key-management
  algorithm such as `RSA-OAEP-256` beside a signing algorithm in `OpenId:Tenant:KeyProvisioning:Keys`
  ([ADR-0169](../../docs/adr/0169-every-tenant-is-provisioned-with-persisted-keys.md)), and persist the settings the
  tenant needs with `HostApplicationFactory.SeedTenantSettingsAsync` before the first request. Assert that the token is
  a JWE (five segments), so a key that was never provisioned fails the test (`TokenIntrospectionEndpointTests`).
- 👁 **Drive the authorization endpoint the way a conforming client does.** Every authorization code is bound to an
  `S256` PKCE challenge by default ([ADR-0144](../../docs/adr/0144-authorization-codes-are-bound-to-pkce-by-default.md)),
  so an integration test that requests a code sends `TestPkce.QueryParameters` (or `TestPkce.FormFields` at the pushed
  authorization request endpoint) and redeems it with `TestPkce.CodeVerifier`; a test without them exercises the
  refusal, not the flow. Sign a user in with `InteractiveLogin.SignInAsync` rather than another copy of the antiforgery
  dance.
- 👁 **Drive the device flow through its endpoints.** Start it at `/oauth2/device_authorization`, sign the user in with
  `InteractiveLogin.SignInAsync`, approve at `/oauth2/device` with the session cookie and the form fields `user_code`
  and `action=approve`, and redeem the `device_code` at `/oauth2/token` (`LocalAccountLifecycleTests`).
- 👁 **Post an interactive form through `InteractiveLogin.PostFormAsync`.** It reads the page's antiforgery tokens and
  posts the fields with them and the session cookie; a bundle page answers `503` in the integration host but still
  issues the tokens. Assert a refused sign-in with `SubmitLoginAsync`, which returns the response, and
  `ReadSessionCookie`.
- 👁 **Assert a URI the server must deliver as written on `Headers.Location.OriginalString`.** `HttpClient` parses
  the header into a `Uri`, whose `AbsoluteUri` and `ToString()` normalize it, so such an assertion passes while the
  server sends `myapp://callback/` for `myapp://callback`.
- 👁 **A private-use redirect URI in a test is a reverse domain name** (`com.example.app:/callback`);
  `myapp://callback` is of no registered kind and is refused
  ([ADR-0203](../../docs/adr/0203-redirect-uris-are-matched-exactly.md)).
- 👁 **The ephemeral developer key signs only `RS256`.** A test that needs the tenant to sign with `PS256` or
  `ES256`, such as a FAPI client's JARM response, selects `DeveloperKeys:Mode` `None` with `builder.UseSetting` (a
  host setting, above), so the tenant is provisioned `RS256`, `PS256`, and `ES256` keys
  (`Fapi1AdvancedAuthorizationTests`).
- 👁 **Seed a client registered for the method the test authenticates with.** A client authenticates only with its
  registered `token_endpoint_auth_method`
  ([ADR-0145](../../docs/adr/0145-a-client-authenticates-only-with-its-registered-method.md)), so each integration-test
  seeder registers one: `SeedConfidentialClientAsync` (`client_secret_post`), `SeedClientSecretBasicClientAsync`,
  `SeedClientSecretJwtClientAsync`, the certificate and `jwks_uri` seeders (`private_key_jwt`), the mutual-TLS seeders,
  and `SeedPublicClientAsync` (`none`). A negative test whose client is registered for another method is refused for the
  wrong reason — `invalid_client` from the method mismatch — and passes without exercising the rule it names.
- 👁 **Build a certificate subject with `X500DistinguishedNameBuilder` to choose each value's string type.** A subject
  DN is matched by RFC 4517 rules, not by bytes.
- 👁 **Serve a CRL through a stub handler, never the network.** Build it with `CertificateRevocationListBuilder`
  (`TestCrls` for extensions the builder cannot write), signed by a CA whose key usage includes `CrlSign`. Unit tests
  stub `IHttpClientFactory`; integration tests set `HostApplicationFactory.CertificateRevocationListResponder` before
  the first request.
- 👁 **Validate new tests in Release before running `dod.ps1`.** The gate builds and tests in **Release**, where
  `Debug.Assert` and `#if DEBUG` blocks disappear; run `dotnet test <proj> --configuration Release` first to catch
  Debug-vs-Release differences fast (a `#if DEBUG` auth bypass, a stripped assertion).
- 👁 **Build a JWT with exact time claims through `IJoseSerializer`, not `IJwtEncoder`.** The JWT encoder
  (`JwtEncoderOptions.EnsureTokenLifetime`, on by default) injects a default `iat` / `nbf` / `exp` and overwrites a
  non-integer `iat`, so a test of lifetime validation (an absent, malformed, or far-off time claim) must encode its
  payload with `IJoseSerializer.CreateEncoder(new JoseSigningOptions(credentials)).Encode(payload)`.
- 👁 **Assert a background job's effect by polling with a deadline** (a `Stopwatch`, 100 ms steps, about 15 s), never
  with a fixed delay (`TenantKeyProvisioningTests.WaitForSecretIdsAsync`).
- 👁 **Change configuration at runtime through the host's `IConfigurationRoot`.** Set the key on
  `factory.Services.GetRequiredService<IConfiguration>()` cast to `IConfigurationRoot` and call `Reload()`: the test's
  in-memory overlay is the last provider and keeps the value through the reload, and every reload token fires
  (`TenantKeyProvisioningTests`).
