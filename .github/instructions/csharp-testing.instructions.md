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
- 👁 **Assert on the exception type the contract guarantees, not the one a platform happens to throw.**
  `Assert.Throws<T>` demands an **exact** type match, so it fails on a derived exception. When the concrete type is an
  implementation detail that varies by platform or provider, assert the documented base type with
  `Assert.ThrowsAny<T>`. Signing with a public-only RSA key is the canonical case: Windows raises
  `CryptographicException` itself, while OpenSSL-backed platforms raise a derived
  `Interop+Crypto+OpenSslCryptographicException`, so only `Assert.ThrowsAny<CryptographicException>` holds on both.
  Keep `Assert.Throws<T>` where the exact type **is** the contract (a library's own
  `JoseException`/`OpenIdException`, `ArgumentNullException`, and similar).

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

## Persistence & integration tests

- 👁 **Store / EF behavior is tested against a real provider, not a mocked store.** Anything that depends on
  `SaveChanges` side effects — concurrency-token generation, cascade deletes, `SaveChangesInterceptor`s, value
  converters — must run against an actual `DbContext` (EF **InMemory** covers most cases). Endpoint tests that mock the
  store cannot observe these effects: a `SavingChanges`-only interceptor sat dead for exactly this reason (the codebase
  saves via `SaveChangesAsync`; see
  [ADR-0012](../../docs/adr/0012-interceptor-managed-concurrency-tokens.md)). Keep both layers — mock the store for
  endpoint logic, and exercise the store itself against a real provider.
- 👁 **EF InMemory does not enforce `[ConcurrencyCheck]`.** It honors a change-tracker interceptor but does **not** put
  the token in the `UPDATE … WHERE` clause, so it cannot detect a concurrent-write conflict. A true
  optimistic-concurrency test uses a **relational** provider — SQLite in-memory (a shared, open `SqliteConnection` +
  `EnsureCreated`) with two `DbContext`s to stand in for the racing writers.
- 👁 **EF InMemory does not enforce unique indexes** either — including a **filtered** unique index
  (`.HasIndex(…).IsUnique().HasFilter("… IS NOT NULL")`). A test that must observe the constraint (a duplicate
  rejected, or multiple rows allowed past a filtered `NULL`) runs against **SQLite in-memory** (`EnsureCreated` builds
  and enforces the index); the collision surfaces as a `DbUpdateException` on the offending `SaveChanges`.
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
- 👁 **Each factory instance owns an isolated in-memory database, and `WithWebHostBuilder` builds a _separate_ host with
  its _own_ database.** `HostApplicationFactory.ConfigureWebHost` creates a fresh `InMemoryDatabaseRoot` per host,
  so `factory` and `factory.WithWebHostBuilder(…)` do **not** share data. A test that both **seeds** data and
  **configures** the host must do both on the **same** instance: configure through a subclass that overrides
  `ConfigureWebHost` (calling `base` first), then seed via that instance's helpers — never seed one instance and request
  another, or the request hits a different, empty database.
- 👁 **Never assert a startup failure through `WebApplicationFactory`.** When the host's entry point throws after
  `Build()` — mapping endpoints resolves options, so a configuration error usually lands there — the host's
  `await using var app` disposes the service provider, and under load the factory's deferred `StartAsync` reaches that
  disposed provider first and throws an `ObjectDisposedException` that masks the real error. The test passes alone and
  fails in the parallel gate. Assert startup failures against a host built directly
  (`Host.CreateEmptyApplicationBuilder`, the registration under test, then `StartAsync`), and keep factory-based tests
  for hosts that start.
- 👁 **A directly built host that composes the whole server needs a store-factory stub.** `AddIdentityServer()` leaves
  the persistence provider to the host, yet resolving the setting-descriptor catalog resolves the client-authentication
  handlers, which depend on `IStoreManagerFactory`. A configuration-only test registers a never-called strict
  `new Mock<IStoreManagerFactory>(MockBehavior.Strict).Object` rather than disabling DI validation or composing a
  database.
- 👁 **Drive the authorization endpoint the way a conforming client does.** Every authorization code is bound to an
  `S256` PKCE challenge by default ([ADR-0144](../../docs/adr/0144-authorization-codes-are-bound-to-pkce-by-default.md)),
  so an integration test that requests a code sends `TestPkce.QueryParameters` (or `TestPkce.FormFields` at the pushed
  authorization request endpoint) and redeems it with `TestPkce.CodeVerifier`; a test without them exercises the
  refusal, not the flow. Sign a user in with `InteractiveLogin.SignInAsync` rather than another copy of the antiforgery
  dance.
- 👁 **Seed a client registered for the method the test authenticates with.** A client authenticates only with its
  registered `token_endpoint_auth_method`
  ([ADR-0145](../../docs/adr/0145-a-client-authenticates-only-with-its-registered-method.md)), so each integration-test
  seeder registers one: `SeedConfidentialClientAsync` (`client_secret_post`), `SeedClientSecretBasicClientAsync`,
  `SeedClientSecretJwtClientAsync`, the certificate and `jwks_uri` seeders (`private_key_jwt`), the mutual-TLS seeders,
  and `SeedPublicClientAsync` (`none`). A negative test whose client is registered for another method is refused for the
  wrong reason — `invalid_client` from the method mismatch — and passes without exercising the rule it names.
- 👁 **Validate new tests in Release before running `dod.ps1`.** The gate builds and tests in **Release**, where
  `Debug.Assert` and `#if DEBUG` blocks disappear; run `dotnet test <proj> --configuration Release` first to catch
  Debug-vs-Release differences fast (a `#if DEBUG` auth bypass, a stripped assertion).
- 👁 **Build a JWT with exact time claims through `IJoseSerializer`, not `IJwtEncoder`.** The JWT encoder
  (`JwtEncoderOptions.EnsureTokenLifetime`, on by default) injects a default `iat` / `nbf` / `exp` and overwrites a
  non-integer `iat`, so a test of lifetime validation (an absent, malformed, or far-off time claim) must encode its
  payload with `IJoseSerializer.CreateEncoder(new JoseSigningOptions(credentials)).Encode(payload)`.
