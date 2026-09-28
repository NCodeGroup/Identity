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
  in tests (`CA1707`, `CA1816`, `CA1859`, `CA1861`, `CA2012`, `IDE0005`) are disabled for test projects in
  [`.editorconfig`](../../.editorconfig); they stay active on production code.
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
- 👁 **Hermetic — no network, no ambient host.** Use `DefaultHttpContext` with a test `RequestServices`, in-memory
  fakes, and constructed `ClaimsPrincipal`s. A resource/principal/stub a single test needs is a small local
  `private sealed` type in the test file.
- 👁 **Keep test-only reach through `InternalsVisibleTo`** (`internal`), never by widening the production surface. The
  project under test exposes internals with:

    ```csharp
    [assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]  // lets Moq proxy internal types/members
    [assembly: InternalsVisibleTo("NCode.Identity.Jose.Tests")] // the matching test assembly
    ```
