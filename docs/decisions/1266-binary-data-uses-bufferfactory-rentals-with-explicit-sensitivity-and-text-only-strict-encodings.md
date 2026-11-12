# 1266. Binary data uses BufferFactory rentals with explicit sensitivity, and text only strict encodings

- **Status:** Accepted
- **Date:** 2026-10-26
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0112](project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md)

## Context

[project-template ADR-0112](project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md) sets
the general rules for binary data: spans, writers, pooling, clearing secrets, sequences and hot paths. The family
needs one concrete way to apply them, so that sensitivity is a decision at every rental rather than an afterthought.

Text is the second force. `Encoding.UTF8` and `Encoding.ASCII` are lenient: an unencodable character becomes `?` and
an invalid byte sequence becomes U+FFFD, silently. In a protocol library that turns a malformed input into a
different, valid-looking one, and two distinct inputs can then collide. The same defect recurred across packages:

- The compact `b64=false` JWS signing input encodes the payload; with a lenient ASCII encoding, non-ASCII payload
  characters would be signed as `?`.
- The DPoP `ath` claim hashes the access token; with a lenient ASCII encoding, `accéss-token` would hash like
  `acc?ss-token`.
- Lone surrogates collapse to U+FFFD, so different strings would produce the same hashed key, derived key or
  authenticated data in `ExpiringKeys`, `SecretProtectionFormat` (the HKDF info and the length-prefixed AAD fields)
  and `SettingsFingerprint`.

## Decision

Binary data is rented from `BufferFactory`, with its sensitivity stated at the call. Text becomes bytes, and bytes
become text, only through a strict encoding.

### Buffers

1. **One source of buffers.** The family applies project-template ADR-0112's span, writer, pooling, clearing, sequence
   and hot-path rules through `NCode.Buffers` (`BufferFactory`, `SecureMemoryPool<T>`, `SecureEncoding`,
   `GetSpanLease`) over `Nerdbank.Streams`. `NCode.Identity.Json` depends on `NCode.Buffers`.
2. **Every pooled buffer comes from `BufferFactory`** (`Rent`, `CreatePooledBufferWriter`, `CreatePinnedArray`), and a
   contiguous writer that never holds a secret comes from `CreateArrayBufferWriter()`: public output, such as a public
   JWK or a JWKS, a wrapped key, or a document for the user's own browser. Code must not use a bare
   `ArrayPool<T>.Shared`, `MemoryPool<T>.Shared`, `SecureMemoryPool<T>.Shared`, `new ArrayBufferWriter<T>` or
   `new Sequence<T>`, so sensitivity is an argument at every rental. `BannedSymbols.Buffers.txt`, which
   `Directory.Build.targets` adds to every project that is not a test project, bans them with `RS0030`; test projects
   may build their own fixtures with them.
3. **Sensitivity.** `isSensitive: true` rents from the pinned `SecureMemoryPool`, zeroed when returned. It is always
   used for keys, passwords, nonces, decrypted JWE plaintext and a payload serialized or compressed before
   encryption. `isSensitive: false` delegates to `MemoryPool<T>.Shared`. Sensitive text is encoded once with
   `SecureEncoding`.
4. **Contiguous spans.** A consumer that needs one contiguous span of a `Sequence<T>` (a cryptographic primitive, a
   base64url codec) takes `GetSpanLease(isSensitive:)`, which returns the single segment without copying and
   otherwise a pooled copy. Any other consumer reads segment by segment.
5. **Hot paths.** The hot-path primitives are the field, scalar and group arithmetic, the encoders and the
   constant-time helpers of `NCode.Identity.Jose.EdDsa`. An optimization of one is justified by a benchmark in the
   benchmark projects ([ADR-1386](1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md)).

### Text

6. **Strict encodings only.** All text-to-bytes and bytes-to-text conversion goes through
   `NCode.Buffers.SecureEncoding.UTF8` or `.ASCII`, or through a strict constructor (`throwOnInvalidBytes: true`,
   `throwOnInvalidCharacters: true`) for another encoding. A strict encoding throws on input it cannot represent, and
   inbound paths map the exception to their protocol error rather than substituting a character.
7. **No implicit encoding.** An overload that takes no encoding is not used.
8. **HTTP text results.** `TypedResults.Text` and `TypedResults.Content` always receive
   `contentEncoding: SecureEncoding.UTF8`, so `text/plain` responses carry `charset=utf-8`. A media type without a
   charset parameter, `application/jwt`, is written as strict UTF-8 bytes through `Utf8TextResults.Create`
   (`NCode.Identity.OpenId.Authentication.Endpoints`) and carries no charset.
9. **The ban.** `BannedSymbols.Encoding.txt`, which `Directory.Build.targets` adds to every project, tests included,
   bans with `RS0030` the lenient `Encoding` properties (`UTF8`, `ASCII`, `Unicode`, `BigEndianUnicode`, `UTF32`,
   `Latin1`, `Default`), `GetEncoding`, the lenient encoding constructors and the `File` text overloads without an
   encoding. Tests are bound too, so they never pass on input the product rejects; a test that needs UTF-16 or a
   byte-order mark builds a strict encoding explicitly.
10. **Two things are deliberately not banned.** `HttpResponse.WriteAsync(string)` stays allowed because the
    request-delegate generator emits it. `TypedResults.Text` and `TypedResults.Content` stay allowed because one method
    symbol covers calls with and without an encoding, so review checks them.

### Implementation notes

- A struct whose fields are one blittable type is viewed as a span with
  `MemoryMarshal.CreateSpan(ref Unsafe.As<TStruct, TField>(ref value), count)`. This relies on sequential layout,
  which round-trip tests check.
- A `ref`-view helper and an `in`-view helper need distinct names (CS0663).
- `BinaryPrimitives` reads and writes a defined little-endian wire encoding.
- `MemoryMarshal.Cast<byte, T>` applies only to byte-aligned limb radixes, not radix-2^51.
- Direct field access beats a span view for tiny fixed-limb structs.

## Options considered

- **Lenient defaults with per-call review.** The same defect recurred across packages, so a reviewer's attention is
  not a control. Rejected.
- **Ban lenient encodings only in shipping projects.** A test that passes on input the product rejects proves
  nothing, so tests are bound too. Rejected.
- **Ban `HttpResponse.WriteAsync(string)`.** The request-delegate generator emits it, so the ban fails generated
  code. Rejected.
- **Rent every buffer from `BufferFactory`, state sensitivity at the call, and ban the lenient encodings in every
  project.** The analyzer enforces the encoding rule and the one rental API makes sensitivity visible. Chosen.

## Consequences

- Sensitive buffers are zeroed on return, and the rental call shows at review whether a buffer is sensitive.
- The compact `b64=false` JWS signs and verifies the payload's real bytes. A DPoP proof is rejected when the access
  token is not ASCII (`Ascii.IsValid` in `DefaultDPoPProofService`). A lone surrogate throws instead of colliding with
  U+FFFD in `ExpiringKeys`, `SecretProtectionFormat` and `SettingsFingerprint`.
- `text/plain` responses declare `charset=utf-8`, and `application/jwt` responses declare none.
- A new use of a lenient encoding fails the build with `RS0030`; the two allowed text-result calls rely on review.
- A bare buffer source in code that is not a test fails the build with `RS0030`.
- A test that needs UTF-16 or a byte-order mark constructs a strict encoding itself.

## References

- [project-template ADR-0112](project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md)
- [ADR-1127](1127-every-project-builds-with-zero-warnings-and-the-familys-own-banned-apis-bind-shipping-code.md)
- [ADR-1386](1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md)
- `BannedSymbols.Encoding.txt`, `BannedSymbols.Buffers.txt`, `Directory.Build.targets`
- `NCode.Buffers` (`BufferFactory`, `SecureMemoryPool<T>`, `SecureEncoding`, `GetSpanLease`)
- `Utf8TextResults` in `NCode.Identity.OpenId.Authentication.Endpoints`
- `DefaultDPoPProofService`, `ExpiringKeys`, `SecretProtectionFormat`, `SettingsFingerprint`
