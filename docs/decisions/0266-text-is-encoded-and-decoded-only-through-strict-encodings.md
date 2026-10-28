# 266. Text is encoded and decoded only through strict encodings

- **Status:** Accepted (amends [ADR-0127](0127-warnings-are-errors-in-every-project.md))
- **Date:** 2026-10-26
- **Deciders:** NCode Group

## Context

`Encoding.UTF8` and `Encoding.ASCII` are lenient: an unencodable character becomes `?` and an invalid byte sequence
becomes U+FFFD, silently. In a protocol library that turns a malformed input into a different, valid-looking one, and
two distinct inputs can then collide:

- The compact `b64=false` JWS signing input encoded the payload with `Encoding.ASCII`, so non-ASCII payload characters
  were signed as `?` (fixed in `JoseSerializer.Jws.Encode.cs`).
- The DPoP `ath` claim hashed `Encoding.ASCII(access token)`, so `accéss-token` hashed like `acc?ss-token`
  (fixed in `DefaultDPoPProofService`, which now rejects non-ASCII tokens via `Ascii.IsValid`).
- Lone surrogates collapsed to U+FFFD in `ExpiringKeys`, `SecretProtectionFormat` (the HKDF info and the length-prefixed
  AAD fields) and `ServerSettingsFingerprint`, so different strings produced the same hashed key, derived key or
  authenticated data.

## Decision

Text becomes bytes, and bytes become text, only through a strict encoding.

1. **Strict encodings only.** All text-to-bytes and bytes-to-text conversion goes through
   `NCode.Buffers.SecureEncoding.UTF8` or `.ASCII`, or through a strict constructor (`throwOnInvalidBytes: true`) for
   another encoding.
2. **No implicit encoding.** An overload that takes no encoding is replaced by the one that takes
   `SecureEncoding.UTF8`.
3. **HTTP text results.** `TypedResults.Text` and `TypedResults.Content` always receive
   `contentEncoding: SecureEncoding.UTF8`, except for a media type without a charset parameter (`application/jwt`),
   which is written as strict UTF-8 bytes through `Utf8TextResults.Create` in
   `NCode.Identity.OpenId.Authentication.Endpoints`, so `application/jwt` carries no charset.
4. **Enforcement.** `BannedSymbols.Encoding.txt` enforces rules 1 and 2 with `RS0030`. `Directory.Build.targets` loads
   it in every project, tests included, while `BannedSymbols.txt` stays shipping-only
   ([ADR-0127](0127-warnings-are-errors-in-every-project.md)). Two things are deliberately not banned:
   `HttpResponse.WriteAsync(string)`, because the ASP.NET request-delegate source generator emits it, and
   `TypedResults.Text`/`Content`, because a single method symbol covers calls with and without an encoding, so rule 3 is
   checked in review instead.
5. **Invalid input throws.** A strict encoding throws on input it cannot represent, and inbound paths map the exception
   to their protocol error rather than substituting a character.

## Options considered

- **Keep the lenient defaults and review each call.** Does not scale, and the same defect recurred in unrelated
  packages. Rejected.
- **Ban only in shipping projects.** Tests that encode with a lenient encoding would pass on input the product rejects,
  and would hide such regressions. Rejected: the ban binds tests too.
- **Ban `HttpResponse.WriteAsync(string)`.** The request-delegate generator emits it, so the ban would fail the build of
  generated code. Rejected.

## Consequences

- A compact `b64=false` JWS with a non-ASCII payload now signs and verifies the real bytes, and a DPoP proof is rejected
  when the access token is not ASCII.
- A lone surrogate in a hashed key, HKDF info or AAD field now throws instead of colliding with U+FFFD.
- `NCode.Json` depends on `NCode.Buffers`.
- `text/plain` responses now carry `charset=utf-8`; `application/jwt` responses still carry no charset.
- A test that needs UTF-16 or a byte-order mark builds a strict encoding explicitly.

## References

- [ADR-0127](0127-warnings-are-errors-in-every-project.md) — the analyzer set and why `BannedSymbols.txt` is
  shipping-only.
- [ADR-0116](0116-memory-allocations-and-buffer-operations.md) — buffers.
