# 298. A package traces its own mechanisms through one activity source named like its meter

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

[ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) settled how the family
measures: each signal has one owner, a package's instruments live in one meter named after the package, and every
attribute is bounded. It said nothing about traces, because no package emitted any. ASP.NET Core already traces each
inbound request and `System.Net.Http` each outbound call, so a request to the OP is traced without the family's help.

The BFF ([ADR-0290](0290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md),
[ADR-0291](0291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md)) is the first package
whose latency hides inside a request: a token refresh may wait for the session's lock inside the replica and for
another replica's lease before it calls the token endpoint, and every session read or write goes through a replaceable
`IBffSessionStore`. Without spans of its own, a slow `/api` request shows only the outbound call, not the waits around
it.

## Decision

1. **One activity source per package that traces, named like its meter.** The name is the package name, a public
   constant beside the meter's (`BffTelemetry.ActivitySourceName`, `"NCode.Identity.Bff"`), so a host adds the whole
   family with `AddSource("NCode.Identity.*")`. The source is a static `ActivitySource`: unlike a meter it has no factory
   to scope it to a service provider, and it costs nothing until a listener samples it.
2. **A package traces only its own mechanisms.** It never adds a span for what ASP.NET Core or `System.Net.Http`
   already traces: the inbound request and the outbound call are theirs, and a package's span wraps the work around
   them, such as waits and store operations.
3. **A span mirrors its histogram.** Each span measures what one of the package's duration histograms measures, is
   named after it without the `.duration` suffix (`ncode.identity.bff.token.refresh`,
   `ncode.identity.bff.session_store.operation`), and carries the same bounded attributes. A failure sets the span's
   status to `Error` and its `error.type`, as the histogram does. So one vocabulary describes both signals, and
   [`docs/metrics.md`](../metrics.md) catalogs both.
4. **A span carries no secret and no personal data.** Traces could carry higher-cardinality values than metrics, but a
   span never carries a token, handle, cookie, key, subject, client id or path; correlation with a request comes from
   the parent span.
5. **The host opts in.** `AddOpenTelemetryMetrics` configures metrics only; a host that collects traces adds the
   family's sources to its own tracing pipeline.

## Options considered

- **A span for every BFF endpoint.** Rejected: ASP.NET Core's request span already covers `/bff/login`,
  `/bff/callback`, `/bff/user` and `/bff/logout`.
- **A span around each token endpoint call.** Rejected: `System.Net.Http` traces it; the refresh span is its parent,
  and the waits are the refresh span's time outside it.
- **A factory-scoped source, like the meter.** Rejected: .NET has no `ActivitySource` factory, and spans are filtered by
  their listener and sampler, not by service provider.
- **Richer span attributes (subject, client id).** Rejected: the BFF design forbids them on any telemetry, and the
  request span's own attributes already correlate a span with its request.

## Consequences

- A slow BFF request shows its refresh, its waits and its store operations as child spans, with the result of each.
- A package that adds a span adds its histogram, or names the one it mirrors, and lists both in
  [`docs/metrics.md`](../metrics.md).
- Tests observe spans with an `ActivityListener` filtered to a parent activity the test started, because the source is
  shared by every test in the process.

## References

- [ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) — metrics conventions
- [OpenTelemetry semantic conventions: recording errors](https://opentelemetry.io/docs/specs/semconv/general/recording-errors/)
- [`docs/backend-for-frontend.md` §5.13](../backend-for-frontend.md#513-observability) — the BFF's observability design
