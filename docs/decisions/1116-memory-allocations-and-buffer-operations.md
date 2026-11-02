# 1116. Memory allocations and buffer operations use spans, pooled writers, and secure lifetimes

- **Status:** Accepted
- **Date:** 2026-10-16
- **Deciders:** NCode.Identity maintainers
- **Refines:** [project-template ADR-0112](project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md)

## Context

The template record sets the span, caller-owned writer, pooling, clearing, sequence, UTF-8 literal, `MemoryMarshal` and
hot-path rules. The family's binary work — JOSE/JWT, key material, DER/PEM, base64url — uses `NCode.Buffers`
(`BufferFactory`, `SecureMemoryPool<T>`, `SecureEncoding`, `GetSpanLease`) over `Nerdbank.Streams`, the pooled-buffer
helper library the template's Options considered allow.

## Decision

1. **Every rental goes through `BufferFactory`.** `Rent`, `CreatePooledBufferWriter` and `CreatePinnedArray` are the only
   sources of a pooled buffer; code never uses a bare `ArrayPool<T>.Shared`, `MemoryPool<T>.Shared` or
   `new ArrayBufferWriter<T>`, so sensitivity is an argument at every rental.

2. **Sensitivity is an explicit parameter.** `isSensitive: true` rents from the pinned `SecureMemoryPool`, zeroed when
   returned, which meets the template's clearing and pinning rule; secret material is always `true` — keys, passwords,
   nonces, decrypted JWE plaintext, and a payload serialized or compressed before encryption — and sensitive text is
   encoded once with `SecureEncoding`. `isSensitive: false` delegates to `MemoryPool<T>.Shared`.

3. **A contiguous span of a sequence is taken only when a consumer needs one.** When a consumer needs one contiguous
   span of a `Sequence<T>` (a cryptographic primitive, a base64url codec), `GetSpanLease(isSensitive:)` returns the
   single segment without copying and otherwise a pooled copy; any other consumer reads segment by segment as the
   template says.

4. **Hot-path optimizations are justified by a benchmark.** The hot-path primitives are the field, scalar and group
   arithmetic, encoders and constant-time helpers of `NCode.Identity.Jose.EdDsa`, and a new optimization of one is
   justified by a benchmark, as the template's rule 7 says.

## Consequences

- Buffer handling is uniform across the family, and secret-zeroing is the default rather than an easy-to-forget extra
  step.
- The writer-first shape underpins [ADR-1115](1115-asymmetric-keys-model-der-material-via-buffer-writers.md), which
  applies it to SPKI/PKCS#8 key export.
- Existing `byte[]` surfaces convert as they are touched.

## Gotchas (lessons learned)

These are concrete traps encountered applying the conventions; the analyzers catch most, but the fixes are worth
recording:

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
  reinterpretation (with a `BitConverter.IsLittleEndian` guard), but for parsing a defined little-endian wire
  encoding prefer `BinaryPrimitives` — it is a single `mov`(+`bswap`) and correct on every architecture.
- **`MemoryMarshal.Cast<byte, T>` only applies to byte-aligned limb radixes.** A radix-2^56 limb is exactly 7 bytes
  and a radix-2^64 limb 8 bytes (castable), but a non-byte-aligned radix (e.g. the radix-2^51 limbs of the Curve25519
  field) cannot be reinterpreted and must pack/unpack with shifts.
- **Direct field access can beat a span view for tiny fixed-limb structs.** For a 5-limb field element, unrolled
  `a._l0 … a._l4` arithmetic is faster than a `Span<ulong>` loop (no indexing, no bounds-check consideration), so do
  **not** force `MemoryMarshal`/`Unsafe` where it would regress a hot path — reserve the span view for cases that
  genuinely remove a copy (larger limb counts, or byte I/O).
