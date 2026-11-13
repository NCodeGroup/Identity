# 1513. Management responses carry strong entity tags that preconditions compare as RFC 9110 says

- **Status:** Accepted
- **Date:** 2026-11-13
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0125](project-template/0125-a-write-that-loses-a-race-is-answered-412.md)

## Context

A management resource that is written under optimistic concurrency carries a `concurrencyToken` in its JSON, and a read
of it sends the same token in the `ETag` response header. A write sends the token back in `If-Match`, and a route
answers `412` when it does not match
([project-template ADR-0125](project-template/0125-a-write-that-loses-a-race-is-answered-412.md)). Three forces shape
the format (issue [#782](https://github.com/NCodeGroup/identity/issues/782)):

- **An `ETag` is a quoted entity tag.** RFC 9110 §8.8.3 defines the value as an opaque string between double quotes,
  optionally prefixed `W/` for a weak tag. A header value without the quotes is not an entity tag.
- **Standard HTTP clients and caches send the tag back verbatim.** A client that reads `ETag: "abc"` sends
  `If-Match: "abc"`, and a cache revalidates with `If-None-Match`. A caller may also send a list of tags or `*`.
- **A caller holds the bare token.** The `concurrencyToken` member of a resource's JSON is the token without quotes, and
  the management console sends it as `If-Match`.

## Decision

**Every management response that carries a concurrency token sends it as a strong entity tag, and the `If-Match` and
`If-None-Match` request headers are compared as RFC 9110 says.** The public static class `ManagementEntityTags` in
`NCode.Identity.OpenId.Management.Abstractions` is the one place that holds the rules, and every handler and validator
calls it.

1. **`Format` sends a strong tag.** `ManagementEntityTags.Format(concurrencyToken)` returns the token between double
   quotes, such as `"abc"`, and validates it as an entity tag, so an invalid token throws. Every `ETag` the API sends is
   strong.
2. **`If-None-Match` is compared weakly.** `ManagementEntityTags.MatchesIfNoneMatch` returns true when the header is
   `*` or lists the current tag under weak comparison (RFC 9110 §13.1.2), and a read then answers `304`. A missing or
   malformed header does not match. The header takes only the RFC forms.
3. **`If-Match` accepts three forms.** `ManagementEntityTags.GetIfMatchErrorOrDefault(ifMatch, concurrencyToken,
   resource)` returns no error when the header is absent or empty, when it is `*`, when a strong tag in a list equals
   the current tag under strong comparison (RFC 9110 §13.1.1), or when it is a lone bare token equal ordinally to the
   concurrency token. A bare token has no `"`, no `,`, no white space and no `W/` prefix. The API documents the bare
   token as the one non-RFC form it accepts.
4. **A weak tag in `If-Match` never matches.** RFC 9110 §13.1.1 requires strong comparison for `If-Match`, and every tag
   the API sends is strong, so the helper does not strip a `W/` prefix.
5. **A failed or malformed `If-Match` is answered `412`.** A bare token that differs, or a tag list with no strong
   match, gets the detail "The supplied If-Match entity tag does not match the current {resource}." A value that is
   neither a bare token nor a parseable tag list gets the detail
   ``The If-Match header must be "*", a list of entity tags such as
   "\"<concurrency token>\"", or the resource's concurrencyToken.``, which names the accepted forms and never echoes
   the caller's value. The error is a `ManagementError`, and a handler or a validator answers it through
   `ToErrorResult`, so a handler and a validator share one path.
6. **The OpenAPI document describes the accepted forms.** The `If-Match` parameter of a route that reads it says that it
   accepts a quoted strong entity tag, a list of them, `*` or the bare `concurrencyToken`.

## Options considered

- **Accept only RFC forms.** Rejected: the `concurrencyToken` of the JSON body is what a caller holds, and the console
  sends it bare, so a caller would have to quote a value the API gave unquoted.
- **Accept `W/` tags in `If-Match`.** Rejected: RFC 9110 §13.1.1 requires strong comparison, and the API sends no weak
  tag, so a weak tag in `If-Match` did not come from the API.
- **Answer a malformed `If-Match` with `400`.** Rejected: every route that reads `If-Match` declares `412`
  ([ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md)), and the precondition is
  what fails.
- **Compare the header in each handler.** Rejected: every handler and validator would repeat the rules, and the forms
  would drift apart.

## Consequences

- A standard client or cache reads the `ETag`, sends it back unchanged in `If-Match` or `If-None-Match`, and gets the
  RFC 9110 behavior.
- A caller that holds only the JSON body sends the bare `concurrencyToken`, and the management console needs no quoting.
- A malformed `If-Match` is answered `412` with a detail that names the accepted forms, and a mismatch names the
  resource, so a caller sees which precondition failed.
- A new route that reads `If-Match` or `If-None-Match` calls `ManagementEntityTags`, and never compares the header
  itself.

## References

- RFC 9110 §8.8.3 (`ETag`), §13.1.1 (`If-Match`), §13.1.2 (`If-None-Match`) and §13.2.2 (precedence of preconditions).
- [ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md): every management error is a
  problem document that its route declares.
- [project-template ADR-0125](project-template/0125-a-write-that-loses-a-race-is-answered-412.md): a write that loses a
  race is answered `412`.
- Issue [#782](https://github.com/NCodeGroup/identity/issues/782)
- Code: `src/NCode.Identity.OpenId.Management.Abstractions/Endpoints/ManagementEntityTags.cs`
