# 116. Memory allocations and buffer operations use spans, pooled writers, and secure lifetimes

- **Status:** Accepted
- **Date:** 2026-10-16
- **Deciders:** NCode.Identity maintainers

## Context

This family does a large amount of binary work — JOSE/JWT encode/decode, cryptographic key material, DER/PEM, base64url,
UTF-8 encoding, protocol serialization. The naïve .NET idiom for all of this is `byte[]`: allocate an array, return it,
let the GC collect it. At the scale and sensitivity of an OpenID Provider that is the wrong default on three axes:

- **Allocation pressure.** Returning a fresh `byte[]` from every encode/export/transform allocates on the GC heap and
  churns Gen-0, on hot paths (token signing, JWKS, request parsing) that run per request.
- **Secret hygiene.** A `byte[]` holding key material or a password cannot be pinned or reliably zeroed; it lingers on
  the managed heap until GC and can be moved by compaction, leaving copies in process memory (a snapshot/audit concern).
- **Inconsistency.** Without a stated convention, different code reaches for `byte[]`, `ArrayPool<byte>`,
  `new ArrayBufferWriter<byte>`, `MemoryStream`, or manual segment concatenation, so buffer handling is ad-hoc and
  secret-zeroing is easy to forget.

The repo already ships the building blocks to do this well — `NCode.Buffers` (`BufferFactory`, `SecureMemoryPool<T>`,
`SecureEncoding`, `GetSpanLease`, the fixed-buffer writers) over `Nerdbank.Streams` (`Sequence<T>`) — and already uses
them in places (for example `SymmetricSecretKey.ExportPrivateKey`, the data-protection export path, AES-KW). This ADR
makes that the **general, project-wide** convention rather than a per-feature choice.

## Decision

These rules apply to **all** memory/buffer operations across the family (not just secrets or cryptography); existing
`byte[]` surfaces migrate incrementally rather than in a single sweep.

1. **Model binary data as spans/memory, not arrays.** Public and internal APIs accept `ReadOnlySpan<T>` /
   `ReadOnlyMemory<T>` inputs and write results into a caller-supplied `IBufferWriter<T>`; they do **not** return a
   freshly-allocated `byte[]`. A producer uses the generic ref-writer shape so the caller owns the allocation strategy:
   `void Write…<TWriter>(ref TWriter destination) where TWriter : IBufferWriter<byte>, allows ref struct` (as in
   `SymmetricSecretKey.ExportPrivateKey` and `AsymmetricSecretKey.ExportSubjectPublicKeyInfo`). `Try…(Span<T>, out int
bytesWritten)` is the span-first variant when a fixed-size result is known.

2. **Rent pooled buffers; don't allocate transients.** For a transient working buffer or an accumulating writer, rent
   from `BufferFactory` (`CreatePooledBufferWriter(isSensitive:)`, `Rent(…)`, `CreatePinnedArray(…)`) rather than
   `new byte[]`, `new ArrayBufferWriter<byte>`, or a bare `ArrayPool<byte>`. Fixed-size, stack-bounded buffers use
   `stackalloc` with the `FixedSpanBufferWriter` / `FixedMemoryBufferWriter` helpers.

3. **Sensitivity is an explicit parameter.** `isSensitive: true` rents from the pinned, zeroed `SecureMemoryPool`;
   `isSensitive: false` delegates to the shared `MemoryPool`. **Secret** material (keys, passwords, nonces, plaintext)
   is always `true`; non-secret bytes (public key material, wire buffers) are `false`. Secrets are zeroed on return —
   via the secure pool's lifetime or `CryptographicOperations.ZeroMemory` — never left on the GC heap, and are threaded
   through the (async) call chain as `ReadOnlyMemory<byte>`, encoded **once** at the earliest boundary (`SecureEncoding`
   for sensitive text), not re-encoded from a `string` in a leaf method.

4. **Flatten multi-segment sequences with the shared helper.** A pooled `Sequence<byte>` writer may be multi-segment;
   obtain a contiguous `ReadOnlySpan<byte>` via `GetSpanLease(isSensitive:)` (which leases, and for sensitive data
   zeroes, a contiguous view) — never manual segment concatenation or `.ToArray()`.

5. **Build byte constants from UTF-8 literals.** Use `u8` literals for `ReadOnlySpan<byte>` constants from valid UTF-8
   text instead of `Encoding.UTF8.GetBytes` at runtime.

6. **Reinterpret byte buffers with `MemoryMarshal`, not element-by-element copies.** For conversion between
   `Span<byte>`/`ReadOnlySpan<byte>` and spans of blittable values (`ulong` limbs, fixed structs, etc.), use the
   **zero-copy** `MemoryMarshal` surface — `MemoryMarshal.Cast<byte, T>`, `MemoryMarshal.AsBytes`,
   `MemoryMarshal.Read`/`Write`, and `MemoryMarshal.GetReference` with `Unsafe` for `ref`-based bulk access — rather
   than per-element `BinaryPrimitives` loops, `BitConverter` array allocations, or manual byte shuffling. These are
   the modern, allocation-free memory operations and the preferred default wherever a buffer is viewed as structured
   data. Endianness is handled with a JIT-constant `BitConverter.IsLittleEndian` guard (the big-endian branch is
   eliminated on little-endian targets), so the zero-copy fast path stays both correct and portable. Example: the
   scalar codec loads/stores four `ulong` limbs via `MemoryMarshal.Cast`/`AsBytes` instead of eight
   `BinaryPrimitives` calls.

7. **Optimize cryptographic/numeric hot-path primitives aggressively, readability second.** The inner loops of the
   math and cryptographic primitives (field/scalar/group arithmetic, encoders, constant-time helpers) use every
   applicable performance technique — `[MethodImpl(AggressiveInlining | AggressiveOptimization)]`, manual unrolling of
   tiny fixed-count limb loops, `stackalloc`, the `MemoryMarshal`/`Unsafe` zero-copy reinterpretation of point 6, and
   `Unsafe` bounds-check elision — even when the result is dense and less readable. Two
   constraints always override speed: (a) **constant-time** code must not gain a data-dependent branch or memory
   access from an optimization; (b) correctness is pinned by test vectors / differential tests that stay green. This
   is a deliberate, scoped exception to the family's clean-code conventions, limited to performance-critical
   primitives.

## Consequences

- Buffer handling is uniform and allocation-light across the family, and secret-zeroing is the default rather than an
  easy-to-forget extra step. Hot paths avoid per-operation `byte[]` allocations.
- The writer-first API shape composes: a caller can target a pooled writer, a fixed span, or a network pipe without the
  producer allocating. It also underpins the asymmetric-key material model ([ADR-0115](0115-asymmetric-keys-model-der-material-via-buffer-writers.md)),
  which applies this convention to SPKI/PKCS#8 key export.
- There is a learning/consistency cost: contributors must use `BufferFactory`/`GetSpanLease` rather than reflexive
  `byte[]`. The scoped C# production conventions (§10) carry the short rule; this ADR is the rationale.
- Migration is incremental — pre-existing `byte[]` surfaces are converted as they are touched, not in a single breaking
  sweep — so both shapes coexist transiently.

## Gotchas (lessons learned)

These are concrete traps encountered applying the conventions; the analyzers catch most, but the fixes are worth
recording:

- **Never `stackalloc` inside a loop (CA2014).** A `stackalloc` is reclaimed only when the _method_ returns, not at the
  end of the iteration, so one placed in a loop body accumulates across every iteration and overflows the stack.
  Hoist the buffer to a single `stackalloc` before the loop and `.Clear()`/overwrite it per iteration.
- **`stackalloc` is for small, fixed, bounded sizes only.** A field element, a hash, a 32/57-byte scalar — never a
  caller-controlled or unbounded length; those rent from `BufferFactory` (point 2). A constant small cap keeps the
  stack safe.
- **You cannot pass a `using` local by `ref` (CS1657).** When writing into a pooled `Sequence<byte>` through a
  `ref TWriter` writer API, keep the `using var` that owns disposal and bind a **separate** non-`using`
  `IBufferWriter<byte>` local to it, then pass that local by `ref` (both reference the same writer). This is the
  established pattern in `DataProtectorExtensions` and `AsymmetricSecretKey.ExportSubjectPublicKeyInfoPem`.
- **`MemoryMarshal.Cast`/`AsBytes` reinterpretation is little-endian-host-specific.** The zero-copy fast path is only
  byte-correct on a little-endian host; always keep the `BitConverter.IsLittleEndian` guard with a
  `BinaryPrimitives.ReverseEndianness` fallback so portability is retained while the branch is JIT-eliminated on real
  targets (point 6).
- **Multi-part hashing uses `IncrementalHash`, not buffer concatenation.** Feeding `prefix || message` or
  `R || A || message` through `IncrementalHash.AppendData` (or the `Shake256` instance's `AppendData`) avoids
  allocating a combined buffer for a large message.
- **View a struct's fields as a `Span<T>` with `Unsafe.As` + `MemoryMarshal.CreateSpan`, not field-by-field copies.**
  For a value type whose fields are all the same blittable type (e.g. the eight `ulong` limbs of a field element),
  `MemoryMarshal.CreateSpan(ref Unsafe.As<TStruct, TField>(ref value), count)` (and `CreateReadOnlySpan` with
  `Unsafe.AsRef(in value)` for an `in` parameter) gives a **zero-copy** span over the fields, eliminating `ToLimbs`-style
  copies and field-by-field writebacks. This relies on the struct's **sequential layout** (the C# default for structs
  without reference fields); the round-trip tests validate the assumption.
- **A `ref`-view helper and an `in`-view helper cannot share a name (CS0663).** Overloads differing only by `ref`
  vs `in` are a compile error, so the mutable and read-only span-view helpers need distinct names (for example
  `LimbsOf(ref …)` and `ReadOnlyLimbsOf(in …)`).
- **`MemoryMarshal.Read<T>`/`Write<T>` are host-endian; `BinaryPrimitives.Read/WriteUInt64LittleEndian` is the
  portable choice for byte↔integer I/O.** Use `MemoryMarshal.Cast`/`CreateSpan` for same-endianness blittable
  reinterpretation (with the `BitConverter.IsLittleEndian` guard above), but for parsing a defined little-endian wire
  encoding prefer `BinaryPrimitives` — it is a single `mov`(+`bswap`) and correct on every architecture.
- **`MemoryMarshal.Cast<byte, T>` only applies to byte-aligned limb radixes.** A radix-2^56 limb is exactly 7 bytes
  and a radix-2^64 limb 8 bytes (castable), but a non-byte-aligned radix (e.g. the radix-2^51 limbs of the Curve25519
  field) cannot be reinterpreted and must pack/unpack with shifts.
- **Direct field access can beat a span view for tiny fixed-limb structs.** For a 5-limb field element, unrolled
  `a._l0 … a._l4` arithmetic is faster than a `Span<ulong>` loop (no indexing, no bounds-check consideration), so do
  **not** force `MemoryMarshal`/`Unsafe` where it would regress a hot path — reserve the span view for cases that
  genuinely remove a copy (larger limb counts, or byte I/O).
