# Public API compatibility

These are shipped libraries: once a type or member is `public` it is a contract every consumer depends on. This document
is the procedure for evolving that surface without breaking consumers. The surface itself is tracked by
`Microsoft.CodeAnalysis.PublicApiAnalyzers` via each packable project's `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`
pair ([ADR-0005](adr/0005-public-api-surface-is-tracked-and-evolves-compatibly.md)).

## The rule

**Once GA, a shipped public surface evolves additively.** A change that removes or alters existing surface is a
**major** version bump. Prefer, in order:

1. **Add, don't change.** A new type, a new member, or a **new overload** is additive. Changing an existing method's
   signature (parameters, return type, nullability) is breaking — add an overload instead.
2. **Required inputs are constructor parameters; optional inputs are `init` properties.** Never add an optional value as
   a new constructor parameter — that is a binary-breaking signature change. Add it as an `init` property, model
   "unspecified" as nullable, and resolve the default **inside the library** (fail-closed) rather than baking it into
   consumer code.
3. **A configuration callback takes one growable type.** An `Action<TOptions>` grows by adding a property to `TOptions`;
   a new bare callback parameter breaks every caller.
4. **Wire/enum members are appended, never renumbered.** Reordering or renumbering changes serialized values.
5. **Deprecate, then remove across a major.** Mark the old surface `[Obsolete("…", error: false)]` in a minor, keep it
   working, and remove it only in the next major.
6. **Never share a type across two independently-versioned surfaces.** A type used by two packages that could version
   apart becomes a diamond conflict; keep it in one owner or duplicate deliberately.

Nullability is part of the signature: changing `string` ↔ `string?` on the public surface is a tracked, potentially
breaking change (the `#nullable enable` header in the `PublicAPI.*.txt` files records it).

## Worked example — adding an optional input

```csharp
// v1 — shipped
public sealed class TokenRequestOptions
{
    public required string ClientId { get; init; }
}

// v1.1 — add an OPTIONAL input. Additive: a new init property, nullable = "unspecified".
public sealed class TokenRequestOptions
{
    public required string ClientId { get; init; }
    public TimeSpan? Lifetime { get; init; }   // library resolves the default when null
}
```

Adding `Lifetime` as a constructor parameter instead would break every `new TokenRequestOptions(...)` call site and
every serializer — hence the `init` property.

## The `PublicAPI.*.txt` workflow

Each packable project has two files next to its `.csproj`, each starting with `#nullable enable`:

- **`PublicAPI.Shipped.txt`** — surface already released.
- **`PublicAPI.Unshipped.txt`** — surface added since the last release.

The analyzer compares the compiled public symbols against the union of the two files on every build:

- A public symbol in **neither** file → `RS0016` (build error under warnings-as-errors).
- A listed symbol that **no longer exists** → `RS0017`.
- A missing `PublicAPI.*.txt` file → `RS0024`.

Steps:

1. **Add public API** → add its lines to `PublicAPI.Unshipped.txt`. The IDE offers a code fix — _"Add to public API"_ —
   that generates the correctly-formatted, sorted entries; headless equivalent:
   `dotnet format analyzers --diagnostics RS0016`.
2. **Remove public API** → prefix the removed line with `*REMOVED*` in `PublicAPI.Unshipped.txt`.
3. **At release** → move everything from `PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt` (dropping `*REMOVED*`
   lines); `Unshipped` returns to just `#nullable enable`. That snapshot is the baseline the next release diffs against.

Pre-1.0, `Shipped.txt` stays empty and the entire surface lives in `Unshipped.txt` — the surface is diff-visible while
we remain free to break it before the first stable release. At `1.0.0` the surface moves into `Shipped.txt`.

## Review checklist

- [ ] Does the change add surface rather than alter it? (new overload over changed signature)
- [ ] Are new optional inputs `init` properties, not new constructor parameters?
- [ ] Are removals staged through `[Obsolete]` for a major, not deleted in a minor/patch?
- [ ] Is the `PublicAPI.Unshipped.txt` diff in the PR and intentional?
- [ ] If anything was removed or a signature changed, is the version bumped **major**?
